using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Treasury;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Application.Mappers.Treasury;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Exceptions.Accounting;
using MarcoERP.Domain.Exceptions.Treasury;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Settings;
using MarcoERP.Domain.Interfaces.Treasury;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Treasury
{
    /// <summary>
    /// Application service for CashTransfer (تحويل بين الخزن) lifecycle.
    /// On Post: auto-generates journal entry:
    ///   DR: Target Cashbox GL Account (money arrives)
    ///   CR: Source Cashbox GL Account (money leaves)
    /// SourceType: CashTransfer (6)
    /// </summary>
    [Module(SystemModule.Treasury)]
    public sealed class CashTransferService : ICashTransferService
    {
        private readonly ICashTransferRepository _transferRepo;
        private readonly ICashboxRepository _cashboxRepo;
        private readonly IJournalEntryRepository _journalRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly IJournalNumberGenerator _journalNumberGen;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreateCashTransferDto> _createValidator;
        private readonly IValidator<UpdateCashTransferDto> _updateValidator;
        private readonly ILogger<CashTransferService> _logger;
        private readonly ISystemSettingRepository _systemSettingRepository;
        private readonly JournalEntryFactory _journalFactory;
        private readonly FiscalPeriodValidator _fiscalValidator;
        private readonly IFeatureService _featureService;
        private readonly IAuditLogger _auditLogger;
        private const string TransferNotFoundMessage = "التحويل غير موجود.";

        public CashTransferService(
            CashTransferRepositories repos,
            CashTransferServices services,
            CashTransferValidators validators,
            JournalEntryFactory journalFactory,
            FiscalPeriodValidator fiscalValidator,
            ILogger<CashTransferService> logger)
        {
            if (repos == null) throw new ArgumentNullException(nameof(repos));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (validators == null) throw new ArgumentNullException(nameof(validators));

            _transferRepo = repos.TransferRepo;
            _cashboxRepo = repos.CashboxRepo;
            _journalRepo = repos.JournalRepo;
            _accountRepo = repos.AccountRepo;

            _journalNumberGen = services.JournalNumberGen;
            _unitOfWork = services.UnitOfWork;
            _currentUser = services.CurrentUser;
            _dateTime = services.DateTime;
            _systemSettingRepository = services.SystemSettingRepo;
            _featureService = services.FeatureService;
            _auditLogger = services.AuditLogger;

            _createValidator = validators.CreateValidator;
            _updateValidator = validators.UpdateValidator;
            _journalFactory = journalFactory ?? throw new ArgumentNullException(nameof(journalFactory));
            _fiscalValidator = fiscalValidator ?? throw new ArgumentNullException(nameof(fiscalValidator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ══════════════════════════════════════════════════════════
        //  QUERIES
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<IReadOnlyList<CashTransferListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _transferRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<CashTransferListDto>>.Success(
                entities.Select(CashTransferMapper.ToListDto).ToList());
        }

        public async Task<ServiceResult<CashTransferDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _transferRepo.GetWithDetailsAsync(id, ct);
            if (entity == null)
                return ServiceResult<CashTransferDto>.Failure(TransferNotFoundMessage);

            return ServiceResult<CashTransferDto>.Success(CashTransferMapper.ToDto(entity));
        }

        public async Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default)
        {
            var next = await _transferRepo.GetNextNumberAsync(ct);
            return ServiceResult<string>.Success(next);
        }

        // ══════════════════════════════════════════════════════════
        //  CREATE (Draft)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<CashTransferDto>> CreateAsync(CreateCashTransferDto dto, CancellationToken ct = default)
        {
            // Feature Guard — block operation if Treasury module is disabled
            var guard = await FeatureGuard.CheckAsync<CashTransferDto>(_featureService, FeatureKeys.Treasury, ct);
            if (guard != null) return guard;

            _logger.LogInformation(
                "CreateAsync started for {Entity} operation {Operation}.",
                nameof(CashTransfer),
                "Create");

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<CashTransferDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            // Validate source cashbox
            var sourceCashbox = await _cashboxRepo.GetByIdAsync(dto.SourceCashboxId, ct);
            if (sourceCashbox == null)
                return ServiceResult<CashTransferDto>.Failure("خزنة المصدر غير موجودة.");
            if (!sourceCashbox.IsActive)
                return ServiceResult<CashTransferDto>.Failure("خزنة المصدر غير نشطة.");

            // Validate target cashbox
            var targetCashbox = await _cashboxRepo.GetByIdAsync(dto.TargetCashboxId, ct);
            if (targetCashbox == null)
                return ServiceResult<CashTransferDto>.Failure("خزنة الاستلام غير موجودة.");
            if (!targetCashbox.IsActive)
                return ServiceResult<CashTransferDto>.Failure("خزنة الاستلام غير نشطة.");

            try
            {
                var transferNumber = await _transferRepo.GetNextNumberAsync(ct);

                var transfer = new CashTransfer(
                    transferNumber,
                    dto.TransferDate,
                    dto.SourceCashboxId,
                    dto.TargetCashboxId,
                    dto.Amount,
                    dto.Description,
                    dto.Notes);

                await _transferRepo.AddAsync(transfer, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _transferRepo.GetWithDetailsAsync(transfer.Id, ct);
                return ServiceResult<CashTransferDto>.Success(CashTransferMapper.ToDto(saved));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<CashTransferDto>.Failure(ex.Message);
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<CashTransferDto>.Failure("تعذر حفظ التحويل بسبب تعارض تزامن. الرجاء إعادة التحميل والمحاولة مرة أخرى.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult<CashTransferDto>.Failure("رقم التحويل مستخدم بالفعل. الرجاء إعادة المحاولة.");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  UPDATE (Draft only)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<CashTransferDto>> UpdateAsync(UpdateCashTransferDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "CashTransfer", dto.Id);

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<CashTransferDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var transfer = await _transferRepo.GetWithDetailsTrackedAsync(dto.Id, ct);
            if (transfer == null)
                return ServiceResult<CashTransferDto>.Failure(TransferNotFoundMessage);

            if (transfer.Status != InvoiceStatus.Draft)
                return ServiceResult<CashTransferDto>.Failure("لا يمكن تعديل تحويل مرحّل أو ملغى.");

            try
            {
                transfer.UpdateHeader(
                    dto.TransferDate, dto.SourceCashboxId, dto.TargetCashboxId,
                    dto.Amount, dto.Description, dto.Notes);

                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _transferRepo.GetWithDetailsAsync(transfer.Id, ct);
                return ServiceResult<CashTransferDto>.Success(CashTransferMapper.ToDto(saved));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<CashTransferDto>.Failure(ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  POST — DR Target Cashbox GL / CR Source Cashbox GL
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Posts a draft cash transfer. Auto-generates journal entry:
        ///   DR: Target Cashbox GL Account (money arrives)
        ///   CR: Source Cashbox GL Account (money leaves)
        /// Wrapped in a Serializable transaction.
        /// </summary>
        public async Task<ServiceResult<CashTransferDto>> PostAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "PostAsync started for {Entity}Id {EntityId} operation {Operation}.",
                nameof(CashTransfer),
                id,
                "Post");

            var transfer = await _transferRepo.GetWithDetailsAsync(id, ct);
            var preCheck = ValidatePostPreconditions(transfer);
            if (preCheck != null) return preCheck;

            var allowNegativeCash = await IsNegativeCashAllowedAsync(ct);
            var warningMessages = new List<string>();

            CashTransfer saved = null;

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Use tracked query to avoid EF Core graph attachment conflicts
                    transfer = await _transferRepo.GetWithDetailsTrackedAsync(id, ct);
                    var txCheck = ValidatePostPreconditions(transfer);
                    if (txCheck != null)
                        throw new TreasuryDomainException(txCheck.ErrorMessage ?? "لا يمكن ترحيل التحويل.");

                    var accounts = await GetPostingAccountsAsync(transfer, ct);
                    var postingCtx = await _fiscalValidator.ValidateForPostingAsync(transfer.TransferDate, ct);

                    // CSH-03: Domain-level balance protection (inside Serializable tx)
                    if (allowNegativeCash && accounts.sourceCashbox.Balance < transfer.Amount)
                    {
                        var warning = $"Negative cash allowed for cashbox {accounts.sourceCashbox.NameAr}";
                        warningMessages.Add(warning);

                        if (_auditLogger != null)
                        {
                            var performedBy = _currentUser.Username ?? "System";
                            await _auditLogger.LogAsync(
                                "CashTransfer",
                                transfer.Id,
                                "RiskOperation",
                                performedBy,
                                warning,
                                ct);
                        }
                    }

                    var cashboxOrder = new[]
                    {
                        (cashbox: accounts.sourceCashbox, isSource: true),
                        (cashbox: accounts.targetCashbox, isSource: false)
                    }.OrderBy(x => x.cashbox.Id);

                    foreach (var entry in cashboxOrder)
                    {
                        if (entry.isSource)
                        {
                            if (allowNegativeCash)
                                entry.cashbox.DecreaseBalanceAllowNegative(transfer.Amount);
                            else
                                entry.cashbox.DecreaseBalance(transfer.Amount);
                        }
                        else
                        {
                            entry.cashbox.IncreaseBalance(transfer.Amount);
                        }

                        _cashboxRepo.Update(entry.cashbox);
                    }

                    var journalEntry = await CreateJournalEntryAsync(
                        transfer,
                        accounts,
                        postingCtx.FiscalYear,
                        postingCtx.Period,
                        postingCtx.Now,
                        ct);

                    await _unitOfWork.SaveChangesAsync(ct);

                    transfer.Post(journalEntry.Id);
                    // Entity is already tracked — no need for explicit Update
                    await _unitOfWork.SaveChangesAsync(ct);

                    saved = await _transferRepo.GetWithDetailsAsync(transfer.Id, ct);
                }, IsolationLevel.Serializable, ct);

                var dto = CashTransferMapper.ToDto(saved);
                if (warningMessages.Count > 0)
                    dto.WarningMessage = string.Join(" | ", warningMessages);
                return ServiceResult<CashTransferDto>.Success(dto);
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<CashTransferDto>.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while posting cash transfer.");
                return ServiceResult<CashTransferDto>.Failure(
                    ErrorSanitizer.Sanitize(ex, "ترحيل التحويل"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<CashTransferDto>.Failure("تعذر ترحيل التحويل بسبب تعارض تزامن. الرجاء إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult<CashTransferDto>.Failure("تعذر ترحيل التحويل بسبب تعارض في البيانات. الرجاء إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ غير متوقع أثناء ترحيل التحويل {TransferId}", id);
                return ServiceResult<CashTransferDto>.Failure("حدث خطأ غير متوقع أثناء ترحيل التحويل.");
            }
        }

        private static ServiceResult<CashTransferDto> ValidatePostPreconditions(CashTransfer transfer)
        {
            if (transfer == null)
                return ServiceResult<CashTransferDto>.Failure(TransferNotFoundMessage);

            if (transfer.Status != InvoiceStatus.Draft)
                return ServiceResult<CashTransferDto>.Failure("لا يمكن ترحيل تحويل مرحّل بالفعل أو ملغى.");

            return null;
        }

        private async Task<(Cashbox sourceCashbox, Cashbox targetCashbox, Account sourceAccount, Account targetAccount)>
            GetPostingAccountsAsync(CashTransfer transfer, CancellationToken ct)
        {
            var sourceCashbox = await _cashboxRepo.GetByIdAsync(transfer.SourceCashboxId, ct);
            if (sourceCashbox == null)
                throw new TreasuryDomainException("خزنة المصدر غير موجودة.");
            if (!sourceCashbox.AccountId.HasValue)
                throw new TreasuryDomainException(
                    "خزنة المصدر ليس لها حساب GL مرتبط. يجب ربط الخزنة بحساب أولاً.");

            var targetCashbox = await _cashboxRepo.GetByIdAsync(transfer.TargetCashboxId, ct);
            if (targetCashbox == null)
                throw new TreasuryDomainException("خزنة الاستلام غير موجودة.");
            if (!targetCashbox.AccountId.HasValue)
                throw new TreasuryDomainException(
                    "خزنة الاستلام ليس لها حساب GL مرتبط. يجب ربط الخزنة بحساب أولاً.");

            var sourceAccount = await _accountRepo.GetByIdAsync(sourceCashbox.AccountId.Value, ct);
            if (sourceAccount == null || !sourceAccount.CanReceivePostings())
                throw new TreasuryDomainException("حساب خزنة المصدر المرتبط غير صالح للترحيل.");

            var targetAccount = await _accountRepo.GetByIdAsync(targetCashbox.AccountId.Value, ct);
            if (targetAccount == null || !targetAccount.CanReceivePostings())
                throw new TreasuryDomainException("حساب خزنة الاستلام المرتبط غير صالح للترحيل.");

            return (sourceCashbox, targetCashbox, sourceAccount, targetAccount);
        }

        private async Task<JournalEntry> CreateJournalEntryAsync(
            CashTransfer transfer,
            (Cashbox sourceCashbox, Cashbox targetCashbox, Account sourceAccount, Account targetAccount) accounts,
            FiscalYear fiscalYear,
            FiscalPeriod period,
            DateTime now,
            CancellationToken ct)
        {
            var username = _currentUser.Username ?? "System";
            var lines = new[]
            {
                new JournalLineSpec(accounts.targetAccount.Id, transfer.Amount, 0,
                    $"تحويل وارد — {accounts.targetCashbox.NameAr}"),
                new JournalLineSpec(accounts.sourceAccount.Id, 0, transfer.Amount,
                    $"تحويل صادر — {accounts.sourceCashbox.NameAr}")
            };

            return await _journalFactory.CreateAndPostAsync(
                transfer.TransferDate,
                $"تحويل بين الخزن رقم {transfer.TransferNumber}",
                SourceType.CashTransfer,
                fiscalYear.Id,
                period.Id,
                lines,
                username,
                now,
                referenceNumber: transfer.TransferNumber,
                sourceId: transfer.Id,
                ct: ct);
        }

        // ══════════════════════════════════════════════════════════
        //  CANCEL
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "CancelAsync started for {Entity}Id {EntityId} operation {Operation}.",
                nameof(CashTransfer),
                id,
                "Cancel");

            var transfer = await _transferRepo.GetWithDetailsAsync(id, ct);
            if (transfer == null)
                return ServiceResult.Failure(TransferNotFoundMessage);

            if (transfer.Status != InvoiceStatus.Posted)
                return ServiceResult.Failure("لا يمكن إلغاء إلا التحويلات المرحّلة.");

            if (!transfer.JournalEntryId.HasValue)
                return ServiceResult.Failure("لا يمكن إلغاء تحويل بدون قيد محاسبي.");

            try
            {
                var cancelCtx = await _fiscalValidator.ValidateForCancelAsync(transfer.TransferDate, ct);

                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Reload as tracked inside the transaction to ensure fresh data
                    // and avoid stale-entity / EF graph-attachment issues (C-05 fix)
                    var tracked = await _transferRepo.GetWithDetailsTrackedAsync(id, ct);
                    if (tracked == null)
                        throw new TreasuryDomainException(TransferNotFoundMessage);
                    if (tracked.Status != InvoiceStatus.Posted)
                        throw new TreasuryDomainException("لا يمكن إلغاء إلا التحويلات المرحّلة.");

                    // ── Reverse journal entry ──
                    var originalJournal = await _journalRepo.GetWithLinesAsync(tracked.JournalEntryId.Value, ct);
                    if (originalJournal == null)
                        throw new TreasuryDomainException("القيد المحاسبي الأصلي غير موجود.");

                    var reversalEntry = originalJournal.CreateReversal(
                        cancelCtx.Today, $"عكس تحويل رقم {tracked.TransferNumber}",
                        cancelCtx.FiscalYear.Id, cancelCtx.Period.Id);

                    var reversalNumber = await _journalNumberGen.NextNumberAsync(cancelCtx.FiscalYear.Id, ct);
                    reversalEntry.Post(reversalNumber, _currentUser.Username, _dateTime.UtcNow);
                    await _journalRepo.AddAsync(reversalEntry, ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                    originalJournal.MarkAsReversed(reversalEntry.Id);
                    _journalRepo.Update(originalJournal);

                    // ── Cancel the transfer ──
                    tracked.Cancel();
                    // Entity is already tracked — no need for explicit Update

                    // CSH-03: Reverse balance changes (source gets money back, target loses it)
                    var sourceCashbox = await _cashboxRepo.GetByIdAsync(tracked.SourceCashboxId, ct);
                    var targetCashbox = await _cashboxRepo.GetByIdAsync(tracked.TargetCashboxId, ct);

                    var cashboxes = new List<(Cashbox cashbox, bool isSource)>(2);
                    if (sourceCashbox != null)
                        cashboxes.Add((sourceCashbox, true));
                    if (targetCashbox != null)
                        cashboxes.Add((targetCashbox, false));

                    // G-02 fix: Cancel is a correction — always allow negative on target
                    // cashbox, consistent with CashReceiptService H-23 fix.
                    // If we block the cancel because the target cashbox was depleted,
                    // the user gets stuck with an incorrect transfer that can't be undone.
                    foreach (var entry in cashboxes.OrderBy(x => x.cashbox.Id))
                    {
                        if (entry.isSource)
                        {
                            entry.cashbox.IncreaseBalance(tracked.Amount);
                        }
                        else
                        {
                            entry.cashbox.DecreaseBalanceAllowNegative(tracked.Amount);
                        }

                        _cashboxRepo.Update(entry.cashbox);
                    }

                    await _unitOfWork.SaveChangesAsync(ct);

                }, IsolationLevel.Serializable, ct);

                return ServiceResult.Success();
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (JournalEntryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while cancelling cash transfer.");
                return ServiceResult.Failure(
                    ErrorSanitizer.Sanitize(ex, "إلغاء التحويل"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult.Failure("تعذر إلغاء التحويل بسبب تعارض تزامن. الرجاء إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult.Failure("تعذر إلغاء التحويل بسبب تعارض في البيانات. الرجاء إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ غير متوقع أثناء إلغاء التحويل {TransferId}", id);
                return ServiceResult.Failure("حدث خطأ غير متوقع أثناء إلغاء التحويل.");
            }
        }

        private async Task<bool> IsNegativeCashAllowedAsync(CancellationToken ct)
        {
            if (_featureService == null)
                return false;

            var result = await _featureService.IsEnabledAsync(FeatureKeys.AllowNegativeCash, ct);
            return result.IsSuccess && result.Data;
        }

        // ══════════════════════════════════════════════════════════
        //  DELETE (Draft only)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteDraftAsync", "CashTransfer", id);

            var transfer = await _transferRepo.GetByIdAsync(id, ct);
            if (transfer == null)
                return ServiceResult.Failure(TransferNotFoundMessage);

            if (transfer.Status != InvoiceStatus.Draft)
                return ServiceResult.Failure("لا يمكن حذف إلا التحويلات المسودة.");

            transfer.SoftDelete(_currentUser.Username, _dateTime.UtcNow);
            _transferRepo.Update(transfer);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }
    }

    public sealed class CashTransferRepositories
    {
        public CashTransferRepositories(
            ICashTransferRepository transferRepo,
            ICashboxRepository cashboxRepo,
            IJournalEntryRepository journalRepo,
            IAccountRepository accountRepo)
        {
            TransferRepo = transferRepo ?? throw new ArgumentNullException(nameof(transferRepo));
            CashboxRepo = cashboxRepo ?? throw new ArgumentNullException(nameof(cashboxRepo));
            JournalRepo = journalRepo ?? throw new ArgumentNullException(nameof(journalRepo));
            AccountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
        }

        public ICashTransferRepository TransferRepo { get; }
        public ICashboxRepository CashboxRepo { get; }
        public IJournalEntryRepository JournalRepo { get; }
        public IAccountRepository AccountRepo { get; }
    }

    public sealed class CashTransferServices
    {
        public CashTransferServices(
            IFiscalYearRepository fiscalYearRepo,
            IJournalNumberGenerator journalNumberGen,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime,
            ISystemSettingRepository systemSettingRepo,
            IFeatureService featureService,
            IAuditLogger auditLogger)
        {
            FiscalYearRepo = fiscalYearRepo ?? throw new ArgumentNullException(nameof(fiscalYearRepo));
            JournalNumberGen = journalNumberGen ?? throw new ArgumentNullException(nameof(journalNumberGen));
            UnitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            CurrentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            DateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            SystemSettingRepo = systemSettingRepo ?? throw new ArgumentNullException(nameof(systemSettingRepo));
            FeatureService = featureService;
            AuditLogger = auditLogger;
        }

        public IFiscalYearRepository FiscalYearRepo { get; }
        public IJournalNumberGenerator JournalNumberGen { get; }
        public IUnitOfWork UnitOfWork { get; }
        public ICurrentUserService CurrentUser { get; }
        public IDateTimeProvider DateTime { get; }
        public ISystemSettingRepository SystemSettingRepo { get; }
        public IFeatureService FeatureService { get; }
        public IAuditLogger AuditLogger { get; }
    }

    public sealed class CashTransferValidators
    {
        public CashTransferValidators(
            IValidator<CreateCashTransferDto> createValidator,
            IValidator<UpdateCashTransferDto> updateValidator)
        {
            CreateValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            UpdateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
        }

        public IValidator<CreateCashTransferDto> CreateValidator { get; }
        public IValidator<UpdateCashTransferDto> UpdateValidator { get; }
    }
}
