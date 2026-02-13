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
    /// Implements purchase invoice lifecycle: Create → Edit → Post → (Cancel).
    /// On Post: auto-generates journal, updates WAC, creates stock movements.
    /// </summary>
    [Module(SystemModule.Purchases)]
    public sealed class PurchaseInvoiceService : IPurchaseInvoiceService
    {
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
        private readonly IValidator<CreatePurchaseInvoiceDto> _createValidator;
        private readonly IValidator<UpdatePurchaseInvoiceDto> _updateValidator;
        private readonly ILogger<PurchaseInvoiceService> _logger;

        // ── GL Account Codes (from SystemAccountSeed) ───────────
        // 1131 = المخزون — المستودع الرئيسي (Inventory)
        // 1141 = ضريبة مدخلات مستحقة (VAT Input)
        // 2111 = الدائنون — ذمم تجارية (AP — Trade Payables)
        private const string InventoryAccountCode = "1131";
        private const string VatInputAccountCode = "1141";
        private const string ApAccountCode = "2111";
        private const string InvoiceNotFoundMessage = "فاتورة الشراء غير موجودة.";

        public PurchaseInvoiceService(
            PurchaseInvoiceRepositories repos,
            PurchaseInvoiceServices services,
            PurchaseInvoiceValidators validators,
            ILogger<PurchaseInvoiceService> logger)
        {
            if (repos == null) throw new ArgumentNullException(nameof(repos));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (validators == null) throw new ArgumentNullException(nameof(validators));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

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

        public async Task<ServiceResult<IReadOnlyList<PurchaseInvoiceListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _invoiceRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<PurchaseInvoiceListDto>>.Success(
                entities.Select(PurchaseInvoiceMapper.ToListDto).ToList());
        }

        public async Task<ServiceResult<PurchaseInvoiceDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _invoiceRepo.GetWithLinesAsync(id, ct);
            if (entity == null)
                return ServiceResult<PurchaseInvoiceDto>.Failure(InvoiceNotFoundMessage);

            return ServiceResult<PurchaseInvoiceDto>.Success(PurchaseInvoiceMapper.ToDto(entity));
        }

        public async Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default)
        {
            var next = await _invoiceRepo.GetNextNumberAsync(ct);
            return ServiceResult<string>.Success(next);
        }

        public async Task<ServiceResult<PurchaseInvoiceDto>> CreateAsync(CreatePurchaseInvoiceDto dto, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<PurchaseInvoiceDto>(_currentUser, PermissionKeys.PurchasesCreate);
            if (authCheck != null) return authCheck;

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<PurchaseInvoiceDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            try
            {
                var invoiceNumber = await _invoiceRepo.GetNextNumberAsync(ct);

                var invoice = new PurchaseInvoice(
                    invoiceNumber,
                    dto.InvoiceDate,
                    dto.SupplierId,
                    dto.WarehouseId,
                    dto.Notes,
                    salesRepresentativeId: dto.SalesRepresentativeId,
                    counterpartyType: dto.CounterpartyType,
                    customerId: dto.CounterpartyCustomerId);

                // Add lines — look up conversion factor from product units
                foreach (var lineDto in dto.Lines)
                {
                    var product = await _productRepo.GetByIdWithUnitsAsync(lineDto.ProductId, ct);
                    if (product == null)
                        return ServiceResult<PurchaseInvoiceDto>.Failure($"الصنف برقم {lineDto.ProductId} غير موجود.");

                    var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                    if (productUnit == null)
                        return ServiceResult<PurchaseInvoiceDto>.Failure(
                            $"الوحدة المحددة غير مرتبطة بالصنف ({product.NameAr}).");

                    invoice.AddLine(
                        lineDto.ProductId,
                        lineDto.UnitId,
                        lineDto.Quantity,
                        lineDto.UnitPrice,
                        productUnit.ConversionFactor,
                        lineDto.DiscountPercent,
                        product.VatRate);
                }

                await _invoiceRepo.AddAsync(invoice, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                // Re-fetch with navigation properties
                var saved = await _invoiceRepo.GetWithLinesAsync(invoice.Id, ct);
                return ServiceResult<PurchaseInvoiceDto>.Success(PurchaseInvoiceMapper.ToDto(saved));
            }
            catch (PurchaseInvoiceDomainException ex)
            {
                return ServiceResult<PurchaseInvoiceDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<PurchaseInvoiceDto>> UpdateAsync(UpdatePurchaseInvoiceDto dto, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<PurchaseInvoiceDto>(_currentUser, PermissionKeys.PurchasesCreate);
            if (authCheck != null) return authCheck;

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<PurchaseInvoiceDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var invoice = await _invoiceRepo.GetWithLinesAsync(dto.Id, ct);
            if (invoice == null)
                return ServiceResult<PurchaseInvoiceDto>.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Draft)
                return ServiceResult<PurchaseInvoiceDto>.Failure("لا يمكن تعديل فاتورة مرحّلة أو ملغاة.");

            try
            {
                invoice.UpdateHeader(dto.InvoiceDate, dto.SupplierId, dto.WarehouseId, dto.Notes,
                    salesRepresentativeId: dto.SalesRepresentativeId,
                    counterpartyType: dto.CounterpartyType, customerId: dto.CounterpartyCustomerId);

                // Rebuild lines from DTO
                var newLines = new List<PurchaseInvoiceLine>();
                foreach (var lineDto in dto.Lines)
                {
                    var product = await _productRepo.GetByIdWithUnitsAsync(lineDto.ProductId, ct);
                    if (product == null)
                        return ServiceResult<PurchaseInvoiceDto>.Failure($"الصنف برقم {lineDto.ProductId} غير موجود.");

                    var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                    if (productUnit == null)
                        return ServiceResult<PurchaseInvoiceDto>.Failure(
                            $"الوحدة المحددة غير مرتبطة بالصنف ({product.NameAr}).");

                    newLines.Add(new PurchaseInvoiceLine(
                        lineDto.ProductId,
                        lineDto.UnitId,
                        lineDto.Quantity,
                        lineDto.UnitPrice,
                        productUnit.ConversionFactor,
                        lineDto.DiscountPercent,
                        product.VatRate));
                }

                invoice.ReplaceLines(newLines);
                _invoiceRepo.Update(invoice);
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _invoiceRepo.GetWithLinesAsync(invoice.Id, ct);
                return ServiceResult<PurchaseInvoiceDto>.Success(PurchaseInvoiceMapper.ToDto(saved));
            }
            catch (PurchaseInvoiceDomainException ex)
            {
                return ServiceResult<PurchaseInvoiceDto>.Failure(ex.Message);
            }
        }

        /// <summary>
        /// Posts a draft purchase invoice. This triggers:
        /// 1. Auto journal: DR Inventory + DR VAT Input / CR AP
        /// 2. WAC recalculation for each product
        /// 3. Stock movement records + warehouse stock increase
        /// </summary>
        public async Task<ServiceResult<PurchaseInvoiceDto>> PostAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<PurchaseInvoiceDto>(_currentUser, PermissionKeys.PurchasesPost);
            if (authCheck != null) return authCheck;

            var invoice = await _invoiceRepo.GetWithLinesAsync(id, ct);
            if (invoice == null)
                return ServiceResult<PurchaseInvoiceDto>.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Draft)
                return ServiceResult<PurchaseInvoiceDto>.Failure("لا يمكن ترحيل فاتورة مرحّلة بالفعل أو ملغاة.");

            if (!invoice.Lines.Any())
                return ServiceResult<PurchaseInvoiceDto>.Failure("لا يمكن ترحيل فاتورة بدون بنود.");

            try
            {
                var saved = await ExecutePostAsync(invoice, ct);
                return ServiceResult<PurchaseInvoiceDto>.Success(PurchaseInvoiceMapper.ToDto(saved));
            }
            catch (PurchaseInvoiceDomainException ex)
            {
                return ServiceResult<PurchaseInvoiceDto>.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post purchase invoice {InvoiceId}.", invoice?.Id);
                return ServiceResult<PurchaseInvoiceDto>.Failure("حدث خطأ غير متوقع أثناء ترحيل الفاتورة.");
            }
        }

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.PurchasesPost);
            if (authCheck != null) return authCheck;

            var invoice = await _invoiceRepo.GetWithLinesAsync(id, ct);
            if (invoice == null)
                return ServiceResult.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Posted)
                return ServiceResult.Failure("لا يمكن إلغاء إلا الفواتير المرحّلة.");

            if (!invoice.JournalEntryId.HasValue)
                return ServiceResult.Failure("لا يمكن إلغاء فاتورة بدون قيد محاسبي.");

            if (invoice.PaidAmount > 0)
                return ServiceResult.Failure(
                    $"لا يمكن إلغاء فاتورة عليها دفعات ({invoice.PaidAmount:N2}). يجب إلغاء سندات الصرف المرتبطة أولاً.");

            try
            {
                var context = await GetCancelContextAsync(invoice.InvoiceDate, ct);
                await ExecuteCancelAsync(invoice, context, ct);
                return ServiceResult.Success();
            }
            catch (PurchaseInvoiceDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel purchase invoice {InvoiceId}.", invoice?.Id);
                return ServiceResult.Failure("حدث خطأ غير متوقع أثناء إلغاء فاتورة الشراء.");
            }
        }

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.PurchasesCreate);
            if (authCheck != null) return authCheck;

            var invoice = await _invoiceRepo.GetWithLinesAsync(id, ct);
            if (invoice == null)
                return ServiceResult.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Draft)
                return ServiceResult.Failure("لا يمكن حذف إلا الفواتير المسودة.");

            invoice.SoftDelete(_currentUser.Username, _dateTime.UtcNow);
            _invoiceRepo.Update(invoice);
            await _unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Success();
        }

        private async Task<PurchaseInvoice> ExecutePostAsync(PurchaseInvoice invoice, CancellationToken ct)
        {
            PurchaseInvoice saved = null;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var context = await GetPostingContextAsync(invoice.InvoiceDate, ct);
                var accounts = await ResolveAccountsAsync(ct);

                var journalEntry = await CreateJournalEntryAsync(invoice, accounts, context, ct);

                await _unitOfWork.SaveChangesAsync(ct);

                await ApplyStockAndWacAsync(invoice, ct);

                invoice.Post(journalEntry.Id);
                _invoiceRepo.Update(invoice);
                await _unitOfWork.SaveChangesAsync(ct);

                saved = await _invoiceRepo.GetWithLinesAsync(invoice.Id, ct);
            }, IsolationLevel.Serializable, ct);

            return saved ?? invoice;
        }

        private async Task<PurchaseInvoicePostingContext> GetPostingContextAsync(DateTime invoiceDate, CancellationToken ct)
        {
            var fiscalYear = await _fiscalYearRepo.GetActiveYearAsync(ct);
            if (fiscalYear == null)
                throw new PurchaseInvoiceDomainException("لا توجد سنة مالية نشطة.");

            if (!fiscalYear.ContainsDate(invoiceDate))
                throw new PurchaseInvoiceDomainException(
                    $"تاريخ الفاتورة {invoiceDate:yyyy-MM-dd} لا يقع ضمن السنة المالية النشطة.");

            var yearWithPeriods = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            var period = yearWithPeriods.GetPeriod(invoiceDate.Month);
            if (period == null)
                throw new PurchaseInvoiceDomainException("لا توجد فترة مالية للشهر المحدد.");

            if (!period.IsOpen)
                throw new PurchaseInvoiceDomainException(
                    $"الفترة المالية ({period.Year}-{period.Month:D2}) مقفلة. لا يمكن الترحيل.");

            return new PurchaseInvoicePostingContext
            {
                FiscalYear = yearWithPeriods,
                Period = period,
                Now = _dateTime.UtcNow,
                Username = _currentUser.Username ?? "System"
            };
        }

        private async Task<PurchaseInvoiceAccounts> ResolveAccountsAsync(CancellationToken ct)
        {
            var inventoryAccount = await _accountRepo.GetByCodeAsync(InventoryAccountCode, ct);
            var vatInputAccount = await _accountRepo.GetByCodeAsync(VatInputAccountCode, ct);
            var apAccount = await _accountRepo.GetByCodeAsync(ApAccountCode, ct);

            if (inventoryAccount == null || vatInputAccount == null || apAccount == null)
                throw new PurchaseInvoiceDomainException(
                    "حسابات النظام المطلوبة (مخزون / ضريبة مدخلات / دائنون) غير موجودة. تأكد من تشغيل Seed.");

            return new PurchaseInvoiceAccounts
            {
                Inventory = inventoryAccount,
                VatInput = vatInputAccount,
                Ap = apAccount
            };
        }

        private async Task<JournalEntry> CreateJournalEntryAsync(
            PurchaseInvoice invoice,
            PurchaseInvoiceAccounts accounts,
            PurchaseInvoicePostingContext context,
            CancellationToken ct)
        {
            var netExVat = invoice.Subtotal - invoice.DiscountTotal;
            var journalEntry = JournalEntry.CreateDraft(
                invoice.InvoiceDate,
                $"فاتورة شراء رقم {invoice.InvoiceNumber}",
                SourceType.PurchaseInvoice,
                context.FiscalYear.Id,
                context.Period.Id,
                referenceNumber: invoice.InvoiceNumber,
                sourceId: invoice.Id);

            if (netExVat > 0)
                journalEntry.AddLine(accounts.Inventory.Id, netExVat, 0, context.Now,
                    $"مخزون — فاتورة شراء {invoice.InvoiceNumber}");

            if (invoice.VatTotal > 0)
                journalEntry.AddLine(accounts.VatInput.Id, invoice.VatTotal, 0, context.Now,
                    $"ضريبة مدخلات — فاتورة شراء {invoice.InvoiceNumber}");

            journalEntry.AddLine(accounts.Ap.Id, 0, invoice.NetTotal, context.Now,
                $"مورد — فاتورة شراء {invoice.InvoiceNumber}");

            var journalNumber = _journalNumberGen.NextNumber(context.FiscalYear.Id);
            journalEntry.Post(journalNumber, context.Username, context.Now);

            await _journalRepo.AddAsync(journalEntry, ct);
            return journalEntry;
        }

        private async Task ApplyStockAndWacAsync(PurchaseInvoice invoice, CancellationToken ct)
        {
            var runningTotals = new Dictionary<int, (Product product, decimal totalQty)>();

            foreach (var line in invoice.Lines)
            {
                var whProduct = await _whProductRepo.GetOrCreateAsync(
                    invoice.WarehouseId, line.ProductId, ct);

                if (!runningTotals.TryGetValue(line.ProductId, out var state))
                {
                    var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                    var existingTotalQty = await _whProductRepo.GetTotalStockAsync(line.ProductId, ct);
                    state = (product, existingTotalQty);
                }

                var costPerBaseUnit = line.BaseQuantity > 0
                    ? line.NetTotal / line.BaseQuantity
                    : 0;

                // Use running totals to keep WAC accurate across multiple lines for the same product.
                state.product.UpdateWeightedAverageCost(state.totalQty, line.BaseQuantity, costPerBaseUnit);
                state.totalQty += line.BaseQuantity;
                runningTotals[line.ProductId] = state;
                _productRepo.Update(state.product);

                whProduct.IncreaseStock(line.BaseQuantity);
                _whProductRepo.Update(whProduct);

                var totalCost = Math.Round(line.BaseQuantity * costPerBaseUnit, 4);

                var movement = new InventoryMovement(
                    line.ProductId,
                    invoice.WarehouseId,
                    line.UnitId,
                    MovementType.PurchaseIn,
                    line.Quantity,
                    line.BaseQuantity,
                    costPerBaseUnit,
                    totalCost,
                    invoice.InvoiceDate,
                    invoice.InvoiceNumber,
                    SourceType.PurchaseInvoice,
                    sourceId: invoice.Id,
                    notes: $"فاتورة شراء رقم {invoice.InvoiceNumber}");

                movement.SetBalanceAfter(whProduct.Quantity);
                await _movementRepo.AddAsync(movement, ct);
            }
        }

        private async Task<PurchaseInvoiceCancelContext> GetCancelContextAsync(DateTime invoiceDate, CancellationToken ct)
        {
            var fiscalYear = await _fiscalYearRepo.GetByYearAsync(invoiceDate.Year, ct);
            if (fiscalYear == null)
                throw new PurchaseInvoiceDomainException($"لا توجد سنة مالية للعام {invoiceDate.Year}.");

            fiscalYear = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            if (fiscalYear.Status != FiscalYearStatus.Active)
                throw new PurchaseInvoiceDomainException($"السنة المالية {fiscalYear.Year} ليست فعّالة.");

            var period = fiscalYear.GetPeriod(invoiceDate.Month);
            if (period == null || !period.IsOpen)
                throw new PurchaseInvoiceDomainException($"الفترة المالية لـ {invoiceDate:yyyy-MM} مُقفلة.");

            return new PurchaseInvoiceCancelContext
            {
                FiscalYear = fiscalYear,
                Period = period,
                Today = invoiceDate
            };
        }

        private async Task ExecuteCancelAsync(
            PurchaseInvoice invoice,
            PurchaseInvoiceCancelContext context,
            CancellationToken ct)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await ReverseStockAsync(invoice, context, ct);

                await ReverseJournalAsync(
                    invoice.JournalEntryId.Value,
                    $"عكس فاتورة شراء رقم {invoice.InvoiceNumber}",
                    "القيد المحاسبي الأصلي غير موجود.",
                    context,
                    ct);

                invoice.Cancel();
                _invoiceRepo.Update(invoice);
                await _unitOfWork.SaveChangesAsync(ct);

            }, IsolationLevel.Serializable, ct);
        }

        private async Task ReverseStockAsync(
            PurchaseInvoice invoice,
            PurchaseInvoiceCancelContext context,
            CancellationToken ct)
        {
            // Running totals to handle duplicate product lines in same invoice (mirrors posting logic)
            var runningDeductions = new Dictionary<int, decimal>();

            foreach (var line in invoice.Lines)
            {
                var whProduct = await _whProductRepo.GetAsync(
                    invoice.WarehouseId, line.ProductId, ct);

                if (whProduct == null)
                    throw new PurchaseInvoiceDomainException(
                        $"تعذر إلغاء الفاتورة: سجل المخزون للصنف {line.ProductId} في المستودع {invoice.WarehouseId} غير موجود.");

                whProduct.DecreaseStock(line.BaseQuantity);
                _whProductRepo.Update(whProduct);

                // Track cumulative deductions per product for stale-DB correction
                if (!runningDeductions.TryGetValue(line.ProductId, out var previousDeductions))
                    previousDeductions = 0;

                // Recalculate WAC after removing this purchase batch's contribution
                var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                var dbTotalQty = await _whProductRepo.GetTotalStockAsync(line.ProductId, ct);
                // DB hasn't been saved yet, so subtract all deductions made in this loop
                var remainingTotalQty = dbTotalQty - previousDeductions - line.BaseQuantity;

                if (remainingTotalQty > 0)
                {
                    var costPerUnit = line.BaseQuantity > 0
                        ? line.NetTotal / line.BaseQuantity
                        : 0;
                    // Reverse the WAC: remove this batch's cost contribution
                    var totalValueBefore = product.WeightedAverageCost * (remainingTotalQty + line.BaseQuantity);
                    var batchValue = line.BaseQuantity * costPerUnit;
                    var newWac = (totalValueBefore - batchValue) / remainingTotalQty;
                    product.SetWeightedAverageCost(Math.Round(newWac, 4));
                }
                else
                {
                    product.SetWeightedAverageCost(product.CostPrice); // Fallback to base cost
                }
                _productRepo.Update(product);

                runningDeductions[line.ProductId] = previousDeductions + line.BaseQuantity;

                var costPerBaseUnit = line.BaseQuantity > 0
                    ? line.NetTotal / line.BaseQuantity
                    : 0;

                var totalCost = Math.Round(line.BaseQuantity * costPerBaseUnit, 4);

                var movement = new InventoryMovement(
                    line.ProductId,
                    invoice.WarehouseId,
                    line.UnitId,
                    MovementType.PurchaseReturn,
                    line.Quantity,
                    line.BaseQuantity,
                    costPerBaseUnit,
                    totalCost,
                    context.Today,
                    invoice.InvoiceNumber,
                    SourceType.PurchaseInvoice,
                    sourceId: invoice.Id,
                    notes: $"إلغاء فاتورة شراء رقم {invoice.InvoiceNumber}");

                movement.SetBalanceAfter(whProduct.Quantity);
                await _movementRepo.AddAsync(movement, ct);
            }
        }

        private async Task ReverseJournalAsync(
            int journalId,
            string description,
            string notFoundMessage,
            PurchaseInvoiceCancelContext context,
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

        private sealed class PurchaseInvoicePostingContext
        {
            public FiscalYear FiscalYear { get; init; } = default!;
            public FiscalPeriod Period { get; init; } = default!;
            public DateTime Now { get; init; }
            public string Username { get; init; } = string.Empty;
        }

        private sealed class PurchaseInvoiceAccounts
        {
            public Account Inventory { get; init; } = default!;
            public Account VatInput { get; init; } = default!;
            public Account Ap { get; init; } = default!;
        }

        private sealed class PurchaseInvoiceCancelContext
        {
            public FiscalYear FiscalYear { get; init; } = default!;
            public FiscalPeriod Period { get; init; } = default!;
            public DateTime Today { get; init; }
        }
    }

    public sealed class PurchaseInvoiceRepositories
    {
        public PurchaseInvoiceRepositories(
            IPurchaseInvoiceRepository invoiceRepo,
            IProductRepository productRepo,
            IWarehouseProductRepository whProductRepo,
            IInventoryMovementRepository movementRepo,
            IJournalEntryRepository journalRepo,
            IAccountRepository accountRepo)
        {
            InvoiceRepo = invoiceRepo ?? throw new ArgumentNullException(nameof(invoiceRepo));
            ProductRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
            WhProductRepo = whProductRepo ?? throw new ArgumentNullException(nameof(whProductRepo));
            MovementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
            JournalRepo = journalRepo ?? throw new ArgumentNullException(nameof(journalRepo));
            AccountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
        }

        public IPurchaseInvoiceRepository InvoiceRepo { get; }
        public IProductRepository ProductRepo { get; }
        public IWarehouseProductRepository WhProductRepo { get; }
        public IInventoryMovementRepository MovementRepo { get; }
        public IJournalEntryRepository JournalRepo { get; }
        public IAccountRepository AccountRepo { get; }
    }

    public sealed class PurchaseInvoiceServices
    {
        public PurchaseInvoiceServices(
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

    public sealed class PurchaseInvoiceValidators
    {
        public PurchaseInvoiceValidators(
            IValidator<CreatePurchaseInvoiceDto> createValidator,
            IValidator<UpdatePurchaseInvoiceDto> updateValidator)
        {
            CreateValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            UpdateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
        }

        public IValidator<CreatePurchaseInvoiceDto> CreateValidator { get; }
        public IValidator<UpdatePurchaseInvoiceDto> UpdateValidator { get; }
    }
}
