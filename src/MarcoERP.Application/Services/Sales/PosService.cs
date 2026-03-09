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
using MarcoERP.Application.Interfaces.Settings;
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
using MarcoERP.Domain.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Sales
{
    /// <summary>
    /// Implements POS operations: session management, atomic sale completion,
    /// cancellation with full reversal, and POS reporting.
    /// 
    /// Reuses existing SalesInvoice entity, JournalEntry auto-posting, and InventoryMovement.
    /// Does NOT duplicate SalesInvoiceService posting logic — it follows the same pattern
    /// but wraps everything in a Serializable transaction for POS atomicity.
    /// 
    /// Revenue Journal:  DR Cash/Bank/AR  /  CR 4111 Sales  /  CR 2121 VAT Output
    /// COGS Journal:     DR 5111 COGS      /  CR 1131 Inventory  (per-line at WAC)
    /// </summary>
    [Module(SystemModule.Sales)]
    public sealed partial class PosService : IPosService
    {
        private readonly IPosSessionRepository _sessionRepo;
        private readonly IPosPaymentRepository _paymentRepo;
        private readonly ISalesInvoiceRepository _invoiceRepo;
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
        private readonly IValidator<OpenPosSessionDto> _openSessionValidator;
        private readonly IValidator<ClosePosSessionDto> _closeSessionValidator;
        private readonly IValidator<CompletePoseSaleDto> _completeSaleValidator;
        private readonly ISystemSettingRepository _systemSettingRepository;
        private readonly IFeatureService _featureService;
        private readonly IAuditLogger _auditLogger;
        private readonly IReceiptPrinterService _receiptPrinterService;
        private readonly JournalEntryFactory _journalFactory;
        private readonly FiscalPeriodValidator _fiscalValidator;
        private readonly StockManager _stockManager;
        private readonly ILogger<PosService> _logger;

        // ── GL Account Codes (same as SalesInvoiceService) ──────
        private const string CashAccountCode = "1111";    // النقدية — الصندوق الرئيسي
        private const string CardAccountCode = "1112";    // البنك / بطاقات الدفع
        private const string ArAccountCode = "1121";      // المدينون — ذمم تجارية
        private const string SalesAccountCode = "4111";   // المبيعات — عام
        private const string VatOutputAccountCode = "2121"; // ضريبة مخرجات مستحقة
        private const string CogsAccountCode = "5111";    // تكلفة البضاعة المباعة
        private const string InventoryAccountCode = "1131"; // المخزون
        private const string CashOverShortAccountCode = SystemAccountCodes.CashOverShort; // فروقات الصندوق

        public PosService(
            PosRepositories repos,
            PosServices services,
            PosValidators validators,
            JournalEntryFactory journalFactory,
            FiscalPeriodValidator fiscalValidator,
            StockManager stockManager,
            ILogger<PosService> logger = null)
        {
            if (repos == null) throw new ArgumentNullException(nameof(repos));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (validators == null) throw new ArgumentNullException(nameof(validators));

            _sessionRepo = repos.SessionRepo;
            _paymentRepo = repos.PaymentRepo;
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
            _systemSettingRepository = services.SystemSettingRepo;
            _featureService = services.FeatureService;
            _auditLogger = services.AuditLogger;
            _receiptPrinterService = services.ReceiptPrinterService;

            _openSessionValidator = validators.OpenSessionValidator;
            _closeSessionValidator = validators.CloseSessionValidator;
            _completeSaleValidator = validators.CompleteSaleValidator;
            _journalFactory = journalFactory ?? throw new ArgumentNullException(nameof(journalFactory));
            _fiscalValidator = fiscalValidator ?? throw new ArgumentNullException(nameof(fiscalValidator));
            _stockManager = stockManager ?? throw new ArgumentNullException(nameof(stockManager));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PosService>.Instance;
        }

        // ══════════════════════════════════════════════════════════
        //  PRODUCT LOOKUP (lightweight, AsNoTracking-style)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<IReadOnlyList<PosProductLookupDto>>> LoadProductCacheAsync(CancellationToken ct = default)
        {
            var products = await _productRepo.GetAllWithUnitsAsync(ct);
            var result = products
                .Where(p => p.Status == ProductStatus.Active)
                .Select(PosMapper.ToProductLookupDto)
                .ToList();
            return ServiceResult<IReadOnlyList<PosProductLookupDto>>.Success(result);
        }

        public async Task<ServiceResult<PosProductLookupDto>> FindByBarcodeAsync(string barcode, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return ServiceResult<PosProductLookupDto>.Failure("الباركود مطلوب.");

            var product = await _productRepo.GetByBarcodeAsync(barcode.Trim(), ct);
            if (product == null)
                return ServiceResult<PosProductLookupDto>.Failure($"لم يتم العثور على صنف بالباركود: {barcode}");

            if (product.Status != ProductStatus.Active)
                return ServiceResult<PosProductLookupDto>.Failure($"الصنف ({product.NameAr}) غير نشط.");

            return ServiceResult<PosProductLookupDto>.Success(PosMapper.ToProductLookupDto(product));
        }

        public async Task<ServiceResult<IReadOnlyList<PosProductLookupDto>>> SearchProductsAsync(string term, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(term))
                return ServiceResult<IReadOnlyList<PosProductLookupDto>>.Success(new List<PosProductLookupDto>());

            var products = await _productRepo.SearchAsync(term.Trim(), ct);
            var result = products
                .Where(p => p.Status == ProductStatus.Active)
                .Select(PosMapper.ToProductLookupDto)
                .ToList();
            return ServiceResult<IReadOnlyList<PosProductLookupDto>>.Success(result);
        }

        public async Task<ServiceResult<decimal>> GetAvailableStockAsync(int productId, int warehouseId, CancellationToken ct = default)
        {
            var whProduct = await _whProductRepo.GetAsync(warehouseId, productId, ct);
            return ServiceResult<decimal>.Success(whProduct?.Quantity ?? 0);
        }

        private sealed class PosPaymentBreakdown
        {
            public decimal TotalCash { get; init; }
            public decimal TotalCard { get; init; }
            public decimal TotalOnAccount { get; init; }
            public int CustomerId { get; init; }
            public List<PosParsedPayment> Payments { get; init; } = new();
            public decimal TotalPaid => TotalCash + TotalCard + TotalOnAccount;
        }



        private sealed class PosSaleResult
        {
            public SalesInvoiceDto Invoice { get; init; }
            public ReceiptDto Receipt { get; init; }
            public string WarningMessage { get; init; }
        }

        private sealed record PosParsedPayment(PaymentMethod Method, decimal Amount, string Reference);
    }
}
