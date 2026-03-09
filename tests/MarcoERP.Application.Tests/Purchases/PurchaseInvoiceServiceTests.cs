using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Services.Purchases;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Purchases;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Interfaces.Settings;

namespace MarcoERP.Application.Tests.Purchases
{
    public sealed class PurchaseInvoiceServiceTests
    {
        private readonly Mock<IPurchaseInvoiceRepository> _invoiceRepoMock = new();
        private readonly Mock<IProductRepository> _productRepoMock = new();
        private readonly Mock<IWarehouseProductRepository> _whProductRepoMock = new();
        private readonly Mock<IInventoryMovementRepository> _movementRepoMock = new();
        private readonly Mock<IJournalEntryRepository> _journalRepoMock = new();
        private readonly Mock<IAccountRepository> _accountRepoMock = new();
        private readonly Mock<ISupplierRepository> _supplierRepoMock = new();
        private readonly Mock<IFiscalYearRepository> _fiscalYearRepoMock = new();
        private readonly Mock<IJournalNumberGenerator> _journalNumberGenMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<ICurrentUserService> _currentUserMock = new();
        private readonly Mock<IDateTimeProvider> _dateTimeMock = new();
        private readonly Mock<IValidator<CreatePurchaseInvoiceDto>> _createValidatorMock = new();
        private readonly Mock<IValidator<UpdatePurchaseInvoiceDto>> _updateValidatorMock = new();
        private readonly Mock<ISystemSettingRepository> _systemSettingRepoMock = new();
        private readonly PurchaseInvoiceService _sut;

        public PurchaseInvoiceServiceTests()
        {
            _currentUserMock.Setup(c => c.IsAuthenticated).Returns(true);
            _currentUserMock.Setup(c => c.HasPermission(PermissionKeys.PurchasesCreate)).Returns(true);
            _currentUserMock.Setup(c => c.HasPermission(PermissionKeys.PurchasesPost)).Returns(true);
            _currentUserMock.Setup(c => c.Username).Returns("purchuser");

            _dateTimeMock.Setup(d => d.UtcNow).Returns(new DateTime(2026, 2, 9, 12, 0, 0, DateTimeKind.Utc));

            _createValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<CreatePurchaseInvoiceDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
            _updateValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<UpdatePurchaseInvoiceDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Default: supplier exists (FK validation)
            _supplierRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken _) =>
                {
                    var s = new Supplier(new SupplierDraft { Code = "S001", NameAr = "مورد اختبار" });
                    typeof(Supplier).GetProperty("Id").SetValue(s, id);
                    return s;
                });

            // CRITICAL: ExecuteInTransactionAsync must invoke the delegate
            _unitOfWorkMock.Setup(u => u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(), It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()))
                .Returns<Func<Task>, IsolationLevel, CancellationToken>((op, _, __) => op());

            _sut = new PurchaseInvoiceService(
                new PurchaseInvoiceRepositories(
                    _invoiceRepoMock.Object,
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
                    _dateTimeMock.Object),
                new PurchaseInvoiceValidators(
                    _createValidatorMock.Object,
                    _updateValidatorMock.Object),
                new JournalEntryFactory(_journalRepoMock.Object, _journalNumberGenMock.Object),
                new FiscalPeriodValidator(_fiscalYearRepoMock.Object, _systemSettingRepoMock.Object, _dateTimeMock.Object, _currentUserMock.Object),
                new StockManager(_whProductRepoMock.Object, _movementRepoMock.Object));
        }

        private static Product CreateProduct(int id = 1, decimal wac = 10m, decimal vatRate = 14m)
        {
            var product = new Product("P001", "صنف اختبار", "Test Product", 1, 1, wac, 20m, 0m, 0m, vatRate);
            typeof(Product).GetProperty("Id").SetValue(product, id);
            product.AddUnit(new ProductUnit(0, 1, 1m, 20m, wac));
            return product;
        }

        private static PurchaseInvoice CreateDraftInvoiceWithLine(int id = 1)
        {
            var invoice = new PurchaseInvoice("PI-202602-0001", new DateTime(2026, 2, 9, 0, 0, 0, DateTimeKind.Utc), 1, 1, "ملاحظات");
            typeof(PurchaseInvoice).GetProperty("Id").SetValue(invoice, id);
            invoice.AddLine(1, 1, 5m, 20m, 1m, 0m, 14m);
            return invoice;
        }

        private static CreatePurchaseInvoiceDto CreateValidDto()
        {
            return new CreatePurchaseInvoiceDto
            {
                InvoiceDate = new DateTime(2026, 2, 9, 0, 0, 0, DateTimeKind.Utc),
                SupplierId = 1,
                WarehouseId = 1,
                CounterpartyType = CounterpartyType.Supplier,
                Notes = "فاتورة شراء",
                Lines = new List<CreatePurchaseInvoiceLineDto>
                {
                    new CreatePurchaseInvoiceLineDto
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

        [Fact]
        public async Task CreateAsync_WithValidData_ReturnsSuccess()
        {
            var dto = CreateValidDto();
            var product = CreateProduct();

            _invoiceRepoMock.Setup(r => r.GetNextNumberAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("PI-202602-0001");
            _productRepoMock.Setup(r => r.GetByIdWithUnitsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);
            _invoiceRepoMock.Setup(r => r.GetWithLinesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken _) => CreateDraftInvoiceWithLine(id));

            var result = await _sut.CreateAsync(dto, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();
            _invoiceRepoMock.Verify(r => r.AddAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteDraftAsync_WhenDraft_ReturnsSuccess()
        {
            var draft = CreateDraftInvoiceWithLine(1);
            _invoiceRepoMock.Setup(r => r.GetWithLinesAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(draft);

            var result = await _sut.DeleteDraftAsync(1, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            _invoiceRepoMock.Verify(r => r.Update(It.IsAny<PurchaseInvoice>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelAsync_WhenNotPosted_ReturnsFailure()
        {
            var draft = CreateDraftInvoiceWithLine(1);
            _invoiceRepoMock.Setup(r => r.GetWithLinesAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(draft);

            var result = await _sut.CancelAsync(1, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("لا يمكن إلغاء إلا الفواتير المرحّلة");
        }
    }
}
