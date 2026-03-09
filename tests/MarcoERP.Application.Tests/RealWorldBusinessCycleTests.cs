using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.SmartEntry;
using MarcoERP.Application.Services.Purchases;
using MarcoERP.Application.Services.Sales;
using MarcoERP.Application.Services.Treasury;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Entities.Settings;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Purchases;
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Domain.Interfaces.Settings;
using MarcoERP.Domain.Interfaces.Treasury;
using MarcoERP.Application.Interfaces.Settings;

namespace MarcoERP.Application.Tests
{
    /// <summary>
    /// Real-world full business cycle scenario test.
    /// Simulates the complete flow a business would follow:
    ///   1. Purchase Invoice → create + post → stock increases
    ///   2. Sales Invoice → create + post → stock decreases
    ///   3. Sales Return → create + post → stock increases back
    ///   4. Purchase Return → create + post → stock decreases back
    ///   5. Cash Receipt → create + post → cashbox balance increases
    ///   6. Cash Payment → create + post → cashbox balance decreases
    /// Verifies inventory quantities and cashbox balances at each step.
    /// </summary>
    public class RealWorldBusinessCycleTests
    {
        // ── Shared Mocks ───────────────────────────────────────

        // Repositories
        private readonly Mock<IPurchaseInvoiceRepository> _purchaseInvoiceRepoMock = new();
        private readonly Mock<ISalesInvoiceRepository> _salesInvoiceRepoMock = new();
        private readonly Mock<ISalesReturnRepository> _salesReturnRepoMock = new();
        private readonly Mock<IPurchaseReturnRepository> _purchaseReturnRepoMock = new();
        private readonly Mock<ICashReceiptRepository> _cashReceiptRepoMock = new();
        private readonly Mock<ICashPaymentRepository> _cashPaymentRepoMock = new();
        private readonly Mock<IProductRepository> _productRepoMock = new();
        private readonly Mock<IWarehouseProductRepository> _whProductRepoMock = new();
        private readonly Mock<IInventoryMovementRepository> _movementRepoMock = new();
        private readonly Mock<IJournalEntryRepository> _journalRepoMock = new();
        private readonly Mock<IAccountRepository> _accountRepoMock = new();
        private readonly Mock<IFiscalYearRepository> _fiscalYearRepoMock = new();
        private readonly Mock<ICashboxRepository> _cashboxRepoMock = new();
        private readonly Mock<ICustomerRepository> _customerRepoMock = new();
        private readonly Mock<ISupplierRepository> _supplierRepoMock = new();

        // Services & Utilities
        private readonly Mock<IJournalNumberGenerator> _journalNumberGenMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<ICurrentUserService> _currentUserMock = new();
        private readonly Mock<IDateTimeProvider> _dateTimeMock = new();
        private readonly Mock<IAuditLogger> _auditLoggerMock = new();
        private readonly Mock<ISmartEntryQueryService> _smartEntryMock = new();
        private readonly Mock<IFeatureService> _featureServiceMock = new();
        private readonly Mock<ISystemSettingRepository> _systemSettingRepoMock = new();

        // Validators
        private readonly Mock<IValidator<CreatePurchaseInvoiceDto>> _purchaseInvoiceCreateValidator = new();
        private readonly Mock<IValidator<UpdatePurchaseInvoiceDto>> _purchaseInvoiceUpdateValidator = new();
        private readonly Mock<IValidator<CreateSalesInvoiceDto>> _salesInvoiceCreateValidator = new();
        private readonly Mock<IValidator<UpdateSalesInvoiceDto>> _salesInvoiceUpdateValidator = new();
        private readonly Mock<IValidator<CreateSalesReturnDto>> _salesReturnCreateValidator = new();
        private readonly Mock<IValidator<UpdateSalesReturnDto>> _salesReturnUpdateValidator = new();
        private readonly Mock<IValidator<CreatePurchaseReturnDto>> _purchaseReturnCreateValidator = new();
        private readonly Mock<IValidator<UpdatePurchaseReturnDto>> _purchaseReturnUpdateValidator = new();
        private readonly Mock<IValidator<CreateCashReceiptDto>> _cashReceiptCreateValidator = new();
        private readonly Mock<IValidator<UpdateCashReceiptDto>> _cashReceiptUpdateValidator = new();
        private readonly Mock<IValidator<CreateCashPaymentDto>> _cashPaymentCreateValidator = new();
        private readonly Mock<IValidator<UpdateCashPaymentDto>> _cashPaymentUpdateValidator = new();

        // ── Shared Domain Entities (tracked across steps) ──────

        private readonly Product _product;
        private readonly WarehouseProduct _warehouseProduct;
        private readonly Cashbox _cashbox;
        private readonly FiscalYear _fiscalYear;

        // GL Accounts
        private readonly Account _arAccount;         // 1121 المدينون
        private readonly Account _inventoryAccount;  // 1131 المخزون
        private readonly Account _vatInputAccount;   // 1141 ضريبة مدخلات
        private readonly Account _apAccount;         // 2111 الدائنون
        private readonly Account _vatOutputAccount;  // 2121 ضريبة مخرجات
        private readonly Account _salesAccount;      // 4111 المبيعات
        private readonly Account _cogsAccount;       // 5111 ت.ب.م
        private readonly Account _cashAccount;       // 1111 النقدية

        // Tracking IDs
        private int _nextJournalId = 100;

        // ── Constants ──────────────────────────────────────────

        private const int WarehouseId = 1;
        private const int SupplierId = 1;
        private const int CustomerId = 1;
        private const int ProductId = 1;
        private const int UnitId = 1;
        private const decimal UnitCost = 50m;        // Purchase price per unit
        private const decimal SalePrice = 100m;      // Sale price per unit
        private const decimal VatRate = 15m;          // 15% VAT
        private const decimal PurchaseQty = 20m;     // Buy 20 units
        private const decimal SaleQty = 8m;          // Sell 8 units
        private const decimal ReturnQty = 3m;        // Return 3 units (both sales and purchase)
        private const decimal ReceiptAmount = 500m;  // Cash receipt
        private const decimal PaymentAmount = 300m;  // Cash payment

        public RealWorldBusinessCycleTests()
        {
            // ── Create Shared Entities ──────────────────────

            // Product with base unit
            _product = CreateProduct(ProductId, UnitCost, VatRate);

            // Warehouse stock starts at 0
            _warehouseProduct = new WarehouseProduct(WarehouseId, ProductId, 0);
            SetEntityId(_warehouseProduct, 1);

            // Cashbox starts with 10,000 balance
            _cashbox = new Cashbox("CSH-001", "الخزنة الرئيسية", "Main Cash", accountId: 80);
            SetEntityId(_cashbox, 1);
            _cashbox.IncreaseBalance(10_000m); // Opening balance

            // Fiscal Year 2026 — active with open periods
            _fiscalYear = new FiscalYear(2026);
            SetEntityId(_fiscalYear, 1);
            _fiscalYear.Activate();

            // GL Accounts (level 4 = postable)
            _arAccount = CreateAccount("1121", "المدينون", AccountType.Asset, 10);
            _inventoryAccount = CreateAccount("1131", "المخزون", AccountType.Asset, 50);
            _vatInputAccount = CreateAccount("1141", "ضريبة مدخلات", AccountType.Asset, 55);
            _apAccount = CreateAccount("2111", "الدائنون", AccountType.Liability, 60);
            _vatOutputAccount = CreateAccount("2121", "ضريبة مخرجات", AccountType.Liability, 30);
            _salesAccount = CreateAccount("4111", "المبيعات", AccountType.Revenue, 20);
            _cogsAccount = CreateAccount("5111", "ت.ب.م", AccountType.Expense, 40);
            _cashAccount = CreateAccount("1111", "النقدية", AccountType.Asset, 80);

            // ── Setup Common Mocks ─────────────────────────

            SetupCommonMocks();
        }

        // ═══════════════════════════════════════════════════════
        //                    MAIN SCENARIO TEST
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task FullBusinessCycle_PurchaseToSaleToReturnsToPayments_AllBalancesCorrect()
        {
            // ── Initial State Verification ──────────────────
            _warehouseProduct.Quantity.Should().Be(0, "المخزون يبدأ من صفر");
            _cashbox.Balance.Should().Be(10_000m, "الخزنة تبدأ بـ 10,000");

            // ═══════════════════════════════════════════════════
            // STEP 1: Purchase Invoice — buy 20 units @ 50 each
            // ═══════════════════════════════════════════════════

            var purchaseService = CreatePurchaseInvoiceService();
            var purchaseInvoice = SetupPurchaseInvoiceCreate(PurchaseQty, UnitCost);
            var purchaseDto = new CreatePurchaseInvoiceDto
            {
                InvoiceDate = new DateTime(2026, 2, 15),
                SupplierId = SupplierId,
                WarehouseId = WarehouseId,
                CounterpartyType = CounterpartyType.Supplier,
                Lines = new List<CreatePurchaseInvoiceLineDto>
                {
                    new() { ProductId = ProductId, UnitId = UnitId, Quantity = PurchaseQty, UnitPrice = UnitCost }
                }
            };

            var createPurchaseResult = await purchaseService.CreateAsync(purchaseDto, CancellationToken.None);
            createPurchaseResult.IsSuccess.Should().BeTrue("فاتورة الشراء يجب أن تُنشأ بنجاح");

            // Post purchase invoice
            SetupPurchaseInvoicePost(purchaseInvoice);
            var postPurchaseResult = await purchaseService.PostAsync(purchaseInvoice.Id, CancellationToken.None);
            postPurchaseResult.IsSuccess.Should().BeTrue("فاتورة الشراء يجب أن تُرحّل بنجاح");

            // Verify: stock increased by 20
            _warehouseProduct.Quantity.Should().Be(PurchaseQty,
                $"بعد شراء {PurchaseQty} وحدة، المخزون يجب أن يكون {PurchaseQty}");

            // Verify: journal entries created for purchase
            _journalRepoMock.Verify(
                r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce,
                "يجب إنشاء قيد محاسبي لفاتورة الشراء");

            // ═══════════════════════════════════════════════════
            // STEP 2: Sales Invoice — sell 8 units @ 100 each
            // ═══════════════════════════════════════════════════

            ResetJournalVerification();
            var salesService = CreateSalesInvoiceService();
            var salesInvoice = SetupSalesInvoiceCreate(SaleQty, SalePrice);
            var salesDto = new CreateSalesInvoiceDto
            {
                InvoiceDate = new DateTime(2026, 2, 16),
                CustomerId = CustomerId,
                WarehouseId = WarehouseId,
                CounterpartyType = CounterpartyType.Customer,
                Lines = new List<CreateSalesInvoiceLineDto>
                {
                    new() { ProductId = ProductId, UnitId = UnitId, Quantity = SaleQty, UnitPrice = SalePrice }
                }
            };

            var createSalesResult = await salesService.CreateAsync(salesDto, CancellationToken.None);
            createSalesResult.IsSuccess.Should().BeTrue("فاتورة البيع يجب أن تُنشأ بنجاح");

            // Post sales invoice
            SetupSalesInvoicePost(salesInvoice);
            var postSalesResult = await salesService.PostAsync(salesInvoice.Id, CancellationToken.None);
            postSalesResult.IsSuccess.Should().BeTrue("فاتورة البيع يجب أن تُرحّل بنجاح");

            // Verify: stock decreased from 20 to 12
            var expectedAfterSale = PurchaseQty - SaleQty;
            _warehouseProduct.Quantity.Should().Be(expectedAfterSale,
                $"بعد بيع {SaleQty} وحدة، المخزون يجب أن يكون {expectedAfterSale}");

            // ═══════════════════════════════════════════════════
            // STEP 3: Sales Return — customer returns 3 units
            // ═══════════════════════════════════════════════════

            ResetJournalVerification();
            var salesReturnService = CreateSalesReturnService();
            var salesReturn = SetupSalesReturnCreate(ReturnQty, SalePrice);
            var salesReturnDto = new CreateSalesReturnDto
            {
                ReturnDate = new DateTime(2026, 2, 17),
                CustomerId = CustomerId,
                WarehouseId = WarehouseId,
                CounterpartyType = CounterpartyType.Customer,
                OriginalInvoiceId = salesInvoice.Id,
                Lines = new List<CreateSalesReturnLineDto>
                {
                    new() { ProductId = ProductId, UnitId = UnitId, Quantity = ReturnQty, UnitPrice = SalePrice }
                }
            };

            var createSalesReturnResult = await salesReturnService.CreateAsync(salesReturnDto, CancellationToken.None);
            createSalesReturnResult.IsSuccess.Should().BeTrue("مرتجع البيع يجب أن يُنشأ بنجاح");

            // Post sales return
            SetupSalesReturnPost(salesReturn);
            var postSalesReturnResult = await salesReturnService.PostAsync(salesReturn.Id, CancellationToken.None);
            postSalesReturnResult.IsSuccess.Should().BeTrue("مرتجع البيع يجب أن يُرحّل بنجاح");

            // Verify: stock increased from 12 to 15
            var expectedAfterSalesReturn = expectedAfterSale + ReturnQty;
            _warehouseProduct.Quantity.Should().Be(expectedAfterSalesReturn,
                $"بعد مرتجع بيع {ReturnQty} وحدة، المخزون يجب أن يكون {expectedAfterSalesReturn}");

            // ═══════════════════════════════════════════════════
            // STEP 4: Purchase Return — return 3 units to supplier
            // ═══════════════════════════════════════════════════

            ResetJournalVerification();
            var purchaseReturnService = CreatePurchaseReturnService();
            var purchaseReturn = SetupPurchaseReturnCreate(ReturnQty, UnitCost);
            var purchaseReturnDto = new CreatePurchaseReturnDto
            {
                ReturnDate = new DateTime(2026, 2, 18),
                SupplierId = SupplierId,
                WarehouseId = WarehouseId,
                CounterpartyType = CounterpartyType.Supplier,
                OriginalInvoiceId = purchaseInvoice.Id,
                Lines = new List<CreatePurchaseReturnLineDto>
                {
                    new() { ProductId = ProductId, UnitId = UnitId, Quantity = ReturnQty, UnitPrice = UnitCost }
                }
            };

            var createPurchaseReturnResult = await purchaseReturnService.CreateAsync(purchaseReturnDto, CancellationToken.None);
            createPurchaseReturnResult.IsSuccess.Should().BeTrue("مرتجع الشراء يجب أن يُنشأ بنجاح");

            // Post purchase return
            SetupPurchaseReturnPost(purchaseReturn);
            var postPurchaseReturnResult = await purchaseReturnService.PostAsync(purchaseReturn.Id, CancellationToken.None);
            postPurchaseReturnResult.IsSuccess.Should().BeTrue("مرتجع الشراء يجب أن يُرحّل بنجاح");

            // Verify: stock decreased from 15 to 12
            var expectedAfterPurchaseReturn = expectedAfterSalesReturn - ReturnQty;
            _warehouseProduct.Quantity.Should().Be(expectedAfterPurchaseReturn,
                $"بعد مرتجع شراء {ReturnQty} وحدة، المخزون يجب أن يكون {expectedAfterPurchaseReturn}");

            // ═══════════════════════════════════════════════════
            // STEP 5: Cash Receipt — receive 500 from customer
            // ═══════════════════════════════════════════════════

            ResetJournalVerification();
            var cashReceiptService = CreateCashReceiptService();
            var cashReceipt = SetupCashReceiptCreate();
            var cashReceiptDto = new CreateCashReceiptDto
            {
                ReceiptDate = new DateTime(2026, 2, 19),
                CashboxId = 1,
                AccountId = _arAccount.Id,   // contra = AR account
                CustomerId = CustomerId,
                Amount = ReceiptAmount,
                Description = "تحصيل من العميل"
            };

            var balanceBeforeReceipt = _cashbox.Balance;
            var createReceiptResult = await cashReceiptService.CreateAsync(cashReceiptDto, CancellationToken.None);
            createReceiptResult.IsSuccess.Should().BeTrue("سند القبض يجب أن يُنشأ بنجاح");

            // Post cash receipt
            SetupCashReceiptPost(cashReceipt);
            var postReceiptResult = await cashReceiptService.PostAsync(cashReceipt.Id, CancellationToken.None);
            postReceiptResult.IsSuccess.Should().BeTrue("سند القبض يجب أن يُرحّل بنجاح");

            // Verify: cashbox balance increased by 500
            _cashbox.Balance.Should().Be(balanceBeforeReceipt + ReceiptAmount,
                $"بعد قبض {ReceiptAmount}، رصيد الخزنة يجب أن يزيد");

            // ═══════════════════════════════════════════════════
            // STEP 6: Cash Payment — pay 300 to supplier
            // ═══════════════════════════════════════════════════

            ResetJournalVerification();
            var cashPaymentService = CreateCashPaymentService();
            var cashPayment = SetupCashPaymentCreate();
            var cashPaymentDto = new CreateCashPaymentDto
            {
                PaymentDate = new DateTime(2026, 2, 20),
                CashboxId = 1,
                AccountId = _apAccount.Id,   // contra = AP account
                SupplierId = SupplierId,
                Amount = PaymentAmount,
                Description = "سداد للمورد"
            };

            var balanceBeforePayment = _cashbox.Balance;
            var createPaymentResult = await cashPaymentService.CreateAsync(cashPaymentDto, CancellationToken.None);
            createPaymentResult.IsSuccess.Should().BeTrue("سند الصرف يجب أن يُنشأ بنجاح");

            // Post cash payment
            SetupCashPaymentPost(cashPayment);
            var postPaymentResult = await cashPaymentService.PostAsync(cashPayment.Id, CancellationToken.None);
            postPaymentResult.IsSuccess.Should().BeTrue("سند الصرف يجب أن يُرحّل بنجاح");

            // Verify: cashbox balance decreased by 300
            _cashbox.Balance.Should().Be(balanceBeforePayment - PaymentAmount,
                $"بعد صرف {PaymentAmount}، رصيد الخزنة يجب أن ينقص");

            // ═══════════════════════════════════════════════════
            //               FINAL STATE VERIFICATION
            // ═══════════════════════════════════════════════════

            // Final stock: started 0 + 20 (purchase) - 8 (sale) + 3 (sales return) - 3 (purchase return) = 12
            var expectedFinalStock = PurchaseQty - SaleQty + ReturnQty - ReturnQty;
            _warehouseProduct.Quantity.Should().Be(expectedFinalStock,
                $"المخزون النهائي: 0 + {PurchaseQty} - {SaleQty} + {ReturnQty} - {ReturnQty} = {expectedFinalStock}");

            // Final cashbox: started 10,000 + 500 (receipt) - 300 (payment) = 10,200
            var expectedFinalBalance = 10_000m + ReceiptAmount - PaymentAmount;
            _cashbox.Balance.Should().Be(expectedFinalBalance,
                $"رصيد الخزنة النهائي: 10,000 + {ReceiptAmount} - {PaymentAmount} = {expectedFinalBalance}");
        }

        // ═══════════════════════════════════════════════════════
        //            INDIVIDUAL STEP VERIFICATION TESTS
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task PurchaseInvoice_Post_IncreasesStock()
        {
            var service = CreatePurchaseInvoiceService();
            var invoice = SetupPurchaseInvoiceCreate(10, 50);
            var dto = new CreatePurchaseInvoiceDto
            {
                InvoiceDate = new DateTime(2026, 2, 15),
                SupplierId = SupplierId,
                WarehouseId = WarehouseId,
                CounterpartyType = CounterpartyType.Supplier,
                Lines = new List<CreatePurchaseInvoiceLineDto>
                {
                    new() { ProductId = ProductId, UnitId = UnitId, Quantity = 10, UnitPrice = 50 }
                }
            };

            var create = await service.CreateAsync(dto, CancellationToken.None);
            create.IsSuccess.Should().BeTrue();

            SetupPurchaseInvoicePost(invoice);
            var post = await service.PostAsync(invoice.Id, CancellationToken.None);
            post.IsSuccess.Should().BeTrue();

            _warehouseProduct.Quantity.Should().Be(10, "شراء 10 وحدات يجب أن يزيد المخزون إلى 10");
        }

        [Fact]
        public async Task SalesInvoice_Post_DecreasesStock()
        {
            // Setup initial stock = 20
            _warehouseProduct.IncreaseStock(20);

            var service = CreateSalesInvoiceService();
            var invoice = SetupSalesInvoiceCreate(5, 100);
            var dto = new CreateSalesInvoiceDto
            {
                InvoiceDate = new DateTime(2026, 2, 16),
                CustomerId = CustomerId,
                WarehouseId = WarehouseId,
                CounterpartyType = CounterpartyType.Customer,
                Lines = new List<CreateSalesInvoiceLineDto>
                {
                    new() { ProductId = ProductId, UnitId = UnitId, Quantity = 5, UnitPrice = 100 }
                }
            };

            var create = await service.CreateAsync(dto, CancellationToken.None);
            create.IsSuccess.Should().BeTrue();

            SetupSalesInvoicePost(invoice);
            var post = await service.PostAsync(invoice.Id, CancellationToken.None);
            post.IsSuccess.Should().BeTrue();

            _warehouseProduct.Quantity.Should().Be(15, "بيع 5 من 20 يجب أن ينقص المخزون إلى 15");
        }

        [Fact]
        public async Task CashReceipt_Post_IncreasesCashboxBalance()
        {
            var initialBalance = _cashbox.Balance;
            var service = CreateCashReceiptService();
            var receipt = SetupCashReceiptCreate(1000m);
            var dto = new CreateCashReceiptDto
            {
                ReceiptDate = new DateTime(2026, 2, 19),
                CashboxId = 1,
                AccountId = _arAccount.Id,
                Amount = 1000m,
                Description = "تحصيل"
            };

            var create = await service.CreateAsync(dto, CancellationToken.None);
            create.IsSuccess.Should().BeTrue();

            SetupCashReceiptPost(receipt);
            var post = await service.PostAsync(receipt.Id, CancellationToken.None);
            post.IsSuccess.Should().BeTrue();

            _cashbox.Balance.Should().Be(initialBalance + 1000m, "قبض 1000 يجب أن يزيد الرصيد");
        }

        [Fact]
        public async Task CashPayment_Post_DecreasesCashboxBalance()
        {
            var initialBalance = _cashbox.Balance;
            var service = CreateCashPaymentService();
            var payment = SetupCashPaymentCreate(500m);
            var dto = new CreateCashPaymentDto
            {
                PaymentDate = new DateTime(2026, 2, 20),
                CashboxId = 1,
                AccountId = _apAccount.Id,
                Amount = 500m,
                Description = "سداد"
            };

            var create = await service.CreateAsync(dto, CancellationToken.None);
            create.IsSuccess.Should().BeTrue();

            SetupCashPaymentPost(payment);
            var post = await service.PostAsync(payment.Id, CancellationToken.None);
            post.IsSuccess.Should().BeTrue();

            _cashbox.Balance.Should().Be(initialBalance - 500m, "صرف 500 يجب أن ينقص الرصيد");
        }

        // ═══════════════════════════════════════════════════════
        //                  COMMON MOCK SETUP
        // ═══════════════════════════════════════════════════════

        private void SetupCommonMocks()
        {
            // DateTime
            var now = new DateTime(2026, 2, 15, 10, 0, 0, DateTimeKind.Utc);
            _dateTimeMock.Setup(d => d.UtcNow).Returns(now);
            _dateTimeMock.Setup(d => d.Today).Returns(now.Date);

            // Current User — authenticated admin
            _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);
            _currentUserMock.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
            _currentUserMock.Setup(x => x.Username).Returns("admin");
            _currentUserMock.Setup(x => x.UserId).Returns(1);

            // UnitOfWork — execute delegates directly
            _unitOfWorkMock
                .Setup(u => u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<IsolationLevel>(),
                    It.IsAny<CancellationToken>()))
                .Returns<Func<Task>, IsolationLevel, CancellationToken>((op, _, __) => op());

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // All validators pass by default
            _purchaseInvoiceCreateValidator.Setup(v => v.ValidateAsync(It.IsAny<CreatePurchaseInvoiceDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
            _purchaseInvoiceUpdateValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdatePurchaseInvoiceDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
            _salesInvoiceCreateValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateSalesInvoiceDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
            _salesInvoiceUpdateValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateSalesInvoiceDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
            _salesReturnCreateValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateSalesReturnDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
            _salesReturnUpdateValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateSalesReturnDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
            _purchaseReturnCreateValidator.Setup(v => v.ValidateAsync(It.IsAny<CreatePurchaseReturnDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
            _purchaseReturnUpdateValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdatePurchaseReturnDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
            _cashReceiptCreateValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateCashReceiptDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
            _cashReceiptUpdateValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateCashReceiptDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
            _cashPaymentCreateValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateCashPaymentDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
            _cashPaymentUpdateValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateCashPaymentDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());

            // Default: customer & supplier exist (FK validation)
            _customerRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken _) =>
                {
                    var c = new Customer(new Customer.CustomerDraft { Code = "C001", NameAr = "عميل اختبار" });
                    typeof(Customer).GetProperty("Id").SetValue(c, id);
                    return c;
                });
            _supplierRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken _) =>
                {
                    var s = new Supplier(new SupplierDraft { Code = "S001", NameAr = "مورد اختبار" });
                    typeof(Supplier).GetProperty("Id").SetValue(s, id);
                    return s;
                });

            // Feature service — all features enabled
            _featureServiceMock.Setup(f => f.IsEnabledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<bool>.Success(true));

            // Audit logger — no-op
            _auditLoggerMock.Setup(a => a.LogAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Product setup
            _productRepoMock
                .Setup(r => r.GetByIdWithUnitsAsync(ProductId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_product);

            // Warehouse Product — shared instance tracked across steps
            _whProductRepoMock
                .Setup(r => r.GetAsync(WarehouseId, ProductId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_warehouseProduct);
            _whProductRepoMock
                .Setup(r => r.GetOrCreateAsync(WarehouseId, ProductId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_warehouseProduct);
            _whProductRepoMock
                .Setup(r => r.Update(It.IsAny<WarehouseProduct>()));

            // Inventory Movement — accept all additions
            _movementRepoMock
                .Setup(r => r.AddAsync(It.IsAny<InventoryMovement>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Default: no previous returns linked to original invoices
            _salesReturnRepoMock
                .Setup(r => r.GetByOriginalInvoiceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SalesReturn>());
            _purchaseReturnRepoMock
                .Setup(r => r.GetByOriginalInvoiceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PurchaseReturn>());

            // Fiscal Year — active year with open periods
            _fiscalYearRepoMock
                .Setup(r => r.GetActiveYearAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_fiscalYear);
            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_fiscalYear);

            // Journal Number Generator
            _journalNumberGenMock
                .Setup(g => g.NextNumberAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => $"JV-2026-{_nextJournalId:D4}");

            // Journal Entry — auto-assign IDs
            _journalRepoMock
                .Setup(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
                .Callback<JournalEntry, CancellationToken>((je, _) =>
                    SetEntityId(je, _nextJournalId++))
                .Returns(Task.CompletedTask);

            // GL Accounts — by code
            SetupGLAccounts();

            // GL Accounts — by ID (for cash receipt/payment which resolve by ID)
            _accountRepoMock.Setup(r => r.GetByIdAsync(_arAccount.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_arAccount);
            _accountRepoMock.Setup(r => r.GetByIdAsync(_apAccount.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_apAccount);
            _accountRepoMock.Setup(r => r.GetByIdAsync(_cashAccount.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_cashAccount);
            _accountRepoMock.Setup(r => r.GetByIdAsync(_inventoryAccount.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_inventoryAccount);
            _accountRepoMock.Setup(r => r.GetByIdAsync(_vatInputAccount.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_vatInputAccount);
            _accountRepoMock.Setup(r => r.GetByIdAsync(_vatOutputAccount.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_vatOutputAccount);
            _accountRepoMock.Setup(r => r.GetByIdAsync(_salesAccount.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_salesAccount);
            _accountRepoMock.Setup(r => r.GetByIdAsync(_cogsAccount.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_cogsAccount);

            // Cashbox
            _cashboxRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(_cashbox);
            _cashboxRepoMock.Setup(r => r.Update(It.IsAny<Cashbox>()));

            // SmartEntry — no overdue invoices, 0 outstanding
            _smartEntryMock.Setup(s => s.HasOverduePostedSalesInvoicesAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _smartEntryMock.Setup(s => s.GetCustomerOutstandingSalesBalanceAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0m);

            // SystemSetting — no production mode, no negative cash allowed
            _systemSettingRepoMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SystemSetting)null);
        }

        private void SetupGLAccounts()
        {
            _accountRepoMock.Setup(r => r.GetByCodeAsync("1121", It.IsAny<CancellationToken>())).ReturnsAsync(_arAccount);
            _accountRepoMock.Setup(r => r.GetByCodeAsync("1131", It.IsAny<CancellationToken>())).ReturnsAsync(_inventoryAccount);
            _accountRepoMock.Setup(r => r.GetByCodeAsync("1141", It.IsAny<CancellationToken>())).ReturnsAsync(_vatInputAccount);
            _accountRepoMock.Setup(r => r.GetByCodeAsync("2111", It.IsAny<CancellationToken>())).ReturnsAsync(_apAccount);
            _accountRepoMock.Setup(r => r.GetByCodeAsync("2121", It.IsAny<CancellationToken>())).ReturnsAsync(_vatOutputAccount);
            _accountRepoMock.Setup(r => r.GetByCodeAsync("4111", It.IsAny<CancellationToken>())).ReturnsAsync(_salesAccount);
            _accountRepoMock.Setup(r => r.GetByCodeAsync("5111", It.IsAny<CancellationToken>())).ReturnsAsync(_cogsAccount);
            _accountRepoMock.Setup(r => r.GetByCodeAsync("1111", It.IsAny<CancellationToken>())).ReturnsAsync(_cashAccount);
        }

        // ═══════════════════════════════════════════════════════
        //              SERVICE FACTORY METHODS
        // ═══════════════════════════════════════════════════════

        private PurchaseInvoiceService CreatePurchaseInvoiceService()
        {
            return new PurchaseInvoiceService(
                new PurchaseInvoiceRepositories(
                    _purchaseInvoiceRepoMock.Object,
                    _productRepoMock.Object,
                    _whProductRepoMock.Object,
                    _movementRepoMock.Object,
                    _journalRepoMock.Object,
                    _accountRepoMock.Object,
                    _supplierRepoMock.Object),
                new PurchaseInvoiceServices(
                    _fiscalYearRepoMock.Object,
                    _journalNumberGenMock.Object,
                    _unitOfWorkMock.Object,
                    _currentUserMock.Object,
                    _dateTimeMock.Object,
                    _systemSettingRepoMock.Object),
                new PurchaseInvoiceValidators(
                    _purchaseInvoiceCreateValidator.Object,
                    _purchaseInvoiceUpdateValidator.Object),
                new JournalEntryFactory(_journalRepoMock.Object, _journalNumberGenMock.Object),
                new FiscalPeriodValidator(
                    _fiscalYearRepoMock.Object,
                    _systemSettingRepoMock.Object,
                    _dateTimeMock.Object,
                    _currentUserMock.Object),
                new StockManager(_whProductRepoMock.Object, _movementRepoMock.Object));
        }

        private SalesInvoiceService CreateSalesInvoiceService()
        {
            return new SalesInvoiceService(
                new SalesInvoiceRepositories(
                    _salesInvoiceRepoMock.Object,
                    _productRepoMock.Object,
                    _whProductRepoMock.Object,
                    _movementRepoMock.Object,
                    _journalRepoMock.Object,
                    _accountRepoMock.Object,
                    _customerRepoMock.Object),
                new SalesInvoiceServices(
                    _fiscalYearRepoMock.Object,
                    _journalNumberGenMock.Object,
                    _unitOfWorkMock.Object,
                    _currentUserMock.Object,
                    _dateTimeMock.Object,
                    _smartEntryMock.Object,
                    _systemSettingRepoMock.Object,
                    _featureServiceMock.Object,
                    _auditLoggerMock.Object),
                new SalesInvoiceValidators(
                    _salesInvoiceCreateValidator.Object,
                    _salesInvoiceUpdateValidator.Object),
                new JournalEntryFactory(_journalRepoMock.Object, _journalNumberGenMock.Object),
                new FiscalPeriodValidator(
                    _fiscalYearRepoMock.Object,
                    _systemSettingRepoMock.Object,
                    _dateTimeMock.Object,
                    _currentUserMock.Object),
                new StockManager(_whProductRepoMock.Object, _movementRepoMock.Object));
        }

        private SalesReturnService CreateSalesReturnService()
        {
            return new SalesReturnService(
                new SalesReturnRepositories(
                    _salesReturnRepoMock.Object,
                    _productRepoMock.Object,
                    _whProductRepoMock.Object,
                    _movementRepoMock.Object,
                    _journalRepoMock.Object,
                    _accountRepoMock.Object,
                    _salesInvoiceRepoMock.Object),
                new SalesReturnServices(
                    _fiscalYearRepoMock.Object,
                    _journalNumberGenMock.Object,
                    _unitOfWorkMock.Object,
                    _currentUserMock.Object,
                    _dateTimeMock.Object,
                    _systemSettingRepoMock.Object),
                new SalesReturnValidators(
                    _salesReturnCreateValidator.Object,
                    _salesReturnUpdateValidator.Object),
                new JournalEntryFactory(_journalRepoMock.Object, _journalNumberGenMock.Object),
                new FiscalPeriodValidator(
                    _fiscalYearRepoMock.Object,
                    _systemSettingRepoMock.Object,
                    _dateTimeMock.Object,
                    _currentUserMock.Object),
                new StockManager(_whProductRepoMock.Object, _movementRepoMock.Object));
        }

        private PurchaseReturnService CreatePurchaseReturnService()
        {
            return new PurchaseReturnService(
                new PurchaseReturnRepositories(
                    _purchaseReturnRepoMock.Object,
                    _purchaseInvoiceRepoMock.Object,
                    _productRepoMock.Object,
                    _whProductRepoMock.Object,
                    _movementRepoMock.Object,
                    _journalRepoMock.Object,
                    _accountRepoMock.Object),
                new PurchaseReturnServices(
                    _fiscalYearRepoMock.Object,
                    _journalNumberGenMock.Object,
                    _unitOfWorkMock.Object,
                    _currentUserMock.Object,
                    _dateTimeMock.Object,
                    _systemSettingRepoMock.Object),
                new PurchaseReturnValidators(
                    _purchaseReturnCreateValidator.Object,
                    _purchaseReturnUpdateValidator.Object),
                new JournalEntryFactory(_journalRepoMock.Object, _journalNumberGenMock.Object),
                new FiscalPeriodValidator(
                    _fiscalYearRepoMock.Object,
                    _systemSettingRepoMock.Object,
                    _dateTimeMock.Object,
                    _currentUserMock.Object),
                new StockManager(_whProductRepoMock.Object, _movementRepoMock.Object));
        }

        private CashReceiptService CreateCashReceiptService()
        {
            return new CashReceiptService(
                new CashReceiptRepositories(
                    _cashReceiptRepoMock.Object,
                    _cashboxRepoMock.Object,
                    _journalRepoMock.Object,
                    _accountRepoMock.Object,
                    _salesInvoiceRepoMock.Object),
                new CashReceiptServices(
                    _fiscalYearRepoMock.Object,
                    _journalNumberGenMock.Object,
                    _unitOfWorkMock.Object,
                    _currentUserMock.Object,
                    _dateTimeMock.Object,
                    _systemSettingRepoMock.Object),
                new CashReceiptValidators(
                    _cashReceiptCreateValidator.Object,
                    _cashReceiptUpdateValidator.Object),
                new JournalEntryFactory(_journalRepoMock.Object, _journalNumberGenMock.Object),
                new FiscalPeriodValidator(
                    _fiscalYearRepoMock.Object,
                    _systemSettingRepoMock.Object,
                    _dateTimeMock.Object,
                    _currentUserMock.Object),
                Mock.Of<ILogger<CashReceiptService>>(),
                _featureServiceMock.Object);
        }

        private CashPaymentService CreateCashPaymentService()
        {
            return new CashPaymentService(
                new CashPaymentRepositories(
                    _cashPaymentRepoMock.Object,
                    _cashboxRepoMock.Object,
                    _journalRepoMock.Object,
                    _accountRepoMock.Object,
                    _purchaseInvoiceRepoMock.Object),
                new CashPaymentServices(
                    _fiscalYearRepoMock.Object,
                    _journalNumberGenMock.Object,
                    _unitOfWorkMock.Object,
                    _currentUserMock.Object,
                    _dateTimeMock.Object,
                    _systemSettingRepoMock.Object,
                    _featureServiceMock.Object,
                    _auditLoggerMock.Object),
                new CashPaymentValidators(
                    _cashPaymentCreateValidator.Object,
                    _cashPaymentUpdateValidator.Object),
                new JournalEntryFactory(_journalRepoMock.Object, _journalNumberGenMock.Object),
                new FiscalPeriodValidator(
                    _fiscalYearRepoMock.Object,
                    _systemSettingRepoMock.Object,
                    _dateTimeMock.Object,
                    _currentUserMock.Object),
                Mock.Of<ILogger<CashPaymentService>>());
        }

        // ═══════════════════════════════════════════════════════
        //            STEP SETUP HELPERS
        // ═══════════════════════════════════════════════════════

        // ── Purchase Invoice ────────────────────────────────

        private PurchaseInvoice SetupPurchaseInvoiceCreate(decimal qty, decimal price)
        {
            var invoiceNumber = "PI-202602-0001";
            var invoice = new PurchaseInvoice(invoiceNumber, new DateTime(2026, 2, 15), SupplierId, WarehouseId, null);
            SetEntityId(invoice, 1);
            invoice.AddLine(ProductId, UnitId, qty, price, 1m, 0m, VatRate);

            _purchaseInvoiceRepoMock
                .Setup(r => r.GetNextNumberAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoiceNumber);
            _purchaseInvoiceRepoMock
                .Setup(r => r.AddAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>()))
                .Callback<PurchaseInvoice, CancellationToken>((inv, _) => SetEntityId(inv, 1))
                .Returns(Task.CompletedTask);
            _purchaseInvoiceRepoMock
                .Setup(r => r.GetWithLinesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);

            return invoice;
        }

        private void SetupPurchaseInvoicePost(PurchaseInvoice invoice)
        {
            _purchaseInvoiceRepoMock
                .Setup(r => r.GetWithLinesTrackedAsync(invoice.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);
        }

        // ── Sales Invoice ───────────────────────────────────

        private SalesInvoice SetupSalesInvoiceCreate(decimal qty, decimal price)
        {
            var invoiceNumber = "SI-202602-0001";
            var invoice = new SalesInvoice(invoiceNumber, new DateTime(2026, 2, 16), CustomerId, WarehouseId, null);
            SetEntityId(invoice, 2);
            invoice.AddLine(ProductId, UnitId, qty, price, 1m, 0m, VatRate);

            _salesInvoiceRepoMock
                .Setup(r => r.GetNextNumberAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoiceNumber);
            _salesInvoiceRepoMock
                .Setup(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()))
                .Callback<SalesInvoice, CancellationToken>((inv, _) => SetEntityId(inv, 2))
                .Returns(Task.CompletedTask);
            _salesInvoiceRepoMock
                .Setup(r => r.GetWithLinesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);

            return invoice;
        }

        private void SetupSalesInvoicePost(SalesInvoice invoice)
        {
            _salesInvoiceRepoMock
                .Setup(r => r.GetWithLinesTrackedAsync(invoice.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);
        }

        // ── Sales Return ────────────────────────────────────

        private SalesReturn SetupSalesReturnCreate(decimal qty, decimal price)
        {
            var returnNumber = "SR-202602-0001";
            var returnDoc = new SalesReturn(returnNumber, new DateTime(2026, 2, 17), CustomerId, WarehouseId, null, null);
            SetEntityId(returnDoc, 3);
            returnDoc.AddLine(ProductId, UnitId, qty, price, 1m, 0m, VatRate);

            _salesReturnRepoMock
                .Setup(r => r.GetNextNumberAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(returnNumber);
            _salesReturnRepoMock
                .Setup(r => r.AddAsync(It.IsAny<SalesReturn>(), It.IsAny<CancellationToken>()))
                .Callback<SalesReturn, CancellationToken>((ret, _) => SetEntityId(ret, 3))
                .Returns(Task.CompletedTask);
            _salesReturnRepoMock
                .Setup(r => r.GetWithLinesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(returnDoc);

            return returnDoc;
        }

        private void SetupSalesReturnPost(SalesReturn returnDoc)
        {
            _salesReturnRepoMock
                .Setup(r => r.GetWithLinesTrackedAsync(returnDoc.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(returnDoc);
        }

        // ── Purchase Return ─────────────────────────────────

        private PurchaseReturn SetupPurchaseReturnCreate(decimal qty, decimal price)
        {
            var returnNumber = "PR-202602-0001";
            var returnDoc = new PurchaseReturn(returnNumber, new DateTime(2026, 2, 18), SupplierId, WarehouseId, null, null);
            SetEntityId(returnDoc, 4);
            returnDoc.AddLine(ProductId, UnitId, qty, price, 1m, 0m, VatRate);

            _purchaseReturnRepoMock
                .Setup(r => r.GetNextNumberAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(returnNumber);
            _purchaseReturnRepoMock
                .Setup(r => r.AddAsync(It.IsAny<PurchaseReturn>(), It.IsAny<CancellationToken>()))
                .Callback<PurchaseReturn, CancellationToken>((ret, _) => SetEntityId(ret, 4))
                .Returns(Task.CompletedTask);
            _purchaseReturnRepoMock
                .Setup(r => r.GetWithLinesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(returnDoc);

            return returnDoc;
        }

        private void SetupPurchaseReturnPost(PurchaseReturn returnDoc)
        {
            _purchaseReturnRepoMock
                .Setup(r => r.GetWithLinesTrackedAsync(returnDoc.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(returnDoc);
        }

        // ── Cash Receipt ────────────────────────────────────

        private CashReceipt SetupCashReceiptCreate(decimal amount = ReceiptAmount)
        {
            var receiptNumber = "CR-202602-0001";
            var receipt = new CashReceipt(new CashReceiptDraft
            {
                ReceiptNumber = receiptNumber,
                ReceiptDate = new DateTime(2026, 2, 19),
                CashboxId = 1,
                AccountId = _arAccount.Id,
                Amount = amount,
                Description = "تحصيل من العميل",
                CustomerId = CustomerId
            });
            SetEntityId(receipt, 5);

            _cashReceiptRepoMock
                .Setup(r => r.GetNextNumberAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(receiptNumber);
            _cashReceiptRepoMock
                .Setup(r => r.AddAsync(It.IsAny<CashReceipt>(), It.IsAny<CancellationToken>()))
                .Callback<CashReceipt, CancellationToken>((rec, _) => SetEntityId(rec, 5))
                .Returns(Task.CompletedTask);
            _cashReceiptRepoMock
                .Setup(r => r.GetWithDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(receipt);

            return receipt;
        }

        private void SetupCashReceiptPost(CashReceipt receipt)
        {
            _cashReceiptRepoMock
                .Setup(r => r.GetWithDetailsTrackedAsync(receipt.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(receipt);
        }

        // ── Cash Payment ────────────────────────────────────

        private CashPayment SetupCashPaymentCreate(decimal amount = PaymentAmount)
        {
            var paymentNumber = "CP-202602-0001";
            var payment = new CashPayment(new CashPaymentDraft
            {
                PaymentNumber = paymentNumber,
                PaymentDate = new DateTime(2026, 2, 20),
                CashboxId = 1,
                AccountId = _apAccount.Id,
                Amount = amount,
                Description = "سداد للمورد",
                SupplierId = SupplierId
            });
            SetEntityId(payment, 6);

            _cashPaymentRepoMock
                .Setup(r => r.GetNextNumberAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(paymentNumber);
            _cashPaymentRepoMock
                .Setup(r => r.AddAsync(It.IsAny<CashPayment>(), It.IsAny<CancellationToken>()))
                .Callback<CashPayment, CancellationToken>((pay, _) => SetEntityId(pay, 6))
                .Returns(Task.CompletedTask);
            _cashPaymentRepoMock
                .Setup(r => r.GetWithDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(payment);

            return payment;
        }

        private void SetupCashPaymentPost(CashPayment payment)
        {
            _cashPaymentRepoMock
                .Setup(r => r.GetWithDetailsTrackedAsync(payment.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(payment);
        }

        // ═══════════════════════════════════════════════════════
        //                  ENTITY HELPERS
        // ═══════════════════════════════════════════════════════

        private static Product CreateProduct(int id, decimal wac, decimal vatRate)
        {
            var product = new Product("P001", "صنف اختبار", "Test Product", 1, 1, wac, 100m, 0m, 0m, vatRate);
            SetEntityId(product, id);
            product.AddUnit(new ProductUnit(0, 1, 1m, 100m, wac));
            return product;
        }

        private static Account CreateAccount(string code, string nameAr, AccountType type, int id)
        {
            var account = new Account(code, nameAr, nameAr, type, 1, 4, true, "SAR");
            SetEntityId(account, id);
            return account;
        }

        private static void SetEntityId<T>(T entity, int id) where T : class
        {
            typeof(T).GetProperty("Id")?.SetValue(entity, id);
        }

        private void ResetJournalVerification()
        {
            _journalRepoMock.Invocations.Clear();
        }
    }
}
