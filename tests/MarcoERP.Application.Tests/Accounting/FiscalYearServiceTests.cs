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
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Services.Accounting;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;

namespace MarcoERP.Application.Tests.Accounting
{
    public class FiscalYearServiceTests
    {
        private readonly Mock<IFiscalYearRepository> _fiscalYearRepoMock;
        private readonly Mock<IJournalEntryRepository> _journalEntryRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ICurrentUserService> _currentUserMock;
        private readonly Mock<IAuditLogger> _auditLoggerMock;
        private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
        private readonly Mock<IValidator<CreateFiscalYearDto>> _createValidatorMock;
        private readonly Mock<IAccountRepository> _accountRepoMock;
        private readonly Mock<IJournalNumberGenerator> _journalNumberGenMock;
        private readonly FiscalYearService _sut;

        public FiscalYearServiceTests()
        {
            _fiscalYearRepoMock = new Mock<IFiscalYearRepository>();
            _journalEntryRepoMock = new Mock<IJournalEntryRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _currentUserMock = new Mock<ICurrentUserService>();
            _auditLoggerMock = new Mock<IAuditLogger>();
            _dateTimeProviderMock = new Mock<IDateTimeProvider>();
            _createValidatorMock = new Mock<IValidator<CreateFiscalYearDto>>();
            _accountRepoMock = new Mock<IAccountRepository>();
            _journalNumberGenMock = new Mock<IJournalNumberGenerator>();

            // Default: authenticated user with all permissions
            _currentUserMock.Setup(u => u.IsAuthenticated).Returns(true);
            _currentUserMock.Setup(u => u.HasPermission(It.IsAny<string>())).Returns(true);
            _currentUserMock.Setup(u => u.Username).Returns("testuser");
            _currentUserMock.Setup(u => u.UserId).Returns(1);

            // Default: valid validation
            _createValidatorMock
                .Setup(v => v.ValidateAsync(It.IsAny<CreateFiscalYearDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            // Default: dateTimeProvider
            _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc));
            _dateTimeProviderMock.Setup(d => d.Today).Returns(new DateTime(2024, 6, 15));

            // UnitOfWork: ExecuteInTransactionAsync must invoke the delegate
            _unitOfWorkMock
                .Setup(u => u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<IsolationLevel>(),
                    It.IsAny<CancellationToken>()))
                .Returns<Func<Task>, IsolationLevel, CancellationToken>((op, _, __) => op());

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _sut = new FiscalYearService(
                _fiscalYearRepoMock.Object,
                _journalEntryRepoMock.Object,
                _unitOfWorkMock.Object,
                _currentUserMock.Object,
                _auditLoggerMock.Object,
                _dateTimeProviderMock.Object,
                _createValidatorMock.Object,
                new YearEndClosingService(
                    _accountRepoMock.Object,
                    _journalEntryRepoMock.Object,
                    _fiscalYearRepoMock.Object,
                    _journalNumberGenMock.Object,
                    _unitOfWorkMock.Object,
                    _currentUserMock.Object,
                    _dateTimeProviderMock.Object));
        }

        // ───────────────────────────── Auth / Permission ─────────────────────────────

        [Fact]
        public async Task CreateAsync_WhenNotAuthenticated_ReturnsFailure()
        {
            // Arrange
            _currentUserMock.Setup(u => u.IsAuthenticated).Returns(false);
            var dto = new CreateFiscalYearDto { Year = 2024 };

            // Act
            var result = await _sut.CreateAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
        }

        [Fact]
        public async Task CreateAsync_WhenNoPermission_ReturnsFailure()
        {
            // Arrange
            _currentUserMock.Setup(u => u.HasPermission(PermissionKeys.FiscalYearManage)).Returns(false);
            var dto = new CreateFiscalYearDto { Year = 2024 };

            // Act
            var result = await _sut.CreateAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
        }

        // ───────────────────────────── GetByIdAsync ─────────────────────────────

        [Fact]
        public async Task GetByIdAsync_WhenExists_ReturnsSuccess()
        {
            // Arrange
            var fy = new FiscalYear(2024);
            typeof(FiscalYear).GetProperty("Id").SetValue(fy, 1);
            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            // Act
            var result = await _sut.GetByIdAsync(1, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task GetByIdAsync_WhenNotFound_ReturnsFailure()
        {
            // Arrange
            var id = 999;
            _fiscalYearRepoMock
                .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((FiscalYear)null);

            // Act
            var result = await _sut.GetByIdAsync(id, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
        }

        // ───────────────────────────── CreateAsync ─────────────────────────────

        [Fact]
        public async Task CreateAsync_WhenValidationFails_ReturnsFailure()
        {
            // Arrange
            var dto = new CreateFiscalYearDto { Year = -1 };
            var failures = new List<ValidationFailure>
            {
                new ValidationFailure("Year", "Year is invalid")
            };
            _createValidatorMock
                .Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult(failures));

            // Act
            var result = await _sut.CreateAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
        }

        [Fact]
        public async Task CreateAsync_WhenYearExists_ReturnsFailure()
        {
            // Arrange
            var dto = new CreateFiscalYearDto { Year = 2024 };
            _fiscalYearRepoMock
                .Setup(r => r.YearExistsAsync(2024, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.CreateAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
        }

        [Fact]
        public async Task CreateAsync_WithValidData_ReturnsSuccess()
        {
            // Arrange
            var dto = new CreateFiscalYearDto { Year = 2024 };
            _fiscalYearRepoMock
                .Setup(r => r.YearExistsAsync(2024, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _fiscalYearRepoMock
                .Setup(r => r.AddAsync(It.IsAny<FiscalYear>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.CreateAsync(dto, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        // ───────────────────────────── ActivateAsync ─────────────────────────────

        [Fact]
        public async Task ActivateAsync_WhenNotFound_ReturnsFailure()
        {
            // Arrange
            var id = 999;
            _fiscalYearRepoMock
                .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((FiscalYear)null);

            // Act
            var result = await _sut.ActivateAsync(id, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
        }

        [Fact]
        public async Task ActivateAsync_WhenAnotherYearActive_ReturnsFailure()
        {
            // Arrange
            var fy = new FiscalYear(2024);
            var otherActive = new FiscalYear(2023);
            otherActive.Activate();

            _fiscalYearRepoMock
                .Setup(r => r.GetByIdAsync(fy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);
            _fiscalYearRepoMock
                .Setup(r => r.GetActiveYearAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(otherActive);

            // Act
            var result = await _sut.ActivateAsync(fy.Id, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
        }

        [Fact]
        public async Task ActivateAsync_WithValidYear_ReturnsSuccess()
        {
            // Arrange
            var fy = new FiscalYear(2024);
            typeof(FiscalYear).GetProperty("Id").SetValue(fy, 1);
            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);
            _fiscalYearRepoMock
                .Setup(r => r.GetActiveYearAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((FiscalYear)null);

            // Act
            var result = await _sut.ActivateAsync(1, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        // ───────────────────────────── CloseAsync ─────────────────────────────

        [Fact]
        public async Task CloseAsync_WhenNotFound_ReturnsFailure()
        {
            // Arrange
            var id = 999;
            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((FiscalYear)null);

            // Act
            var result = await _sut.CloseAsync(id, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
        }

        [Fact]
        public async Task CloseAsync_WhenPendingDrafts_ReturnsFailure()
        {
            // Arrange
            var fy = new FiscalYear(2024);
            fy.Activate();

            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(fy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            var draft = JournalEntry.CreateDraft(new DateTime(2024, 6, 15), "test", SourceType.Manual, fy.Id, 1);
            var drafts = new List<JournalEntry> { draft };
            _journalEntryRepoMock
                .Setup(r => r.GetDraftsByYearAsync(fy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(drafts);

            // Act
            var result = await _sut.CloseAsync(fy.Id, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
        }

        [Fact]
        public async Task CloseAsync_WithNoDrafts_ReturnsSuccess()
        {
            // Arrange
            var fy = new FiscalYear(2024);
            fy.Activate();

            // Lock all 12 periods before closing
            var closedBy = "testuser";
            var closedAt = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);
            foreach (var period in Enumerable.Range(1, 12))
            {
                var fp = fy.GetPeriod(period);
                fp.Lock(closedBy, closedAt);
            }

            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(fy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            _journalEntryRepoMock
                .Setup(r => r.GetDraftsByYearAsync(fy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JournalEntry>());

            // Year-end closing mocks: retained earnings account + revenue line to close
            var retainedEarnings = new Account("3121", "الأرباح المحتجزة", "Retained Earnings",
                AccountType.Equity, 1, 4, true, "EGP");
            _accountRepoMock
                .Setup(r => r.GetByCodeAsync("3121", It.IsAny<CancellationToken>()))
                .ReturnsAsync(retainedEarnings);

            // Provide balanced posted lines (trial balance gate before close)
            var revenueLine = JournalEntryLine.Create(100, 1, 0, 1000, "Sales", null, null, DateTime.UtcNow);
            var expenseLine = JournalEntryLine.Create(101, 2, 1000, 0, "Expense", null, null, DateTime.UtcNow);
            _journalEntryRepoMock
                .Setup(r => r.GetPostedLinesByYearAsync(fy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JournalEntryLine> { revenueLine, expenseLine });

            var revenueAccount = new Account("4111", "المبيعات", "Sales",
                AccountType.Revenue, 1, 4, true, "EGP");
            var expenseAccount = new Account("5111", "مصروف", "Expense",
                AccountType.Expense, 1, 4, true, "EGP");
            _accountRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Account> { retainedEarnings, revenueAccount, expenseAccount });

            _journalNumberGenMock
                .Setup(g => g.NextNumberAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("JV-202412-0001");

            // Idempotency guard: no existing closing entry for last period
            _journalEntryRepoMock
                .Setup(r => r.GetByPeriodAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JournalEntry>());

            // Act
            var result = await _sut.CloseAsync(fy.Id, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        // ───────────────────────────── LockPeriodAsync ─────────────────────────────

        [Fact]
        public async Task LockPeriodAsync_WhenPeriodNotFound_ReturnsFailure()
        {
            // Arrange
            var periodId = 999;
            _fiscalYearRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FiscalYear>());

            // Act
            var result = await _sut.LockPeriodAsync(periodId, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
        }

        [Fact]
        public async Task LockPeriodAsync_WhenPriorPeriodsNotLocked_ReturnsFailure()
        {
            // Arrange
            var fy = new FiscalYear(2024);
            fy.Activate();
            var periodsToSet = fy.Periods.OrderBy(p => p.PeriodNumber).ToList();
            for (int i = 0; i < periodsToSet.Count; i++)
                typeof(FiscalPeriod).GetProperty("Id")!.SetValue(periodsToSet[i], i + 1);

            _fiscalYearRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FiscalYear> { fy });
            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(fy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            // Try to lock period 2 without locking period 1 first
            var period2 = fy.GetPeriod(2);

            // Act
            var result = await _sut.LockPeriodAsync(period2.Id, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
        }

        [Fact]
        public async Task LockPeriodAsync_WithValidPeriod_ReturnsSuccess()
        {
            // Arrange
            var fy = new FiscalYear(2024);
            fy.Activate();
            var periodsToSet = fy.Periods.OrderBy(p => p.PeriodNumber).ToList();
            for (int i = 0; i < periodsToSet.Count; i++)
                typeof(FiscalPeriod).GetProperty("Id")!.SetValue(periodsToSet[i], i + 1);

            _fiscalYearRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FiscalYear> { fy });
            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(fy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            // Lock period 1 — no prior periods needed
            var period1 = fy.GetPeriod(1);

            _journalEntryRepoMock
                .Setup(r => r.GetByPeriodAsync(period1.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JournalEntry>());

            // Act
            var result = await _sut.LockPeriodAsync(period1.Id, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        // ───────────────────────────── UnlockPeriodAsync ─────────────────────────────

        [Fact]
        public async Task UnlockPeriodAsync_WhenReasonEmpty_ReturnsFailure()
        {
            // Arrange
            var fy = new FiscalYear(2024);
            fy.Activate();
            var periodsToSet = fy.Periods.OrderBy(p => p.PeriodNumber).ToList();
            for (int i = 0; i < periodsToSet.Count; i++)
                typeof(FiscalPeriod).GetProperty("Id")!.SetValue(periodsToSet[i], i + 1);
            var period1 = fy.GetPeriod(1);
            period1.Lock("testuser", DateTime.UtcNow);

            _fiscalYearRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FiscalYear> { fy });
            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(fy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            // Act
            var result = await _sut.UnlockPeriodAsync(period1.Id, string.Empty, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
        }

        [Fact]
        public async Task UnlockPeriodAsync_WhenNotMostRecentLocked_ReturnsFailure()
        {
            // Arrange
            var fy = new FiscalYear(2024);
            fy.Activate();
            var periodsToSet = fy.Periods.OrderBy(p => p.PeriodNumber).ToList();
            for (int i = 0; i < periodsToSet.Count; i++)
                typeof(FiscalPeriod).GetProperty("Id")!.SetValue(periodsToSet[i], i + 1);

            // Lock periods 1 and 2
            var period1 = fy.GetPeriod(1);
            var period2 = fy.GetPeriod(2);
            period1.Lock("testuser", DateTime.UtcNow);
            period2.Lock("testuser", DateTime.UtcNow);

            _fiscalYearRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FiscalYear> { fy });
            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(fy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            // Try to unlock period 1 (period 2 is a later locked period)
            var result = await _sut.UnlockPeriodAsync(period1.Id, "Correction needed", CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
        }
    }
}
