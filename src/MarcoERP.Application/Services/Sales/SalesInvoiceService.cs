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
using MarcoERP.Application.Interfaces.SmartEntry;
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
using Microsoft.EntityFrameworkCore;

namespace MarcoERP.Application.Services.Sales
{
    /// <summary>
    /// Implements sales invoice lifecycle: Create → Edit → Post → (Cancel).
    /// On Post: auto-generates two journals (Revenue + COGS), validates &amp; deducts stock.
    /// 
    /// Revenue Journal:  DR 1121 AR  /  CR 4111 Sales  /  CR 2121 VAT Output
    /// COGS Journal:     DR 5111 COGS  /  CR 1131 Inventory  (per-line at WAC)
    /// </summary>
    [Module(SystemModule.Sales)]
    public sealed class SalesInvoiceService : ISalesInvoiceService
    {
        private readonly ISalesInvoiceRepository _invoiceRepo;
        private readonly IProductRepository _productRepo;
        private readonly IWarehouseProductRepository _whProductRepo;
        private readonly IInventoryMovementRepository _movementRepo;
        private readonly IJournalEntryRepository _journalRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly ICustomerRepository _customerRepo;
        private readonly ISmartEntryQueryService _smartEntryQueryService;
        private readonly IFiscalYearRepository _fiscalYearRepo;
        private readonly IJournalNumberGenerator _journalNumberGen;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreateSalesInvoiceDto> _createValidator;
        private readonly IValidator<UpdateSalesInvoiceDto> _updateValidator;
        private readonly ILogger<SalesInvoiceService> _logger;

        // ── GL Account Codes (from SystemAccountSeed) ───────────
        // 1121 = المدينون — ذمم تجارية (AR — Trade Receivables)
        // 4111 = المبيعات — عام (Sales — General)
        // 2121 = ضريبة مخرجات مستحقة (VAT Output Payable)
        // 5111 = تكلفة البضاعة المباعة (COGS — General)
        // 1131 = المخزون — المستودع الرئيسي (Inventory)
        private const string ArAccountCode = "1121";
        private const string SalesAccountCode = "4111";
        private const string VatOutputAccountCode = "2121";
        private const string CogsAccountCode = "5111";
        private const string InventoryAccountCode = "1131";
        private const string InvoiceNotFoundMessage = "فاتورة البيع غير موجودة.";

        public SalesInvoiceService(
            SalesInvoiceRepositories repos,
            SalesInvoiceServices services,
            SalesInvoiceValidators validators,
            ILogger<SalesInvoiceService> logger)
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
            _customerRepo = repos.CustomerRepo;

            _fiscalYearRepo = services.FiscalYearRepo;
            _journalNumberGen = services.JournalNumberGen;
            _unitOfWork = services.UnitOfWork;
            _currentUser = services.CurrentUser;
            _dateTime = services.DateTime;
            _smartEntryQueryService = services.SmartEntryQueryService;

            _createValidator = validators.CreateValidator;
            _updateValidator = validators.UpdateValidator;
            _logger = logger;
        }

        // ══════════════════════════════════════════════════════════
        //  QUERIES
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<IReadOnlyList<SalesInvoiceListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _invoiceRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<SalesInvoiceListDto>>.Success(
                entities.Select(SalesInvoiceMapper.ToListDto).ToList());
        }

        public async Task<ServiceResult<SalesInvoiceDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _invoiceRepo.GetWithLinesAsync(id, ct);
            if (entity == null)
            {
                return ServiceResult<SalesInvoiceDto>.Failure(InvoiceNotFoundMessage);
            }

            return ServiceResult<SalesInvoiceDto>.Success(SalesInvoiceMapper.ToDto(entity));
        }

        public async Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default)
        {
            var next = await _invoiceRepo.GetNextNumberAsync(ct);
            return ServiceResult<string>.Success(next);
        }

        // ══════════════════════════════════════════════════════════
        //  CREATE (Draft)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<SalesInvoiceDto>> CreateAsync(CreateSalesInvoiceDto dto, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<SalesInvoiceDto>(_currentUser, PermissionKeys.SalesCreate);
            if (authCheck != null) return authCheck;

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<SalesInvoiceDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            const int maxRetries = 3;
            int attempt = 0;

            while (attempt < maxRetries)
            {
                attempt++;
                
                try
                {
                    SalesInvoice invoice = null;

                    await _unitOfWork.ExecuteInTransactionAsync(async () =>
                    {
                        var invoiceNumber = await _invoiceRepo.GetNextNumberAsync(ct);

                        invoice = new SalesInvoice(
                            invoiceNumber,
                            dto.InvoiceDate,
                            dto.CustomerId,
                            dto.WarehouseId,
                            dto.Notes,
                            salesRepresentativeId: dto.SalesRepresentativeId,
                            counterpartyType: dto.CounterpartyType,
                            supplierId: dto.SupplierId);

                        foreach (var lineDto in dto.Lines)
                        {
                            var product = await _productRepo.GetByIdWithUnitsAsync(lineDto.ProductId, ct);
                            if (product == null)
                                throw new SalesInvoiceDomainException($"الصنف برقم {lineDto.ProductId} غير موجود.");

                            var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                            if (productUnit == null)
                                throw new SalesInvoiceDomainException(
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

                        var creditError = await GetCreditControlErrorAsync(invoice.CustomerId, invoice.NetTotal, ct);
                        if (creditError != null)
                            throw new SalesInvoiceDomainException(creditError);

                        await _invoiceRepo.AddAsync(invoice, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }, IsolationLevel.Serializable, ct);

                    var saved = await _invoiceRepo.GetWithLinesAsync(invoice.Id, ct);
                    return ServiceResult<SalesInvoiceDto>.Success(SalesInvoiceMapper.ToDto(saved));
                }
                catch (DbUpdateException ex) when (attempt < maxRetries && 
                    (ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true ||
                     ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true))
                {
                    // Race condition detected: another user created an invoice with the same number
                    // Wait briefly and retry with a new number
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
                    continue;
                }
                catch (DbUpdateException ex) when (
                    ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true ||
                    ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // All retries exhausted
                    return ServiceResult<SalesInvoiceDto>.Failure(
                        "فشل حفظ الفاتورة بسبب تعارض في رقم الفاتورة. يرجى إعادة المحاولة مرة أخرى.");
                }
                catch (SalesInvoiceDomainException ex)
                {
                    return ServiceResult<SalesInvoiceDto>.Failure(ex.Message);
                }
            }

            // Should never reach here
            return ServiceResult<SalesInvoiceDto>.Failure("فشل حفظ الفاتورة بعد عدة محاولات.");
        }

        // ══════════════════════════════════════════════════════════
        //  UPDATE (Draft only)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<SalesInvoiceDto>> UpdateAsync(UpdateSalesInvoiceDto dto, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<SalesInvoiceDto>(_currentUser, PermissionKeys.SalesCreate);
            if (authCheck != null) return authCheck;

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<SalesInvoiceDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var invoice = await _invoiceRepo.GetWithLinesAsync(dto.Id, ct);
            if (invoice == null)
                return ServiceResult<SalesInvoiceDto>.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Draft)
                return ServiceResult<SalesInvoiceDto>.Failure("لا يمكن تعديل فاتورة مرحّلة أو ملغاة.");

            try
            {
                invoice.UpdateHeader(dto.InvoiceDate, dto.CustomerId, dto.WarehouseId, dto.Notes,
                    salesRepresentativeId: dto.SalesRepresentativeId,
                    counterpartyType: dto.CounterpartyType, supplierId: dto.SupplierId);

                var newLines = new List<SalesInvoiceLine>();
                foreach (var lineDto in dto.Lines)
                {
                    var product = await _productRepo.GetByIdWithUnitsAsync(lineDto.ProductId, ct);
                    if (product == null)
                        return ServiceResult<SalesInvoiceDto>.Failure($"الصنف برقم {lineDto.ProductId} غير موجود.");

                    var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                    if (productUnit == null)
                        return ServiceResult<SalesInvoiceDto>.Failure(
                            $"الوحدة المحددة غير مرتبطة بالصنف ({product.NameAr}).");

                    newLines.Add(new SalesInvoiceLine(
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
                return ServiceResult<SalesInvoiceDto>.Success(SalesInvoiceMapper.ToDto(saved));
            }
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult<SalesInvoiceDto>.Failure(ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  POST — The critical operation
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Posts a draft sales invoice. This triggers:
        ///   1. Stock validation — every line must have sufficient warehouse stock.
        ///   2. Revenue journal — DR AR / CR Sales / CR VAT Output.
        ///   3. COGS journal — DR COGS / CR Inventory (per-line at current WAC).
        ///   4. Warehouse stock decrease + inventory movement records.
        ///   5. Invoice status → Posted.
        /// </summary>
        public async Task<ServiceResult<SalesInvoiceDto>> PostAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<SalesInvoiceDto>(_currentUser, PermissionKeys.SalesPost);
            if (authCheck != null) return authCheck;

            var invoice = await _invoiceRepo.GetWithLinesAsync(id, ct);
            var preCheck = ValidatePostPreconditions(invoice);
            if (preCheck != null) return preCheck;

            // ── Credit Control Re-check at Post ──────────────────
            var creditError = await GetCreditControlErrorAsync(invoice.CustomerId, invoice.NetTotal, ct);
            if (creditError != null)
                return ServiceResult<SalesInvoiceDto>.Failure(creditError);

            SalesInvoice saved = null;

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    await ValidateStockAsync(invoice, ct);

                    var (fiscalYear, period) = await GetPostingPeriodAsync(invoice, ct);
                    var accounts = await ResolvePostingAccountsAsync(ct);
                    var now = _dateTime.UtcNow;
                    var username = _currentUser.Username ?? "System";

                    var revenueJournal = await CreateRevenueJournalAsync(
                        invoice,
                        fiscalYear,
                        period,
                        accounts,
                        now,
                        username,
                        ct);

                    var cogsResult = await CreateCogsJournalAsync(
                        invoice,
                        fiscalYear,
                        period,
                        accounts,
                        now,
                        username,
                        ct);

                    await _unitOfWork.SaveChangesAsync(ct);

                    await DeductStockAsync(invoice, cogsResult.lineCosts, ct);

                    invoice.Post(revenueJournal.Id, cogsResult.journal.Id);
                    _invoiceRepo.Update(invoice);
                    await _unitOfWork.SaveChangesAsync(ct);

                    saved = await _invoiceRepo.GetWithLinesAsync(invoice.Id, ct);
                }, IsolationLevel.Serializable, ct);

                return ServiceResult<SalesInvoiceDto>.Success(SalesInvoiceMapper.ToDto(saved));
            }
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult<SalesInvoiceDto>.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post sales invoice {InvoiceId}.", invoice?.Id);
                return ServiceResult<SalesInvoiceDto>.Failure("حدث خطأ غير متوقع أثناء ترحيل الفاتورة.");
            }
        }

        private async Task<string> GetCreditControlErrorAsync(int customerId, decimal invoiceNetTotal, CancellationToken ct)
        {
            var customer = await _customerRepo.GetByIdAsync(customerId, ct);
            if (customer == null) return null;

            if (customer.BlockedOnOverdue && customer.DaysAllowed is int daysAllowed && daysAllowed > 0)
            {
                var cutoff = _dateTime.UtcNow.Date.AddDays(-daysAllowed);
                var hasOverdue = await _smartEntryQueryService.HasOverduePostedSalesInvoicesAsync(customerId, cutoff, ct);
                if (hasOverdue)
                    return $"العميل ({customer.NameAr}) محظور بسبب وجود فواتير متأخرة السداد.";
            }

            if (customer.CreditLimit > 0)
            {
                // Outstanding = PreviousBalance + (Posted Invoices - Posted Receipts)
                var outstanding = customer.PreviousBalance +
                                  await _smartEntryQueryService.GetCustomerOutstandingSalesBalanceAsync(customerId, ct);

                var newExposure = outstanding + invoiceNetTotal;
                if (newExposure > customer.CreditLimit)
                {
                    return $"تجاوز الحد الائتماني للعميل ({customer.NameAr}). " +
                           $"الرصيد المستحق: {outstanding:N2}, الفاتورة: {invoiceNetTotal:N2}, الحد: {customer.CreditLimit:N2}.";
                }
            }

            return null;
        }

        // ══════════════════════════════════════════════════════════
        //  CANCEL
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.SalesPost);
            if (authCheck != null) return authCheck;

            var invoice = await _invoiceRepo.GetWithLinesAsync(id, ct);
            var preCheck = ValidateCancelPreconditions(invoice);
            if (preCheck != null) return preCheck;

            try
            {
                var reversalInfo = await GetReversalPeriodAsync(invoice.InvoiceDate, ct);
                var fiscalYear = reversalInfo.fiscalYear;
                var period = reversalInfo.period;
                var reversalDate = reversalInfo.reversalDate;

                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    await ReverseStockAsync(invoice, reversalDate, ct);
                    await CreateRevenueReversalAsync(invoice, fiscalYear, period, reversalDate, ct);
                    await CreateCogsReversalAsync(invoice, fiscalYear, period, reversalDate, ct);

                    invoice.Cancel();
                    _invoiceRepo.Update(invoice);
                    await _unitOfWork.SaveChangesAsync(ct);
                }, IsolationLevel.Serializable, ct);

                return ServiceResult.Success();
            }
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel sales invoice {InvoiceId}.", invoice?.Id);
                return ServiceResult.Failure("حدث خطأ غير متوقع أثناء إلغاء فاتورة البيع.");
            }
        }

        private static ServiceResult<SalesInvoiceDto> ValidatePostPreconditions(SalesInvoice invoice)
        {
            if (invoice == null)
                return ServiceResult<SalesInvoiceDto>.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Draft)
                return ServiceResult<SalesInvoiceDto>.Failure("لا يمكن ترحيل فاتورة مرحّلة بالفعل أو ملغاة.");

            if (!invoice.Lines.Any())
                return ServiceResult<SalesInvoiceDto>.Failure("لا يمكن ترحيل فاتورة بدون بنود.");

            return null;
        }

        private async Task ValidateStockAsync(SalesInvoice invoice, CancellationToken ct)
        {
            foreach (var line in invoice.Lines)
            {
                var whProduct = await _whProductRepo.GetAsync(
                    invoice.WarehouseId, line.ProductId, ct);

                if (whProduct == null || whProduct.Quantity < line.BaseQuantity)
                {
                    var available = whProduct?.Quantity ?? 0;
                    var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                    var productName = product?.NameAr ?? $"#{line.ProductId}";
                    throw new SalesInvoiceDomainException(
                        $"الكمية المتاحة للصنف ({productName}) = {available:N2} أقل من الكمية المطلوبة ({line.BaseQuantity:N2}).");
                }
            }
        }

        private async Task<(FiscalYear fiscalYear, FiscalPeriod period)> GetPostingPeriodAsync(
            SalesInvoice invoice,
            CancellationToken ct)
        {
            var fiscalYear = await _fiscalYearRepo.GetActiveYearAsync(ct);
            if (fiscalYear == null)
                throw new SalesInvoiceDomainException("لا توجد سنة مالية نشطة.");

            if (!fiscalYear.ContainsDate(invoice.InvoiceDate))
                throw new SalesInvoiceDomainException(
                    $"تاريخ الفاتورة {invoice.InvoiceDate:yyyy-MM-dd} لا يقع ضمن السنة المالية النشطة.");

            var yearWithPeriods = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            var period = yearWithPeriods.GetPeriod(invoice.InvoiceDate.Month);
            if (period == null)
                throw new SalesInvoiceDomainException("لا توجد فترة مالية للشهر المحدد.");

            if (!period.IsOpen)
                throw new SalesInvoiceDomainException(
                    $"الفترة المالية ({period.Year}-{period.Month:D2}) مقفلة. لا يمكن الترحيل.");

            return (fiscalYear, period);
        }

        private async Task<(Account ar, Account sales, Account vatOutput, Account cogs, Account inventory)>
            ResolvePostingAccountsAsync(CancellationToken ct)
        {
            var arAccount = await _accountRepo.GetByCodeAsync(ArAccountCode, ct);
            var salesAccount = await _accountRepo.GetByCodeAsync(SalesAccountCode, ct);
            var vatOutputAccount = await _accountRepo.GetByCodeAsync(VatOutputAccountCode, ct);
            var cogsAccount = await _accountRepo.GetByCodeAsync(CogsAccountCode, ct);
            var inventoryAccount = await _accountRepo.GetByCodeAsync(InventoryAccountCode, ct);

            if (arAccount == null || salesAccount == null || vatOutputAccount == null
                || cogsAccount == null || inventoryAccount == null)
            {
                throw new SalesInvoiceDomainException(
                    "حسابات النظام المطلوبة (مدينون / مبيعات / ضريبة مخرجات / تكلفة بضاعة / مخزون) غير موجودة. تأكد من تشغيل Seed.");
            }

            return (arAccount, salesAccount, vatOutputAccount, cogsAccount, inventoryAccount);
        }

        private async Task<JournalEntry> CreateRevenueJournalAsync(
            SalesInvoice invoice,
            FiscalYear fiscalYear,
            FiscalPeriod period,
            (Account ar, Account sales, Account vatOutput, Account cogs, Account inventory) accounts,
            DateTime now,
            string username,
            CancellationToken ct)
        {
            var revenueJournal = JournalEntry.CreateDraft(
                invoice.InvoiceDate,
                $"فاتورة بيع رقم {invoice.InvoiceNumber}",
                SourceType.SalesInvoice,
                fiscalYear.Id,
                period.Id,
                referenceNumber: invoice.InvoiceNumber,
                sourceId: invoice.Id);

            revenueJournal.AddLine(accounts.ar.Id, invoice.NetTotal, 0, now,
                $"عميل — فاتورة بيع {invoice.InvoiceNumber}");

            var netSalesRevenue = invoice.Subtotal - invoice.DiscountTotal;
            if (netSalesRevenue > 0)
                revenueJournal.AddLine(accounts.sales.Id, 0, netSalesRevenue, now,
                    $"مبيعات — فاتورة بيع {invoice.InvoiceNumber}");

            if (invoice.VatTotal > 0)
                revenueJournal.AddLine(accounts.vatOutput.Id, 0, invoice.VatTotal, now,
                    $"ضريبة مخرجات — فاتورة بيع {invoice.InvoiceNumber}");

            var revenueJournalNumber = _journalNumberGen.NextNumber(fiscalYear.Id);
            revenueJournal.Post(revenueJournalNumber, username, now);
            await _journalRepo.AddAsync(revenueJournal, ct);
            return revenueJournal;
        }

        private async Task<(JournalEntry journal, Dictionary<int, decimal> lineCosts)> CreateCogsJournalAsync(
            SalesInvoice invoice,
            FiscalYear fiscalYear,
            FiscalPeriod period,
            (Account ar, Account sales, Account vatOutput, Account cogs, Account inventory) accounts,
            DateTime now,
            string username,
            CancellationToken ct)
        {
            var cogsJournal = JournalEntry.CreateDraft(
                invoice.InvoiceDate,
                $"تكلفة بضاعة مباعة — فاتورة بيع {invoice.InvoiceNumber}",
                SourceType.SalesInvoice,
                fiscalYear.Id,
                period.Id,
                referenceNumber: invoice.InvoiceNumber,
                sourceId: invoice.Id);

            decimal totalCogs = 0;
            var lineCosts = new Dictionary<int, decimal>();

            foreach (var line in invoice.Lines)
            {
                var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                var costPerBaseUnit = product.WeightedAverageCost;
                var lineCost = Math.Round(line.BaseQuantity * costPerBaseUnit, 4);
                totalCogs += lineCost;
                lineCosts[line.Id] = costPerBaseUnit;
            }

            if (totalCogs > 0)
            {
                cogsJournal.AddLine(accounts.cogs.Id, totalCogs, 0, now,
                    $"تكلفة بضاعة مباعة — فاتورة بيع {invoice.InvoiceNumber}");

                cogsJournal.AddLine(accounts.inventory.Id, 0, totalCogs, now,
                    $"مخزون — تكلفة بضاعة مباعة {invoice.InvoiceNumber}");
            }

            var cogsJournalNumber = _journalNumberGen.NextNumber(fiscalYear.Id);
            cogsJournal.Post(cogsJournalNumber, username, now);
            await _journalRepo.AddAsync(cogsJournal, ct);

            return (cogsJournal, lineCosts);
        }

        private async Task DeductStockAsync(
            SalesInvoice invoice,
            IReadOnlyDictionary<int, decimal> lineCosts,
            CancellationToken ct)
        {
            foreach (var line in invoice.Lines)
            {
                var whProduct = await _whProductRepo.GetAsync(
                    invoice.WarehouseId, line.ProductId, ct);

                whProduct.DecreaseStock(line.BaseQuantity);
                _whProductRepo.Update(whProduct);

                var costPerBaseUnit = lineCosts.TryGetValue(line.Id, out var unitCost) ? unitCost : 0;
                var lineCost = Math.Round(line.BaseQuantity * costPerBaseUnit, 4);

                var movement = new InventoryMovement(
                    line.ProductId,
                    invoice.WarehouseId,
                    line.UnitId,
                    MovementType.SalesOut,
                    line.Quantity,
                    line.BaseQuantity,
                    costPerBaseUnit,
                    lineCost,
                    invoice.InvoiceDate,
                    invoice.InvoiceNumber,
                    SourceType.SalesInvoice,
                    sourceId: invoice.Id,
                    notes: $"فاتورة بيع رقم {invoice.InvoiceNumber}");

                movement.SetBalanceAfter(whProduct.Quantity);
                await _movementRepo.AddAsync(movement, ct);
            }
        }

        private static ServiceResult ValidateCancelPreconditions(SalesInvoice invoice)
        {
            if (invoice == null)
                return ServiceResult.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Posted)
                return ServiceResult.Failure("لا يمكن إلغاء إلا الفواتير المرحّلة.");

            if (!invoice.JournalEntryId.HasValue || !invoice.CogsJournalEntryId.HasValue)
                return ServiceResult.Failure("لا يمكن إلغاء فاتورة بدون قيود محاسبية.");

            if (invoice.PaidAmount > 0)
                return ServiceResult.Failure(
                    $"لا يمكن إلغاء فاتورة عليها دفعات ({invoice.PaidAmount:N2}). يجب إلغاء سندات القبض المرتبطة أولاً.");

            return null;
        }

        private async Task<(FiscalYear fiscalYear, FiscalPeriod period, DateTime reversalDate)> GetReversalPeriodAsync(
            DateTime reversalDate,
            CancellationToken ct)
        {
            var fiscalYear = await _fiscalYearRepo.GetByYearAsync(reversalDate.Year, ct);
            if (fiscalYear == null)
                throw new SalesInvoiceDomainException($"لا توجد سنة مالية للعام {reversalDate.Year}.");

            fiscalYear = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            if (fiscalYear.Status != FiscalYearStatus.Active)
                throw new SalesInvoiceDomainException($"السنة المالية {fiscalYear.Year} ليست فعّالة.");

            var period = fiscalYear.GetPeriod(reversalDate.Month);
            if (period == null || !period.IsOpen)
                throw new SalesInvoiceDomainException($"الفترة المالية لـ {reversalDate:yyyy-MM} مُقفلة.");

            return (fiscalYear, period, reversalDate);
        }

        private async Task ReverseStockAsync(SalesInvoice invoice, DateTime today, CancellationToken ct)
        {
            foreach (var line in invoice.Lines)
            {
                var whProduct = await _whProductRepo.GetOrCreateAsync(
                    invoice.WarehouseId, line.ProductId, ct);

                whProduct.IncreaseStock(line.BaseQuantity);
                _whProductRepo.Update(whProduct);

                var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                var costPerBaseUnit = product.WeightedAverageCost;
                var lineCost = Math.Round(line.BaseQuantity * costPerBaseUnit, 4);

                var movement = new InventoryMovement(
                    line.ProductId,
                    invoice.WarehouseId,
                    line.UnitId,
                    MovementType.SalesReturn,
                    line.Quantity,
                    line.BaseQuantity,
                    costPerBaseUnit,
                    lineCost,
                    today,
                    invoice.InvoiceNumber,
                    SourceType.SalesInvoice,
                    sourceId: invoice.Id,
                    notes: $"إلغاء فاتورة بيع رقم {invoice.InvoiceNumber}");

                movement.SetBalanceAfter(whProduct.Quantity);
                await _movementRepo.AddAsync(movement, ct);
            }
        }

        private async Task CreateRevenueReversalAsync(
            SalesInvoice invoice,
            FiscalYear fiscalYear,
            FiscalPeriod period,
            DateTime today,
            CancellationToken ct)
        {
            var revenueJournal = await _journalRepo.GetWithLinesAsync(invoice.JournalEntryId.Value, ct);
            if (revenueJournal == null)
                throw new SalesInvoiceDomainException("قيد الإيراد الأصلي غير موجود.");

            var revenueReversal = revenueJournal.CreateReversal(
                today,
                $"عكس إيراد فاتورة بيع رقم {invoice.InvoiceNumber}",
                fiscalYear.Id,
                period.Id);

            var revenueNumber = _journalNumberGen.NextNumber(fiscalYear.Id);
            revenueReversal.Post(revenueNumber, _currentUser.Username, _dateTime.UtcNow);
            await _journalRepo.AddAsync(revenueReversal, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            revenueJournal.MarkAsReversed(revenueReversal.Id);
            _journalRepo.Update(revenueJournal);
        }

        private async Task CreateCogsReversalAsync(
            SalesInvoice invoice,
            FiscalYear fiscalYear,
            FiscalPeriod period,
            DateTime today,
            CancellationToken ct)
        {
            var cogsJournal = await _journalRepo.GetWithLinesAsync(invoice.CogsJournalEntryId.Value, ct);
            if (cogsJournal == null)
                throw new SalesInvoiceDomainException("قيد التكلفة الأصلي غير موجود.");

            var cogsReversal = cogsJournal.CreateReversal(
                today,
                $"عكس تكلفة فاتورة بيع رقم {invoice.InvoiceNumber}",
                fiscalYear.Id,
                period.Id);

            var cogsNumber = _journalNumberGen.NextNumber(fiscalYear.Id);
            cogsReversal.Post(cogsNumber, _currentUser.Username, _dateTime.UtcNow);
            await _journalRepo.AddAsync(cogsReversal, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            cogsJournal.MarkAsReversed(cogsReversal.Id);
            _journalRepo.Update(cogsJournal);
        }

        // ══════════════════════════════════════════════════════════
        //  DELETE (Draft only)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.SalesCreate);
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
    }

    public sealed class SalesInvoiceRepositories
    {
        public SalesInvoiceRepositories(
            ISalesInvoiceRepository invoiceRepo,
            IProductRepository productRepo,
            IWarehouseProductRepository whProductRepo,
            IInventoryMovementRepository movementRepo,
            IJournalEntryRepository journalRepo,
            IAccountRepository accountRepo,
            ICustomerRepository customerRepo)
        {
            InvoiceRepo = invoiceRepo ?? throw new ArgumentNullException(nameof(invoiceRepo));
            ProductRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
            WhProductRepo = whProductRepo ?? throw new ArgumentNullException(nameof(whProductRepo));
            MovementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
            JournalRepo = journalRepo ?? throw new ArgumentNullException(nameof(journalRepo));
            AccountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
            CustomerRepo = customerRepo ?? throw new ArgumentNullException(nameof(customerRepo));
        }

        public ISalesInvoiceRepository InvoiceRepo { get; }
        public IProductRepository ProductRepo { get; }
        public IWarehouseProductRepository WhProductRepo { get; }
        public IInventoryMovementRepository MovementRepo { get; }
        public IJournalEntryRepository JournalRepo { get; }
        public IAccountRepository AccountRepo { get; }
        public ICustomerRepository CustomerRepo { get; }
    }

    public sealed class SalesInvoiceServices
    {
        public SalesInvoiceServices(
            IFiscalYearRepository fiscalYearRepo,
            IJournalNumberGenerator journalNumberGen,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime,
            ISmartEntryQueryService smartEntryQueryService)
        {
            FiscalYearRepo = fiscalYearRepo ?? throw new ArgumentNullException(nameof(fiscalYearRepo));
            JournalNumberGen = journalNumberGen ?? throw new ArgumentNullException(nameof(journalNumberGen));
            UnitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            CurrentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            DateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            SmartEntryQueryService = smartEntryQueryService ?? throw new ArgumentNullException(nameof(smartEntryQueryService));
        }

        public IFiscalYearRepository FiscalYearRepo { get; }
        public IJournalNumberGenerator JournalNumberGen { get; }
        public IUnitOfWork UnitOfWork { get; }
        public ICurrentUserService CurrentUser { get; }
        public IDateTimeProvider DateTime { get; }
        public ISmartEntryQueryService SmartEntryQueryService { get; }
    }

    public sealed class SalesInvoiceValidators
    {
        public SalesInvoiceValidators(
            IValidator<CreateSalesInvoiceDto> createValidator,
            IValidator<UpdateSalesInvoiceDto> updateValidator)
        {
            CreateValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            UpdateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
        }

        public IValidator<CreateSalesInvoiceDto> CreateValidator { get; }
        public IValidator<UpdateSalesInvoiceDto> UpdateValidator { get; }
    }
}
