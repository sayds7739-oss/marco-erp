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
using MarcoERP.Domain.Exceptions.Purchases;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Purchases;
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
        private readonly IWarehouseProductRepository _whProductRepo;
        private readonly IInventoryMovementRepository _movementRepo;
        private readonly IJournalEntryRepository _journalRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly IFiscalYearRepository _fiscalYearRepo;
        private readonly IJournalNumberGenerator _journalNumberGen;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreatePurchaseReturnDto> _createValidator;
        private readonly IValidator<UpdatePurchaseReturnDto> _updateValidator;
        private readonly ILogger<PurchaseReturnService> _logger;

        // ── GL Account Codes (from SystemAccountSeed) ───────────
        private const string InventoryAccountCode = "1131";
        private const string VatInputAccountCode = "1141";
        private const string ApAccountCode = "2111";
        private const string ReturnNotFoundMessage = "مرتجع الشراء غير موجود.";

        public PurchaseReturnService(
            PurchaseReturnRepositories repos,
            PurchaseReturnServices services,
            PurchaseReturnValidators validators,
            ILogger<PurchaseReturnService> logger)
        {
            if (repos == null) throw new ArgumentNullException(nameof(repos));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (validators == null) throw new ArgumentNullException(nameof(validators));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            _returnRepo = repos.ReturnRepo;
            _invoiceRepo = repos.InvoiceRepo;
            _productRepo = repos.ProductRepo;
            _whProductRepo = repos.WhProductRepo;
            _movementRepo = repos.MovementRepo;
            _journalRepo = repos.JournalRepo;
            _accountRepo = repos.AccountRepo;

            _fiscalYearRepo = services.FiscalYearRepo;
            _journalNumberGen = services.JournalNumberGen;
            _unitOfWork = services.UnitOfWork;
            _currentUser = services.CurrentUser;
            _dateTime = services.DateTime;

            _createValidator = validators.CreateValidator;
            _updateValidator = validators.UpdateValidator;
            _logger = logger;
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
            var authCheck = AuthorizationGuard.Check<PurchaseReturnDto>(_currentUser, PermissionKeys.PurchasesCreate);
            if (authCheck != null) return authCheck;

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<PurchaseReturnDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            try
            {
                // ── Validate return quantities against original invoice ──
                if (dto.OriginalInvoiceId.HasValue)
                {
                    var originalInvoice = await _invoiceRepo.GetWithLinesAsync(dto.OriginalInvoiceId.Value, ct);
                    if (originalInvoice == null)
                        return ServiceResult<PurchaseReturnDto>.Failure("فاتورة الشراء الأصلية غير موجودة.");

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
                            return ServiceResult<PurchaseReturnDto>.Failure(
                                $"الصنف {lineDto.ProductId} بالوحدة {lineDto.UnitId} غير موجود في فاتورة الشراء الأصلية.");

                        var alreadyReturned = previouslyReturnedQty
                            .GetValueOrDefault(new { lineDto.ProductId, lineDto.UnitId }, 0m);
                        var remainingReturnable = invoiceLine.Quantity - alreadyReturned;

                        if (lineDto.Quantity > remainingReturnable)
                            return ServiceResult<PurchaseReturnDto>.Failure(
                                $"كمية المرتجع ({lineDto.Quantity}) تتجاوز الكمية المتاحة للإرجاع ({remainingReturnable}) للصنف {lineDto.ProductId}. " +
                                $"(الكمية الأصلية: {invoiceLine.Quantity}، تم إرجاع: {alreadyReturned})");
                    }
                }

                var returnNumber = await _returnRepo.GetNextNumberAsync(ct);

                var purchaseReturn = new PurchaseReturn(
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
                        return ServiceResult<PurchaseReturnDto>.Failure($"الصنف برقم {lineDto.ProductId} غير موجود.");

                    var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                    if (productUnit == null)
                        return ServiceResult<PurchaseReturnDto>.Failure(
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

                var saved = await _returnRepo.GetWithLinesAsync(purchaseReturn.Id, ct);
                return ServiceResult<PurchaseReturnDto>.Success(PurchaseReturnMapper.ToDto(saved));
            }
            catch (PurchaseInvoiceDomainException ex)
            {
                return ServiceResult<PurchaseReturnDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<PurchaseReturnDto>> UpdateAsync(UpdatePurchaseReturnDto dto, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<PurchaseReturnDto>(_currentUser, PermissionKeys.PurchasesCreate);
            if (authCheck != null) return authCheck;

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<PurchaseReturnDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var purchaseReturn = await _returnRepo.GetWithLinesAsync(dto.Id, ct);
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
                        product.VatRate));
                }

                purchaseReturn.ReplaceLines(newLines);
                _returnRepo.Update(purchaseReturn);
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _returnRepo.GetWithLinesAsync(purchaseReturn.Id, ct);
                return ServiceResult<PurchaseReturnDto>.Success(PurchaseReturnMapper.ToDto(saved));
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
            var authCheck = AuthorizationGuard.Check<PurchaseReturnDto>(_currentUser, PermissionKeys.PurchasesPost);
            if (authCheck != null) return authCheck;

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
            catch (PurchaseInvoiceDomainException ex)
            {
                return ServiceResult<PurchaseReturnDto>.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post purchase return {ReturnId}.", purchaseReturn?.Id);
                return ServiceResult<PurchaseReturnDto>.Failure("حدث خطأ غير متوقع أثناء ترحيل المرتجع.");
            }
        }

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.PurchasesPost);
            if (authCheck != null) return authCheck;

            var purchaseReturn = await _returnRepo.GetWithLinesAsync(id, ct);
            if (purchaseReturn == null)
                return ServiceResult.Failure(ReturnNotFoundMessage);

            if (purchaseReturn.Status != InvoiceStatus.Posted)
                return ServiceResult.Failure("لا يمكن إلغاء إلا المرتجعات المرحّلة.");

            if (!purchaseReturn.JournalEntryId.HasValue)
                return ServiceResult.Failure("لا يمكن إلغاء مرتجع بدون قيد محاسبي.");

            try
            {
                var context = await GetCancelContextAsync(purchaseReturn.ReturnDate, ct);
                await ExecuteCancelAsync(purchaseReturn, context, ct);
                return ServiceResult.Success();
            }
            catch (PurchaseInvoiceDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel purchase return {ReturnId}.", purchaseReturn?.Id);
                return ServiceResult.Failure("حدث خطأ غير متوقع أثناء إلغاء مرتجع الشراء.");
            }
        }

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.PurchasesCreate);
            if (authCheck != null) return authCheck;

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
                var context = await GetPostingContextAsync(purchaseReturn.ReturnDate, ct);
                var accounts = await ResolveAccountsAsync(ct);

                var journalEntry = await CreateReversalJournalAsync(purchaseReturn, accounts, context, ct);

                await _unitOfWork.SaveChangesAsync(ct);

                await DeductStockAsync(purchaseReturn, ct);

                purchaseReturn.Post(journalEntry.Id);
                _returnRepo.Update(purchaseReturn);
                await _unitOfWork.SaveChangesAsync(ct);

                saved = await _returnRepo.GetWithLinesAsync(purchaseReturn.Id, ct);
            }, IsolationLevel.Serializable, ct);

            return saved ?? purchaseReturn;
        }

        private async Task<PurchaseReturnPostingContext> GetPostingContextAsync(DateTime returnDate, CancellationToken ct)
        {
            var fiscalYear = await _fiscalYearRepo.GetActiveYearAsync(ct);
            if (fiscalYear == null)
                throw new PurchaseInvoiceDomainException("لا توجد سنة مالية نشطة.");

            if (!fiscalYear.ContainsDate(returnDate))
                throw new PurchaseInvoiceDomainException(
                    $"تاريخ المرتجع {returnDate:yyyy-MM-dd} لا يقع ضمن السنة المالية النشطة.");

            var yearWithPeriods = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            var period = yearWithPeriods.GetPeriod(returnDate.Month);
            if (period == null)
                throw new PurchaseInvoiceDomainException("لا توجد فترة مالية للشهر المحدد.");

            if (!period.IsOpen)
                throw new PurchaseInvoiceDomainException(
                    $"الفترة المالية ({period.Year}-{period.Month:D2}) مقفلة. لا يمكن الترحيل.");

            return new PurchaseReturnPostingContext
            {
                FiscalYear = yearWithPeriods,
                Period = period,
                Now = _dateTime.UtcNow,
                Username = _currentUser.Username ?? "System"
            };
        }

        private async Task<PurchaseReturnAccounts> ResolveAccountsAsync(CancellationToken ct)
        {
            var inventoryAccount = await _accountRepo.GetByCodeAsync(InventoryAccountCode, ct);
            var vatInputAccount = await _accountRepo.GetByCodeAsync(VatInputAccountCode, ct);
            var apAccount = await _accountRepo.GetByCodeAsync(ApAccountCode, ct);

            if (inventoryAccount == null || vatInputAccount == null || apAccount == null)
                throw new PurchaseInvoiceDomainException(
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
            PurchaseReturnPostingContext context,
            CancellationToken ct)
        {
            var netExVat = purchaseReturn.Subtotal - purchaseReturn.DiscountTotal;
            var journalEntry = JournalEntry.CreateDraft(
                purchaseReturn.ReturnDate,
                $"مرتجع شراء رقم {purchaseReturn.ReturnNumber}",
                SourceType.PurchaseReturn,
                context.FiscalYear.Id,
                context.Period.Id,
                referenceNumber: purchaseReturn.ReturnNumber,
                sourceId: purchaseReturn.Id);

            journalEntry.AddLine(accounts.Ap.Id, purchaseReturn.NetTotal, 0, context.Now,
                $"مورد — مرتجع شراء {purchaseReturn.ReturnNumber}");

            if (netExVat > 0)
                journalEntry.AddLine(accounts.Inventory.Id, 0, netExVat, context.Now,
                    $"مخزون — مرتجع شراء {purchaseReturn.ReturnNumber}");

            if (purchaseReturn.VatTotal > 0)
                journalEntry.AddLine(accounts.VatInput.Id, 0, purchaseReturn.VatTotal, context.Now,
                    $"ضريبة مدخلات — مرتجع شراء {purchaseReturn.ReturnNumber}");

            var journalNumber = _journalNumberGen.NextNumber(context.FiscalYear.Id);
            journalEntry.Post(journalNumber, context.Username, context.Now);

            await _journalRepo.AddAsync(journalEntry, ct);
            return journalEntry;
        }

        private async Task DeductStockAsync(PurchaseReturn purchaseReturn, CancellationToken ct)
        {
            foreach (var line in purchaseReturn.Lines)
            {
                var whProduct = await _whProductRepo.GetAsync(
                    purchaseReturn.WarehouseId, line.ProductId, ct);

                if (whProduct == null)
                    throw new PurchaseInvoiceDomainException(
                        "لا يوجد مخزون للصنف في المستودع المحدد. تحقق من مطابقة المستودع.");

                if (whProduct.Quantity < line.BaseQuantity)
                    throw new PurchaseInvoiceDomainException(
                        $"الكمية المتاحة ({whProduct.Quantity:N2}) أقل من كمية المرتجع ({line.BaseQuantity:N2}).");

                var costPerBaseUnit = line.BaseQuantity > 0
                    ? line.NetTotal / line.BaseQuantity
                    : 0;

                whProduct.DecreaseStock(line.BaseQuantity);
                _whProductRepo.Update(whProduct);

                var totalCost = Math.Round(line.BaseQuantity * costPerBaseUnit, 4);

                var movement = new InventoryMovement(
                    line.ProductId,
                    purchaseReturn.WarehouseId,
                    line.UnitId,
                    MovementType.PurchaseReturn,
                    line.Quantity,
                    line.BaseQuantity,
                    costPerBaseUnit,
                    totalCost,
                    purchaseReturn.ReturnDate,
                    purchaseReturn.ReturnNumber,
                    SourceType.PurchaseReturn,
                    sourceId: purchaseReturn.Id,
                    notes: $"مرتجع شراء رقم {purchaseReturn.ReturnNumber}");

                movement.SetBalanceAfter(whProduct.Quantity);
                await _movementRepo.AddAsync(movement, ct);
            }
        }

        private async Task<PurchaseReturnCancelContext> GetCancelContextAsync(DateTime returnDate, CancellationToken ct)
        {
            var fiscalYear = await _fiscalYearRepo.GetByYearAsync(returnDate.Year, ct);
            if (fiscalYear == null)
                throw new PurchaseInvoiceDomainException($"لا توجد سنة مالية للعام {returnDate.Year}.");

            fiscalYear = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            if (fiscalYear.Status != FiscalYearStatus.Active)
                throw new PurchaseInvoiceDomainException($"السنة المالية {fiscalYear.Year} ليست فعّالة.");

            var period = fiscalYear.GetPeriod(returnDate.Month);
            if (period == null || !period.IsOpen)
                throw new PurchaseInvoiceDomainException($"الفترة المالية لـ {returnDate:yyyy-MM} مُقفلة.");

            return new PurchaseReturnCancelContext
            {
                FiscalYear = fiscalYear,
                Period = period,
                Today = returnDate
            };
        }

        private async Task ExecuteCancelAsync(
            PurchaseReturn purchaseReturn,
            PurchaseReturnCancelContext context,
            CancellationToken ct)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await ReverseStockAsync(purchaseReturn, context, ct);

                await ReverseJournalAsync(
                    purchaseReturn.JournalEntryId.Value,
                    $"عكس مرتجع شراء رقم {purchaseReturn.ReturnNumber}",
                    "القيد المحاسبي الأصلي غير موجود.",
                    context,
                    ct);

                purchaseReturn.Cancel();
                _returnRepo.Update(purchaseReturn);
                await _unitOfWork.SaveChangesAsync(ct);

            }, IsolationLevel.Serializable, ct);
        }

        private async Task ReverseStockAsync(
            PurchaseReturn purchaseReturn,
            PurchaseReturnCancelContext context,
            CancellationToken ct)
        {
            foreach (var line in purchaseReturn.Lines)
            {
                var whProduct = await _whProductRepo.GetOrCreateAsync(
                    purchaseReturn.WarehouseId, line.ProductId, ct);

                whProduct.IncreaseStock(line.BaseQuantity);
                _whProductRepo.Update(whProduct);

                var costPerBaseUnit = line.BaseQuantity > 0
                    ? line.NetTotal / line.BaseQuantity
                    : 0;

                var totalCost = Math.Round(line.BaseQuantity * costPerBaseUnit, 4);

                var movement = new InventoryMovement(
                    line.ProductId,
                    purchaseReturn.WarehouseId,
                    line.UnitId,
                    MovementType.PurchaseIn,
                    line.Quantity,
                    line.BaseQuantity,
                    costPerBaseUnit,
                    totalCost,
                    context.Today,
                    purchaseReturn.ReturnNumber,
                    SourceType.PurchaseReturn,
                    sourceId: purchaseReturn.Id,
                    notes: $"إلغاء مرتجع شراء رقم {purchaseReturn.ReturnNumber}");

                movement.SetBalanceAfter(whProduct.Quantity);
                await _movementRepo.AddAsync(movement, ct);
            }
        }

        private async Task ReverseJournalAsync(
            int journalId,
            string description,
            string notFoundMessage,
            PurchaseReturnCancelContext context,
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

            var reversalNumber = _journalNumberGen.NextNumber(context.FiscalYear.Id);
            var username = _currentUser.Username ?? "System";
            var now = _dateTime.UtcNow;
            reversalEntry.Post(reversalNumber, username, now);
            await _journalRepo.AddAsync(reversalEntry, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            originalJournal.MarkAsReversed(reversalEntry.Id);
            _journalRepo.Update(originalJournal);
        }

        private sealed class PurchaseReturnPostingContext
        {
            public FiscalYear FiscalYear { get; init; } = default!;
            public FiscalPeriod Period { get; init; } = default!;
            public DateTime Now { get; init; }
            public string Username { get; init; } = string.Empty;
        }

        private sealed class PurchaseReturnAccounts
        {
            public Account Inventory { get; init; } = default!;
            public Account VatInput { get; init; } = default!;
            public Account Ap { get; init; } = default!;
        }

        private sealed class PurchaseReturnCancelContext
        {
            public FiscalYear FiscalYear { get; init; } = default!;
            public FiscalPeriod Period { get; init; } = default!;
            public DateTime Today { get; init; }
        }
    }

    public sealed class PurchaseReturnRepositories
    {
        public PurchaseReturnRepositories(
            IPurchaseReturnRepository returnRepo,
            IPurchaseInvoiceRepository invoiceRepo,
            IProductRepository productRepo,
            IWarehouseProductRepository whProductRepo,
            IInventoryMovementRepository movementRepo,
            IJournalEntryRepository journalRepo,
            IAccountRepository accountRepo)
        {
            ReturnRepo = returnRepo ?? throw new ArgumentNullException(nameof(returnRepo));
            InvoiceRepo = invoiceRepo ?? throw new ArgumentNullException(nameof(invoiceRepo));
            ProductRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
            WhProductRepo = whProductRepo ?? throw new ArgumentNullException(nameof(whProductRepo));
            MovementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
            JournalRepo = journalRepo ?? throw new ArgumentNullException(nameof(journalRepo));
            AccountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
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
