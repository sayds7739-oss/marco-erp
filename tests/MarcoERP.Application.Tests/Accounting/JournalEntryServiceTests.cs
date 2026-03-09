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
    public class JournalEntryServiceTests
    {
        private readonly Mock<IJournalEntryRepository> _journalEntryRepoMock;
        private readonly Mock<IAccountRepository> _accountRepoMock;
        private readonly Mock<IFiscalYearRepository> _fiscalYearRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IJournalNumberGenerator> _numberGeneratorMock;
        private readonly Mock<ICurrentUserService> _currentUserMock;
        private readonly Mock<IAuditLogger> _auditLoggerMock;
        private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
        private readonly Mock<IValidator<CreateJournalEntryDto>> _createValidatorMock;
        private readonly JournalEntryService _sut;

        public JournalEntryServiceTests()
        {
            _journalEntryRepoMock = new Mock<IJournalEntryRepository>();
            _accountRepoMock = new Mock<IAccountRepository>();
            _fiscalYearRepoMock = new Mock<IFiscalYearRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _numberGeneratorMock = new Mock<IJournalNumberGenerator>();
            _currentUserMock = new Mock<ICurrentUserService>();
            _auditLoggerMock = new Mock<IAuditLogger>();
            _dateTimeProviderMock = new Mock<IDateTimeProvider>();
            _createValidatorMock = new Mock<IValidator<CreateJournalEntryDto>>();

            // Default: authenticated user with all permissions
            SetupAuth();

            // Default: valid validation result
            _createValidatorMock
                .Setup(v => v.ValidateAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
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

            _sut = new JournalEntryService(
                _journalEntryRepoMock.Object,
                _accountRepoMock.Object,
                _fiscalYearRepoMock.Object,
                _unitOfWorkMock.Object,
                _numberGeneratorMock.Object,
                _currentUserMock.Object,
                _auditLoggerMock.Object,
                _dateTimeProviderMock.Object,
                _createValidatorMock.Object);
        }

        // ── Helpers ─────────────────────────────────────────────

        private void SetupAuth(bool isAuth = true, bool hasPerm = true)
        {
            _currentUserMock.Setup(x => x.IsAuthenticated).Returns(isAuth);
            _currentUserMock.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(hasPerm);
            _currentUserMock.Setup(x => x.Username).Returns("admin");
        }

        private Account CreatePostableAccount(int id, string code = "1111")
        {
            var acct = new Account(code, "حساب", "Account", AccountType.Asset, 1, 4, false, "SAR");
            typeof(Account).GetProperty("Id").SetValue(acct, id);
            return acct;
        }

        private FiscalYear CreateActiveFiscalYear(int year = 2024)
        {
            var fy = new FiscalYear(year);
            fy.Activate();
            typeof(FiscalYear).GetProperty("Id").SetValue(fy, 1);
            return fy;
        }

        private CreateJournalEntryDto CreateValidDto(DateTime? journalDate = null)
        {
            var date = journalDate ?? new DateTime(2024, 6, 15);
            return new CreateJournalEntryDto
            {
                JournalDate = date,
                Description = "قيد اختبار",
                SourceType = SourceType.Manual,
                ReferenceNumber = "REF-001",
                CostCenterId = null,
                Lines = new List<CreateJournalEntryLineDto>
                {
                    new CreateJournalEntryLineDto
                    {
                        AccountId = 100,
                        DebitAmount = 1000m,
                        CreditAmount = 0m,
                        Description = "مدين"
                    },
                    new CreateJournalEntryLineDto
                    {
                        AccountId = 200,
                        DebitAmount = 0m,
                        CreditAmount = 1000m,
                        Description = "دائن"
                    }
                }
            };
        }

        private JournalEntry CreateDraftEntry(int id = 1, DateTime? date = null)
        {
            var journalDate = date ?? new DateTime(2024, 6, 15);
            var entry = JournalEntry.CreateDraft(
                journalDate,
                "قيد اختبار",
                SourceType.Manual,
                1,  // fiscalYearId
                6,  // fiscalPeriodId (June)
                "REF-001");

            entry.AddLine(100, 1000m, 0m, DateTime.UtcNow, "مدين");
            entry.AddLine(200, 0m, 1000m, DateTime.UtcNow, "دائن");

            typeof(JournalEntry).GetProperty("Id").SetValue(entry, id);
            return entry;
        }

        private JournalEntry CreatePostedEntry(int id = 1, DateTime? date = null)
        {
            var entry = CreateDraftEntry(id, date);
            entry.Post("JV-2024-00001", "admin", DateTime.UtcNow);
            return entry;
        }

        private void SetupFiscalYearForDate(DateTime date, bool isActive = true, bool periodOpen = true)
        {
            var fy = new FiscalYear(date.Year);
            if (isActive) fy.Activate();
            typeof(FiscalYear).GetProperty("Id").SetValue(fy, 1);

            // Lock the period if needed
            if (!periodOpen)
            {
                var period = fy.GetPeriod(date.Month);
                period.Lock("admin", DateTime.UtcNow);
            }

            // Set FiscalYearId on each period via reflection
            foreach (var p in fy.Periods)
            {
                typeof(FiscalPeriod).GetProperty("FiscalYearId").SetValue(p, fy.Id);
            }

            _fiscalYearRepoMock
                .Setup(r => r.GetByYearAsync(date.Year, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(fy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);
        }

        private void SetupAccountsPostable(params int[] accountIds)
        {
            foreach (var accountId in accountIds)
            {
                var acct = CreatePostableAccount(accountId, $"{accountId:D4}");
                _accountRepoMock
                    .Setup(r => r.GetByIdAsync(accountId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(acct);
            }
        }

        // ══════════════════════════════════════════════════════════
        // Auth (3)
        // ══════════════════════════════════════════════════════════

        [Fact]
        public async Task CreateDraftAsync_WhenNotAuthenticated_ReturnsFailure()
        {
            // Arrange
            SetupAuth(isAuth: false);
            var dto = CreateValidDto();

            // Act
            var result = await _sut.CreateDraftAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("تسجيل الدخول"));
        }

        [Fact]
        public async Task PostAsync_WhenNoPermission_ReturnsFailure()
        {
            // Arrange
            _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);
            _currentUserMock.Setup(x => x.HasPermission(PermissionKeys.JournalPost)).Returns(false);
            _currentUserMock.Setup(x => x.Username).Returns("admin");

            // Act
            var result = await _sut.PostAsync(1, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("صلاحية"));
        }

        [Fact]
        public async Task ReverseAsync_WhenNoPermission_ReturnsFailure()
        {
            // Arrange
            _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);
            _currentUserMock.Setup(x => x.HasPermission(PermissionKeys.JournalReverse)).Returns(false);
            _currentUserMock.Setup(x => x.Username).Returns("admin");

            var dto = new ReverseJournalEntryDto
            {
                JournalEntryId = 1,
                ReversalReason = "خطأ",
                ReversalDate = new DateTime(2024, 6, 20)
            };

            // Act
            var result = await _sut.ReverseAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("صلاحية"));
        }

        // ══════════════════════════════════════════════════════════
        // GetByIdAsync (2)
        // ══════════════════════════════════════════════════════════

        [Fact]
        public async Task GetByIdAsync_WhenExists_ReturnsSuccess()
        {
            // Arrange
            var entry = CreateDraftEntry(id: 42);
            _journalEntryRepoMock
                .Setup(r => r.GetWithLinesAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entry);

            // Act
            var result = await _sut.GetByIdAsync(42, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();
        }

        [Fact]
        public async Task GetByIdAsync_WhenNotFound_ReturnsFailure()
        {
            // Arrange
            _journalEntryRepoMock
                .Setup(r => r.GetWithLinesAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((JournalEntry)null);

            // Act
            var result = await _sut.GetByIdAsync(999, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().NotBeEmpty();
        }

        // ══════════════════════════════════════════════════════════
        // CreateDraftAsync (6)
        // ══════════════════════════════════════════════════════════

        [Fact]
        public async Task CreateDraftAsync_WhenValidationFails_ReturnsFailure()
        {
            // Arrange
            var dto = CreateValidDto();
            var failures = new List<ValidationFailure>
            {
                new ValidationFailure("Description", "الوصف مطلوب")
            };
            _createValidatorMock
                .Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult(failures));

            // Act
            var result = await _sut.CreateDraftAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain("الوصف مطلوب");
        }

        [Fact]
        public async Task CreateDraftAsync_WhenNoFiscalYear_ReturnsFailure()
        {
            // Arrange
            var dto = CreateValidDto();
            _fiscalYearRepoMock
                .Setup(r => r.GetByYearAsync(dto.JournalDate.Year, It.IsAny<CancellationToken>()))
                .ReturnsAsync((FiscalYear)null);

            // Act
            var result = await _sut.CreateDraftAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("سنة مالية"));
        }

        [Fact]
        public async Task CreateDraftAsync_WhenYearNotActive_ReturnsFailure()
        {
            // Arrange
            var dto = CreateValidDto();
            // Create FY but do NOT activate it (stays in Setup status)
            SetupFiscalYearForDate(dto.JournalDate, isActive: false);

            // Act
            var result = await _sut.CreateDraftAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("فعّالة") || e.Contains("Active"));
        }

        [Fact]
        public async Task CreateDraftAsync_WhenPeriodLocked_ReturnsFailure()
        {
            // Arrange
            var dto = CreateValidDto();
            SetupFiscalYearForDate(dto.JournalDate, isActive: true, periodOpen: false);

            // Act
            var result = await _sut.CreateDraftAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("مُقفلة"));
        }

        [Fact]
        public async Task CreateDraftAsync_WhenAccountNotPostable_ReturnsFailure()
        {
            // Arrange
            var dto = CreateValidDto();
            SetupFiscalYearForDate(dto.JournalDate);

            // Setup account 100 as non-postable (level 1 → AllowPosting = false)
            var nonPostableAccount = new Account("1000", "حساب رئيسي", "Parent", AccountType.Asset, null, 1, false, "SAR");
            typeof(Account).GetProperty("Id").SetValue(nonPostableAccount, 100);

            _accountRepoMock
                .Setup(r => r.GetByIdAsync(100, It.IsAny<CancellationToken>()))
                .ReturnsAsync(nonPostableAccount);

            // Account 200 is fine
            SetupAccountsPostable(200);

            // Act
            var result = await _sut.CreateDraftAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("لا يقبل الترحيل"));
        }

        [Fact]
        public async Task CreateDraftAsync_WithValidData_ReturnsSuccess()
        {
            // Arrange
            var dto = CreateValidDto();
            SetupFiscalYearForDate(dto.JournalDate);
            SetupAccountsPostable(100, 200);

            // Act
            var result = await _sut.CreateDraftAsync(dto, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();

            _journalEntryRepoMock.Verify(
                r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
                Times.Once);

            _auditLoggerMock.Verify(
                a => a.LogAsync("JournalEntry", It.IsAny<int>(), "DraftCreated",
                    "admin", It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // ══════════════════════════════════════════════════════════
        // PostAsync (6)
        // ══════════════════════════════════════════════════════════

        [Fact]
        public async Task PostAsync_WhenNotFound_ReturnsFailure()
        {
            // Arrange
            _journalEntryRepoMock
                .Setup(r => r.GetWithLinesAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((JournalEntry)null);

            // Act
            var result = await _sut.PostAsync(999, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("غير موجود"));
        }

        [Fact]
        public async Task PostAsync_WhenNotDraft_ReturnsFailure()
        {
            // Arrange — posted entry (not draft)
            var entry = CreatePostedEntry(id: 1);
            _journalEntryRepoMock
                .Setup(r => r.GetWithLinesAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entry);

            // Act
            var result = await _sut.PostAsync(1, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("مسودات") || e.Contains("Draft"));
        }

        [Fact]
        public async Task PostAsync_WhenPeriodClosed_ReturnsFailure()
        {
            // Arrange
            var journalDate = new DateTime(2024, 6, 15);
            var entry = CreateDraftEntry(id: 1, date: journalDate);

            _journalEntryRepoMock
                .Setup(r => r.GetWithLinesAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entry);

            // Create FY with the period locked
            var fy = CreateActiveFiscalYear(2024);
            var period = fy.GetPeriod(6);
            period.Lock("admin", DateTime.UtcNow);

            // Set FiscalYearId on periods
            foreach (var p in fy.Periods)
            {
                typeof(FiscalPeriod).GetProperty("FiscalYearId").SetValue(p, fy.Id);
            }

            // Set period Id to match the entry's FiscalPeriodId (6)
            typeof(FiscalPeriod).GetProperty("Id").SetValue(period, 6);

            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(entry.FiscalYearId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            // Act
            var result = await _sut.PostAsync(1, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("مُقفلة"));
        }

        [Fact]
        public async Task PostAsync_WhenYearInactive_ReturnsFailure()
        {
            // Arrange
            var journalDate = new DateTime(2024, 6, 15);
            var entry = CreateDraftEntry(id: 1, date: journalDate);

            _journalEntryRepoMock
                .Setup(r => r.GetWithLinesAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entry);

            // Create FY that is NOT active (Setup status)
            var fy = new FiscalYear(2024);
            typeof(FiscalYear).GetProperty("Id").SetValue(fy, 1);

            // We need a period with matching Id for the entry
            var period = fy.GetPeriod(6);
            typeof(FiscalPeriod).GetProperty("Id").SetValue(period, 6);
            foreach (var p in fy.Periods)
            {
                typeof(FiscalPeriod).GetProperty("FiscalYearId").SetValue(p, fy.Id);
            }

            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(entry.FiscalYearId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            // Act
            var result = await _sut.PostAsync(1, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("فعّالة") || e.Contains("Active"));
        }

        [Fact]
        public async Task PostAsync_WhenAccountNotPostable_ReturnsFailure()
        {
            // Arrange
            var journalDate = new DateTime(2024, 6, 15);
            var entry = CreateDraftEntry(id: 1, date: journalDate);

            _journalEntryRepoMock
                .Setup(r => r.GetWithLinesAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entry);

            // Setup active FY with open period
            var fy = CreateActiveFiscalYear(2024);
            var period = fy.GetPeriod(6);
            typeof(FiscalPeriod).GetProperty("Id").SetValue(period, 6);
            foreach (var p in fy.Periods)
            {
                typeof(FiscalPeriod).GetProperty("FiscalYearId").SetValue(p, fy.Id);
            }

            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(entry.FiscalYearId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            // Account 100 is not postable (level 1)
            var nonPostableAccount = new Account("1000", "حساب", "Account", AccountType.Asset, null, 1, false, "SAR");
            typeof(Account).GetProperty("Id").SetValue(nonPostableAccount, 100);
            _accountRepoMock
                .Setup(r => r.GetByIdAsync(100, It.IsAny<CancellationToken>()))
                .ReturnsAsync(nonPostableAccount);

            // Account 200 is fine
            SetupAccountsPostable(200);

            // Act
            var result = await _sut.PostAsync(1, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("لا يقبل الترحيل"));
        }

        [Fact]
        public async Task PostAsync_WithValidEntry_PostsSuccessfully()
        {
            // Arrange
            var journalDate = new DateTime(2024, 6, 15);
            var entry = CreateDraftEntry(id: 1, date: journalDate);

            _journalEntryRepoMock
                .Setup(r => r.GetWithLinesAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entry);

            // Setup active FY with open period
            var fy = CreateActiveFiscalYear(2024);
            var period = fy.GetPeriod(6);
            typeof(FiscalPeriod).GetProperty("Id").SetValue(period, 6);
            foreach (var p in fy.Periods)
            {
                typeof(FiscalPeriod).GetProperty("FiscalYearId").SetValue(p, fy.Id);
            }

            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(entry.FiscalYearId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fy);

            // Setup all accounts as postable
            SetupAccountsPostable(100, 200);

            // Setup number generator
            _numberGeneratorMock
                .Setup(n => n.NextNumberAsync(fy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync("JV-2024-00001");

            // Act
            var result = await _sut.PostAsync(1, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.JournalNumber.Should().Be("JV-2024-00001");

            entry.Status.Should().Be(JournalEntryStatus.Posted);
            entry.JournalNumber.Should().Be("JV-2024-00001");

            _journalEntryRepoMock.Verify(r => r.Update(entry), Times.Once);
            _auditLoggerMock.Verify(
                a => a.LogAsync("JournalEntry", 1, "Posted",
                    "admin", It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // ══════════════════════════════════════════════════════════
        // ReverseAsync (3)
        // ══════════════════════════════════════════════════════════

        [Fact]
        public async Task ReverseAsync_WhenOriginalNotFound_ReturnsFailure()
        {
            // Arrange
            _journalEntryRepoMock
                .Setup(r => r.GetWithLinesAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((JournalEntry)null);

            var dto = new ReverseJournalEntryDto
            {
                JournalEntryId = 999,
                ReversalReason = "خطأ",
                ReversalDate = new DateTime(2024, 6, 20)
            };

            // Act
            var result = await _sut.ReverseAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("غير موجود"));
        }

        [Fact]
        public async Task ReverseAsync_WhenReversalYearInactive_ReturnsFailure()
        {
            // Arrange
            var originalEntry = CreatePostedEntry(id: 1, date: new DateTime(2024, 6, 15));

            _journalEntryRepoMock
                .Setup(r => r.GetWithLinesAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalEntry);

            // Reversal year is not active (Setup status)
            var reversalFy = new FiscalYear(2024);
            typeof(FiscalYear).GetProperty("Id").SetValue(reversalFy, 1);

            _fiscalYearRepoMock
                .Setup(r => r.GetByYearAsync(2024, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reversalFy);

            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(reversalFy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reversalFy);

            var dto = new ReverseJournalEntryDto
            {
                JournalEntryId = 1,
                ReversalReason = "تصحيح خطأ",
                ReversalDate = new DateTime(2024, 6, 20)
            };

            // Act
            var result = await _sut.ReverseAsync(dto, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("فعّالة") || e.Contains("Active"));
        }

        [Fact]
        public async Task ReverseAsync_WithValidData_ReturnsSuccess()
        {
            // Arrange
            var originalEntry = CreatePostedEntry(id: 1, date: new DateTime(2024, 6, 15));

            _journalEntryRepoMock
                .Setup(r => r.GetWithLinesAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalEntry);

            // Setup active reversal FY with open period
            var reversalFy = CreateActiveFiscalYear(2024);
            foreach (var p in reversalFy.Periods)
            {
                typeof(FiscalPeriod).GetProperty("FiscalYearId").SetValue(p, reversalFy.Id);
            }

            _fiscalYearRepoMock
                .Setup(r => r.GetByYearAsync(2024, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reversalFy);

            _fiscalYearRepoMock
                .Setup(r => r.GetWithPeriodsAsync(reversalFy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reversalFy);

            _numberGeneratorMock
                .Setup(n => n.NextNumberAsync(reversalFy.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync("JV-2024-00002");

            var dto = new ReverseJournalEntryDto
            {
                JournalEntryId = 1,
                ReversalReason = "تصحيح خطأ",
                ReversalDate = new DateTime(2024, 6, 20)
            };

            // Act
            var result = await _sut.ReverseAsync(dto, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data.JournalNumber.Should().Be("JV-2024-00002");

            originalEntry.Status.Should().Be(JournalEntryStatus.Reversed);

            _journalEntryRepoMock.Verify(
                r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
                Times.Once);

            _journalEntryRepoMock.Verify(r => r.Update(originalEntry), Times.Once);
        }

        // ══════════════════════════════════════════════════════════
        // DeleteDraftAsync (2)
        // ══════════════════════════════════════════════════════════

        [Fact]
        public async Task DeleteDraftAsync_WhenNotFound_ReturnsFailure()
        {
            // Arrange
            _journalEntryRepoMock
                .Setup(r => r.GetWithLinesAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((JournalEntry)null);

            // Act
            var result = await _sut.DeleteDraftAsync(999, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Contains("غير موجود"));
        }

        [Fact]
        public async Task DeleteDraftAsync_WithDraftEntry_ReturnsSuccess()
        {
            // Arrange
            var entry = CreateDraftEntry(id: 10);
            _journalEntryRepoMock
                .Setup(r => r.GetWithLinesAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entry);

            // Act
            var result = await _sut.DeleteDraftAsync(10, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            entry.IsDeleted.Should().BeTrue();

            _journalEntryRepoMock.Verify(r => r.Update(entry), Times.Once);
            _auditLoggerMock.Verify(
                a => a.LogAsync("JournalEntry", 10, "Deleted",
                    "admin", It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
