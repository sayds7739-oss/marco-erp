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
using MarcoERP.Domain.Exceptions.Sales;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Sales;
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
        private readonly IWarehouseProductRepository _whProductRepo;
        private readonly IInventoryMovementRepository _movementRepo;
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
            ILogger<SalesReturnService> logger)
        {
            if (repos == null) throw new ArgumentNullException(nameof(repos));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (validators == null) throw new ArgumentNullException(nameof(validators));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            _returnRepo = repos.ReturnRepo;
            _productRepo = repos.ProductRepo;
            _whProductRepo = repos.WhProductRepo;
            _movementRepo = repos.MovementRepo;
            _journalRepo = repos.JournalRepo;
            _accountRepo = repos.AccountRepo;
            _invoiceRepo = repos.InvoiceRepo;

            _fiscalYearRepo = services.FiscalYearRepo;
            _journalNumberGen = services.JournalNumberGen;
            _unitOfWork = services.UnitOfWork;
            _currentUser = services.CurrentUser;
            _dateTime = services.DateTime;

            _createValidator = validators.CreateValidator;
            _updateValidator = validators.UpdateValidator;
            _logger = logger;
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
            var authCheck = AuthorizationGuard.Check<SalesReturnDto>(_currentUser, PermissionKeys.SalesCreate);
            if (authCheck != null) return authCheck;

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<SalesReturnDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            try
            {
                // ── Validate return quantities against original invoice ──
                if (dto.OriginalInvoiceId.HasValue)
                {
                    var originalInvoice = await _invoiceRepo.GetWithLinesAsync(dto.OriginalInvoiceId.Value, ct);
                    if (originalInvoice == null)
                        return ServiceResult<SalesReturnDto>.Failure("الفاتورة الأصلية غير موجودة.");

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
                            return ServiceResult<SalesReturnDto>.Failure(
                                $"الصنف {lineDto.ProductId} بالوحدة {lineDto.UnitId} غير موجود في الفاتورة الأصلية.");

                        var alreadyReturned = previouslyReturnedQty
                            .GetValueOrDefault(new { lineDto.ProductId, lineDto.UnitId }, 0m);
                        var remainingReturnable = invoiceLine.Quantity - alreadyReturned;

                        if (lineDto.Quantity > remainingReturnable)
                            return ServiceResult<SalesReturnDto>.Failure(
                                $"كمية المرتجع ({lineDto.Quantity}) تتجاوز الكمية المتاحة للإرجاع ({remainingReturnable}) للصنف {lineDto.ProductId}. " +
                                $"(الكمية الأصلية: {invoiceLine.Quantity}، تم إرجاع: {alreadyReturned})");
                    }
                }

                var returnNumber = await _returnRepo.GetNextNumberAsync(ct);

                var salesReturn = new SalesReturn(
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
                        return ServiceResult<SalesReturnDto>.Failure($"الصنف برقم {lineDto.ProductId} غير موجود.");

                    var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                    if (productUnit == null)
                        return ServiceResult<SalesReturnDto>.Failure(
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

                var saved = await _returnRepo.GetWithLinesAsync(salesReturn.Id, ct);
                return ServiceResult<SalesReturnDto>.Success(SalesReturnMapper.ToDto(saved));
            }
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult<SalesReturnDto>.Failure(ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  UPDATE (Draft only)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<SalesReturnDto>> UpdateAsync(UpdateSalesReturnDto dto, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<SalesReturnDto>(_currentUser, PermissionKeys.SalesCreate);
            if (authCheck != null) return authCheck;

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<SalesReturnDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var salesReturn = await _returnRepo.GetWithLinesAsync(dto.Id, ct);
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
                        product.VatRate));
                }

                salesReturn.ReplaceLines(newLines);
                _returnRepo.Update(salesReturn);
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _returnRepo.GetWithLinesAsync(salesReturn.Id, ct);
                return ServiceResult<SalesReturnDto>.Success(SalesReturnMapper.ToDto(saved));
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
            var authCheck = AuthorizationGuard.Check<SalesReturnDto>(_currentUser, PermissionKeys.SalesPost);
            if (authCheck != null) return authCheck;

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
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult<SalesReturnDto>.Failure(ex.Message);
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
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.SalesPost);
            if (authCheck != null) return authCheck;

            var salesReturn = await _returnRepo.GetWithLinesAsync(id, ct);
            if (salesReturn == null)
                return ServiceResult.Failure(ReturnNotFoundMessage);

            if (salesReturn.Status != InvoiceStatus.Posted)
                return ServiceResult.Failure("لا يمكن إلغاء إلا المرتجعات المرحّلة.");

            if (!salesReturn.JournalEntryId.HasValue || !salesReturn.CogsJournalEntryId.HasValue)
                return ServiceResult.Failure("لا يمكن إلغاء مرتجع بدون قيود محاسبية.");

            try
            {
                var context = await GetCancelContextAsync(salesReturn.ReturnDate, ct);
                await ExecuteCancelAsync(salesReturn, context, ct);
                return ServiceResult.Success();
            }
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
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
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.SalesCreate);
            if (authCheck != null) return authCheck;

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
                var context = await GetPostingContextAsync(salesReturn.ReturnDate, ct);
                var accounts = await ResolveAccountsAsync(ct);

                var revenueJournal = await CreateRevenueReversalJournalAsync(salesReturn, accounts, context, ct);
                var cogsResult = await CreateCogsReversalJournalAsync(salesReturn, accounts, context, ct);

                await _unitOfWork.SaveChangesAsync(ct);

                await IncreaseStockAsync(salesReturn, cogsResult.lineCosts, ct);

                salesReturn.Post(revenueJournal.Id, cogsResult.journal.Id);
                _returnRepo.Update(salesReturn);
                await _unitOfWork.SaveChangesAsync(ct);

                saved = await _returnRepo.GetWithLinesAsync(salesReturn.Id, ct);
            }, IsolationLevel.Serializable, ct);

            return saved ?? salesReturn;
        }

        private async Task<SalesReturnPostingContext> GetPostingContextAsync(DateTime returnDate, CancellationToken ct)
        {
            var fiscalYear = await _fiscalYearRepo.GetActiveYearAsync(ct);
            if (fiscalYear == null)
                throw new SalesInvoiceDomainException("لا توجد سنة مالية نشطة.");

            if (!fiscalYear.ContainsDate(returnDate))
                throw new SalesInvoiceDomainException(
                    $"تاريخ المرتجع {returnDate:yyyy-MM-dd} لا يقع ضمن السنة المالية النشطة.");

            var yearWithPeriods = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            var period = yearWithPeriods.GetPeriod(returnDate.Month);
            if (period == null)
                throw new SalesInvoiceDomainException("لا توجد فترة مالية للشهر المحدد.");

            if (!period.IsOpen)
                throw new SalesInvoiceDomainException(
                    $"الفترة المالية ({period.Year}-{period.Month:D2}) مقفلة. لا يمكن الترحيل.");

            return new SalesReturnPostingContext
            {
                FiscalYear = yearWithPeriods,
                Period = period,
                Now = _dateTime.UtcNow,
                Username = _currentUser.Username ?? "System"
            };
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
            SalesReturnPostingContext context,
            CancellationToken ct)
        {
            var netSalesRevenue = salesReturn.Subtotal - salesReturn.DiscountTotal;
            var revenueReversalJournal = JournalEntry.CreateDraft(
                salesReturn.ReturnDate,
                $"مرتجع بيع رقم {salesReturn.ReturnNumber}",
                SourceType.SalesReturn,
                context.FiscalYear.Id,
                context.Period.Id,
                referenceNumber: salesReturn.ReturnNumber,
                sourceId: salesReturn.Id);

            if (netSalesRevenue > 0)
                revenueReversalJournal.AddLine(accounts.Sales.Id, netSalesRevenue, 0, context.Now,
                    $"مبيعات — مرتجع بيع {salesReturn.ReturnNumber}");

            if (salesReturn.VatTotal > 0)
                revenueReversalJournal.AddLine(accounts.VatOutput.Id, salesReturn.VatTotal, 0, context.Now,
                    $"ضريبة مخرجات — مرتجع بيع {salesReturn.ReturnNumber}");

            revenueReversalJournal.AddLine(accounts.Ar.Id, 0, salesReturn.NetTotal, context.Now,
                $"عميل — مرتجع بيع {salesReturn.ReturnNumber}");

            var revenueJournalNumber = _journalNumberGen.NextNumber(context.FiscalYear.Id);
            revenueReversalJournal.Post(revenueJournalNumber, context.Username, context.Now);
            await _journalRepo.AddAsync(revenueReversalJournal, ct);

            return revenueReversalJournal;
        }

        private async Task<(JournalEntry journal, Dictionary<int, decimal> lineCosts)> CreateCogsReversalJournalAsync(
            SalesReturn salesReturn,
            SalesReturnAccounts accounts,
            SalesReturnPostingContext context,
            CancellationToken ct)
        {
            var cogsReversalJournal = JournalEntry.CreateDraft(
                salesReturn.ReturnDate,
                $"عكس تكلفة بضاعة — مرتجع بيع {salesReturn.ReturnNumber}",
                SourceType.SalesReturn,
                context.FiscalYear.Id,
                context.Period.Id,
                referenceNumber: salesReturn.ReturnNumber,
                sourceId: salesReturn.Id);

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

            if (totalCogs > 0)
            {
                cogsReversalJournal.AddLine(accounts.Inventory.Id, totalCogs, 0, context.Now,
                    $"مخزون — عكس تكلفة بضاعة {salesReturn.ReturnNumber}");

                cogsReversalJournal.AddLine(accounts.Cogs.Id, 0, totalCogs, context.Now,
                    $"تكلفة بضاعة — عكس مرتجع بيع {salesReturn.ReturnNumber}");
            }

            var cogsJournalNumber = _journalNumberGen.NextNumber(context.FiscalYear.Id);
            cogsReversalJournal.Post(cogsJournalNumber, context.Username, context.Now);
            await _journalRepo.AddAsync(cogsReversalJournal, ct);

            return (cogsReversalJournal, lineCosts);
        }

        private async Task IncreaseStockAsync(
            SalesReturn salesReturn,
            Dictionary<int, decimal> lineCosts,
            CancellationToken ct)
        {
            foreach (var line in salesReturn.Lines)
            {
                var whProduct = await _whProductRepo.GetOrCreateAsync(
                    salesReturn.WarehouseId, line.ProductId, ct);

                whProduct.IncreaseStock(line.BaseQuantity);
                _whProductRepo.Update(whProduct);

                var costPerBaseUnit = lineCosts.TryGetValue(line.Id, out var cost) ? cost : 0;
                var lineCost = Math.Round(line.BaseQuantity * costPerBaseUnit, 4);

                var movement = new InventoryMovement(
                    line.ProductId,
                    salesReturn.WarehouseId,
                    line.UnitId,
                    MovementType.SalesReturn,
                    line.Quantity,
                    line.BaseQuantity,
                    costPerBaseUnit,
                    lineCost,
                    salesReturn.ReturnDate,
                    salesReturn.ReturnNumber,
                    SourceType.SalesReturn,
                    sourceId: salesReturn.Id,
                    notes: $"مرتجع بيع رقم {salesReturn.ReturnNumber}");

                movement.SetBalanceAfter(whProduct.Quantity);
                await _movementRepo.AddAsync(movement, ct);
            }
        }

        private async Task<SalesReturnCancelContext> GetCancelContextAsync(DateTime returnDate, CancellationToken ct)
        {
            var fiscalYear = await _fiscalYearRepo.GetByYearAsync(returnDate.Year, ct);
            if (fiscalYear == null)
                throw new SalesInvoiceDomainException($"لا توجد سنة مالية للعام {returnDate.Year}.");

            fiscalYear = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            if (fiscalYear.Status != FiscalYearStatus.Active)
                throw new SalesInvoiceDomainException($"السنة المالية {fiscalYear.Year} ليست فعّالة.");

            var period = fiscalYear.GetPeriod(returnDate.Month);
            if (period == null || !period.IsOpen)
                throw new SalesInvoiceDomainException($"الفترة المالية لـ {returnDate:yyyy-MM} مُقفلة.");

            return new SalesReturnCancelContext
            {
                FiscalYear = fiscalYear,
                Period = period,
                Today = returnDate
            };
        }

        private async Task ExecuteCancelAsync(
            SalesReturn salesReturn,
            SalesReturnCancelContext context,
            CancellationToken ct)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await ReverseStockAsync(salesReturn, context, ct);

                await ReverseJournalAsync(
                    salesReturn.JournalEntryId.Value,
                    $"عكس مرتجع بيع رقم {salesReturn.ReturnNumber}",
                    "قيد الإيراد الأصلي غير موجود.",
                    context,
                    ct);

                await ReverseJournalAsync(
                    salesReturn.CogsJournalEntryId.Value,
                    $"عكس تكلفة مرتجع بيع رقم {salesReturn.ReturnNumber}",
                    "قيد التكلفة الأصلي غير موجود.",
                    context,
                    ct);

                salesReturn.Cancel();
                _returnRepo.Update(salesReturn);
                await _unitOfWork.SaveChangesAsync(ct);

            }, IsolationLevel.Serializable, ct);
        }

        private async Task ReverseStockAsync(
            SalesReturn salesReturn,
            SalesReturnCancelContext context,
            CancellationToken ct)
        {
            foreach (var line in salesReturn.Lines)
            {
                var whProduct = await _whProductRepo.GetAsync(
                    salesReturn.WarehouseId, line.ProductId, ct);

                if (whProduct == null)
                    throw new InvalidOperationException(
                        $"سجل المخزون غير موجود للمنتج {line.ProductId} في المستودع {salesReturn.WarehouseId}. لا يمكن إلغاء المرتجع.");

                if (whProduct.Quantity < line.BaseQuantity)
                    throw new InvalidOperationException(
                        $"الكمية المتاحة ({whProduct.Quantity}) أقل من كمية المرتجع ({line.BaseQuantity}) للمنتج {line.ProductId}. لا يمكن إلغاء المرتجع.");

                whProduct.DecreaseStock(line.BaseQuantity);
                _whProductRepo.Update(whProduct);

                var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                var costPerBaseUnit = product.WeightedAverageCost;
                var lineCost = Math.Round(line.BaseQuantity * costPerBaseUnit, 4);

                var movement = new InventoryMovement(
                    line.ProductId,
                    salesReturn.WarehouseId,
                    line.UnitId,
                    MovementType.SalesOut,
                    line.Quantity,
                    line.BaseQuantity,
                    costPerBaseUnit,
                    lineCost,
                    context.Today,
                    salesReturn.ReturnNumber,
                    SourceType.SalesReturn,
                    sourceId: salesReturn.Id,
                    notes: $"إلغاء مرتجع بيع رقم {salesReturn.ReturnNumber}");

                movement.SetBalanceAfter(whProduct.Quantity);
                await _movementRepo.AddAsync(movement, ct);
            }
        }

        private async Task ReverseJournalAsync(
            int journalId,
            string description,
            string notFoundMessage,
            SalesReturnCancelContext context,
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

            var reversalNumber = _journalNumberGen.NextNumber(context.FiscalYear.Id);
            var username = _currentUser.Username ?? "System";
            var now = _dateTime.UtcNow;
            reversal.Post(reversalNumber, username, now);
            await _journalRepo.AddAsync(reversal, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            original.MarkAsReversed(reversal.Id);
            _journalRepo.Update(original);
        }

        private sealed class SalesReturnPostingContext
        {
            public FiscalYear FiscalYear { get; init; } = default!;
            public FiscalPeriod Period { get; init; } = default!;
            public DateTime Now { get; init; }
            public string Username { get; init; } = string.Empty;
        }

        private sealed class SalesReturnAccounts
        {
            public Account Ar { get; init; } = default!;
            public Account Sales { get; init; } = default!;
            public Account VatOutput { get; init; } = default!;
            public Account Cogs { get; init; } = default!;
            public Account Inventory { get; init; } = default!;
        }

        private sealed class SalesReturnCancelContext
        {
            public FiscalYear FiscalYear { get; init; } = default!;
            public FiscalPeriod Period { get; init; } = default!;
            public DateTime Today { get; init; }
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
            IDateTimeProvider dateTime)
        {
            FiscalYearRepo = fiscalYearRepo ?? throw new ArgumentNullException(nameof(fiscalYearRepo));
            JournalNumberGen = journalNumberGen ?? throw new ArgumentNullException(nameof(journalNumberGen));
            UnitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            CurrentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            DateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
        }

        public IFiscalYearRepository FiscalYearRepo { get; }
        public IJournalNumberGenerator JournalNumberGen { get; }
        public IUnitOfWork UnitOfWork { get; }
        public ICurrentUserService CurrentUser { get; }
        public IDateTimeProvider DateTime { get; }
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
