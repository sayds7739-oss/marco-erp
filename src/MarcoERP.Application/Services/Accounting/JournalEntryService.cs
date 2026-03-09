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
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Enums;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Exceptions.Accounting;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Accounting
{
    /// <summary>
    /// Application service for journal entry management.
    /// Implements the 15-step posting workflow (Section 3.5).
    /// Application-layer validates JE-INV-07 through JE-INV-10.
    /// Domain-layer validates JE-INV-01 through JE-INV-06, JE-INV-11 through JE-INV-13.
    /// </summary>
    [Module(SystemModule.Accounting)]
    public sealed class JournalEntryService : IJournalEntryService
    {
        private readonly IJournalEntryRepository _journalEntryRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IFiscalYearRepository _fiscalYearRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJournalNumberGenerator _numberGenerator;
        private readonly ICurrentUserService _currentUser;
        private readonly IAuditLogger _auditLogger;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IValidator<CreateJournalEntryDto> _createValidator;
        private readonly ISystemSettingRepository _systemSettingRepository;
        private readonly ILogger<JournalEntryService> _logger;
        private readonly IFeatureService _featureService;

        public JournalEntryService(
            IJournalEntryRepository journalEntryRepository,
            IAccountRepository accountRepository,
            IFiscalYearRepository fiscalYearRepository,
            IUnitOfWork unitOfWork,
            IJournalNumberGenerator numberGenerator,
            ICurrentUserService currentUser,
            IAuditLogger auditLogger,
            IDateTimeProvider dateTimeProvider,
            IValidator<CreateJournalEntryDto> createValidator,
            ISystemSettingRepository systemSettingRepository = null,
            ILogger<JournalEntryService> logger = null,
            IFeatureService featureService = null)
        {
            _journalEntryRepository = journalEntryRepository ?? throw new ArgumentNullException(nameof(journalEntryRepository));
            _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
            _fiscalYearRepository = fiscalYearRepository ?? throw new ArgumentNullException(nameof(fiscalYearRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _numberGenerator = numberGenerator ?? throw new ArgumentNullException(nameof(numberGenerator));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _systemSettingRepository = systemSettingRepository;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<JournalEntryService>.Instance;
            _featureService = featureService;
        }

        // ── Queries ─────────────────────────────────────────────

        public async Task<ServiceResult<JournalEntryDto>> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            var entry = await _journalEntryRepository.GetWithLinesAsync(id, cancellationToken);
            if (entry == null)
                return ServiceResult<JournalEntryDto>.Failure("القيد غير موجود.");

            return ServiceResult<JournalEntryDto>.Success(JournalEntryMapper.ToDto(entry));
        }

        public async Task<ServiceResult<IReadOnlyList<JournalEntryDto>>> GetByPeriodAsync(
            int fiscalPeriodId, CancellationToken cancellationToken)
        {
            var entries = await _journalEntryRepository.GetByPeriodAsync(fiscalPeriodId, cancellationToken);
            var dtos = entries.Select(JournalEntryMapper.ToDto).ToList();
            return ServiceResult<IReadOnlyList<JournalEntryDto>>.Success(dtos);
        }

        public async Task<ServiceResult<IReadOnlyList<JournalEntryDto>>> GetByStatusAsync(
            JournalEntryStatus status, CancellationToken cancellationToken)
        {
            var entries = await _journalEntryRepository.GetByStatusAsync(status, cancellationToken);
            var dtos = entries.Select(JournalEntryMapper.ToDto).ToList();
            return ServiceResult<IReadOnlyList<JournalEntryDto>>.Success(dtos);
        }

        public async Task<ServiceResult<IReadOnlyList<JournalEntryDto>>> GetDraftsByYearAsync(
            int fiscalYearId, CancellationToken cancellationToken)
        {
            var entries = await _journalEntryRepository.GetDraftsByYearAsync(fiscalYearId, cancellationToken);
            var dtos = entries.Select(JournalEntryMapper.ToDto).ToList();
            return ServiceResult<IReadOnlyList<JournalEntryDto>>.Success(dtos);
        }

        public async Task<ServiceResult<IReadOnlyList<JournalEntryDto>>> GetByDateRangeAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
        {
            var entries = await _journalEntryRepository.GetByDateRangeAsync(startDate, endDate, cancellationToken);
            var dtos = entries.Select(JournalEntryMapper.ToDto).ToList();
            return ServiceResult<IReadOnlyList<JournalEntryDto>>.Success(dtos);
        }

        // ── Commands ────────────────────────────────────────────

        /// <summary>
        /// Creates a new draft journal entry.
        /// Steps:
        /// 1. Validate DTO format (FluentValidation)
        /// 2. Resolve fiscal year and period from JournalDate
        /// 3. Verify year is Active (JE-INV-10)
        /// 4. Verify period is Open (JE-INV-09)
        /// 5. Validate all accounts are postable (JE-INV-06)
        /// 6. Create domain entity + add lines
        /// 7. Persist and audit
        /// </summary>
        public async Task<ServiceResult<JournalEntryDto>> CreateDraftAsync(
            CreateJournalEntryDto dto, CancellationToken cancellationToken)
        {
            // Defense-in-depth: auth guard
            if (!_currentUser.IsAuthenticated)
                return ServiceResult<JournalEntryDto>.Failure("يجب تسجيل الدخول أولاً.");
            if (!_currentUser.HasPermission(PermissionKeys.JournalCreate))
                return ServiceResult<JournalEntryDto>.Failure("لا تملك الصلاحية لتنفيذ هذه العملية.");

            // Feature Guard — block operation if Accounting module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<JournalEntryDto>(_featureService, FeatureKeys.Accounting, cancellationToken);
                if (guard != null) return guard;
            }

            _logger.LogInformation(
                "CreateDraftAsync started for {Entity} operation {Operation}.",
                nameof(JournalEntry),
                "Create");

            // Step 1: DTO format validation
            var validationResult = await _createValidator.ValidateAsync(dto, cancellationToken);
            if (!validationResult.IsValid)
                return ServiceResult<JournalEntryDto>.Failure(validationResult.Errors.Select(e => e.ErrorMessage));

            // Step 2: Resolve fiscal year from JournalDate
            var fiscalYear = await _fiscalYearRepository.GetByYearAsync(dto.JournalDate.Year, cancellationToken);
            if (fiscalYear == null)
                return ServiceResult<JournalEntryDto>.Failure(
                    $"لا توجد سنة مالية للعام {dto.JournalDate.Year}.");

            // Load full year with periods
            fiscalYear = await _fiscalYearRepository.GetWithPeriodsAsync(fiscalYear.Id, cancellationToken);

            // Step 3: JE-INV-10 — Fiscal year must be Active
            if (fiscalYear.Status != FiscalYearStatus.Active)
                return ServiceResult<JournalEntryDto>.Failure(
                    $"السنة المالية {fiscalYear.Year} ليست في حالة فعّالة. الحالة الحالية: {fiscalYear.Status}.");

            // Step 4: Resolve fiscal period and verify Open (JE-INV-09)
            var period = fiscalYear.GetPeriod(dto.JournalDate.Month);
            if (period == null)
                return ServiceResult<JournalEntryDto>.Failure(
                    $"لا توجد فترة مالية تحتوي على التاريخ {dto.JournalDate:yyyy-MM-dd}.");

            if (!period.IsOpen)
                return ServiceResult<JournalEntryDto>.Failure(
                    $"الفترة المالية ({period.PeriodNumber}/{period.Year}) مُقفلة.");

            // Step 5: Validate all line accounts are postable (JE-INV-06)
            var accountValidationErrors = await ValidateLineAccountsAsync(dto.Lines, cancellationToken);
            if (accountValidationErrors.Count > 0)
                return ServiceResult<JournalEntryDto>.Failure(accountValidationErrors);

            // Step 6: Create domain entity
            JournalEntry entry;
            try
            {
                entry = JournalEntry.CreateDraft(
                    dto.JournalDate,
                    dto.Description,
                    dto.SourceType,
                    fiscalYear.Id,
                    period.Id,
                    dto.ReferenceNumber,
                    dto.CostCenterId);

                // Add all lines
                foreach (var lineDto in dto.Lines)
                {
                    entry.AddLine(
                        lineDto.AccountId,
                        lineDto.DebitAmount,
                        lineDto.CreditAmount,
                        _dateTimeProvider.UtcNow,
                        lineDto.Description,
                        lineDto.CostCenterId,
                        lineDto.WarehouseId);
                }
            }
            catch (JournalEntryDomainException ex)
            {
                return ServiceResult<JournalEntryDto>.Failure(ex.Message);
            }

            // Step 7: Persist + audit in one transaction
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    await _journalEntryRepository.AddAsync(entry, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    await _auditLogger.LogAsync(
                        "JournalEntry", entry.Id, "DraftCreated",
                        _currentUser.Username,
                        $"Draft '{entry.DraftCode}' created with {entry.Lines.Count} lines. Total: {entry.TotalDebit:N2}.",
                        cancellationToken);

                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }, IsolationLevel.ReadCommitted, cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<JournalEntryDto>.Failure("تعذر حفظ القيد بسبب تعارض تزامن. الرجاء إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult<JournalEntryDto>.Failure("رقم/مرجع القيد مستخدم بالفعل. الرجاء إعادة المحاولة.");
            }

            return ServiceResult<JournalEntryDto>.Success(JournalEntryMapper.ToDto(entry));
        }

        /// <summary>
        /// Posts a draft journal entry — 15-step posting workflow (Section 3.5).
        /// 
        /// Step 1:  Receive command (journal entry ID)
        /// Step 2:  Load JournalEntry with all Lines
        /// Step 3:  Verify Status == Draft
        /// Step 4:  Load FiscalPeriod for JournalDate
        /// Step 5:  Verify FiscalPeriod.Status == Open (JE-INV-09)
        /// Step 6:  Load FiscalYear
        /// Step 7:  Verify FiscalYear.Status == Active (JE-INV-10)
        /// Step 8:  Call JournalEntry.Validate() (domain invariants JE-INV-01–06, 11–13)
        /// Step 9:  If validation fails → return errors
        /// Step 10: Check user authorization
        /// Step 11: Generate JournalNumber via IJournalNumberGenerator
        /// Step 12: Call JournalEntry.Post()
        /// Step 13: Persist via IUnitOfWork (single transaction)
        /// Step 14: Log to audit trail
        /// Step 15: Return PostResult
        /// </summary>
        public async Task<ServiceResult<PostJournalResultDto>> PostAsync(
            int journalEntryId, CancellationToken cancellationToken)
        {
            // Defense-in-depth: auth guard
            if (!_currentUser.IsAuthenticated)
                return ServiceResult<PostJournalResultDto>.Failure("يجب تسجيل الدخول أولاً.");
            if (!_currentUser.HasPermission(PermissionKeys.JournalPost))
                return ServiceResult<PostJournalResultDto>.Failure("لا تملك الصلاحية لتنفيذ هذه العملية.");

            _logger.LogInformation(
                "PostAsync started for {Entity}Id {EntityId} operation {Operation}.",
                nameof(JournalEntry),
                journalEntryId,
                "Post");

            // Step 1+2: Load with lines
            var entry = await _journalEntryRepository.GetWithLinesAsync(journalEntryId, cancellationToken);
            if (entry == null)
                return ServiceResult<PostJournalResultDto>.Failure("القيد غير موجود.");

            if (await ProductionHardening.IsProductionModeAsync(_systemSettingRepository, cancellationToken)
                && ProductionHardening.IsBackdated(entry.JournalDate, _dateTimeProvider.UtcNow))
            {
                return ServiceResult<PostJournalResultDto>.Failure("لا يمكن الترحيل بتاريخ سابق أثناء وضع الإنتاج.");
            }

            // Step 3: Must be Draft
            if (entry.Status != JournalEntryStatus.Draft)
                return ServiceResult<PostJournalResultDto>.Failure("يمكن ترحيل المسودات فقط.");

            // Step 4+5: Load and verify period is Open (JE-INV-09)
            var fiscalYear = await _fiscalYearRepository.GetWithPeriodsAsync(entry.FiscalYearId, cancellationToken);
            if (fiscalYear == null)
                return ServiceResult<PostJournalResultDto>.Failure("السنة المالية غير موجودة.");

            var period = fiscalYear.Periods.FirstOrDefault(p => p.Id == entry.FiscalPeriodId);
            if (period == null)
                return ServiceResult<PostJournalResultDto>.Failure("الفترة المالية غير موجودة.");

            if (!period.IsOpen)
                return ServiceResult<PostJournalResultDto>.Failure(
                    $"الفترة المالية ({period.PeriodNumber}/{period.Year}) مُقفلة — لا يمكن الترحيل.");

            // Step 6+7: Verify year is Active (JE-INV-10)
            if (fiscalYear.Status != FiscalYearStatus.Active)
                return ServiceResult<PostJournalResultDto>.Failure(
                    $"السنة المالية {fiscalYear.Year} ليست فعّالة — لا يمكن الترحيل.");

            // JE-INV-07: JournalDate must fall within fiscal year
            if (entry.JournalDate < fiscalYear.StartDate || entry.JournalDate > fiscalYear.EndDate)
                return ServiceResult<PostJournalResultDto>.Failure(
                    $"تاريخ القيد {entry.JournalDate:yyyy-MM-dd} لا يقع ضمن السنة المالية {fiscalYear.Year}.");

            // JE-INV-08: JournalDate must fall within the period
            if (!period.ContainsDate(entry.JournalDate))
                return ServiceResult<PostJournalResultDto>.Failure(
                    $"تاريخ القيد {entry.JournalDate:yyyy-MM-dd} لا يقع ضمن الفترة المالية {period.PeriodNumber}/{period.Year}.");

            // Validate all accounts are still postable at posting time
            var lineAccountIds = entry.Lines.Select(l => l.AccountId).Distinct().ToList();
            foreach (var accountId in lineAccountIds)
            {
                var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
                if (account == null)
                    return ServiceResult<PostJournalResultDto>.Failure($"حساب بالمعرّف {accountId} غير موجود.");

                if (!account.CanReceivePostings())
                    return ServiceResult<PostJournalResultDto>.Failure(
                        $"الحساب '{account.AccountCode} - {account.AccountNameAr}' لا يقبل الترحيل.");
            }

            // Step 8: Domain validation
            var domainErrors = entry.Validate();

            // Step 9: If validation fails, return errors
            if (domainErrors.Count > 0)
                return ServiceResult<PostJournalResultDto>.Failure(domainErrors);

            string journalNumber = null;

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    entry = await _journalEntryRepository.GetWithLinesAsync(journalEntryId, cancellationToken);
                    if (entry == null)
                        throw new JournalEntryDomainException("القيد غير موجود.");

                    if (entry.Status != JournalEntryStatus.Draft)
                        throw new JournalEntryDomainException("يمكن ترحيل المسودات فقط.");

                    // Step 11: Generate JournalNumber
                    journalNumber = await _numberGenerator.NextNumberAsync(entry.FiscalYearId, cancellationToken);

                    // Step 12: Post (domain method — sets Status, JournalNumber, PostedBy, PostingDate)
                    entry.Post(journalNumber, _currentUser.Username, _dateTimeProvider.UtcNow);

                    // Mark accounts as used
                    foreach (var accountId in lineAccountIds)
                    {
                        var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
                        if (account != null && !account.HasPostings)
                        {
                            account.MarkAsUsed();
                            _accountRepository.Update(account);
                        }
                    }

                    // Step 13: Persist via UnitOfWork (single transaction — Steps 11-14 atomic)
                    _journalEntryRepository.Update(entry);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    // Step 14: Audit log
                    await _auditLogger.LogAsync(
                        "JournalEntry", entry.Id, "Posted",
                        _currentUser.Username,
                        $"Journal '{journalNumber}' posted. Debit: {entry.TotalDebit:N2}, Credit: {entry.TotalCredit:N2}.",
                        cancellationToken);

                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }, IsolationLevel.Serializable, cancellationToken);
            }
            catch (JournalEntryDomainException ex)
            {
                return ServiceResult<PostJournalResultDto>.Failure(ex.Message);
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<PostJournalResultDto>.Failure("تعذر ترحيل القيد بسبب تعارض تزامن. الرجاء إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult<PostJournalResultDto>.Failure("تعذر ترحيل القيد بسبب تعارض في البيانات. الرجاء إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PostAsync failed for JournalEntry.");
                return ServiceResult<PostJournalResultDto>.Failure(
                    ErrorSanitizer.SanitizeGeneric(ex, "ترحيل القيد"));
            }

            // Step 15: Return result
            return ServiceResult<PostJournalResultDto>.Success(JournalEntryMapper.ToPostResult(entry));
        }

        /// <summary>
        /// Reverses a posted journal entry.
        /// Creates a reversal entry with all debit/credit swapped.
        /// Both the original (marked as Reversed) and the new reversal entry are persisted atomically.
        /// The reversal entry is auto-posted.
        /// </summary>
        public async Task<ServiceResult<PostJournalResultDto>> ReverseAsync(
            ReverseJournalEntryDto dto, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ReverseAsync", "JournalEntry", dto.JournalEntryId);

            // Defense-in-depth: auth guard
            if (!_currentUser.IsAuthenticated)
                return ServiceResult<PostJournalResultDto>.Failure("يجب تسجيل الدخول أولاً.");
            if (!_currentUser.HasPermission(PermissionKeys.JournalReverse))
                return ServiceResult<PostJournalResultDto>.Failure("لا تملك الصلاحية لتنفيذ هذه العملية.");

            // Load original entry
            var originalEntry = await _journalEntryRepository.GetWithLinesAsync(dto.JournalEntryId, cancellationToken);
            if (originalEntry == null)
                return ServiceResult<PostJournalResultDto>.Failure("القيد الأصلي غير موجود.");

            // Resolve reversal period
            var reversalYear = await _fiscalYearRepository.GetByYearAsync(dto.ReversalDate.Year, cancellationToken);
            if (reversalYear == null)
                return ServiceResult<PostJournalResultDto>.Failure(
                    $"لا توجد سنة مالية للعام {dto.ReversalDate.Year}.");

            reversalYear = await _fiscalYearRepository.GetWithPeriodsAsync(reversalYear.Id, cancellationToken);

            if (reversalYear.Status != FiscalYearStatus.Active)
                return ServiceResult<PostJournalResultDto>.Failure(
                    $"السنة المالية {reversalYear.Year} ليست فعّالة.");

            var reversalPeriod = reversalYear.GetPeriod(dto.ReversalDate.Month);
            if (reversalPeriod == null)
                return ServiceResult<PostJournalResultDto>.Failure(
                    $"لا توجد فترة مالية تحتوي على تاريخ العكس {dto.ReversalDate:yyyy-MM-dd}.");

            if (!reversalPeriod.IsOpen)
                return ServiceResult<PostJournalResultDto>.Failure(
                    $"الفترة المالية ({reversalPeriod.PeriodNumber}/{reversalPeriod.Year}) مُقفلة.");

            // Create reversal via domain method
            JournalEntry reversalEntry;
            try
            {
                reversalEntry = originalEntry.CreateReversal(
                    dto.ReversalDate,
                    dto.ReversalReason,
                    reversalYear.Id,
                    reversalPeriod.Id);
            }
            catch (JournalEntryDomainException ex)
            {
                return ServiceResult<PostJournalResultDto>.Failure(ex.Message);
            }

            string reversalNumber = null;

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Generate number and auto-post the reversal entry
                    reversalNumber = await _numberGenerator.NextNumberAsync(reversalYear.Id, cancellationToken);
                    reversalEntry.Post(reversalNumber, _currentUser.Username, _dateTimeProvider.UtcNow);

                    // Persist reversal entry first (to get its Id)
                    await _journalEntryRepository.AddAsync(reversalEntry, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    // Mark original as reversed (set ReversalEntryId)
                    originalEntry.MarkAsReversed(reversalEntry.Id);
                    _journalEntryRepository.Update(originalEntry);

                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    // Audit both
                    await _auditLogger.LogAsync(
                        "JournalEntry", originalEntry.Id, "Reversed",
                        _currentUser.Username,
                        $"Original journal reversed. Reversal entry: {reversalNumber}.",
                        cancellationToken);

                    await _auditLogger.LogAsync(
                        "JournalEntry", reversalEntry.Id, "Posted",
                        _currentUser.Username,
                        $"Reversal journal '{reversalNumber}' posted for original entry #{originalEntry.JournalNumber}.",
                        cancellationToken);

                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }, IsolationLevel.Serializable, cancellationToken);
            }
            catch (JournalEntryDomainException ex)
            {
                return ServiceResult<PostJournalResultDto>.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReverseAsync failed for JournalEntry.");
                return ServiceResult<PostJournalResultDto>.Failure(
                    ErrorSanitizer.SanitizeGeneric(ex, "عكس القيد"));
            }

            return ServiceResult<PostJournalResultDto>.Success(JournalEntryMapper.ToPostResult(reversalEntry));
        }

        public async Task<ServiceResult> DeleteDraftAsync(int journalEntryId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteDraftAsync", "JournalEntry", journalEntryId);

            var entry = await _journalEntryRepository.GetWithLinesAsync(journalEntryId, cancellationToken);
            if (entry == null)
                return ServiceResult.Failure("القيد غير موجود.");

            try
            {
                entry.SoftDelete(_currentUser.Username, _dateTimeProvider.UtcNow);
            }
            catch (JournalEntryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                _journalEntryRepository.Update(entry);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _auditLogger.LogAsync(
                    "JournalEntry", entry.Id, "Deleted",
                    _currentUser.Username,
                    $"Draft '{entry.DraftCode}' soft-deleted.",
                    cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, IsolationLevel.ReadCommitted, cancellationToken);

            return ServiceResult.Success();
        }

        // ── Private Helpers ─────────────────────────────────────

        /// <summary>
        /// Validates that all accounts referenced in journal lines are postable.
        /// JE-INV-06: Each line must reference a valid, active, leaf account.
        /// </summary>
        private async Task<List<string>> ValidateLineAccountsAsync(
            List<CreateJournalEntryLineDto> lines, CancellationToken cancellationToken)
        {
            var errors = new List<string>();
            var accountIds = lines.Select(l => l.AccountId).Distinct().ToList();

            foreach (var accountId in accountIds)
            {
                var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
                if (account == null)
                {
                    errors.Add($"حساب بالمعرّف {accountId} غير موجود.");
                    continue;
                }

                if (!account.CanReceivePostings())
                {
                    errors.Add(
                        $"الحساب '{account.AccountCode} - {account.AccountNameAr}' لا يقبل الترحيل " +
                        $"(IsActive={account.IsActive}, IsLeaf={account.IsLeaf}, AllowPosting={account.AllowPosting}).");
                }
            }

            return errors;
        }
    }
}
