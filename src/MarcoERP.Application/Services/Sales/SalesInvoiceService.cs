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
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Application.Mappers.Sales;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Exceptions.Accounting;
using MarcoERP.Domain.Exceptions.Inventory;
using MarcoERP.Domain.Exceptions.Sales;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Domain.Interfaces.Settings;
using Microsoft.Extensions.Logging;

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
    public sealed partial class SalesInvoiceService : ISalesInvoiceService
    {
        private readonly ISalesInvoiceRepository _invoiceRepo;
        private readonly IProductRepository _productRepo;
        private readonly IWarehouseProductRepository _whProductRepo;
        private readonly IInventoryMovementRepository _movementRepo;
        private readonly IJournalEntryRepository _journalRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly ICustomerRepository _customerRepo;
        private readonly ISmartEntryQueryService _smartEntryQueryService;
        private readonly IJournalNumberGenerator _journalNumberGen;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreateSalesInvoiceDto> _createValidator;
        private readonly IValidator<UpdateSalesInvoiceDto> _updateValidator;
        private readonly ILogger<SalesInvoiceService> _logger;
        private readonly ISystemSettingRepository _systemSettingRepository;
        private readonly IFeatureService _featureService;
        private readonly IAuditLogger _auditLogger;
        private readonly JournalEntryFactory _journalFactory;
        private readonly FiscalPeriodValidator _fiscalValidator;
        private readonly StockManager _stockManager;

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
        private const string CommissionExpenseAccountCode = SystemAccountCodes.CommissionExpense;   // 6201
        private const string CommissionPayableAccountCode = SystemAccountCodes.CommissionPayable;   // 2301
        private const string InvoiceNotFoundMessage = "فاتورة البيع غير موجودة.";

        public SalesInvoiceService(
            SalesInvoiceRepositories repos,
            SalesInvoiceServices services,
            SalesInvoiceValidators validators,
            JournalEntryFactory journalFactory,
            FiscalPeriodValidator fiscalValidator,
            StockManager stockManager,
            ILogger<SalesInvoiceService> logger = null)
        {
            if (repos == null) throw new ArgumentNullException(nameof(repos));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (validators == null) throw new ArgumentNullException(nameof(validators));

            _invoiceRepo = repos.InvoiceRepo;
            _productRepo = repos.ProductRepo;
            _whProductRepo = repos.WhProductRepo;
            _movementRepo = repos.MovementRepo;
            _journalRepo = repos.JournalRepo;
            _accountRepo = repos.AccountRepo;
            _customerRepo = repos.CustomerRepo;

            _journalNumberGen = services.JournalNumberGen;
            _unitOfWork = services.UnitOfWork;
            _currentUser = services.CurrentUser;
            _dateTime = services.DateTime;
            _smartEntryQueryService = services.SmartEntryQueryService;
            _systemSettingRepository = services.SystemSettingRepo;
            _featureService = services.FeatureService;
            _auditLogger = services.AuditLogger;

            _stockManager = stockManager ?? throw new ArgumentNullException(nameof(stockManager));

            _createValidator = validators.CreateValidator;
            _updateValidator = validators.UpdateValidator;
            _journalFactory = journalFactory ?? throw new ArgumentNullException(nameof(journalFactory));
            _fiscalValidator = fiscalValidator ?? throw new ArgumentNullException(nameof(fiscalValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SalesInvoiceService>.Instance;
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
            // Feature Guard — block operation if Sales module is disabled
            var guard = await FeatureGuard.CheckAsync<SalesInvoiceDto>(_featureService, FeatureKeys.Sales, ct);
            if (guard != null) return guard;

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "SalesInvoice", 0);

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<SalesInvoiceDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            // SD-04 fix: Validate FK references exist and are not soft-deleted
            if (dto.CustomerId.HasValue)
            {
                var customer = await _customerRepo.GetByIdAsync(dto.CustomerId.Value, ct);
                if (customer == null)
                    return ServiceResult<SalesInvoiceDto>.Failure("العميل غير موجود أو محذوف.");
            }

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
                            supplierId: dto.SupplierId,
                            invoiceType: dto.InvoiceType,
                            paymentMethod: dto.PaymentMethod,
                            dueDate: dto.DueDate);

                        // Apply header-level discount & delivery fee
                        invoice.UpdateHeader(
                            dto.InvoiceDate,
                            dto.CustomerId,
                            dto.WarehouseId,
                            dto.Notes,
                            salesRepresentativeId: dto.SalesRepresentativeId,
                            counterpartyType: dto.CounterpartyType,
                            supplierId: dto.SupplierId,
                            headerDiscountPercent: dto.HeaderDiscountPercent,
                            headerDiscountAmount: dto.HeaderDiscountAmount,
                            deliveryFee: dto.DeliveryFee,
                            invoiceType: dto.InvoiceType,
                            paymentMethod: dto.PaymentMethod,
                            dueDate: dto.DueDate);

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
                catch (DuplicateRecordException) when (attempt < maxRetries)
                {
                    // Race condition detected: another user created an invoice with the same number
                    // Wait briefly and retry with a new number
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
                    continue;
                }
                catch (ConcurrencyConflictException)
                {
                    return ServiceResult<SalesInvoiceDto>.Failure("تم تعديل الفاتورة بواسطة مستخدم آخر. يرجى إعادة المحاولة.");
                }
                catch (DuplicateRecordException)
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
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "SalesInvoice", dto.Id);

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<SalesInvoiceDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            // ── CRITICAL FIX: Load WITH tracking so EF Core can detect line changes ──
            // Previously used AsNoTracking which caused orphaned lines:
            //   Old lines remained in DB (never deleted) + new lines inserted = duplication.
            var invoice = await _invoiceRepo.GetWithLinesTrackedAsync(dto.Id, ct);
            if (invoice == null)
                return ServiceResult<SalesInvoiceDto>.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Draft)
                return ServiceResult<SalesInvoiceDto>.Failure("لا يمكن تعديل فاتورة مرحّلة أو ملغاة.");

            try
            {
                invoice.UpdateHeader(dto.InvoiceDate, dto.CustomerId, dto.WarehouseId, dto.Notes,
                    salesRepresentativeId: dto.SalesRepresentativeId,
                    counterpartyType: dto.CounterpartyType, supplierId: dto.SupplierId,
                    headerDiscountPercent: dto.HeaderDiscountPercent,
                    headerDiscountAmount: dto.HeaderDiscountAmount,
                    deliveryFee: dto.DeliveryFee,
                    invoiceType: dto.InvoiceType,
                    paymentMethod: dto.PaymentMethod,
                    dueDate: dto.DueDate);

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
                        product.VatRate,
                        lineDto.Id));
                }

                invoice.ReplaceLines(newLines);
                // Entity is already tracked — no need for _invoiceRepo.Update(invoice)
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
        //  DELETE (Draft only)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteDraftAsync", "SalesInvoice", id);

            var invoice = await _invoiceRepo.GetWithLinesTrackedAsync(id, ct);
            if (invoice == null)
                return ServiceResult.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Draft)
                return ServiceResult.Failure("لا يمكن حذف إلا الفواتير المسودة.");

            invoice.SoftDelete(_currentUser.Username, _dateTime.UtcNow);
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
            ISmartEntryQueryService smartEntryQueryService,
            ISystemSettingRepository systemSettingRepo = null,
            IFeatureService featureService = null,
            IAuditLogger auditLogger = null)
        {
            FiscalYearRepo = fiscalYearRepo ?? throw new ArgumentNullException(nameof(fiscalYearRepo));
            JournalNumberGen = journalNumberGen ?? throw new ArgumentNullException(nameof(journalNumberGen));
            UnitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            CurrentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            DateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            SmartEntryQueryService = smartEntryQueryService ?? throw new ArgumentNullException(nameof(smartEntryQueryService));
            SystemSettingRepo = systemSettingRepo;
            FeatureService = featureService;
            AuditLogger = auditLogger;
        }

        public IFiscalYearRepository FiscalYearRepo { get; }
        public IJournalNumberGenerator JournalNumberGen { get; }
        public IUnitOfWork UnitOfWork { get; }
        public ICurrentUserService CurrentUser { get; }
        public IDateTimeProvider DateTime { get; }
        public ISmartEntryQueryService SmartEntryQueryService { get; }
        public ISystemSettingRepository SystemSettingRepo { get; }
        public IFeatureService FeatureService { get; }
        public IAuditLogger AuditLogger { get; }
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
