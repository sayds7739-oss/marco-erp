using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Application.Services.Common;
using MarcoERP.Application.Services.Sales;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Domain.Interfaces.Settings;

namespace MarcoERP.Application.Tests
{
    /// <summary>
    /// Comprehensive POS module unit tests:
    /// Session lifecycle, sale flow, payment validation, stock checks, COGS accuracy.
    /// </summary>
    public class PosServiceTests
    {
        private readonly Mock<IPosSessionRepository> _sessionRepoMock = new();
        private readonly Mock<IPosPaymentRepository> _paymentRepoMock = new();
        private readonly Mock<ISalesInvoiceRepository> _invoiceRepoMock = new();
        private readonly Mock<IProductRepository> _productRepoMock = new();
        private readonly Mock<IWarehouseProductRepository> _whProductRepoMock = new();
        private readonly Mock<IInventoryMovementRepository> _movementRepoMock = new();
        private readonly Mock<IJournalEntryRepository> _journalRepoMock = new();
        private readonly Mock<IAccountRepository> _accountRepoMock = new();
        private readonly Mock<IFiscalYearRepository> _fiscalYearRepoMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<ICurrentUserService> _currentUserMock = new();
        private readonly Mock<IDateTimeProvider> _dateTimeMock = new();
        private readonly Mock<IValidator<OpenPosSessionDto>> _openValMock = new();
        private readonly Mock<IValidator<ClosePosSessionDto>> _closeValMock = new();
        private readonly Mock<IValidator<CompletePoseSaleDto>> _saleValMock = new();
        private readonly Mock<IJournalNumberGenerator> _journalNumberGenMock = new();
        private readonly Mock<ISystemSettingRepository> _systemSettingRepoMock = new();
        private readonly Mock<IFeatureService> _featureServiceMock = new();
        private readonly Mock<IAuditLogger> _auditLoggerMock = new();
        private readonly Mock<IReceiptPrinterService> _receiptPrinterMock = new();

        private PosService CreateService()
        {
            var repos = new PosRepositories(
                new PosSalesRepositories(
                    _sessionRepoMock.Object,
                    _paymentRepoMock.Object,
                    _invoiceRepoMock.Object),
                new PosInventoryRepositories(
                    _productRepoMock.Object,
                    _whProductRepoMock.Object,
                    _movementRepoMock.Object),
                new PosAccountingRepositories(
                    _journalRepoMock.Object,
                    _accountRepoMock.Object));

            var services = new PosServices(
                _fiscalYearRepoMock.Object,
                _journalNumberGenMock.Object,
                _unitOfWorkMock.Object,
                _currentUserMock.Object,
                _dateTimeMock.Object,
                _systemSettingRepoMock.Object,
                _featureServiceMock.Object,
                _auditLoggerMock.Object,
                _receiptPrinterMock.Object);

            var validators = new PosValidators(
                _openValMock.Object,
                _closeValMock.Object,
                _saleValMock.Object);

            return new PosService(repos, services, validators,
                new JournalEntryFactory(_journalRepoMock.Object, _journalNumberGenMock.Object),
                new FiscalPeriodValidator(_fiscalYearRepoMock.Object, _systemSettingRepoMock.Object, _dateTimeMock.Object, _currentUserMock.Object),
                new StockManager(_whProductRepoMock.Object, _movementRepoMock.Object));
        }

        // ── Helpers ─────────────────────────────────────────────

        private void SetupCurrentUser(int userId = 1)
        {
            _currentUserMock.Setup(x => x.UserId).Returns(userId);
            _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);
            _currentUserMock.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
            _currentUserMock.Setup(x => x.Username).Returns("admin");
        }

        private void SetupValidators()
        {
            _openValMock.Setup(v => v.ValidateAsync(It.IsAny<OpenPosSessionDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
            _closeValMock.Setup(v => v.ValidateAsync(It.IsAny<ClosePosSessionDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
            _saleValMock.Setup(v => v.ValidateAsync(It.IsAny<CompletePoseSaleDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
        }



        // ═══════════════════════════════════════════════════════════
        //  SESSION LIFECYCLE TESTS
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public async Task OpenSession_ValidDto_ReturnsSessionDto()
        {
            // Arrange
            SetupCurrentUser();
            SetupValidators();

            _sessionRepoMock.Setup(x => x.HasOpenSessionAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _sessionRepoMock.Setup(x => x.GetNextSessionNumberAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("POS-20250615-0001");
            _sessionRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PosSession("POS-20250615-0001", 1, 1, 1, 500m, DateTime.UtcNow));

            var dto = new OpenPosSessionDto { CashboxId = 1, WarehouseId = 1, OpeningBalance = 500m };
            var service = CreateService();

            // Act
            var result = await service.OpenSessionAsync(dto, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Data.SessionNumber.Should().Be("POS-20250615-0001");
            _sessionRepoMock.Verify(x => x.AddAsync(It.IsAny<PosSession>(), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task OpenSession_UserAlreadyHasOpen_ReturnsFailure()
        {
            SetupCurrentUser();
            SetupValidators();

            _sessionRepoMock.Setup(x => x.HasOpenSessionAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var dto = new OpenPosSessionDto { CashboxId = 1, WarehouseId = 1 };
            var result = await CreateService().OpenSessionAsync(dto, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("جلسة مفتوحة");
        }

        [Fact]
        public async Task OpenSession_NoCurrentUser_ReturnsFailure()
        {
            _currentUserMock.Setup(x => x.UserId).Returns(0);
            _currentUserMock.Setup(x => x.IsAuthenticated).Returns(false);
            SetupValidators();

            var dto = new OpenPosSessionDto { CashboxId = 1, WarehouseId = 1 };
            var result = await CreateService().OpenSessionAsync(dto, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("لم يتم تحديد المستخدم الحالي");
        }

        [Fact]
        public async Task OpenSession_ValidationFails_ReturnsFailure()
        {
            SetupCurrentUser();
            var failures = new List<ValidationFailure> { new("CashboxId", "مطلوب") };
            _openValMock.Setup(v => v.ValidateAsync(It.IsAny<OpenPosSessionDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult(failures));

            var result = await CreateService().OpenSessionAsync(new OpenPosSessionDto(), CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("مطلوب");
        }

        [Fact]
        public async Task CloseSession_ValidDto_ReturnsClosedSession()
        {
            SetupCurrentUser();
            SetupValidators();

            var session = new PosSession("POS-20250615-0001", 1, 1, 1, 500m, DateTime.UtcNow);
            _sessionRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);

            var dto = new ClosePosSessionDto { SessionId = 1, ActualClosingBalance = 500m, Notes = "نهاية اليوم" };
            var result = await CreateService().CloseSessionAsync(dto, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CloseSession_SessionNotFound_ReturnsFailure()
        {
            SetupCurrentUser();
            SetupValidators();

            _sessionRepoMock.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((PosSession)null);

            var dto = new ClosePosSessionDto { SessionId = 999, ActualClosingBalance = 0 };
            var result = await CreateService().CloseSessionAsync(dto, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("غير موجودة");
        }

        [Fact]
        public async Task GetCurrentSession_NoOpenSession_ReturnsFailure()
        {
            SetupCurrentUser();

            _sessionRepoMock.Setup(x => x.GetOpenSessionByUserAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((PosSession)null);

            var result = await CreateService().GetCurrentSessionAsync(CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("لا توجد جلسة");
        }

        // ═══════════════════════════════════════════════════════════
        //  PRODUCT LOOKUP TESTS
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public async Task LoadProductCache_ReturnsOnlyActiveProducts()
        {
            var activeProduct = CreateTestProduct(1, "PRD001", "منتج نشط", ProductStatus.Active);
            var inactiveProduct = CreateTestProduct(2, "PRD002", "منتج غير نشط", ProductStatus.Inactive);

            _productRepoMock.Setup(x => x.GetAllWithUnitsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Product> { activeProduct, inactiveProduct });

            var result = await CreateService().LoadProductCacheAsync(CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Data.Should().HaveCount(1);
            result.Data[0].Code.Should().Be("PRD001");
        }

        [Fact]
        public async Task FindByBarcode_ExactMatch_ReturnsProduct()
        {
            var product = CreateTestProduct(1, "PRD001", "صنف باركود", ProductStatus.Active, barcode: "1234567890");
            _productRepoMock.Setup(x => x.GetByBarcodeAsync("1234567890", It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            var result = await CreateService().FindByBarcodeAsync("1234567890", CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Data.Barcode.Should().Be("1234567890");
        }

        [Fact]
        public async Task FindByBarcode_NoMatch_ReturnsFailure()
        {
            _productRepoMock.Setup(x => x.GetByBarcodeAsync("NOTEXIST", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product)null);

            var result = await CreateService().FindByBarcodeAsync("NOTEXIST", CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
        }

        [Fact]
        public async Task GetAvailableStock_ReturnsCorrectQuantity()
        {
            var whp = CreateWarehouseProduct(1, 1, 50m);
            _whProductRepoMock.Setup(x => x.GetAsync(1, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(whp);

            var result = await CreateService().GetAvailableStockAsync(1, 1, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be(50m);
        }

        [Fact]
        public async Task GetAvailableStock_NoRecord_ReturnsZero()
        {
            _whProductRepoMock.Setup(x => x.GetAsync(1, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((WarehouseProduct)null);

            var result = await CreateService().GetAvailableStockAsync(1, 1, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be(0);
        }

        // ═══════════════════════════════════════════════════════════
        //  POS CART ITEM DTO CALCULATION TESTS
        //  (fields are now populated via ILineCalculationService, not computed properties)
        // ═══════════════════════════════════════════════════════════

        private static PosCartItemDto CreateAndCalculateCartItem(PosCartItemDto item)
        {
            var svc = new LineCalculationService();
            item.BaseQuantity = svc.ConvertQuantity(item.Quantity, item.ConversionFactor);
            var result = svc.CalculateLine(new LineCalculationRequest
            {
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountPercent = item.DiscountPercent,
                VatRate = item.VatRate,
                ConversionFactor = item.ConversionFactor,
                CostPrice = item.WacPerBaseUnit
            });
            item.SubTotal = result.SubTotal;
            item.DiscountAmount = result.DiscountAmount;
            item.NetTotal = result.NetTotal;
            item.VatAmount = result.VatAmount;
            item.TotalWithVat = result.TotalWithVat;
            item.CostTotal = result.CostTotal;
            item.ProfitAmount = result.NetTotal - result.CostTotal;
            item.ProfitMarginPercent = result.ProfitMarginPercent;
            return item;
        }

        [Fact]
        public void PosCartItemDto_CalculatesCorrectly_NoDiscount()
        {
            var item = CreateAndCalculateCartItem(new PosCartItemDto
            {
                Quantity = 3,
                UnitPrice = 100m,
                ConversionFactor = 1m,
                DiscountPercent = 0,
                VatRate = 15m,
                WacPerBaseUnit = 60m
            });

            item.BaseQuantity.Should().Be(3m);
            item.SubTotal.Should().Be(300m);
            item.DiscountAmount.Should().Be(0);
            item.NetTotal.Should().Be(300m);
            item.VatAmount.Should().Be(45m);
            item.TotalWithVat.Should().Be(345m);
            item.CostTotal.Should().Be(180m);
            item.ProfitAmount.Should().Be(120m);
            item.ProfitMarginPercent.Should().Be(40m);
        }

        [Fact]
        public void PosCartItemDto_CalculatesCorrectly_WithDiscount()
        {
            var item = CreateAndCalculateCartItem(new PosCartItemDto
            {
                Quantity = 2,
                UnitPrice = 200m,
                ConversionFactor = 1m,
                DiscountPercent = 10m,
                VatRate = 15m,
                WacPerBaseUnit = 100m
            });

            item.SubTotal.Should().Be(400m);
            item.DiscountAmount.Should().Be(40m);
            item.NetTotal.Should().Be(360m);
            item.VatAmount.Should().Be(54m);
            item.TotalWithVat.Should().Be(414m);
            item.CostTotal.Should().Be(200m);
            item.ProfitAmount.Should().Be(160m);
        }

        [Fact]
        public void PosCartItemDto_ConversionFactor_AffectsBaseQuantity()
        {
            var item = CreateAndCalculateCartItem(new PosCartItemDto
            {
                Quantity = 5,
                UnitPrice = 50m,
                ConversionFactor = 12m, // e.g., 1 carton = 12 pieces
                DiscountPercent = 0,
                VatRate = 0,
                WacPerBaseUnit = 3m
            });

            item.BaseQuantity.Should().Be(60m); // 5 × 12
            item.CostTotal.Should().Be(180m);   // 60 × 3
            item.ProfitAmount.Should().Be(70m);  // 250 (revenue) - 180 (cost)
        }

        [Fact]
        public void PosCartItemDto_ZeroNetTotal_ReturnsZeroProfitMargin()
        {
            var item = CreateAndCalculateCartItem(new PosCartItemDto
            {
                Quantity = 0, UnitPrice = 100m, ConversionFactor = 1m,
                DiscountPercent = 0, VatRate = 15m, WacPerBaseUnit = 50m
            });

            item.SubTotal.Should().Be(0);
            item.ProfitMarginPercent.Should().Be(0);
        }

        // ═══════════════════════════════════════════════════════════
        //  POS VALIDATOR TESTS
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public async Task OpenSessionValidator_MissingCashboxId_Fails()
        {
            var validator = new Validators.Sales.OpenPosSessionDtoValidator();
            var dto = new OpenPosSessionDto { CashboxId = 0, WarehouseId = 1 };

            var result = await validator.ValidateAsync(dto);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "CashboxId");
        }

        [Fact]
        public async Task OpenSessionValidator_ValidDto_Passes()
        {
            var validator = new Validators.Sales.OpenPosSessionDtoValidator();
            var dto = new OpenPosSessionDto { CashboxId = 1, WarehouseId = 1, OpeningBalance = 0 };

            var result = await validator.ValidateAsync(dto);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task CompleteSaleValidator_NoLines_Fails()
        {
            var validator = new Validators.Sales.CompletePosSaleDtoValidator();
            var dto = new CompletePoseSaleDto
            {
                SessionId = 1,
                Lines = new List<PosSaleLineDto>(),
                Payments = new List<PosPaymentDto> { new() { PaymentMethod = "Cash", Amount = 100m } }
            };

            var result = await validator.ValidateAsync(dto);

            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public async Task CompleteSaleValidator_NoPayments_Fails()
        {
            var validator = new Validators.Sales.CompletePosSaleDtoValidator();
            var dto = new CompletePoseSaleDto
            {
                SessionId = 1,
                Lines = new List<PosSaleLineDto>
                {
                    new() { ProductId = 1, UnitId = 1, Quantity = 1, UnitPrice = 100m }
                },
                Payments = new List<PosPaymentDto>()
            };

            var result = await validator.ValidateAsync(dto);

            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public async Task CompleteSaleValidator_ValidDto_Passes()
        {
            var validator = new Validators.Sales.CompletePosSaleDtoValidator();
            var dto = new CompletePoseSaleDto
            {
                SessionId = 1,
                Lines = new List<PosSaleLineDto>
                {
                    new() { ProductId = 1, UnitId = 1, Quantity = 2, UnitPrice = 100m }
                },
                Payments = new List<PosPaymentDto>
                {
                    new() { PaymentMethod = "Cash", Amount = 200m }
                }
            };

            var result = await validator.ValidateAsync(dto);

            result.IsValid.Should().BeTrue();
        }

        // ═══════════════════════════════════════════════════════════
        //  POS SESSION DOMAIN ENTITY TESTS
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void PosSession_RecordSale_UpdatesTotals()
        {
            var session = new PosSession("POS-001", 1, 1, 1, 500m, DateTime.UtcNow);

            session.RecordSale(230m, 200m, 30m, 0m);

            session.TotalSales.Should().Be(230m);
            session.TotalCashReceived.Should().Be(200m);
            session.TotalCardReceived.Should().Be(30m);
            session.TransactionCount.Should().Be(1);
        }

        [Fact]
        public void PosSession_RecordSale_MultipleSales_AccumulatesCorrectly()
        {
            var session = new PosSession("POS-001", 1, 1, 1, 1000m, DateTime.UtcNow);

            session.RecordSale(100m, 100m, 0m, 0m);
            session.RecordSale(250m, 0m, 250m, 0m);
            session.RecordSale(150m, 0m, 0m, 150m);

            session.TotalSales.Should().Be(500m);
            session.TotalCashReceived.Should().Be(100m);
            session.TotalCardReceived.Should().Be(250m);
            session.TotalOnAccount.Should().Be(150m);
            session.TransactionCount.Should().Be(3);
        }

        [Fact]
        public void PosSession_Close_CalculatesVariance()
        {
            var session = new PosSession("POS-001", 1, 1, 1, 500m, DateTime.UtcNow);
            session.RecordSale(200m, 200m, 0m, 0m);

            session.Close(690m, "إغلاق", DateTime.UtcNow);

            session.IsOpen.Should().BeFalse();
            session.ClosingBalance.Should().Be(690m);
            // Expected = Opening(500) + Cash(200) = 700
            // Variance = Actual(690) - Expected(700) = -10
            session.Variance.Should().Be(-10m);
        }

        [Fact]
        public void PosSession_Close_AlreadyClosed_Throws()
        {
            var session = new PosSession("POS-001", 1, 1, 1, 500m, DateTime.UtcNow);
            session.Close(500m, "إغلاق", DateTime.UtcNow);

            var act = () => session.Close(500m, "محاولة ثانية", DateTime.UtcNow);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void PosSession_ReverseSale_DecrementsTotals()
        {
            var session = new PosSession("POS-001", 1, 1, 1, 500m, DateTime.UtcNow);
            session.RecordSale(300m, 200m, 100m, 0m);
            session.RecordSale(150m, 150m, 0m, 0m);

            session.ReverseSale(300m, 200m, 100m, 0m);

            session.TotalSales.Should().Be(150m);
            session.TotalCashReceived.Should().Be(150m);
            session.TotalCardReceived.Should().Be(0m);
            session.TransactionCount.Should().Be(1);
        }

        // ═══════════════════════════════════════════════════════════
        //  PAYMENT Entity Tests
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void PosPayment_Construction_SetsProperties()
        {
            var payment = new PosPayment(1, 1, PaymentMethod.Cash, 200m, DateTime.UtcNow, null);

            payment.SalesInvoiceId.Should().Be(1);
            payment.PosSessionId.Should().Be(1);
            payment.PaymentMethod.Should().Be(PaymentMethod.Cash);
            payment.Amount.Should().Be(200m);
            payment.PaidAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void PosPayment_CardPayment_HasReferenceNumber()
        {
            var payment = new PosPayment(1, 1, PaymentMethod.Card, 150m, DateTime.UtcNow, "REF-12345");

            payment.PaymentMethod.Should().Be(PaymentMethod.Card);
            payment.ReferenceNumber.Should().Be("REF-12345");
        }

        // ═══════════════════════════════════════════════════════════
        //  ENUM TESTS
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void PaymentMethod_HasExpectedValues()
        {
            ((int)PaymentMethod.Cash).Should().Be(0);
            ((int)PaymentMethod.Card).Should().Be(1);
            ((int)PaymentMethod.OnAccount).Should().Be(2);
        }

        [Fact]
        public void PosSessionStatus_HasExpectedValues()
        {
            ((int)PosSessionStatus.Open).Should().Be(0);
            ((int)PosSessionStatus.Closed).Should().Be(1);
        }

        // ═══════════════════════════════════════════════════════════
        //  Helper Factory Methods
        // ═══════════════════════════════════════════════════════════

        private static Product CreateTestProduct(int id, string code, string name,
            ProductStatus status, string barcode = null)
        {
            // Use reflection or a factory — for testing we create via the domain constructor
            var product = new Product(
                code: code,
                nameAr: name,
                nameEn: name,
                categoryId: 1,
                baseUnitId: 1,
                initialCostPrice: 50m,
                defaultSalePrice: 100m,
                minimumStock: 0,
                reorderLevel: 0,
                vatRate: 15m,
                barcode: barcode);

            // Set Id via reflection for testing
            typeof(Product).BaseType?.BaseType?.GetProperty("Id")
                ?.SetValue(product, id);

            if (status == ProductStatus.Inactive)
                product.Deactivate();
            if (status == ProductStatus.Discontinued)
                product.Discontinue();

            return product;
        }

        private static WarehouseProduct CreateWarehouseProduct(int warehouseId, int productId, decimal qty)
        {
            var whp = new WarehouseProduct(warehouseId, productId);
            if (qty > 0) whp.IncreaseStock(qty);
            return whp;
        }
    }
}
