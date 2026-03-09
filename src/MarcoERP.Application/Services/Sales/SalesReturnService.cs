using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Application.Mappers.Sales;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Exceptions.Sales;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Domain.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Sales
{
    /// <summary>
    /// Implements sales return lifecycle: Create → Edit → Post → (Cancel).
    /// On Post: reversal revenue journal + reversal COGS journal, stock re-addition.
    /// 
    /// Revenue Reversal:  DR 4111 Sales + DR 2121 VAT Output  /  CR 1121 AR
    /// COGS Reversal:     DR 1131 Inventory  /  CR 5111 COGS  (per-line at current WAC)
    /// </summary>
    [Module(SystemModule.Sales)]
    public sealed class SalesReturnService : ISalesReturnService
    {
        private readonly ISalesReturnRepository _returnRepo;
        private readonly IProductRepository _productRepo;
        private readonly IJournalEntryRepository _journalRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly ISalesInvoiceRepository _invoiceRepo;
        private readonly IFiscalYearRepository _fiscalYearRepo;
        private readonly IJournalNumberGenerator _journalNumberGen;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreateSalesReturnDto> _createValidator;
        private readonly IValidator<UpdateSalesReturnDto> _updateValidator;
        private readonly ILogger<SalesReturnService> _logger;
        private readonly ISystemSettingRepository _systemSettingRepository;
        private readonly JournalEntryFactory _journalFactory;
        private readonly FiscalPeriodValidator _fiscalValidator;
        private readonly IFeatureService _featureService;
        private readonly StockManager _stockManager;

        // ── GL Account Codes (from SystemAccountSeed) ───────────
        private const string ArAccountCode = "1121";
        private const string SalesAccountCode = "4111";
        private const string VatOutputAccountCode = "2121";
        private const string CogsAccountCode = "5111";
        private const string InventoryAccountCode = "1131";
        private const string ReturnNotFoundMessage = "مرتجع البيع غير موجود.";

        public SalesReturnService(
            SalesReturnRepositories repos,
            SalesReturnServices services,
            SalesReturnValidators validators,
            JournalEntryFactory journalFactory,
            FiscalPeriodValidator fiscalValidator,
            StockManager stockManager,
            ILogger<SalesReturnService> logger = null,
            IFeatureService featureService = null)
        {
            if (repos == null) throw new ArgumentNullException(nameof(repos));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (validators == null) throw new ArgumentNullException(nameof(validators));

            _returnRepo = repos.ReturnRepo;
            _productRepo = repos.ProductRepo;
            _journalRepo = repos.JournalRepo;
            _accountRepo = repos.AccountRepo;
            _invoiceRepo = repos.InvoiceRepo;

            _fiscalYearRepo = services.FiscalYearRepo;
            _journalNumberGen = services.JournalNumberGen;
            _unitOfWork = services.UnitOfWork;
            _currentUser = services.CurrentUser;
            _dateTime = services.DateTime;
            _systemSettingRepository = services.SystemSettingRepo;

            _createValidator = validators.CreateValidator;
            _updateValidator = validators.UpdateValidator;
            _journalFactory = journalFactory ?? throw new ArgumentNullException(nameof(journalFactory));
            _fiscalValidator = fiscalValidator ?? throw new ArgumentNullException(nameof(fiscalValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SalesReturnService>.Instance;
            _featureService = featureService;
            _stockManager = stockManager ?? throw new ArgumentNullException(nameof(stockManager));
        }

        // ══════════════════════════════════════════════════════════
        //  QUERIES
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<IReadOnlyList<SalesReturnListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _returnRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<SalesReturnListDto>>.Success(
                entities.Select(SalesReturnMapper.ToListDto).ToList());
        }

        public async Task<ServiceResult<SalesReturnDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _returnRepo.GetWithLinesAsync(id, ct);
            if (entity == null)
                return ServiceResult<SalesReturnDto>.Failure(ReturnNotFoundMessage);

            return ServiceResult<SalesReturnDto>.Success(SalesReturnMapper.ToDto(entity));
        }

        public async Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default)
        {
            var next = await _returnRepo.GetNextNumberAsync(ct);
            return ServiceResult<string>.Success(next);
        }

        // ══════════════════════════════════════════════════════════
        //  CREATE (Draft)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<SalesReturnDto>> CreateAsync(CreateSalesReturnDto dto, CancellationToken ct = default)
        {
            // Feature Guard — block operation if Sales module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<SalesReturnDto>(_featureService, FeatureKeys.Sales, ct);
                if (guard != null) return guard;
            }

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "SalesReturn", 0);

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<SalesReturnDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            const int maxRetries = 3;
            var attempt = 0;

            while (attempt < maxRetries)
            {
                attempt++;

                try
                {
                    SalesReturn salesReturn = null;

                    await _unitOfWork.ExecuteInTransactionAsync(async () =>
                    {
                        // ── Validate return quantities against original invoice ──
                        if (dto.OriginalInvoiceId.HasValue)
                        {
                            var originalInvoice = await _invoiceRepo.GetWithLinesAsync(dto.OriginalInvoiceId.Value, ct);
                            if (originalInvoice == null)
                                throw new SalesReturnDomainException("الفاتورة الأصلية غير موجودة.");

                            // Get all previous posted/draft returns for this invoice to prevent over-return
                            var previousReturns = await _returnRepo.GetByOriginalInvoiceAsync(dto.OriginalInvoiceId.Value, ct);
                            var previouslyReturnedQty = previousReturns
                                .Where(r => r.Status != InvoiceStatus.Cancelled)
                                .SelectMany(r => r.Lines)
                                .GroupBy(l => new { l.ProductId, l.UnitId })
                                .ToDictionary(
                                    g => g.Key,
                                    g => g.Sum(l => l.Quantity));

                            foreach (var lineDto in dto.Lines)
                            {
                                var invoiceLine = originalInvoice.Lines
                                    .FirstOrDefault(l => l.ProductId == lineDto.ProductId && l.UnitId == lineDto.UnitId);

                                if (invoiceLine == null)
                                    throw new SalesReturnDomainException(
                                        $"الصنف {lineDto.ProductId} بالوحدة {lineDto.UnitId} غير موجود في الفاتورة الأصلية.");

                                var alreadyReturned = previouslyReturnedQty
                                    .GetValueOrDefault(new { lineDto.ProductId, lineDto.UnitId }, 0m);
                                var remainingReturnable = invoiceLine.Quantity - alreadyReturned;

                                if (lineDto.Quantity > remainingReturnable)
                                    throw new SalesReturnDomainException(
                                        $"كمية المرتجع ({lineDto.Quantity}) تتجاوز الكمية المتاحة للإرجاع ({remainingReturnable}) للصنف {lineDto.ProductId}. " +
                                        $"(الكمية الأصلية: {invoiceLine.Quantity}، تم إرجاع: {alreadyReturned})");
                            }
                        }

                        var returnNumber = await _returnRepo.GetNextNumberAsync(ct);

                        salesReturn = new SalesReturn(
                            returnNumber,
                            dto.ReturnDate,
                            dto.CustomerId,
                            dto.WarehouseId,
                            dto.OriginalInvoiceId,
                            dto.Notes,
                            salesRepresentativeId: dto.SalesRepresentativeId,
                            counterpartyType: dto.CounterpartyType,
                            supplierId: dto.SupplierId);

                        foreach (var lineDto in dto.Lines)
                        {
                            var product = await _productRepo.GetByIdWithUnitsAsync(lineDto.ProductId, ct);
                            if (product == null)
                                throw new SalesReturnDomainException($"الصنف برقم {lineDto.ProductId} غير موجود.");

                            var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                            if (productUnit == null)
                                throw new SalesReturnDomainException(
                                    $"الوحدة المحددة غير مرتبطة بالصنف ({product.NameAr}).");

                            salesReturn.AddLine(
                                lineDto.ProductId,
                                lineDto.UnitId,
                                lineDto.Quantity,
                                lineDto.UnitPrice,
                                productUnit.ConversionFactor,
                                lineDto.DiscountPercent,
                                product.VatRate);
                        }

                        await _returnRepo.AddAsync(salesReturn, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }, IsolationLevel.Serializable, ct);

                    var saved = await _returnRepo.GetWithLinesAsync(salesReturn.Id, ct);
                    return ServiceResult<SalesReturnDto>.Success(SalesReturnMapper.ToDto(saved));
                }
                catch (DuplicateRecordException) when (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
                    continue;
                }
                catch (SalesReturnDomainException ex)
                {
                    return ServiceResult<SalesReturnDto>.Failure(ex.Message);
                }
                catch (SalesInvoiceDomainException ex)
                {
                    return ServiceResult<SalesReturnDto>.Failure(ex.Message);
                }
                catch (ConcurrencyConflictException)
                {
                    return ServiceResult<SalesReturnDto>.Failure("تم تعديل المرتجع بواسطة مستخدم آخر. يرجى إعادة المحاولة.");
                }
                catch (DuplicateRecordException)
                {
                    return ServiceResult<SalesReturnDto>.Failure("تعذر حفظ المرتجع بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
                }
            }

            return ServiceResult<SalesReturnDto>.Failure("فشل حفظ مرتجع البيع بعد عدة محاولات.");
        }

        // ══════════════════════════════════════════════════════════
        //  UPDATE (Draft only)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<SalesReturnDto>> UpdateAsync(UpdateSalesReturnDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "SalesReturn", dto.Id);

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<SalesReturnDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var salesReturn = await _returnRepo.GetWithLinesTrackedAsync(dto.Id, ct);
            if (salesReturn == null)
                return ServiceResult<SalesReturnDto>.Failure(ReturnNotFoundMessage);

            if (salesReturn.Status != InvoiceStatus.Draft)
                return ServiceResult<SalesReturnDto>.Failure("لا يمكن تعديل مرتجع مرحّل أو ملغى.");

            try
            {
                salesReturn.UpdateHeader(dto.ReturnDate, dto.CustomerId, dto.WarehouseId,
                    dto.OriginalInvoiceId, dto.Notes,
                    salesRepresentativeId: dto.SalesRepresentativeId,
                    counterpartyType: dto.CounterpartyType,
                    supplierId: dto.SupplierId);

                var newLines = new List<SalesReturnLine>();
                foreach (var lineDto in dto.Lines)
                {
                    var product = await _productRepo.GetByIdWithUnitsAsync(lineDto.ProductId, ct);
                    if (product == null)
                        return ServiceResult<SalesReturnDto>.Failure($"الصنف برقم {lineDto.ProductId} غير موجود.");

                    var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                    if (productUnit == null)
                        return ServiceResult<SalesReturnDto>.Failure(
                            $"الوحدة المحددة غير مرتبطة بالصنف ({product.NameAr}).");

                    newLines.Add(new SalesReturnLine(
                        lineDto.ProductId,
                        lineDto.UnitId,
                        lineDto.Quantity,
                        lineDto.UnitPrice,
                        productUnit.ConversionFactor,
                        lineDto.DiscountPercent,
                        product.VatRate,
                        lineDto.Id));
                }

                salesReturn.ReplaceLines(newLines);
                // Entity is already tracked — no need for _returnRepo.Update(salesReturn)
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _returnRepo.GetWithLinesAsync(salesReturn.Id, ct);
                return ServiceResult<SalesReturnDto>.Success(SalesReturnMapper.ToDto(saved));
            }
            catch (SalesReturnDomainException ex)
            {
                return ServiceResult<SalesReturnDto>.Failure(ex.Message);
            }
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult<SalesReturnDto>.Failure(ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  POST — Revenue reversal + COGS reversal + stock in
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Posts a draft sales return. This triggers:
        ///   1. Revenue reversal journal — DR Sales + DR VAT Output / CR AR.
        ///   2. COGS reversal journal — DR Inventory / CR COGS (per-line at current WAC).
        ///   3. Warehouse stock increase + inventory movement records.
        ///   4. Return status → Posted.
        /// </summary>
        public async Task<ServiceResult<SalesReturnDto>> PostAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "PostAsync", "SalesReturn", id);

            var salesReturn = await _returnRepo.GetWithLinesAsync(id, ct);
            if (salesReturn == null)
                return ServiceResult<SalesReturnDto>.Failure(ReturnNotFoundMessage);

            if (salesReturn.Status != InvoiceStatus.Draft)
                return ServiceResult<SalesReturnDto>.Failure("لا يمكن ترحيل مرتجع مرحّل بالفعل أو ملغى.");

            if (!salesReturn.Lines.Any())
                return ServiceResult<SalesReturnDto>.Failure("لا يمكن ترحيل مرتجع بدون بنود.");

            try
            {
                var saved = await ExecutePostAsync(salesReturn, ct);
                return ServiceResult<SalesReturnDto>.Success(SalesReturnMapper.ToDto(saved));
            }
            catch (SalesReturnDomainException ex)
            {
                return ServiceResult<SalesReturnDto>.Failure(ex.Message);
            }
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult<SalesReturnDto>.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while posting sales return.");
                return ServiceResult<SalesReturnDto>.Failure(
                    ErrorSanitizer.Sanitize(ex, "ترحيل مرتجع البيع"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<SalesReturnDto>.Failure("تعذر ترحيل المرتجع بسبب تعارض تحديث متزامن. يرجى إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult<SalesReturnDto>.Failure("تعذر ترحيل المرتجع بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post sales return {ReturnId}.", salesReturn?.Id);
                return ServiceResult<SalesReturnDto>.Failure("حدث خطأ غير متوقع أثناء ترحيل المرتجع.");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CANCEL
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CancelAsync", "SalesReturn", id);

            var salesReturn = await _returnRepo.GetWithLinesAsync(id, ct);
            if (salesReturn == null)
                return ServiceResult.Failure(ReturnNotFoundMessage);

            if (salesReturn.Status != InvoiceStatus.Posted)
                return ServiceResult.Failure("لا يمكن إلغاء إلا المرتجعات المرحّلة.");

            if (!salesReturn.JournalEntryId.HasValue)
                return ServiceResult.Failure("لا يمكن إلغاء مرتجع بدون قيد إيراد محاسبي.");

            try
            {
                var context = await _fiscalValidator.ValidateForCancelAsync(salesReturn.ReturnDate, ct);
                await ExecuteCancelAsync(salesReturn, context, ct);
                return ServiceResult.Success();
            }
            catch (SalesReturnDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while cancelling sales return.");
                return ServiceResult.Failure(
                    ErrorSanitizer.Sanitize(ex, "إلغاء مرتجع البيع"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult.Failure("تعذر إلغاء المرتجع بسبب تعارض تحديث متزامن. يرجى إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult.Failure("تعذر إلغاء المرتجع بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel sales return {ReturnId}.", salesReturn?.Id);
                return ServiceResult.Failure("حدث خطأ غير متوقع أثناء إلغاء مرتجع البيع.");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DELETE (Draft only)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteDraftAsync", "SalesReturn", id);

            var salesReturn = await _returnRepo.GetWithLinesAsync(id, ct);
            if (salesReturn == null)
                return ServiceResult.Failure(ReturnNotFoundMessage);

            if (salesReturn.Status != InvoiceStatus.Draft)
                return ServiceResult.Failure("لا يمكن حذف إلا المرتجعات المسودة.");

            salesReturn.SoftDelete(_currentUser.Username, _dateTime.UtcNow);
            _returnRepo.Update(salesReturn);
            await _unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Success();
        }

        private async Task<SalesReturn> ExecutePostAsync(SalesReturn salesReturn, CancellationToken ct)
        {
            SalesReturn saved = null;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Use tracked query to avoid EF Core graph attachment conflicts
                var reloaded = await _returnRepo.GetWithLinesTrackedAsync(salesReturn.Id, ct);
                if (reloaded == null)
                    throw new SalesReturnDomainException(ReturnNotFoundMessage);
                if (reloaded.Status != InvoiceStatus.Draft)
                    throw new SalesReturnDomainException("لا يمكن ترحيل مرتجع مرحّل بالفعل أو ملغى.");

                // Re-validate return quantities against original invoice inside the transaction
                if (reloaded.OriginalInvoiceId.HasValue)
                {
                    var originalInvoice = await _invoiceRepo.GetWithLinesAsync(reloaded.OriginalInvoiceId.Value, ct);
                    if (originalInvoice == null)
                        throw new SalesReturnDomainException("الفاتورة الأصلية غير موجودة.");

                    if (originalInvoice.Status != InvoiceStatus.Posted)
                        throw new SalesReturnDomainException("لا يمكن ترحيل مرتجع إلا لفاتورة مرحّلة.");

                    var previousReturns = await _returnRepo.GetByOriginalInvoiceAsync(reloaded.OriginalInvoiceId.Value, ct);
                    var previouslyReturnedQty = previousReturns
                        .Where(r => r.Status != InvoiceStatus.Cancelled && r.Id != reloaded.Id)
                        .SelectMany(r => r.Lines)
                        .GroupBy(l => new { l.ProductId, l.UnitId })
                        .ToDictionary(
                            g => g.Key,
                            g => g.Sum(l => l.Quantity));

                    foreach (var line in reloaded.Lines)
                    {
                        var invoiceLine = originalInvoice.Lines
                            .FirstOrDefault(l => l.ProductId == line.ProductId && l.UnitId == line.UnitId);

                        if (invoiceLine == null)
                            throw new SalesReturnDomainException(
                                $"الصنف {line.ProductId} بالوحدة {line.UnitId} غير موجود في الفاتورة الأصلية.");

                        var alreadyReturned = previouslyReturnedQty
                            .GetValueOrDefault(new { line.ProductId, line.UnitId }, 0m);
                        var remainingReturnable = invoiceLine.Quantity - alreadyReturned;

                        if (line.Quantity > remainingReturnable)
                            throw new SalesReturnDomainException(
                                $"كمية المرتجع ({line.Quantity}) تتجاوز الكمية المتاحة للإرجاع ({remainingReturnable}) للصنف {line.ProductId}. " +
                                $"(الكمية الأصلية: {invoiceLine.Quantity}، تم إرجاع: {alreadyReturned})");
                    }
                }

                var context = await _fiscalValidator.ValidateForPostingAsync(reloaded.ReturnDate, ct);
                var accounts = await ResolveAccountsAsync(ct);

                var revenueJournal = await CreateRevenueReversalJournalAsync(reloaded, accounts, context, ct);
                var cogsResult = await CreateCogsReversalJournalAsync(reloaded, accounts, context, ct);

                await _unitOfWork.SaveChangesAsync(ct);

                await IncreaseStockAsync(reloaded, cogsResult.lineCosts, ct);

                reloaded.Post(revenueJournal.Id, cogsResult.journal?.Id);
                // Entity is already tracked — no need for explicit Update

                // C-08 fix: Update BalanceDue on original invoice when posting return
                if (reloaded.OriginalInvoiceId.HasValue)
                {
                    var originalInvoice = await _invoiceRepo.GetByIdAsync(reloaded.OriginalInvoiceId.Value, ct);
                    if (originalInvoice != null && originalInvoice.Status == InvoiceStatus.Posted)
                    {
                        originalInvoice.ApplyReturnCredit(reloaded.NetTotal);
                        _invoiceRepo.Update(originalInvoice);
                    }
                }

                await _unitOfWork.SaveChangesAsync(ct);

                saved = await _returnRepo.GetWithLinesAsync(reloaded.Id, ct);
            }, IsolationLevel.Serializable, ct);

            return saved ?? salesReturn;
        }

        private async Task<SalesReturnAccounts> ResolveAccountsAsync(CancellationToken ct)
        {
            var arAccount = await _accountRepo.GetByCodeAsync(ArAccountCode, ct);
            var salesAccount = await _accountRepo.GetByCodeAsync(SalesAccountCode, ct);
            var vatOutputAccount = await _accountRepo.GetByCodeAsync(VatOutputAccountCode, ct);
            var cogsAccount = await _accountRepo.GetByCodeAsync(CogsAccountCode, ct);
            var inventoryAccount = await _accountRepo.GetByCodeAsync(InventoryAccountCode, ct);

            if (arAccount == null || salesAccount == null || vatOutputAccount == null
                || cogsAccount == null || inventoryAccount == null)
                throw new SalesInvoiceDomainException(
                    "حسابات النظام المطلوبة غير موجودة. تأكد من تشغيل Seed.");

            return new SalesReturnAccounts
            {
                Ar = arAccount,
                Sales = salesAccount,
                VatOutput = vatOutputAccount,
                Cogs = cogsAccount,
                Inventory = inventoryAccount
            };
        }

        private async Task<JournalEntry> CreateRevenueReversalJournalAsync(
            SalesReturn salesReturn,
            SalesReturnAccounts accounts,
            PostingContext context,
            CancellationToken ct)
        {
            var netSalesRevenue = salesReturn.Subtotal - salesReturn.DiscountTotal + salesReturn.DeliveryFee;
            var lines = new List<JournalLineSpec>();

            if (netSalesRevenue > 0)
                lines.Add(new JournalLineSpec(accounts.Sales.Id, netSalesRevenue, 0,
                    $"مبيعات — مرتجع بيع {salesReturn.ReturnNumber}"));

            if (salesReturn.VatTotal > 0)
                lines.Add(new JournalLineSpec(accounts.VatOutput.Id, salesReturn.VatTotal, 0,
                    $"ضريبة مخرجات — مرتجع بيع {salesReturn.ReturnNumber}"));

            lines.Add(new JournalLineSpec(accounts.Ar.Id, 0, salesReturn.NetTotal,
                $"عميل — مرتجع بيع {salesReturn.ReturnNumber}"));

            return await _journalFactory.CreateAndPostAsync(
                salesReturn.ReturnDate,
                $"مرتجع بيع رقم {salesReturn.ReturnNumber}",
                SourceType.SalesReturn,
                context.FiscalYear.Id,
                context.Period.Id,
                lines,
                context.Username,
                context.Now,
                referenceNumber: salesReturn.ReturnNumber,
                sourceId: salesReturn.Id,
                ct: ct);
        }

        private async Task<(JournalEntry journal, Dictionary<int, decimal> lineCosts)> CreateCogsReversalJournalAsync(
            SalesReturn salesReturn,
            SalesReturnAccounts accounts,
            PostingContext context,
            CancellationToken ct)
        {
            decimal totalCogs = 0;
            var lineCosts = new Dictionary<int, decimal>();

            foreach (var line in salesReturn.Lines)
            {
                var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                var costPerBaseUnit = product.WeightedAverageCost;
                var lineCost = Math.Round(line.BaseQuantity * costPerBaseUnit, 4);
                totalCogs += lineCost;
                lineCosts[line.Id] = costPerBaseUnit;
            }

            var lines = new List<JournalLineSpec>();
            if (totalCogs > 0)
            {
                lines.Add(new JournalLineSpec(accounts.Inventory.Id, totalCogs, 0,
                    $"مخزون — عكس تكلفة بضاعة {salesReturn.ReturnNumber}"));
                lines.Add(new JournalLineSpec(accounts.Cogs.Id, 0, totalCogs,
                    $"تكلفة بضاعة — عكس مرتجع بيع {salesReturn.ReturnNumber}"));
            }

            // Guard: skip journal creation when total cost is zero (new system before first purchase).
            if (totalCogs <= 0)
                return (null, lineCosts);

            var journal = await _journalFactory.CreateAndPostAsync(
                salesReturn.ReturnDate,
                $"عكس تكلفة بضاعة — مرتجع بيع {salesReturn.ReturnNumber}",
                SourceType.SalesReturn,
                context.FiscalYear.Id,
                context.Period.Id,
                lines,
                context.Username,
                context.Now,
                referenceNumber: salesReturn.ReturnNumber,
                sourceId: salesReturn.Id,
                ct: ct);

            return (journal, lineCosts);
        }

        private async Task IncreaseStockAsync(
            SalesReturn salesReturn,
            Dictionary<int, decimal> lineCosts,
            CancellationToken ct)
        {
            // Running totals to handle duplicate product lines in same return (mirrors PurchaseInvoice pattern)
            var runningTotals = new Dictionary<int, (Product product, decimal totalQty)>();

            foreach (var line in salesReturn.Lines)
            {
                var costPerBaseUnit = lineCosts.TryGetValue(line.Id, out var cost) ? cost : 0;

                // ── WAC recalculation: returned stock re-enters at original cost ──
                if (!runningTotals.TryGetValue(line.ProductId, out var state))
                {
                    var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                    var existingTotalQty = await _stockManager.GetTotalStockAsync(line.ProductId, ct);
                    state = (product, existingTotalQty);
                }

                if (state.totalQty <= 0)
                {
                    state.product.SetWeightedAverageCost(costPerBaseUnit);
                }
                else
                {
                    state.product.UpdateWeightedAverageCost(state.totalQty, line.BaseQuantity, costPerBaseUnit);
                }

                // Track running total so next line for same product uses correct base qty
                state.totalQty += line.BaseQuantity;
                runningTotals[line.ProductId] = state;
                _productRepo.Update(state.product);

                await _stockManager.IncreaseAsync(new StockOperation
                {
                    ProductId = line.ProductId,
                    WarehouseId = salesReturn.WarehouseId,
                    UnitId = line.UnitId,
                    MovementType = MovementType.SalesReturn,
                    Quantity = line.Quantity,
                    BaseQuantity = line.BaseQuantity,
                    CostPerBaseUnit = costPerBaseUnit,
                    DocumentDate = salesReturn.ReturnDate,
                    DocumentNumber = salesReturn.ReturnNumber,
                    SourceType = SourceType.SalesReturn,
                    SourceId = salesReturn.Id,
                    Notes = $"مرتجع بيع رقم {salesReturn.ReturnNumber}",
                }, ct);
            }
        }

        private async Task ExecuteCancelAsync(
            SalesReturn salesReturn,
            CancelContext context,
            CancellationToken ct)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Reload as tracked inside the transaction to ensure fresh data
                // and avoid stale-entity issues (mirrors PurchaseInvoice cancel pattern)
                var tracked = await _returnRepo.GetWithLinesTrackedAsync(salesReturn.Id, ct)
                    ?? throw new SalesInvoiceDomainException(ReturnNotFoundMessage);

                await ReverseStockAsync(tracked, context, ct);

                await ReverseJournalAsync(
                    tracked.JournalEntryId.Value,
                    $"عكس مرتجع بيع رقم {tracked.ReturnNumber}",
                    "قيد الإيراد الأصلي غير موجود.",
                    context,
                    ct);

                // COGS reversal only if COGS journal exists (may be null when products have zero WAC)
                if (tracked.CogsJournalEntryId.HasValue)
                {
                    await ReverseJournalAsync(
                        tracked.CogsJournalEntryId.Value,
                        $"عكس تكلفة مرتجع بيع رقم {tracked.ReturnNumber}",
                        "قيد التكلفة الأصلي غير موجود.",
                        context,
                        ct);
                }

                tracked.Cancel();
                // Entity is already tracked — no need for explicit Update

                // C-08 fix: Reverse payment on original invoice when cancelling return
                if (tracked.OriginalInvoiceId.HasValue)
                {
                    var originalInvoice = await _invoiceRepo.GetByIdAsync(tracked.OriginalInvoiceId.Value, ct);
                    if (originalInvoice != null && originalInvoice.PaidAmount > 0)
                    {
                        var reversalAmount = Math.Min(tracked.NetTotal, originalInvoice.PaidAmount);
                        originalInvoice.ReverseReturnCredit(reversalAmount);
                        _invoiceRepo.Update(originalInvoice);
                    }
                }

                await _unitOfWork.SaveChangesAsync(ct);

            }, IsolationLevel.Serializable, ct);
        }

        private async Task ReverseStockAsync(
            SalesReturn salesReturn,
            CancelContext context,
            CancellationToken ct)
        {
            var allowNegativeStock = await IsNegativeStockAllowedAsync(ct);
            foreach (var line in salesReturn.Lines)
            {
                var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                var costPerBaseUnit = product.WeightedAverageCost;

                await _stockManager.DecreaseAsync(new StockOperation
                {
                    ProductId = line.ProductId,
                    WarehouseId = salesReturn.WarehouseId,
                    UnitId = line.UnitId,
                    MovementType = MovementType.SalesOut,
                    Quantity = line.Quantity,
                    BaseQuantity = line.BaseQuantity,
                    CostPerBaseUnit = costPerBaseUnit,
                    DocumentDate = context.Today,
                    DocumentNumber = salesReturn.ReturnNumber,
                    SourceType = SourceType.SalesReturn,
                    SourceId = salesReturn.Id,
                    Notes = $"إلغاء مرتجع بيع رقم {salesReturn.ReturnNumber}",
                    AllowCreate = false,
                    AllowNegativeStock = allowNegativeStock,
                }, ct);
            }
        }

        private async Task ReverseJournalAsync(
            int journalId,
            string description,
            string notFoundMessage,
            CancelContext context,
            CancellationToken ct)
        {
            var original = await _journalRepo.GetWithLinesAsync(journalId, ct);
            if (original == null)
                throw new SalesInvoiceDomainException(notFoundMessage);

            var reversal = original.CreateReversal(
                context.Today,
                description,
                context.FiscalYear.Id,
                context.Period.Id);

            var reversalNumber = await _journalNumberGen.NextNumberAsync(context.FiscalYear.Id, ct);
            var username = _currentUser.Username ?? "System";
            var now = _dateTime.UtcNow;
            reversal.Post(reversalNumber, username, now);
            await _journalRepo.AddAsync(reversal, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            original.MarkAsReversed(reversal.Id);
            _journalRepo.Update(original);
        }

        private sealed class SalesReturnAccounts
        {
            public Account Ar { get; init; } = default!;
            public Account Sales { get; init; } = default!;
            public Account VatOutput { get; init; } = default!;
            public Account Cogs { get; init; } = default!;
            public Account Inventory { get; init; } = default!;
        }

        private async Task<bool> IsNegativeStockAllowedAsync(CancellationToken ct)
        {
            if (_featureService == null)
                return false;

            var result = await _featureService.IsEnabledAsync(FeatureKeys.AllowNegativeStock, ct);
            return result.IsSuccess && result.Data;
        }

    }

    public sealed class SalesReturnRepositories
    {
        public SalesReturnRepositories(
            ISalesReturnRepository returnRepo,
            IProductRepository productRepo,
            IWarehouseProductRepository whProductRepo,
            IInventoryMovementRepository movementRepo,
            IJournalEntryRepository journalRepo,
            IAccountRepository accountRepo,
            ISalesInvoiceRepository invoiceRepo)
        {
            ReturnRepo = returnRepo ?? throw new ArgumentNullException(nameof(returnRepo));
            ProductRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
            WhProductRepo = whProductRepo ?? throw new ArgumentNullException(nameof(whProductRepo));
            MovementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
            JournalRepo = journalRepo ?? throw new ArgumentNullException(nameof(journalRepo));
            AccountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
            InvoiceRepo = invoiceRepo ?? throw new ArgumentNullException(nameof(invoiceRepo));
        }

        public ISalesReturnRepository ReturnRepo { get; }
        public IProductRepository ProductRepo { get; }
        public IWarehouseProductRepository WhProductRepo { get; }
        public IInventoryMovementRepository MovementRepo { get; }
        public IJournalEntryRepository JournalRepo { get; }
        public IAccountRepository AccountRepo { get; }
        public ISalesInvoiceRepository InvoiceRepo { get; }
    }

    public sealed class SalesReturnServices
    {
        public SalesReturnServices(
            IFiscalYearRepository fiscalYearRepo,
            IJournalNumberGenerator journalNumberGen,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime,
            ISystemSettingRepository systemSettingRepo = null)
        {
            FiscalYearRepo = fiscalYearRepo ?? throw new ArgumentNullException(nameof(fiscalYearRepo));
            JournalNumberGen = journalNumberGen ?? throw new ArgumentNullException(nameof(journalNumberGen));
            UnitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            CurrentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            DateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            SystemSettingRepo = systemSettingRepo;
        }

        public IFiscalYearRepository FiscalYearRepo { get; }
        public IJournalNumberGenerator JournalNumberGen { get; }
        public IUnitOfWork UnitOfWork { get; }
        public ICurrentUserService CurrentUser { get; }
        public IDateTimeProvider DateTime { get; }
        public ISystemSettingRepository SystemSettingRepo { get; }
    }

    public sealed class SalesReturnValidators
    {
        public SalesReturnValidators(
            IValidator<CreateSalesReturnDto> createValidator,
            IValidator<UpdateSalesReturnDto> updateValidator)
        {
            CreateValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            UpdateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
        }

        public IValidator<CreateSalesReturnDto> CreateValidator { get; }
        public IValidator<UpdateSalesReturnDto> UpdateValidator { get; }
    }
}
