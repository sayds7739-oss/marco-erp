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
using MarcoERP.Application.Mappers.Treasury;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Treasury;
using MarcoERP.Domain.Interfaces;
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
        private readonly IFiscalYearRepository _fiscalYearRepo;
        private readonly IJournalNumberGenerator _journalNumberGen;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreateCashTransferDto> _createValidator;
        private readonly IValidator<UpdateCashTransferDto> _updateValidator;
        private readonly ILogger<CashTransferService> _logger;
        private const string TransferNotFoundMessage = "التحويل غير موجود.";

        public CashTransferService(
            CashTransferRepositories repos,
            CashTransferServices services,
            CashTransferValidators validators,
            ILogger<CashTransferService> logger)
        {
            if (repos == null) throw new ArgumentNullException(nameof(repos));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (validators == null) throw new ArgumentNullException(nameof(validators));

            _transferRepo = repos.TransferRepo;
            _cashboxRepo = repos.CashboxRepo;
            _journalRepo = repos.JournalRepo;
            _accountRepo = repos.AccountRepo;

            _fiscalYearRepo = services.FiscalYearRepo;
            _journalNumberGen = services.JournalNumberGen;
            _unitOfWork = services.UnitOfWork;
            _currentUser = services.CurrentUser;
            _dateTime = services.DateTime;

            _createValidator = validators.CreateValidator;
            _updateValidator = validators.UpdateValidator;
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
            var authCheck = AuthorizationGuard.Check<CashTransferDto>(_currentUser, PermissionKeys.TreasuryCreate);
            if (authCheck != null) return authCheck;

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
        }

        // ══════════════════════════════════════════════════════════
        //  UPDATE (Draft only)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<CashTransferDto>> UpdateAsync(UpdateCashTransferDto dto, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<CashTransferDto>(_currentUser, PermissionKeys.TreasuryCreate);
            if (authCheck != null) return authCheck;

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<CashTransferDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var transfer = await _transferRepo.GetWithDetailsAsync(dto.Id, ct);
            if (transfer == null)
                return ServiceResult<CashTransferDto>.Failure(TransferNotFoundMessage);

            if (transfer.Status != InvoiceStatus.Draft)
                return ServiceResult<CashTransferDto>.Failure("لا يمكن تعديل تحويل مرحّل أو ملغى.");

            try
            {
                transfer.UpdateHeader(
                    dto.TransferDate, dto.SourceCashboxId, dto.TargetCashboxId,
                    dto.Amount, dto.Description, dto.Notes);

                _transferRepo.Update(transfer);
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
            var authCheck = AuthorizationGuard.Check<CashTransferDto>(_currentUser, PermissionKeys.TreasuryPost);
            if (authCheck != null) return authCheck;

            var transfer = await _transferRepo.GetWithDetailsAsync(id, ct);
            var preCheck = ValidatePostPreconditions(transfer);
            if (preCheck != null) return preCheck;

            // CSH-03: Prevent source cashbox balance from going negative
            var sourceBalance = await _cashboxRepo.GetGLBalanceAsync(transfer.SourceCashboxId, ct);
            if (sourceBalance < transfer.Amount)
            {
                var sourceCashbox = await _cashboxRepo.GetByIdAsync(transfer.SourceCashboxId, ct);
                return ServiceResult<CashTransferDto>.Failure(
                    $"رصيد الخزنة المصدر ({sourceCashbox?.NameAr ?? "غير معروفة"}) غير كافٍ. الرصيد الحالي: {sourceBalance:N2}، المبلغ المطلوب: {transfer.Amount:N2}");
            }

            CashTransfer saved = null;

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var accounts = await GetPostingAccountsAsync(transfer, ct);
                    var (fiscalYear, period) = await GetPostingPeriodAsync(transfer, ct);
                    var now = _dateTime.UtcNow;

                    var journalEntry = await CreateJournalEntryAsync(
                        transfer,
                        accounts,
                        fiscalYear,
                        period,
                        now,
                        ct);

                    await _unitOfWork.SaveChangesAsync(ct);

                    transfer.Post(journalEntry.Id);
                    _transferRepo.Update(transfer);
                    await _unitOfWork.SaveChangesAsync(ct);

                    saved = await _transferRepo.GetWithDetailsAsync(transfer.Id, ct);
                }, IsolationLevel.Serializable, ct);

                return ServiceResult<CashTransferDto>.Success(CashTransferMapper.ToDto(saved));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<CashTransferDto>.Failure(ex.Message);
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

        private async Task<(FiscalYear fiscalYear, FiscalPeriod period)> GetPostingPeriodAsync(
            CashTransfer transfer,
            CancellationToken ct)
        {
            var fiscalYear = await _fiscalYearRepo.GetActiveYearAsync(ct);
            if (fiscalYear == null)
                throw new TreasuryDomainException("لا توجد سنة مالية نشطة.");

            if (!fiscalYear.ContainsDate(transfer.TransferDate))
                throw new TreasuryDomainException(
                    $"تاريخ التحويل {transfer.TransferDate:yyyy-MM-dd} لا يقع ضمن السنة المالية النشطة.");

            var yearWithPeriods = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            var period = yearWithPeriods.GetPeriod(transfer.TransferDate.Month);
            if (period == null)
                throw new TreasuryDomainException("لا توجد فترة مالية للشهر المحدد.");
            if (!period.IsOpen)
                throw new TreasuryDomainException(
                    $"الفترة المالية ({period.PeriodNumber}/{period.Year}) مُقفلة.");

            return (fiscalYear, period);
        }

        private async Task<JournalEntry> CreateJournalEntryAsync(
            CashTransfer transfer,
            (Cashbox sourceCashbox, Cashbox targetCashbox, Account sourceAccount, Account targetAccount) accounts,
            FiscalYear fiscalYear,
            FiscalPeriod period,
            DateTime now,
            CancellationToken ct)
        {
            var journalEntry = JournalEntry.CreateDraft(
                transfer.TransferDate,
                $"تحويل بين الخزن رقم {transfer.TransferNumber}",
                SourceType.CashTransfer,
                fiscalYear.Id,
                period.Id,
                referenceNumber: transfer.TransferNumber,
                sourceId: transfer.Id);

            journalEntry.AddLine(accounts.targetAccount.Id, transfer.Amount, 0, now,
                $"تحويل وارد — {accounts.targetCashbox.NameAr}");

            journalEntry.AddLine(accounts.sourceAccount.Id, 0, transfer.Amount, now,
                $"تحويل صادر — {accounts.sourceCashbox.NameAr}");

            var journalNumber = _journalNumberGen.NextNumber(fiscalYear.Id);
            var username = _currentUser.Username ?? "System";
            journalEntry.Post(journalNumber, username, now);

            await _journalRepo.AddAsync(journalEntry, ct);
            return journalEntry;
        }

        // ══════════════════════════════════════════════════════════
        //  CANCEL
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.TreasuryPost);
            if (authCheck != null) return authCheck;

            var transfer = await _transferRepo.GetWithDetailsAsync(id, ct);
            if (transfer == null)
                return ServiceResult.Failure(TransferNotFoundMessage);

            if (!transfer.JournalEntryId.HasValue)
                return ServiceResult.Failure("لا يمكن إلغاء تحويل بدون قيد محاسبي.");

            // ── Resolve fiscal year/period for reversal date ──
            var reversalDate = transfer.TransferDate;
            var fiscalYear = await _fiscalYearRepo.GetByYearAsync(reversalDate.Year, ct);
            if (fiscalYear == null)
                return ServiceResult.Failure($"لا توجد سنة مالية للعام {reversalDate.Year}.");

            fiscalYear = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            if (fiscalYear.Status != FiscalYearStatus.Active)
                return ServiceResult.Failure($"السنة المالية {fiscalYear.Year} ليست فعّالة.");

            var period = fiscalYear.GetPeriod(reversalDate.Month);
            if (period == null || !period.IsOpen)
                return ServiceResult.Failure($"الفترة المالية لـ {reversalDate:yyyy-MM} مُقفلة.");

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // ── Reverse journal entry ──
                    var originalJournal = await _journalRepo.GetWithLinesAsync(transfer.JournalEntryId.Value, ct);
                    if (originalJournal == null)
                        throw new TreasuryDomainException("القيد المحاسبي الأصلي غير موجود.");

                    var reversalEntry = originalJournal.CreateReversal(
                        reversalDate, $"عكس تحويل رقم {transfer.TransferNumber}",
                        fiscalYear.Id, period.Id);

                    var reversalNumber = _journalNumberGen.NextNumber(fiscalYear.Id);
                    reversalEntry.Post(reversalNumber, _currentUser.Username, _dateTime.UtcNow);
                    await _journalRepo.AddAsync(reversalEntry, ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                    originalJournal.MarkAsReversed(reversalEntry.Id);
                    _journalRepo.Update(originalJournal);

                    // ── Cancel the transfer ──
                    transfer.Cancel();
                    _transferRepo.Update(transfer);
                    await _unitOfWork.SaveChangesAsync(ct);

                }, IsolationLevel.Serializable, ct);

                return ServiceResult.Success();
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ غير متوقع أثناء إلغاء التحويل {TransferId}", id);
                return ServiceResult.Failure("حدث خطأ غير متوقع أثناء إلغاء التحويل.");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DELETE (Draft only)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.TreasuryCreate);
            if (authCheck != null) return authCheck;

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
            IDateTimeProvider dateTime)
        {
            FiscalYearRepo = fiscalYearRepo ?? throw new ArgumentNullException(nameof(fiscalYearRepo));
            JournalNumberGen = journalNumberGen ?? throw new ArgumentNullException(nameof(journalNumberGen));
            UnitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            CurrentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            DateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
        }

        public IFiscalYearRepository FiscalYearRepo { get; }
        public IJournalNumberGenerator JournalNumberGen { get; }
        public IUnitOfWork UnitOfWork { get; }
        public ICurrentUserService CurrentUser { get; }
        public IDateTimeProvider DateTime { get; }
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
