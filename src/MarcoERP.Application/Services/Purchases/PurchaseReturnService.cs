using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Application.Mappers.Purchases;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Enums;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Exceptions.Purchases;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Purchases;
using MarcoERP.Domain.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Purchases
{
    /// <summary>
    /// Implements purchase return lifecycle: Create → Edit → Post → (Cancel).
    /// On Post: reversal journal (CR Inventory + CR VAT / DR AP), stock deduction.
    /// </summary>
    [Module(SystemModule.Purchases)]
    public sealed class PurchaseReturnService : IPurchaseReturnService
    {
        private readonly IPurchaseReturnRepository _returnRepo;
        private readonly IPurchaseInvoiceRepository _invoiceRepo;
        private readonly IProductRepository _productRepo;
        private readonly IJournalEntryRepository _journalRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly IJournalNumberGenerator _journalNumberGen;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreatePurchaseReturnDto> _createValidator;
        private readonly IValidator<UpdatePurchaseReturnDto> _updateValidator;
        private readonly ILogger<PurchaseReturnService> _logger;
        private readonly JournalEntryFactory _journalFactory;
        private readonly FiscalPeriodValidator _fiscalValidator;
        private readonly IFeatureService _featureService;
        private readonly ISystemSettingRepository _systemSettingRepository;
        private readonly StockManager _stockManager;

        // ── GL Account Codes (from SystemAccountSeed) ───────────
        private const string InventoryAccountCode = "1131";
        private const string VatInputAccountCode = "1141";
        private const string ApAccountCode = "2111";
        private const string ReturnNotFoundMessage = "مرتجع الشراء غير موجود.";

        public PurchaseReturnService(
            PurchaseReturnRepositories repos,
            PurchaseReturnServices services,
            PurchaseReturnValidators validators,
            JournalEntryFactory journalFactory,
            FiscalPeriodValidator fiscalValidator,
            StockManager stockManager,
            ILogger<PurchaseReturnService> logger = null,
            IFeatureService featureService = null)
        {
            if (repos == null) throw new ArgumentNullException(nameof(repos));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (validators == null) throw new ArgumentNullException(nameof(validators));

            _returnRepo = repos.ReturnRepo;
            _invoiceRepo = repos.InvoiceRepo;
            _productRepo = repos.ProductRepo;
            _journalRepo = repos.JournalRepo;
            _accountRepo = repos.AccountRepo;

            _journalNumberGen = services.JournalNumberGen;
            _unitOfWork = services.UnitOfWork;
            _currentUser = services.CurrentUser;
            _dateTime = services.DateTime;

            _createValidator = validators.CreateValidator;
            _updateValidator = validators.UpdateValidator;
            _journalFactory = journalFactory ?? throw new ArgumentNullException(nameof(journalFactory));
            _fiscalValidator = fiscalValidator ?? throw new ArgumentNullException(nameof(fiscalValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PurchaseReturnService>.Instance;
            _featureService = featureService;
            _systemSettingRepository = services.SystemSettingRepo;
            _stockManager = stockManager ?? throw new ArgumentNullException(nameof(stockManager));
        }

        public async Task<ServiceResult<IReadOnlyList<PurchaseReturnListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _returnRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<PurchaseReturnListDto>>.Success(
                entities.Select(PurchaseReturnMapper.ToListDto).ToList());
        }

        public async Task<ServiceResult<PurchaseReturnDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _returnRepo.GetWithLinesAsync(id, ct);
            if (entity == null)
                return ServiceResult<PurchaseReturnDto>.Failure(ReturnNotFoundMessage);

            return ServiceResult<PurchaseReturnDto>.Success(PurchaseReturnMapper.ToDto(entity));
        }

        public async Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default)
        {
            var next = await _returnRepo.GetNextNumberAsync(ct);
            return ServiceResult<string>.Success(next);
        }

        public async Task<ServiceResult<PurchaseReturnDto>> CreateAsync(CreatePurchaseReturnDto dto, CancellationToken ct = default)
        {
            // Feature Guard — block operation if Purchases module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<PurchaseReturnDto>(_featureService, FeatureKeys.Purchases, ct);
                if (guard != null) return guard;
            }

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "PurchaseReturn", 0);

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<PurchaseReturnDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            const int maxRetries = 3;
            var attempt = 0;

            while (attempt < maxRetries)
            {
                attempt++;

                try
                {
                    PurchaseReturn purchaseReturn = null;

                    await _unitOfWork.ExecuteInTransactionAsync(async () =>
                    {
                        // ── Validate return quantities against original invoice ──
                        if (dto.OriginalInvoiceId.HasValue)
                        {
                            var originalInvoice = await _invoiceRepo.GetWithLinesAsync(dto.OriginalInvoiceId.Value, ct);
                            if (originalInvoice == null)
                                throw new PurchaseReturnDomainException("فاتورة الشراء الأصلية غير موجودة.");

                            // Get all previous non-cancelled returns for this invoice
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
                                    throw new PurchaseReturnDomainException(
                                        $"الصنف {lineDto.ProductId} بالوحدة {lineDto.UnitId} غير موجود في فاتورة الشراء الأصلية.");

                                var alreadyReturned = previouslyReturnedQty
                                    .GetValueOrDefault(new { lineDto.ProductId, lineDto.UnitId }, 0m);
                                var remainingReturnable = invoiceLine.Quantity - alreadyReturned;

                                if (lineDto.Quantity > remainingReturnable)
                                    throw new PurchaseReturnDomainException(
                                        $"كمية المرتجع ({lineDto.Quantity}) تتجاوز الكمية المتاحة للإرجاع ({remainingReturnable}) للصنف {lineDto.ProductId}. " +
                                        $"(الكمية الأصلية: {invoiceLine.Quantity}، تم إرجاع: {alreadyReturned})");
                            }
                        }

                        var returnNumber = await _returnRepo.GetNextNumberAsync(ct);

                        purchaseReturn = new PurchaseReturn(
                            returnNumber,
                            dto.ReturnDate,
                            dto.SupplierId,
                            dto.WarehouseId,
                            dto.OriginalInvoiceId,
                            dto.Notes,
                            salesRepresentativeId: dto.SalesRepresentativeId,
                            counterpartyType: dto.CounterpartyType,
                            customerId: dto.CounterpartyCustomerId);

                        foreach (var lineDto in dto.Lines)
                        {
                            var product = await _productRepo.GetByIdWithUnitsAsync(lineDto.ProductId, ct);
                            if (product == null)
                                throw new PurchaseReturnDomainException($"الصنف برقم {lineDto.ProductId} غير موجود.");

                            var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                            if (productUnit == null)
                                throw new PurchaseReturnDomainException(
                                    $"الوحدة المحددة غير مرتبطة بالصنف ({product.NameAr}).");

                            purchaseReturn.AddLine(
                                lineDto.ProductId,
                                lineDto.UnitId,
                                lineDto.Quantity,
                                lineDto.UnitPrice,
                                productUnit.ConversionFactor,
                                lineDto.DiscountPercent,
                                product.VatRate);
                        }

                        await _returnRepo.AddAsync(purchaseReturn, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }, IsolationLevel.Serializable, ct);

                    var saved = await _returnRepo.GetWithLinesAsync(purchaseReturn.Id, ct);
                    return ServiceResult<PurchaseReturnDto>.Success(PurchaseReturnMapper.ToDto(saved));
                }
                catch (DuplicateRecordException) when (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
                    continue;
                }
                catch (PurchaseReturnDomainException ex)
                {
                    return ServiceResult<PurchaseReturnDto>.Failure(ex.Message);
                }
                catch (PurchaseInvoiceDomainException ex)
                {
                    return ServiceResult<PurchaseReturnDto>.Failure(ex.Message);
                }
                catch (ConcurrencyConflictException)
                {
                    return ServiceResult<PurchaseReturnDto>.Failure("تم تعديل المرتجع بواسطة مستخدم آخر. يرجى إعادة المحاولة.");
                }
                catch (DuplicateRecordException)
                {
                    return ServiceResult<PurchaseReturnDto>.Failure("تعذر حفظ المرتجع بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
                }
            }

            return ServiceResult<PurchaseReturnDto>.Failure("فشل حفظ مرتجع الشراء بعد عدة محاولات.");
        }

        public async Task<ServiceResult<PurchaseReturnDto>> UpdateAsync(UpdatePurchaseReturnDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "PurchaseReturn", dto.Id);

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<PurchaseReturnDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var purchaseReturn = await _returnRepo.GetWithLinesTrackedAsync(dto.Id, ct);
            if (purchaseReturn == null)
                return ServiceResult<PurchaseReturnDto>.Failure(ReturnNotFoundMessage);

            if (purchaseReturn.Status != InvoiceStatus.Draft)
                return ServiceResult<PurchaseReturnDto>.Failure("لا يمكن تعديل مرتجع مرحّل أو ملغى.");

            try
            {
                purchaseReturn.UpdateHeader(dto.ReturnDate, dto.SupplierId, dto.WarehouseId,
                    dto.OriginalInvoiceId, dto.Notes,
                    salesRepresentativeId: dto.SalesRepresentativeId,
                    counterpartyType: dto.CounterpartyType,
                    customerId: dto.CounterpartyCustomerId);

                var newLines = new List<PurchaseReturnLine>();
                foreach (var lineDto in dto.Lines)
                {
                    var product = await _productRepo.GetByIdWithUnitsAsync(lineDto.ProductId, ct);
                    if (product == null)
                        return ServiceResult<PurchaseReturnDto>.Failure($"الصنف برقم {lineDto.ProductId} غير موجود.");

                    var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                    if (productUnit == null)
                        return ServiceResult<PurchaseReturnDto>.Failure(
                            $"الوحدة المحددة غير مرتبطة بالصنف ({product.NameAr}).");

                    newLines.Add(new PurchaseReturnLine(
                        lineDto.ProductId,
                        lineDto.UnitId,
                        lineDto.Quantity,
                        lineDto.UnitPrice,
                        productUnit.ConversionFactor,
                        lineDto.DiscountPercent,
                        product.VatRate,
                        lineDto.Id));
                }

                purchaseReturn.ReplaceLines(newLines);
                // Entity is already tracked — no need for _returnRepo.Update(purchaseReturn)
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _returnRepo.GetWithLinesAsync(purchaseReturn.Id, ct);
                return ServiceResult<PurchaseReturnDto>.Success(PurchaseReturnMapper.ToDto(saved));
            }
            catch (PurchaseReturnDomainException ex)
            {
                return ServiceResult<PurchaseReturnDto>.Failure(ex.Message);
            }
            catch (PurchaseInvoiceDomainException ex)
            {
                return ServiceResult<PurchaseReturnDto>.Failure(ex.Message);
            }
        }

        /// <summary>
        /// Posts a draft purchase return. This triggers:
        /// 1. Reversal journal: DR AP / CR Inventory + CR VAT Input
        /// 2. Stock deduction from warehouse
        /// 3. Inventory movement records
        /// </summary>
        public async Task<ServiceResult<PurchaseReturnDto>> PostAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "PostAsync", "PurchaseReturn", id);

            var purchaseReturn = await _returnRepo.GetWithLinesAsync(id, ct);
            if (purchaseReturn == null)
                return ServiceResult<PurchaseReturnDto>.Failure(ReturnNotFoundMessage);

            if (purchaseReturn.Status != InvoiceStatus.Draft)
                return ServiceResult<PurchaseReturnDto>.Failure("لا يمكن ترحيل مرتجع مرحّل بالفعل أو ملغى.");

            if (!purchaseReturn.Lines.Any())
                return ServiceResult<PurchaseReturnDto>.Failure("لا يمكن ترحيل مرتجع بدون بنود.");

            try
            {
                var saved = await ExecutePostAsync(purchaseReturn, ct);
                return ServiceResult<PurchaseReturnDto>.Success(PurchaseReturnMapper.ToDto(saved));
            }
            catch (PurchaseReturnDomainException ex)
            {
                return ServiceResult<PurchaseReturnDto>.Failure(ex.Message);
            }
            catch (PurchaseInvoiceDomainException ex)
            {
                return ServiceResult<PurchaseReturnDto>.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while posting purchase return {ReturnId}.", purchaseReturn?.Id);
                return ServiceResult<PurchaseReturnDto>.Failure(
                    ErrorSanitizer.Sanitize(ex, "ترحيل مرتجع الشراء"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<PurchaseReturnDto>.Failure("تعذر ترحيل المرتجع بسبب تعارض تحديث متزامن. يرجى إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult<PurchaseReturnDto>.Failure("تعذر ترحيل المرتجع بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post purchase return {ReturnId}.", purchaseReturn?.Id);
                return ServiceResult<PurchaseReturnDto>.Failure("حدث خطأ غير متوقع أثناء ترحيل المرتجع.");
            }
        }

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CancelAsync", "PurchaseReturn", id);

            var purchaseReturn = await _returnRepo.GetWithLinesAsync(id, ct);
            if (purchaseReturn == null)
                return ServiceResult.Failure(ReturnNotFoundMessage);

            if (purchaseReturn.Status != InvoiceStatus.Posted)
                return ServiceResult.Failure("لا يمكن إلغاء إلا المرتجعات المرحّلة.");

            if (!purchaseReturn.JournalEntryId.HasValue)
                return ServiceResult.Failure("لا يمكن إلغاء مرتجع بدون قيد محاسبي.");

            try
            {
                var context = await _fiscalValidator.ValidateForCancelAsync(purchaseReturn.ReturnDate, ct);
                await ExecuteCancelAsync(purchaseReturn, context, ct);
                return ServiceResult.Success();
            }
            catch (PurchaseReturnDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (PurchaseInvoiceDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while cancelling purchase return.");
                return ServiceResult.Failure(
                    ErrorSanitizer.Sanitize(ex, "إلغاء مرتجع الشراء"));
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
                _logger.LogError(ex, "Failed to cancel purchase return {ReturnId}.", purchaseReturn?.Id);
                return ServiceResult.Failure("حدث خطأ غير متوقع أثناء إلغاء مرتجع الشراء.");
            }
        }

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteDraftAsync", "PurchaseReturn", id);

            var purchaseReturn = await _returnRepo.GetWithLinesAsync(id, ct);
            if (purchaseReturn == null)
                return ServiceResult.Failure(ReturnNotFoundMessage);

            if (purchaseReturn.Status != InvoiceStatus.Draft)
                return ServiceResult.Failure("لا يمكن حذف إلا المرتجعات المسودة.");

            purchaseReturn.SoftDelete(_currentUser.Username, _dateTime.UtcNow);
            _returnRepo.Update(purchaseReturn);
            await _unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Success();
        }

        private async Task<PurchaseReturn> ExecutePostAsync(PurchaseReturn purchaseReturn, CancellationToken ct)
        {
            PurchaseReturn saved = null;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Use tracked query to avoid EF Core graph attachment conflicts
                var reloaded = await _returnRepo.GetWithLinesTrackedAsync(purchaseReturn.Id, ct);
                if (reloaded == null)
                    throw new PurchaseReturnDomainException(ReturnNotFoundMessage);
                if (reloaded.Status != InvoiceStatus.Draft)
                    throw new PurchaseReturnDomainException("لا يمكن ترحيل مرتجع مرحّل بالفعل أو ملغى.");

                var context = await _fiscalValidator.ValidateForPostingAsync(reloaded.ReturnDate, ct);
                var accounts = await ResolveAccountsAsync(ct);

                var journalEntry = await CreateReversalJournalAsync(reloaded, accounts, context, ct);

                await _unitOfWork.SaveChangesAsync(ct);

                await DeductStockAsync(reloaded, ct);

                reloaded.Post(journalEntry.Id);
                // Entity is already tracked — no need for explicit Update
                await _unitOfWork.SaveChangesAsync(ct);

                saved = await _returnRepo.GetWithLinesAsync(reloaded.Id, ct);
            }, IsolationLevel.Serializable, ct);

            return saved ?? purchaseReturn;
        }

        private async Task<PurchaseReturnAccounts> ResolveAccountsAsync(CancellationToken ct)
        {
            var inventoryAccount = await _accountRepo.GetByCodeAsync(InventoryAccountCode, ct);
            var vatInputAccount = await _accountRepo.GetByCodeAsync(VatInputAccountCode, ct);
            var apAccount = await _accountRepo.GetByCodeAsync(ApAccountCode, ct);

            if (inventoryAccount == null || vatInputAccount == null || apAccount == null)
                throw new PurchaseReturnDomainException(
                    "حسابات النظام المطلوبة (مخزون / ضريبة مدخلات / دائنون) غير موجودة. تأكد من تشغيل Seed.");

            return new PurchaseReturnAccounts
            {
                Inventory = inventoryAccount,
                VatInput = vatInputAccount,
                Ap = apAccount
            };
        }

        private async Task<JournalEntry> CreateReversalJournalAsync(
            PurchaseReturn purchaseReturn,
            PurchaseReturnAccounts accounts,
            PostingContext context,
            CancellationToken ct)
        {
            var netExVat = purchaseReturn.Subtotal - purchaseReturn.DiscountTotal + purchaseReturn.DeliveryFee;
            var lines = new List<JournalLineSpec>();

            lines.Add(new JournalLineSpec(accounts.Ap.Id, purchaseReturn.NetTotal, 0,
                $"مورد — مرتجع شراء {purchaseReturn.ReturnNumber}"));

            if (netExVat > 0)
                lines.Add(new JournalLineSpec(accounts.Inventory.Id, 0, netExVat,
                    $"مخزون — مرتجع شراء {purchaseReturn.ReturnNumber}"));

            if (purchaseReturn.VatTotal > 0)
                lines.Add(new JournalLineSpec(accounts.VatInput.Id, 0, purchaseReturn.VatTotal,
                    $"ضريبة مدخلات — مرتجع شراء {purchaseReturn.ReturnNumber}"));

            return await _journalFactory.CreateAndPostAsync(
                purchaseReturn.ReturnDate,
                $"مرتجع شراء رقم {purchaseReturn.ReturnNumber}",
                SourceType.PurchaseReturn,
                context.FiscalYear.Id,
                context.Period.Id,
                lines,
                context.Username,
                context.Now,
                referenceNumber: purchaseReturn.ReturnNumber,
                sourceId: purchaseReturn.Id,
                ct: ct);
        }

        private async Task DeductStockAsync(PurchaseReturn purchaseReturn, CancellationToken ct)
        {
            // Running totals to handle duplicate product lines (mirrors PurchaseInvoice cancel pattern)
            var runningDeductions = new Dictionary<int, decimal>();
            var runningProducts = new Dictionary<int, Product>();
            var allowNegativeStock = await IsNegativeStockAllowedAsync(ct);

            foreach (var line in purchaseReturn.Lines)
            {
                var costPerBaseUnit = line.BaseQuantity > 0
                    ? Math.Round(line.NetTotal / line.BaseQuantity, 4)
                    : 0;

                await _stockManager.DecreaseAsync(new StockOperation
                {
                    ProductId = line.ProductId,
                    WarehouseId = purchaseReturn.WarehouseId,
                    UnitId = line.UnitId,
                    MovementType = MovementType.PurchaseReturn,
                    Quantity = line.Quantity,
                    BaseQuantity = line.BaseQuantity,
                    CostPerBaseUnit = costPerBaseUnit,
                    DocumentDate = purchaseReturn.ReturnDate,
                    DocumentNumber = purchaseReturn.ReturnNumber,
                    SourceType = SourceType.PurchaseReturn,
                    SourceId = purchaseReturn.Id,
                    Notes = $"مرتجع شراء رقم {purchaseReturn.ReturnNumber}",
                    AllowCreate = false,
                    AllowNegativeStock = allowNegativeStock,
                }, ct);

                // Track cumulative deductions per product for stale-DB correction
                if (!runningDeductions.TryGetValue(line.ProductId, out var previousDeductions))
                    previousDeductions = 0;

                // ── WAC recalculation: Remove this batch's cost contribution ──
                if (!runningProducts.TryGetValue(line.ProductId, out var product))
                {
                    product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                    runningProducts[line.ProductId] = product;
                }
                var dbTotalQty = await _stockManager.GetTotalStockAsync(line.ProductId, ct);
                // DB hasn't been saved yet, so subtract all deductions made in this loop
                var remainingTotalQty = dbTotalQty - previousDeductions - line.BaseQuantity;

                if (remainingTotalQty > 0)
                {
                    var totalValueBefore = product.WeightedAverageCost * (remainingTotalQty + line.BaseQuantity);
                    var batchValue = line.BaseQuantity * costPerBaseUnit;
                    var newWac = (totalValueBefore - batchValue) / remainingTotalQty;
                    product.SetWeightedAverageCost(Math.Round(newWac, 4));
                }
                else
                {
                    product.SetWeightedAverageCost(product.CostPrice);
                }
                _productRepo.Update(product);

                runningDeductions[line.ProductId] = previousDeductions + line.BaseQuantity;
            }
        }

        private async Task ExecuteCancelAsync(
            PurchaseReturn purchaseReturn,
            CancelContext context,
            CancellationToken ct)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Reload as tracked inside the transaction to ensure fresh data
                // and avoid stale-entity issues (mirrors PurchaseInvoice cancel pattern)
                var tracked = await _returnRepo.GetWithLinesTrackedAsync(purchaseReturn.Id, ct)
                    ?? throw new PurchaseReturnDomainException(ReturnNotFoundMessage);

                await ReverseStockAsync(tracked, context, ct);

                await ReverseJournalAsync(
                    tracked.JournalEntryId.Value,
                    $"عكس مرتجع شراء رقم {tracked.ReturnNumber}",
                    "القيد المحاسبي الأصلي غير موجود.",
                    context,
                    ct);

                tracked.Cancel();
                // Entity is already tracked — no need for explicit Update
                await _unitOfWork.SaveChangesAsync(ct);

            }, IsolationLevel.Serializable, ct);
        }

        private async Task ReverseStockAsync(
            PurchaseReturn purchaseReturn,
            CancelContext context,
            CancellationToken ct)
        {
            // Running totals to handle duplicate product lines (C-06 fix: add WAC recalculation)
            var runningTotals = new Dictionary<int, (Product product, decimal totalQty)>();

            foreach (var line in purchaseReturn.Lines)
            {
                var costPerBaseUnit = line.BaseQuantity > 0
                    ? Math.Round(line.NetTotal / line.BaseQuantity, 4)
                    : 0;

                await _stockManager.IncreaseAsync(new StockOperation
                {
                    ProductId = line.ProductId,
                    WarehouseId = purchaseReturn.WarehouseId,
                    UnitId = line.UnitId,
                    MovementType = MovementType.PurchaseIn,
                    Quantity = line.Quantity,
                    BaseQuantity = line.BaseQuantity,
                    CostPerBaseUnit = costPerBaseUnit,
                    DocumentDate = context.Today,
                    DocumentNumber = purchaseReturn.ReturnNumber,
                    SourceType = SourceType.PurchaseReturn,
                    SourceId = purchaseReturn.Id,
                    Notes = $"إلغاء مرتجع شراء رقم {purchaseReturn.ReturnNumber}",
                }, ct);

                // ── WAC recalculation: re-blend the returned stock cost ──
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

                state.totalQty += line.BaseQuantity;
                runningTotals[line.ProductId] = state;
                _productRepo.Update(state.product);
            }
        }

        private async Task ReverseJournalAsync(
            int journalId,
            string description,
            string notFoundMessage,
            CancelContext context,
            CancellationToken ct)
        {
            var originalJournal = await _journalRepo.GetWithLinesAsync(journalId, ct);
            if (originalJournal == null)
                throw new PurchaseInvoiceDomainException(notFoundMessage);

            var reversalEntry = originalJournal.CreateReversal(
                context.Today,
                description,
                context.FiscalYear.Id,
                context.Period.Id);

            var reversalNumber = await _journalNumberGen.NextNumberAsync(context.FiscalYear.Id, ct);
            var username = _currentUser.Username ?? "System";
            var now = _dateTime.UtcNow;
            reversalEntry.Post(reversalNumber, username, now);
            await _journalRepo.AddAsync(reversalEntry, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            originalJournal.MarkAsReversed(reversalEntry.Id);
            _journalRepo.Update(originalJournal);
        }

        private sealed class PurchaseReturnAccounts
        {
            public Account Inventory { get; init; } = default!;
            public Account VatInput { get; init; } = default!;
            public Account Ap { get; init; } = default!;
        }

        private async Task<bool> IsNegativeStockAllowedAsync(CancellationToken ct)
        {
            if (_featureService == null)
                return false;

            var result = await _featureService.IsEnabledAsync(FeatureKeys.AllowNegativeStock, ct);
            return result.IsSuccess && result.Data;
        }

    }

    public sealed class PurchaseReturnRepositories
    {
        public PurchaseReturnRepositories(
            IPurchaseReturnRepository returnRepo,
            IProductRepository productRepo,
            IWarehouseProductRepository whProductRepo,
            IInventoryMovementRepository movementRepo,
            IJournalEntryRepository journalRepo,
            IAccountRepository accountRepo = null)
            : this(returnRepo, null, productRepo, whProductRepo, movementRepo, journalRepo, accountRepo)
        {
        }

        public PurchaseReturnRepositories(
            IPurchaseReturnRepository returnRepo,
            IPurchaseInvoiceRepository invoiceRepo,
            IProductRepository productRepo,
            IWarehouseProductRepository whProductRepo,
            IInventoryMovementRepository movementRepo,
            IJournalEntryRepository journalRepo,
            IAccountRepository accountRepo = null)
        {
            ReturnRepo = returnRepo ?? throw new ArgumentNullException(nameof(returnRepo));
            InvoiceRepo = invoiceRepo;
            ProductRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
            WhProductRepo = whProductRepo ?? throw new ArgumentNullException(nameof(whProductRepo));
            MovementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
            JournalRepo = journalRepo ?? throw new ArgumentNullException(nameof(journalRepo));
            AccountRepo = accountRepo;
        }

        public IPurchaseReturnRepository ReturnRepo { get; }
        public IPurchaseInvoiceRepository InvoiceRepo { get; }
        public IProductRepository ProductRepo { get; }
        public IWarehouseProductRepository WhProductRepo { get; }
        public IInventoryMovementRepository MovementRepo { get; }
        public IJournalEntryRepository JournalRepo { get; }
        public IAccountRepository AccountRepo { get; }
    }

    public sealed class PurchaseReturnServices
    {
        public PurchaseReturnServices(
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

    public sealed class PurchaseReturnValidators
    {
        public PurchaseReturnValidators(
            IValidator<CreatePurchaseReturnDto> createValidator,
            IValidator<UpdatePurchaseReturnDto> updateValidator)
        {
            CreateValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            UpdateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
        }

        public IValidator<CreatePurchaseReturnDto> CreateValidator { get; }
        public IValidator<UpdatePurchaseReturnDto> UpdateValidator { get; }
    }
}
