using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Services.Treasury;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Treasury;

namespace MarcoERP.Application.Tests.Treasury
{
    public class BankReconciliationServiceTests
    {
        private readonly Mock<IBankReconciliationRepository> _reconRepoMock = new();
        private readonly Mock<IBankAccountRepository> _bankAcctRepoMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<ICurrentUserService> _currentUserMock = new();
        private readonly Mock<IValidator<CreateBankReconciliationDto>> _createValidatorMock = new();
        private readonly Mock<IValidator<CreateBankReconciliationItemDto>> _itemValidatorMock = new();
        private readonly Mock<IValidator<UpdateBankReconciliationDto>> _updateValidatorMock = new();

        public BankReconciliationServiceTests()
        {
            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
        }

        private BankReconciliationService CreateService() => new BankReconciliationService(
            _reconRepoMock.Object,
            _bankAcctRepoMock.Object,
            _unitOfWorkMock.Object,
            _currentUserMock.Object,
            _createValidatorMock.Object,
            _itemValidatorMock.Object,
            _updateValidatorMock.Object);

        private void SetupAuth(bool authenticated = true, bool hasPermission = true)
        {
            _currentUserMock.Setup(u => u.IsAuthenticated).Returns(authenticated);
            _currentUserMock.Setup(u => u.HasPermission(It.IsAny<string>())).Returns(hasPermission);
        }

        private void SetupValidValidation()
        {
            _createValidatorMock
                .Setup(v => v.ValidateAsync(It.IsAny<CreateBankReconciliationDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
            _itemValidatorMock
                .Setup(v => v.ValidateAsync(It.IsAny<CreateBankReconciliationItemDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
        }

        private static BankReconciliation CreateTestReconciliation(int bankAccountId = 1)
        {
            return new BankReconciliation(bankAccountId, DateTime.Today, 10000m, "ملاحظات");
        }

        // =====================================================================
        // 1. Authorization Tests
        // =====================================================================

        [Fact]
        public async Task CreateAsync_NotAuthenticated_ReturnsFailure()
        {
            SetupAuth(authenticated: false);
            var service = CreateService();

            var result = await service.CreateAsync(new CreateBankReconciliationDto(), CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
        }

        [Fact]
        public async Task CreateAsync_NoPermission_ReturnsFailure()
        {
            SetupAuth(hasPermission: false);
            var service = CreateService();

            var result = await service.CreateAsync(new CreateBankReconciliationDto(), CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
        }

        // =====================================================================
        // 2. GetAllAsync Tests
        // =====================================================================

        [Fact]
        public async Task GetAllAsync_ReturnsAllReconciliations()
        {
            var entities = new List<BankReconciliation> { CreateTestReconciliation() };
            _reconRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(entities);
            var service = CreateService();

            var result = await service.GetAllAsync(CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Data.Should().HaveCount(1);
        }

        // =====================================================================
        // 3. GetByIdAsync Tests
        // =====================================================================

        [Fact]
        public async Task GetByIdAsync_NotFound_ReturnsFailure()
        {
            _reconRepoMock.Setup(r => r.GetByIdWithItemsAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((BankReconciliation)null);
            var service = CreateService();

            var result = await service.GetByIdAsync(999, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
        }

        [Fact]
        public async Task GetByIdAsync_Found_ReturnsDto()
        {
            var entity = CreateTestReconciliation();
            _reconRepoMock.Setup(r => r.GetByIdWithItemsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
            var service = CreateService();

            var result = await service.GetByIdAsync(1, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();
        }

        // =====================================================================
        // 4. CreateAsync Tests
        // =====================================================================

        [Fact]
        public async Task CreateAsync_ValidationFails_ReturnsFailure()
        {
            SetupAuth();
            var validationResult = new ValidationResult(new[]
            {
                new ValidationFailure("BankAccountId", "الحساب البنكي مطلوب")
            });
            _createValidatorMock
                .Setup(v => v.ValidateAsync(It.IsAny<CreateBankReconciliationDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(validationResult);
            var service = CreateService();

            var result = await service.CreateAsync(new CreateBankReconciliationDto(), CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
        }

        [Fact]
        public async Task CreateAsync_BankAccountNotFound_ReturnsFailure()
        {
            SetupAuth();
            SetupValidValidation();
            _bankAcctRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((BankAccount)null);
            var service = CreateService();

            var result = await service.CreateAsync(new CreateBankReconciliationDto
            {
                BankAccountId = 999,
                ReconciliationDate = DateTime.Today,
                StatementBalance = 5000m
            }, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
        }

        [Fact]
        public async Task CreateAsync_ValidData_ReturnsSuccess()
        {
            SetupAuth();
            SetupValidValidation();
            var bankAccount = new BankAccount("BNK-0001", "بنك", "Bank", "بنك", "123", "SA123", 1);
            _bankAcctRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(bankAccount);
            var service = CreateService();

            var result = await service.CreateAsync(new CreateBankReconciliationDto
            {
                BankAccountId = 1,
                ReconciliationDate = DateTime.Today,
                StatementBalance = 10000m,
                Notes = "تسوية شهرية"
            }, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            _reconRepoMock.Verify(r => r.AddAsync(It.IsAny<BankReconciliation>(), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // =====================================================================
        // 5. CompleteAsync / ReopenAsync Tests
        // =====================================================================

        [Fact]
        public async Task CompleteAsync_NotAuthenticated_ReturnsFailure()
        {
            SetupAuth(authenticated: false);
            var service = CreateService();

            var result = await service.CompleteAsync(1, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
        }

        [Fact]
        public async Task CompleteAsync_NotFound_ReturnsFailure()
        {
            SetupAuth();
            _reconRepoMock.Setup(r => r.GetByIdWithItemsAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((BankReconciliation)null);
            var service = CreateService();

            var result = await service.CompleteAsync(999, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
        }

        [Fact]
        public async Task ReopenAsync_NotFound_ReturnsFailure()
        {
            SetupAuth();
            _reconRepoMock.Setup(r => r.GetByIdWithItemsAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((BankReconciliation)null);
            var service = CreateService();

            var result = await service.ReopenAsync(999, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
        }

        // =====================================================================
        // 6. DeleteAsync Tests
        // =====================================================================

        [Fact]
        public async Task DeleteAsync_NotAuthenticated_ReturnsFailure()
        {
            SetupAuth(authenticated: false);
            var service = CreateService();

            var result = await service.DeleteAsync(1, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteAsync_NotFound_ReturnsFailure()
        {
            SetupAuth();
            _reconRepoMock.Setup(r => r.GetByIdWithItemsAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((BankReconciliation)null);
            var service = CreateService();

            var result = await service.DeleteAsync(999, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteAsync_ValidNotCompleted_ReturnsSuccess()
        {
            SetupAuth();
            var entity = CreateTestReconciliation();
            _reconRepoMock.Setup(r => r.GetByIdWithItemsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);
            var service = CreateService();

            var result = await service.DeleteAsync(1, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            _reconRepoMock.Verify(r => r.Remove(entity), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
