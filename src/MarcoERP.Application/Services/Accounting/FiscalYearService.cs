using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Accounting;
using MarcoERP.Application.Mappers.Accounting;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Accounting;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Settings;
using MarcoERP.Application.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Accounting
{
    /// <summary>
    /// Application service for fiscal year and period management.
    /// Handles year lifecycle (Setup→Active→Closed) and period lock/unlock.
    /// Enforces cross-aggregate invariants that the domain cannot check alone.
    /// </summary>
    [Module(SystemModule.Accounting)]
    public sealed class FiscalYearService : IFiscalYearService
    {
        private readonly IFiscalYearRepository _fiscalYearRepository;
        private readonly IJournalEntryRepository _journalEntryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IAuditLogger _auditLogger;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IValidator<CreateFiscalYearDto> _createValidator;
        private readonly IYearEndClosingService _yearEndClosing;
        private readonly ISystemSettingRepository _systemSettingRepository;
        private readonly ILogger<FiscalYearService> _logger;
        private readonly IFeatureService _featureService;

        public FiscalYearService(
            IFiscalYearRepository fiscalYearRepository,
            IJournalEntryRepository journalEntryRepository,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IAuditLogger auditLogger,
            IDateTimeProvider dateTimeProvider,
            IValidator<CreateFiscalYearDto> createValidator,
            IYearEndClosingService yearEndClosing,
            ISystemSettingRepository systemSettingRepository = null,
            ILogger<FiscalYearService> logger = null,
            IFeatureService featureService = null)
        {
            _fiscalYearRepository = fiscalYearRepository ?? throw new ArgumentNullException(nameof(fiscalYearRepository));
            _journalEntryRepository = journalEntryRepository ?? throw new ArgumentNullException(nameof(journalEntryRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _yearEndClosing = yearEndClosing ?? throw new ArgumentNullException(nameof(yearEndClosing));
            _systemSettingRepository = systemSettingRepository;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<FiscalYearService>.Instance;
            _featureService = featureService;
        }

        // ── Queries ─────────────────────────────────────────────

        public async Task<ServiceResult<FiscalYearDto>> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            var year = await _fiscalYearRepository.GetWithPeriodsAsync(id, cancellationToken);
            if (year == null)
                return ServiceResult<FiscalYearDto>.Failure("السنة المالية غير موجودة.");

            return ServiceResult<FiscalYearDto>.Success(FiscalYearMapper.ToDto(year));
        }

        public async Task<ServiceResult<FiscalYearDto>> GetByYearAsync(int year, CancellationToken cancellationToken)
        {
            var fiscalYear = await _fiscalYearRepository.GetByYearAsync(year, cancellationToken);
            if (fiscalYear == null)
                return ServiceResult<FiscalYearDto>.Failure($"لا توجد سنة مالية للعام {year}.");

            // Load periods
            var withPeriods = await _fiscalYearRepository.GetWithPeriodsAsync(fiscalYear.Id, cancellationToken);
            return ServiceResult<FiscalYearDto>.Success(FiscalYearMapper.ToDto(withPeriods));
        }

        public async Task<ServiceResult<FiscalYearDto>> GetActiveYearAsync(CancellationToken cancellationToken)
        {
            var activeYear = await _fiscalYearRepository.GetActiveYearAsync(cancellationToken);
            if (activeYear == null)
                return ServiceResult<FiscalYearDto>.Failure("لا توجد سنة مالية فعّالة.");

            var withPeriods = await _fiscalYearRepository.GetWithPeriodsAsync(activeYear.Id, cancellationToken);
            return ServiceResult<FiscalYearDto>.Success(FiscalYearMapper.ToDto(withPeriods));
        }

        public async Task<ServiceResult<IReadOnlyList<FiscalYearDto>>> GetAllAsync(CancellationToken cancellationToken)
        {
            var years = await _fiscalYearRepository.GetAllAsync(cancellationToken);
            var dtos = new List<FiscalYearDto>();
            foreach (var year in years)
            {
                var withPeriods = await _fiscalYearRepository.GetWithPeriodsAsync(year.Id, cancellationToken);
                dtos.Add(FiscalYearMapper.ToDto(withPeriods));
            }
            return ServiceResult<IReadOnlyList<FiscalYearDto>>.Success(dtos);
        }

        // ── Fiscal Year Commands ────────────────────────────────

        /// <summary>
        /// Creates a new fiscal year.
        /// Steps:
        /// 1. Validate DTO
        /// 2. Check year doesn't already exist (FY-INV-05)
        /// 3. Create domain entity (auto-creates 12 periods)
        /// 4. Persist and audit
        /// </summary>
        public async Task<ServiceResult<FiscalYearDto>> CreateAsync(
            CreateFiscalYearDto dto, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "FiscalYear", 0);
            // Feature Guard — block operation if Accounting module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<FiscalYearDto>(_featureService, FeatureKeys.Accounting, cancellationToken);
                if (guard != null) return guard;
            }

            // Defense-in-depth: auth guard
            if (!_currentUser.IsAuthenticated)
                return ServiceResult<FiscalYearDto>.Failure("يجب تسجيل الدخول أولاً.");
            if (!_currentUser.HasPermission(PermissionKeys.FiscalYearManage))
                return ServiceResult<FiscalYearDto>.Failure("لا تملك الصلاحية لتنفيذ هذه العملية.");

            // Step 1: Validate
            var validationResult = await _createValidator.ValidateAsync(dto, cancellationToken);
            if (!validationResult.IsValid)
                return ServiceResult<FiscalYearDto>.Failure(validationResult.Errors.Select(e => e.ErrorMessage));

            // Step 2: Check uniqueness (FY-INV-05)
            var exists = await _fiscalYearRepository.YearExistsAsync(dto.Year, cancellationToken);
            if (exists)
                return ServiceResult<FiscalYearDto>.Failure($"السنة المالية {dto.Year} موجودة بالفعل.");

            // Step 3: Create domain entity (12 periods auto-created in constructor)
            FiscalYear fiscalYear;
            try
            {
                fiscalYear = new FiscalYear(dto.Year);
            }
            catch (FiscalYearDomainException ex)
            {
                return ServiceResult<FiscalYearDto>.Failure(ex.Message);
            }

            // Step 4: Persist + audit in one transaction
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _fiscalYearRepository.AddAsync(fiscalYear, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _auditLogger.LogAsync(
                    "FiscalYear", fiscalYear.Id, "Created",
                    _currentUser.Username,
                    $"Fiscal year {dto.Year} created with 12 periods.",
                    cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, IsolationLevel.ReadCommitted, cancellationToken);

            return ServiceResult<FiscalYearDto>.Success(FiscalYearMapper.ToDto(fiscalYear));
        }

        /// <summary>
        /// Activates a fiscal year.
        /// FY-INV-03: Only ONE year can be active at a time.
        /// Application must verify no other active year exists.
        /// </summary>
        public async Task<ServiceResult> ActivateAsync(int fiscalYearId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ActivateAsync", "FiscalYear", fiscalYearId);
            var fiscalYear = await _fiscalYearRepository.GetWithPeriodsAsync(fiscalYearId, cancellationToken);
            if (fiscalYear == null)
                return ServiceResult.Failure("السنة المالية غير موجودة.");

            // FY-INV-03: Check no other active year exists
            var currentActive = await _fiscalYearRepository.GetActiveYearAsync(cancellationToken);
            if (currentActive != null && currentActive.Id != fiscalYearId)
                return ServiceResult.Failure(
                    $"لا يمكن تفعيل هذه السنة — السنة المالية {currentActive.Year} فعّالة بالفعل. يجب إغلاقها أولاً.");

            try
            {
                fiscalYear.Activate();
            }
            catch (FiscalYearDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                _fiscalYearRepository.Update(fiscalYear);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _auditLogger.LogAsync(
                    "FiscalYear", fiscalYear.Id, "Activated",
                    _currentUser.Username,
                    $"Fiscal year {fiscalYear.Year} activated.",
                    cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, IsolationLevel.Serializable, cancellationToken);

            return ServiceResult.Success();
        }

        /// <summary>
        /// Closes a fiscal year.
        /// Preconditions verified by Application layer:
        /// - FY-INV-06: All 12 periods must be Locked (domain also checks)
        /// - FY-INV-07: No pending draft journal entries in this year
        /// - FY-INV-08: Closure is irreversible
        /// </summary>
        public async Task<ServiceResult> CloseAsync(int fiscalYearId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CloseAsync", "FiscalYear", fiscalYearId);
            var fiscalYear = await _fiscalYearRepository.GetWithPeriodsAsync(fiscalYearId, cancellationToken);
            if (fiscalYear == null)
                return ServiceResult.Failure("السنة المالية غير موجودة.");

            // Application-layer check: no pending drafts
            var pendingDrafts = await _journalEntryRepository.GetDraftsByYearAsync(fiscalYearId, cancellationToken);
            if (pendingDrafts.Count > 0)
                return ServiceResult.Failure(
                    $"لا يمكن إغلاق السنة المالية — يوجد {pendingDrafts.Count} مسودة قيد(قيود) معلقة.");

            // FY-07: Trial balance check — verify Total Debits = Total Credits
            var postedLines = await _journalEntryRepository.GetPostedLinesByYearAsync(fiscalYearId, cancellationToken);
            var totalDebits = postedLines.Sum(l => l.DebitAmount);
            var totalCredits = postedLines.Sum(l => l.CreditAmount);
            if (totalDebits != totalCredits)
            {
                var diff = totalDebits - totalCredits;
                return ServiceResult.Failure(
                    $"لا يمكن إغلاق السنة المالية — ميزان المراجعة غير متوازن. " +
                    $"إجمالي المدين: {totalDebits:N2} | إجمالي الدائن: {totalCredits:N2} | الفرق: {diff:N2}");
            }

            // Validate all periods are locked BEFORE generating closing entry
            if (fiscalYear.Periods.Any(p => p.IsOpen))
                return ServiceResult.Failure("يجب قفل جميع الفترات المحاسبية قبل إقفال السنة المالية.");

            // FY-02: Closing entry generation and year close must be atomic.
            // Both operations are wrapped in a single transaction so that if
            // either fails, the entire operation rolls back.
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Generate year-end closing journal (Revenue/Expense -> Retained Earnings)
                    var closingResult = await _yearEndClosing.GenerateClosingEntryAsync(fiscalYearId, cancellationToken);
                    if (!closingResult.IsSuccess)
                        throw new FiscalYearDomainException(closingResult.ErrorMessage);

                    fiscalYear.Close(_currentUser.Username, _dateTimeProvider.UtcNow);

                    _fiscalYearRepository.Update(fiscalYear);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    await _auditLogger.LogAsync(
                        "FiscalYear", fiscalYear.Id, "Closed",
                        _currentUser.Username,
                        $"Fiscal year {fiscalYear.Year} closed permanently. Year-end closing entry generated.",
                        cancellationToken);

                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }, IsolationLevel.Serializable, cancellationToken);
            }
            catch (FiscalYearDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }

            return ServiceResult.Success();
        }

        // ── Period Commands ─────────────────────────────────────

        /// <summary>
        /// Locks a fiscal period.
        /// FP-INV-03: Sequential locking — all prior periods must already be locked.
        /// FP-INV-04: No pending drafts in this period.
        /// </summary>
        public async Task<ServiceResult> LockPeriodAsync(int fiscalPeriodId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "LockPeriodAsync", "FiscalPeriod", fiscalPeriodId);
            // Find the period by loading all years and checking their periods
            // NOTE: In a full implementation, we'd have a IFiscalPeriodRepository.
            // For now, we load via fiscal year.
            var period = await FindPeriodByIdAsync(fiscalPeriodId, cancellationToken);
            if (period == null)
                return ServiceResult.Failure("الفترة المالية غير موجودة.");

            var fiscalYear = await _fiscalYearRepository.GetWithPeriodsAsync(period.FiscalYearId, cancellationToken);
            if (fiscalYear == null)
                return ServiceResult.Failure("السنة المالية غير موجودة.");

            // FIX: Use the tracked period from fiscalYear.Periods to ensure
            // mutations are persisted (avoids Instance A/B detachment bug).
            var trackedPeriod = fiscalYear.Periods.FirstOrDefault(p => p.Id == fiscalPeriodId);
            if (trackedPeriod == null)
                return ServiceResult.Failure("الفترة المالية غير موجودة في السنة المالية.");

            // FP-INV-03: Sequential locking — all previous periods must be locked
            var allPeriods = fiscalYear.Periods.OrderBy(p => p.PeriodNumber).ToList();
            foreach (var priorPeriod in allPeriods)
            {
                if (priorPeriod.PeriodNumber >= trackedPeriod.PeriodNumber)
                    break;

                if (priorPeriod.Status != PeriodStatus.Locked)
                    return ServiceResult.Failure(
                        $"يجب قفل الفترة {priorPeriod.PeriodNumber} ({priorPeriod.Month}/{priorPeriod.Year}) قبل قفل الفترة {trackedPeriod.PeriodNumber}.");
            }

            // FP-INV-04: No pending drafts in this period
            var draftsInPeriod = await _journalEntryRepository.GetByPeriodAsync(fiscalPeriodId, cancellationToken);
            var pendingDrafts = draftsInPeriod.Where(e => e.Status == JournalEntryStatus.Draft).ToList();
            if (pendingDrafts.Count > 0)
                return ServiceResult.Failure(
                    $"لا يمكن قفل الفترة — يوجد {pendingDrafts.Count} مسودة قيد(قيود) معلقة في هذه الفترة.");

            try
            {
                trackedPeriod.Lock(_currentUser.Username, _dateTimeProvider.UtcNow);
            }
            catch (FiscalPeriodDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                _fiscalYearRepository.Update(fiscalYear);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _auditLogger.LogAsync(
                    "FiscalPeriod", trackedPeriod.Id, "Locked",
                    _currentUser.Username,
                    $"Period {trackedPeriod.PeriodNumber}/{trackedPeriod.Year} locked.",
                    cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, IsolationLevel.Serializable, cancellationToken);

            return ServiceResult.Success();
        }

        /// <summary>
        /// Unlocks a fiscal period (admin-only).
        /// FP-INV-05: Only the most recent locked period can be unlocked.
        /// PER-06: Mandatory justification note.
        /// </summary>
        public async Task<ServiceResult> UnlockPeriodAsync(
            int fiscalPeriodId, string reason, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UnlockPeriodAsync", "FiscalPeriod", fiscalPeriodId);
            if (string.IsNullOrWhiteSpace(reason))
                return ServiceResult.Failure("سبب فتح الفترة مطلوب — يتم تسجيله في سجل المراجعة.");

            var isProductionMode = await ProductionHardening.IsProductionModeAsync(_systemSettingRepository, cancellationToken);
            if (isProductionMode)
                return ServiceResult.Failure("لا يمكن فتح الفترات المغلقة في وضع الإنتاج.");

            var period = await FindPeriodByIdAsync(fiscalPeriodId, cancellationToken);
            if (period == null)
                return ServiceResult.Failure("الفترة المالية غير موجودة.");

            var fiscalYear = await _fiscalYearRepository.GetWithPeriodsAsync(period.FiscalYearId, cancellationToken);
            if (fiscalYear == null)
                return ServiceResult.Failure("السنة المالية غير موجودة.");

            // FIX: Use the tracked period from fiscalYear.Periods to ensure
            // mutations are persisted (avoids Instance A/B detachment bug).
            var trackedPeriod = fiscalYear.Periods.FirstOrDefault(p => p.Id == fiscalPeriodId);
            if (trackedPeriod == null)
                return ServiceResult.Failure("الفترة المالية غير موجودة في السنة المالية.");

            // Cannot unlock periods in a closed year
            if (fiscalYear.Status == FiscalYearStatus.Closed)
                return ServiceResult.Failure("لا يمكن فتح فترة في سنة مالية مُغلقة.");

            // FP-INV-05: Only the most recent locked period can be unlocked
            var lockedPeriods = fiscalYear.Periods
                .Where(p => p.Status == PeriodStatus.Locked)
                .OrderByDescending(p => p.PeriodNumber)
                .ToList();

            if (lockedPeriods.Count == 0)
                return ServiceResult.Failure("لا توجد فترات مُقفلة.");

            var mostRecentLocked = lockedPeriods.First();
            if (mostRecentLocked.Id != fiscalPeriodId)
                return ServiceResult.Failure(
                    $"يمكن فتح آخر فترة مُقفلة فقط (الفترة {mostRecentLocked.PeriodNumber}).");

            try
            {
                trackedPeriod.Unlock(reason);
            }
            catch (FiscalPeriodDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                _fiscalYearRepository.Update(fiscalYear);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _auditLogger.LogAsync(
                    "FiscalPeriod", trackedPeriod.Id, "Unlocked",
                    _currentUser.Username,
                    $"Period {trackedPeriod.PeriodNumber}/{trackedPeriod.Year} unlocked. Reason: {reason}",
                    cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, IsolationLevel.Serializable, cancellationToken);

            return ServiceResult.Success();
        }

        // ── Private Helpers ─────────────────────────────────────

        /// <summary>
        /// Finds a FiscalPeriod by its Id by iterating through all fiscal years.
        /// In a production system, this would use a dedicated IFiscalPeriodRepository.
        /// </summary>
        private async Task<FiscalPeriod> FindPeriodByIdAsync(int periodId, CancellationToken cancellationToken)
        {
            var allYears = await _fiscalYearRepository.GetAllAsync(cancellationToken);
            foreach (var year in allYears)
            {
                var withPeriods = await _fiscalYearRepository.GetWithPeriodsAsync(year.Id, cancellationToken);
                var period = withPeriods.Periods.FirstOrDefault(p => p.Id == periodId);
                if (period != null)
                    return period;
            }
            return null;
        }
    }
}
