using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Application.Interfaces.SmartEntry;
using MarcoERP.Application.Services.Sales;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Domain.Interfaces.Settings;

namespace MarcoERP.Application.Tests.Sales
{
    public sealed class SalesInvoiceServiceTests
    {
        private readonly Mock<ISalesInvoiceRepository> _invoiceRepoMock;
        private readonly Mock<IProductRepository> _productRepoMock;
        private readonly Mock<IWarehouseProductRepository> _whProductRepoMock;
        private readonly Mock<IInventoryMovementRepository> _movementRepoMock;
        private readonly Mock<IJournalEntryRepository> _journalRepoMock;
        private readonly Mock<IAccountRepository> _accountRepoMock;
        private readonly Mock<IFiscalYearRepository> _fiscalYearRepoMock;
        private readonly Mock<IJournalNumberGenerator> _journalNumberGenMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ICurrentUserService> _currentUserMock;
        private readonly Mock<IDateTimeProvider> _dateTimeMock;
        private readonly Mock<ICustomerRepository> _customerRepoMock;
        private readonly Mock<ISmartEntryQueryService> _smartEntryQueryServiceMock;
        private readonly Mock<IValidator<CreateSalesInvoiceDto>> _createValidatorMock;
        private readonly Mock<IValidator<UpdateSalesInvoiceDto>> _updateValidatorMock;
        private readonly Mock<ISystemSettingRepository> _systemSettingRepoMock;
        private readonly Mock<IFeatureService> _featureServiceMock;
        private readonly SalesInvoiceService _sut;

        public SalesInvoiceServiceTests()
        {
            _invoiceRepoMock = new Mock<ISalesInvoiceRepository>();
            _productRepoMock = new Mock<IProductRepository>();
            _whProductRepoMock = new Mock<IWarehouseProductRepository>();
            _movementRepoMock = new Mock<IInventoryMovementRepository>();
            _journalRepoMock = new Mock<IJournalEntryRepository>();
            _accountRepoMock = new Mock<IAccountRepository>();
            _customerRepoMock = new Mock<ICustomerRepository>();
            _smartEntryQueryServiceMock = new Mock<ISmartEntryQueryService>();
            _fiscalYearRepoMock = new Mock<IFiscalYearRepository>();
            _journalNumberGenMock = new Mock<IJournalNumberGenerator>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _currentUserMock = new Mock<ICurrentUserService>();
            _dateTimeMock = new Mock<IDateTimeProvider>();
            _createValidatorMock = new Mock<IValidator<CreateSalesInvoiceDto>>();
            _updateValidatorMock = new Mock<IValidator<UpdateSalesInvoiceDto>>();
            _systemSettingRepoMock = new Mock<ISystemSettingRepository>();
            _featureServiceMock = new Mock<IFeatureService>();

            // Default: features enabled
            _featureServiceMock.Setup(f => f.IsEnabledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<bool>.Success(true));

            // Default: authenticated user with sales permissions
            _currentUserMock.Setup(c => c.IsAuthenticated).Returns(true);
            _currentUserMock.Setup(c => c.HasPermission(PermissionKeys.SalesCreate)).Returns(true);
            _currentUserMock.Setup(c => c.HasPermission(PermissionKeys.SalesPost)).Returns(true);
            _currentUserMock.Setup(c => c.Username).Returns("salesuser");

            _dateTimeMock.Setup(d => d.UtcNow).Returns(new DateTime(2026, 2, 9, 12, 0, 0, DateTimeKind.Utc));
            _dateTimeMock.Setup(d => d.Today).Returns(new DateTime(2026, 2, 9, 0, 0, 0, DateTimeKind.Utc));

            // Default: validators pass
            _createValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<CreateSalesInvoiceDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
            _updateValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<UpdateSalesInvoiceDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            // Default: customer exists (FK validation)
            _customerRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken _) =>
                {
                    var c = new Customer(new Customer.CustomerDraft { Code = "C001", NameAr = "عميل اختبار" });
                    typeof(Customer).GetProperty("Id").SetValue(c, id);
                    return c;
                });

            // Default: credit control queries are benign
            _smartEntryQueryServiceMock
                .Setup(s => s.HasOverduePostedSalesInvoicesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _smartEntryQueryServiceMock
                .Setup(s => s.GetCustomerOutstandingSalesBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0m);

            // CRITICAL: ExecuteInTransactionAsync must invoke the delegate
            _unitOfWorkMock.Setup(u => u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(), It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()))
                .Returns<Func<Task>, IsolationLevel, CancellationToken>((op, _, __) => op());

            _sut = new SalesInvoiceService(
                new SalesInvoiceRepositories(
                    _invoiceRepoMock.Object,
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
                    _smartEntryQueryServiceMock.Object,
                    _systemSettingRepoMock.Object,
                    _featureServiceMock.Object),
                new SalesInvoiceValidators(
                    _createValidatorMock.Object,
                    _updateValidatorMock.Object),
                new JournalEntryFactory(_journalRepoMock.Object, _journalNumberGenMock.Object),
                new FiscalPeriodValidator(_fiscalYearRepoMock.Object, _systemSettingRepoMock.Object, _dateTimeMock.Object, _currentUserMock.Object),
                new StockManager(_whProductRepoMock.Object, _movementRepoMock.Object));
        }

        // ── Helpers ─────────────────────────────────────────────

        private static Product CreateProduct(int id = 1, decimal wac = 10m, decimal vatRate = 14m)
        {
            var product = new Product("P001", "صنف اختبار", "Test Product", 1, 1, wac, 20m, 0m, 0m, vatRate);
            typeof(Product).GetProperty("Id").SetValue(product, id);
            // Product constructor does NOT auto-add base unit to ProductUnits — we must add it
            product.AddUnit(new ProductUnit(0, 1, 1m, 20m, wac));
            return product;
        }

        private static SalesInvoice CreateDraftInvoice(int id = 1)
        {
            var invoice = new SalesInvoice("SI-202602-0001", new DateTime(2026, 2, 9, 0, 0, 0, DateTimeKind.Utc), 1, 1, "ملاحظات");
            typeof(SalesInvoice).GetProperty("Id").SetValue(invoice, id);
            return invoice;
        }

        private static SalesInvoice CreateDraftInvoiceWithLine(int id = 1)
        {
            var invoice = CreateDraftInvoice(id);
            var line = invoice.AddLine(productId: 1, unitId: 1, quantity: 5, unitPrice: 20m, conversionFactor: 1m, discountPercent: 0m, vatRate: 14m);
            typeof(SalesInvoiceLine).GetProperty("Id").SetValue(line, 1);
            return invoice;
        }

        private static SalesInvoice CreatePostedInvoice(int id = 1)
        {
            var invoice = CreateDraftInvoiceWithLine(id);
            invoice.Post(journalEntryId: 100, cogsJournalEntryId: 101);
            return invoice;
        }

        private static FiscalYear CreateActiveFiscalYear(int id = 1)
        {
            var fy = new FiscalYear(2026);
            typeof(FiscalYear).GetProperty("Id").SetValue(fy, id);
            fy.Activate();
            return fy;
        }

        private static Account CreateAccount(string code, string nameAr, AccountType type, int id)
        {
            var account = new Account(code, nameAr, nameAr, type, 1, 4, true, "SAR");
            typeof(Account).GetProperty("Id").SetValue(account, id);
            return account;
        }

        private void SetupGLAccounts()
        {
            _accountRepoMock.Setup(r => r.GetByCodeAsync("1121", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAccount("1121", "المدينون", AccountType.Asset, 10));
            _accountRepoMock.Setup(r => r.GetByCodeAsync("4111", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAccount("4111", "المبيعات", AccountType.Revenue, 20));
            _accountRepoMock.Setup(r => r.GetByCodeAsync("2121", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAccount("2121", "ضريبة مخرجات", AccountType.Liability, 30));
            _accountRepoMock.Setup(r => r.GetByCodeAsync("5111", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAccount("5111", "تكلفة بضاعة مباعة", AccountType.Expense, 40));
            _accountRepoMock.Setup(r => r.GetByCodeAsync("1131", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAccount("1131", "المخزون", AccountType.Asset, 50));
        }

        private CreateSalesInvoiceDto CreateValidDto()
        {
            return new CreateSalesInvoiceDto
            {
                InvoiceDate = new DateTime(2026, 2, 9, 0, 0, 0, DateTimeKind.Utc),
                CustomerId = 1,
                WarehouseId = 1,
                Notes = "فاتورة تجربة",
                Lines = new List<CreateSalesInvoiceLineDto>
                {
                    new CreateSalesInvoiceLineDto
                    {
                        ProductId = 1,
                        UnitId = 1,
                        Quantity = 5,
                        UnitPrice = 20m,
                        DiscountPercent = 0m
                    }
                }
            };
        }

        // ═══════════════════════════════════════════════════════
        //  CREATE TESTS
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task CreateAsync_WhenNotAuthenticated_ReturnsFailure()
        {
            // Arrange — auth is enforced by AuthorizationProxy (DI layer);
            // the service itself does NOT check IsAuthenticated.
            // When feature is disabled, FeatureGuard blocks the operation.
            _featureServiceMock.Setup(f => f.IsEnabledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<bool>.Success(false));
            var dto = CreateValidDto();

            // Act
            var result = await _sut.CreateAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("معطلة");
        }

        [Fact]
        public async Task CreateAsync_WhenFeatureDisabled_ReturnsFailure()
        {
            // Arrange — permission enforcement is via AuthorizationProxy (DI layer);
            // service-level guard is FeatureGuard only.
            _featureServiceMock.Setup(f => f.IsEnabledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<bool>.Success(false));
            var dto = CreateValidDto();

            // Act
            var result = await _sut.CreateAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("معطلة");
        }

        [Fact]
        public async Task CreateAsync_WhenValidationFails_ReturnsFailure()
        {
            // Arrange
            var dto = CreateValidDto();
            var failures = new List<ValidationFailure>
            {
                new ValidationFailure("CustomerId", "العميل مطلوب")
            };
            _createValidatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult(failures));

            // Act
            var result = await _sut.CreateAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("العميل مطلوب");
        }

        [Fact]
        public async Task CreateAsync_WithValidData_ReturnsSuccess()
        {
            // Arrange
            var dto = CreateValidDto();
            var product = CreateProduct();

            _invoiceRepoMock.Setup(r => r.GetNextNumberAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("SI-202602-0001");
            _productRepoMock.Setup(r => r.GetByIdWithUnitsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            // After save, return the same invoice with lines for mapping
            _invoiceRepoMock.Setup(r => r.GetWithLinesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken _) =>
                {
                    var inv = CreateDraftInvoiceWithLine(id);
                    return inv;
                });

            // Act
            var result = await _sut.CreateAsync(dto, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();
            _invoiceRepoMock.Verify(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // ═══════════════════════════════════════════════════════
        //  UPDATE TESTS
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task UpdateAsync_WhenNotDraft_ReturnsFailure()
        {
            // Arrange
            var posted = CreatePostedInvoice(1);
            _invoiceRepoMock.Setup(r => r.GetWithLinesTrackedAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(posted);

            var dto = new UpdateSalesInvoiceDto
            {
                Id = 1,
                InvoiceDate = new DateTime(2026, 2, 9, 0, 0, 0, DateTimeKind.Utc),
                CustomerId = 1,
                WarehouseId = 1,
                Lines = new List<CreateSalesInvoiceLineDto>()
            };

            // Act
            var result = await _sut.UpdateAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("لا يمكن تعديل فاتورة مرحّلة أو ملغاة");
        }

        [Fact]
        public async Task UpdateAsync_WhenNotFound_ReturnsFailure()
        {
            // Arrange
            _invoiceRepoMock.Setup(r => r.GetWithLinesTrackedAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SalesInvoice)null);

            var dto = new UpdateSalesInvoiceDto
            {
                Id = 999,
                InvoiceDate = new DateTime(2026, 2, 9, 0, 0, 0, DateTimeKind.Utc),
                CustomerId = 1,
                WarehouseId = 1,
                Lines = new List<CreateSalesInvoiceLineDto>()
            };

            // Act
            var result = await _sut.UpdateAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("فاتورة البيع غير موجودة");
        }

        // ═══════════════════════════════════════════════════════
        //  POST TESTS
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task PostAsync_WhenNotFound_ReturnsFailure()
        {
            // Arrange
            _invoiceRepoMock.Setup(r => r.GetWithLinesTrackedAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SalesInvoice)null);

            // Act
            var result = await _sut.PostAsync(999, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("فاتورة البيع غير موجودة");
        }

        [Fact]
        public async Task PostAsync_WhenNotDraft_ReturnsFailure()
        {
            // Arrange
            var posted = CreatePostedInvoice(1);
            _invoiceRepoMock.Setup(r => r.GetWithLinesTrackedAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(posted);

            // Act
            var result = await _sut.PostAsync(1, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("لا يمكن ترحيل فاتورة مرحّلة بالفعل أو ملغاة");
        }

        [Fact]
        public async Task PostAsync_WhenInsufficientStock_ReturnsFailure()
        {
            // Arrange
            var invoice = CreateDraftInvoiceWithLine(1);
            _invoiceRepoMock.Setup(r => r.GetWithLinesTrackedAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);

            // Stock is 0, but line needs 5 base units
            _whProductRepoMock.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WarehouseProduct(1, 1, 0));
            _productRepoMock.Setup(r => r.GetByIdWithUnitsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateProduct());

            // Act
            var result = await _sut.PostAsync(1, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("الكمية المتاحة");
        }

        [Fact]
        public async Task PostAsync_WhenNoActiveFiscalYear_ReturnsFailure()
        {
            // Arrange
            var invoice = CreateDraftInvoiceWithLine(1);
            _invoiceRepoMock.Setup(r => r.GetWithLinesTrackedAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);

            // Stock is sufficient
            _whProductRepoMock.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WarehouseProduct(1, 1, 100));

            // No active fiscal year
            _fiscalYearRepoMock.Setup(r => r.GetActiveYearAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((FiscalYear)null);

            // Act
            var result = await _sut.PostAsync(1, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("لا توجد سنة مالية نشطة");
        }

        [Fact]
        public async Task PostAsync_WhenPeriodClosed_ReturnsFailure()
        {
            // Arrange
            var invoice = CreateDraftInvoiceWithLine(1);
            _invoiceRepoMock.Setup(r => r.GetWithLinesTrackedAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);

            _whProductRepoMock.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WarehouseProduct(1, 1, 100));

            var fy = CreateActiveFiscalYear(1);
            _fiscalYearRepoMock.Setup(r => r.GetActiveYearAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            // Lock February period so posting fails
            var fyWithPeriods = CreateActiveFiscalYear(1);
            var febPeriod = fyWithPeriods.GetPeriod(2);
            febPeriod.Lock("admin", DateTime.UtcNow);

            _fiscalYearRepoMock.Setup(r => r.GetWithPeriodsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fyWithPeriods);

            // Act
            var result = await _sut.PostAsync(1, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("الفترة المالية");
            result.ErrorMessage.Should().Contain("مقفلة");
        }

        [Fact]
        public async Task PostAsync_WhenMissingGLAccounts_ReturnsFailure()
        {
            // Arrange
            var invoice = CreateDraftInvoiceWithLine(1);
            _invoiceRepoMock.Setup(r => r.GetWithLinesTrackedAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);

            _whProductRepoMock.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WarehouseProduct(1, 1, 100));

            var fy = CreateActiveFiscalYear(1);
            _fiscalYearRepoMock.Setup(r => r.GetActiveYearAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);
            _fiscalYearRepoMock.Setup(r => r.GetWithPeriodsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            // Missing accounts — all return null
            _accountRepoMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Account)null);

            // Act
            var result = await _sut.PostAsync(1, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("حسابات النظام المطلوبة");
        }

        [Fact]
        public async Task PostAsync_WithValidData_ReturnsSuccess()
        {
            // Arrange
            var invoice = CreateDraftInvoiceWithLine(1);
            _invoiceRepoMock.Setup(r => r.GetWithLinesTrackedAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);

            var product = CreateProduct(1, wac: 10m, vatRate: 14m);
            _productRepoMock.Setup(r => r.GetByIdWithUnitsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            var whProduct = new WarehouseProduct(1, 1, 100);
            _whProductRepoMock.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(whProduct);

            var fy = CreateActiveFiscalYear(1);
            _fiscalYearRepoMock.Setup(r => r.GetActiveYearAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);
            _fiscalYearRepoMock.Setup(r => r.GetWithPeriodsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            SetupGLAccounts();

            _journalNumberGenMock.Setup(g => g.NextNumberAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync("JV-2026-0001");

            // Assign journal entry IDs on AddAsync so invoice.Post() receives valid IDs
            var journalIdCounter = 100;
            _journalRepoMock.Setup(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
                .Callback<JournalEntry, CancellationToken>((je, _) =>
                {
                    typeof(JournalEntry).GetProperty("Id").SetValue(je, journalIdCounter++);
                })
                .Returns(Task.CompletedTask);

            // Final reload after posting — used for DTO mapping
            _invoiceRepoMock.Setup(r => r.GetWithLinesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken _) => CreatePostedInvoice(id));

            // Act
            var result = await _sut.PostAsync(1, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();
            _journalRepoMock.Verify(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _movementRepoMock.Verify(r => r.AddAsync(It.IsAny<InventoryMovement>(), It.IsAny<CancellationToken>()), Times.Once);
            _whProductRepoMock.Verify(r => r.Update(It.IsAny<WarehouseProduct>()), Times.Once);
        }

        // ═══════════════════════════════════════════════════════
        //  CANCEL TESTS
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task CancelAsync_WhenNotPosted_ReturnsFailure()
        {
            // Arrange
            var draft = CreateDraftInvoiceWithLine(1);
            _invoiceRepoMock.Setup(r => r.GetWithLinesTrackedAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(draft);

            // Act
            var result = await _sut.CancelAsync(1, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("لا يمكن إلغاء إلا الفواتير المرحّلة");
        }

        // ═══════════════════════════════════════════════════════
        //  DELETE DRAFT TESTS
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task DeleteDraftAsync_WhenNotDraft_ReturnsFailure()
        {
            // Arrange
            var posted = CreatePostedInvoice(1);
            _invoiceRepoMock.Setup(r => r.GetWithLinesTrackedAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(posted);

            // Act
            var result = await _sut.DeleteDraftAsync(1, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("لا يمكن حذف إلا الفواتير المسودة");
        }
    }
}
