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
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Application.Interfaces.Treasury;
using MarcoERP.Application.Mappers.Treasury;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Exceptions.Treasury;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Purchases;
using MarcoERP.Domain.Interfaces.Settings;
using MarcoERP.Domain.Interfaces.Treasury;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Treasury
{
    /// <summary>
    /// Application service for CashPayment (سند صرف) lifecycle.
    /// On Post: auto-generates journal entry:
    ///   DR: Contra Account (e.g. 2111 ذمم تجارية — supplier payment)
    ///   CR: Cashbox GL Account (e.g. 1111 الصندوق الرئيسي)
    /// SourceType: CashPayment (4)
    /// </summary>
    [Module(SystemModule.Treasury)]
    public sealed class CashPaymentService : ICashPaymentService
    {
        private readonly ICashPaymentRepository _paymentRepo;
        private readonly ICashboxRepository _cashboxRepo;
        private readonly IJournalEntryRepository _journalRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly IPurchaseInvoiceRepository _invoiceRepo;
        private readonly IJournalNumberGenerator _journalNumberGen;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreateCashPaymentDto> _createValidator;
        private readonly IValidator<UpdateCashPaymentDto> _updateValidator;
        private const string PaymentNotFoundMessage = "سند الصرف غير موجود.";
        private readonly ILogger<CashPaymentService> _logger;
        private readonly ISystemSettingRepository _systemSettingRepository;
        private readonly JournalEntryFactory _journalFactory;
        private readonly FiscalPeriodValidator _fiscalValidator;
        private readonly IFeatureService _featureService;

        public CashPaymentService(
            CashPaymentRepositories repos,
            CashPaymentServices services,
            CashPaymentValidators validators,
            JournalEntryFactory journalFactory,
            FiscalPeriodValidator fiscalValidator,
            ILogger<CashPaymentService> logger)
        {
            if (repos == null) throw new ArgumentNullException(nameof(repos));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (validators == null) throw new ArgumentNullException(nameof(validators));

            _paymentRepo = repos.PaymentRepo;
            _cashboxRepo = repos.CashboxRepo;
            _journalRepo = repos.JournalRepo;
            _accountRepo = repos.AccountRepo;
            _invoiceRepo = repos.InvoiceRepo;

            _journalNumberGen = services.JournalNumberGen;
            _unitOfWork = services.UnitOfWork;
            _currentUser = services.CurrentUser;
            _dateTime = services.DateTime;
            _systemSettingRepository = services.SystemSettingRepo;
            _featureService = services.FeatureService;

            _createValidator = validators.CreateValidator;
            _updateValidator = validators.UpdateValidator;
            _journalFactory = journalFactory ?? throw new ArgumentNullException(nameof(journalFactory));
            _fiscalValidator = fiscalValidator ?? throw new ArgumentNullException(nameof(fiscalValidator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ── Queries ─────────────────────────────────────────────

        public async Task<ServiceResult<IReadOnlyList<CashPaymentListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _paymentRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<CashPaymentListDto>>.Success(
                entities.Select(CashPaymentMapper.ToListDto).ToList());
        }

        public async Task<ServiceResult<CashPaymentDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _paymentRepo.GetWithDetailsAsync(id, ct);
            if (entity == null)
                return ServiceResult<CashPaymentDto>.Failure(PaymentNotFoundMessage);
            return ServiceResult<CashPaymentDto>.Success(CashPaymentMapper.ToDto(entity));
        }

        public async Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default)
        {
            var next = await _paymentRepo.GetNextNumberAsync(ct);
            return ServiceResult<string>.Success(next);
        }

        // ── Commands ────────────────────────────────────────────

        public async Task<ServiceResult<CashPaymentDto>> CreateAsync(CreateCashPaymentDto dto, CancellationToken ct = default)
        {
            // Feature Guard — block operation if Treasury module is disabled
            var guard = await FeatureGuard.CheckAsync<CashPaymentDto>(_featureService, FeatureKeys.Treasury, ct);
            if (guard != null) return guard;

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "CashPayment", 0);

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<CashPaymentDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            // Validate cashbox exists and is active
            var cashbox = await _cashboxRepo.GetByIdAsync(dto.CashboxId, ct);
            if (cashbox == null)
                return ServiceResult<CashPaymentDto>.Failure("الخزنة غير موجودة.");
            if (!cashbox.IsActive)
                return ServiceResult<CashPaymentDto>.Failure("الخزنة غير نشطة.");

            // Validate contra account exists and is postable
            var account = await _accountRepo.GetByIdAsync(dto.AccountId, ct);
            if (account == null)
                return ServiceResult<CashPaymentDto>.Failure("الحساب المقابل غير موجود.");
            if (!account.CanReceivePostings())
                return ServiceResult<CashPaymentDto>.Failure(
                    $"الحساب '{account.AccountCode} - {account.AccountNameAr}' لا يقبل الترحيل.");

            // Validate payment amount does not exceed invoice balance due
            if (dto.PurchaseInvoiceId.HasValue)
            {
                var linkedInvoice = await _invoiceRepo.GetByIdAsync(dto.PurchaseInvoiceId.Value, ct);
                if (linkedInvoice == null)
                    return ServiceResult<CashPaymentDto>.Failure("فاتورة الشراء المرتبطة غير موجودة.");
                if (linkedInvoice.Status != InvoiceStatus.Posted)
                    return ServiceResult<CashPaymentDto>.Failure("فاتورة الشراء المرتبطة غير مرحلة.");
                if (dto.Amount > linkedInvoice.BalanceDue)
                    return ServiceResult<CashPaymentDto>.Failure(
                        $"مبلغ سند الصرف ({dto.Amount:N2}) يتجاوز الرصيد المستحق على الفاتورة ({linkedInvoice.BalanceDue:N2}).");
            }

            try
            {
                var paymentNumber = await _paymentRepo.GetNextNumberAsync(ct);

                var payment = new CashPayment(new CashPaymentDraft
                {
                    PaymentNumber = paymentNumber,
                    PaymentDate = dto.PaymentDate,
                    CashboxId = dto.CashboxId,
                    AccountId = dto.AccountId,
                    Amount = dto.Amount,
                    Description = dto.Description,
                    SupplierId = dto.SupplierId,
                    PurchaseInvoiceId = dto.PurchaseInvoiceId,
                    Notes = dto.Notes
                });

                await _paymentRepo.AddAsync(payment, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _paymentRepo.GetWithDetailsAsync(payment.Id, ct);
                return ServiceResult<CashPaymentDto>.Success(CashPaymentMapper.ToDto(saved));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<CashPaymentDto>.Failure(ex.Message);
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<CashPaymentDto>.Failure("تم تعديل سند الصرف بواسطة مستخدم آخر. يرجى إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult<CashPaymentDto>.Failure("تعذر حفظ سند الصرف بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
            }
        }

        public async Task<ServiceResult<CashPaymentDto>> UpdateAsync(UpdateCashPaymentDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "CashPayment", dto.Id);

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<CashPaymentDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var payment = await _paymentRepo.GetWithDetailsTrackedAsync(dto.Id, ct);
            if (payment == null)
                return ServiceResult<CashPaymentDto>.Failure(PaymentNotFoundMessage);

            if (payment.Status != InvoiceStatus.Draft)
                return ServiceResult<CashPaymentDto>.Failure("لا يمكن تعديل سند صرف مرحّل أو ملغى.");

            try
            {
                payment.UpdateHeader(
                    dto.PaymentDate, dto.CashboxId, dto.AccountId,
                    dto.Amount, dto.Description, dto.SupplierId, dto.PurchaseInvoiceId, dto.Notes);

                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _paymentRepo.GetWithDetailsAsync(payment.Id, ct);
                return ServiceResult<CashPaymentDto>.Success(CashPaymentMapper.ToDto(saved));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<CashPaymentDto>.Failure(ex.Message);
            }
        }

        /// <summary>
        /// Posts a draft cash payment. Auto-generates journal entry:
        ///   DR: Contra Account (payment.AccountId)
        ///   CR: Cashbox GL Account (via Cashbox.AccountId)
        /// </summary>
        public async Task<ServiceResult<CashPaymentDto>> PostAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "PostAsync", "CashPayment", id);

            var payment = await _paymentRepo.GetWithDetailsTrackedAsync(id, ct);
            var preCheck = ValidatePostPreconditions(payment);
            if (preCheck != null) return preCheck;

            CashPayment saved = null;

            try
            {
                var allowNegativeCash = await IsNegativeCashAllowedAsync(ct);

                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var reloaded = await _paymentRepo.GetWithDetailsTrackedAsync(payment.Id, ct);
                    var statusCheck = ValidatePostPreconditions(reloaded);
                    if (statusCheck != null)
                        throw new TreasuryDomainException(statusCheck.ErrorMessage ?? "البيانات تغيرت أثناء الترحيل.");

                    // CRITICAL: Re-validate invoice balance inside transaction to prevent double-payment
                    if (reloaded.PurchaseInvoiceId.HasValue)
                    {
                        var linkedInvoice = await _invoiceRepo.GetWithLinesTrackedAsync(reloaded.PurchaseInvoiceId.Value, ct);
                        if (linkedInvoice == null)
                            throw new TreasuryDomainException("فاتورة الشراء المرتبطة غير موجودة.");
                        if (linkedInvoice.Status != InvoiceStatus.Posted)
                            throw new TreasuryDomainException("فاتورة الشراء المرتبطة غير مرحلة.");
                        if (reloaded.Amount > linkedInvoice.BalanceDue)
                            throw new TreasuryDomainException(
                                $"مبلغ سند الصرف ({reloaded.Amount:N2}) يتجاوز الرصيد المستحق على الفاتورة ({linkedInvoice.BalanceDue:N2}).");
                    }

                    var accounts = await GetPostingAccountsAsync(reloaded, ct);
                    var postingCtx = await _fiscalValidator.ValidateForPostingAsync(reloaded.PaymentDate, ct);

                    // CSH-03: Domain-level negative balance protection (inside Serializable tx)
                    // Respects AllowNegativeCash feature toggle
                    if (allowNegativeCash)
                        accounts.cashbox.DecreaseBalanceAllowNegative(reloaded.Amount);
                    else
                        accounts.cashbox.DecreaseBalance(reloaded.Amount);
                    _cashboxRepo.Update(accounts.cashbox);

                    var journalEntry = await CreateJournalEntryAsync(
                        reloaded,
                        accounts,
                        postingCtx.FiscalYear,
                        postingCtx.Period,
                        postingCtx.Now,
                        ct);

                    await _unitOfWork.SaveChangesAsync(ct);

                    reloaded.Post(journalEntry.Id);

                    // Apply payment to linked invoice if present
                    if (reloaded.PurchaseInvoiceId.HasValue)
                    {
                        var invoice = await _invoiceRepo.GetWithLinesTrackedAsync(reloaded.PurchaseInvoiceId.Value, ct);
                        if (invoice != null)
                        {
                            invoice.ApplyPayment(reloaded.Amount);
                        }
                    }

                    await _unitOfWork.SaveChangesAsync(ct);

                    saved = await _paymentRepo.GetWithDetailsAsync(reloaded.Id, ct);
                }, IsolationLevel.Serializable, ct);

                return ServiceResult<CashPaymentDto>.Success(CashPaymentMapper.ToDto(saved));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<CashPaymentDto>.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while posting cash payment.");
                return ServiceResult<CashPaymentDto>.Failure(
                    ErrorSanitizer.Sanitize(ex, "ترحيل سند الصرف"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<CashPaymentDto>.Failure("تعذر ترحيل سند الصرف بسبب تعارض تحديث متزامن. يرجى إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult<CashPaymentDto>.Failure("تعذر ترحيل سند الصرف بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ غير متوقع أثناء ترحيل سند الصرف {PaymentId}", id);
                return ServiceResult<CashPaymentDto>.Failure("حدث خطأ غير متوقع أثناء ترحيل سند الصرف.");
            }
        }

        private static ServiceResult<CashPaymentDto> ValidatePostPreconditions(CashPayment payment)
        {
            if (payment == null)
                return ServiceResult<CashPaymentDto>.Failure(PaymentNotFoundMessage);

            if (payment.Status != InvoiceStatus.Draft)
                return ServiceResult<CashPaymentDto>.Failure("لا يمكن ترحيل سند صرف مرحّل بالفعل أو ملغى.");

            return null;
        }

        private async Task<(Cashbox cashbox, Account cashboxAccount, Account contraAccount)> GetPostingAccountsAsync(
            CashPayment payment,
            CancellationToken ct)
        {
            var cashbox = await _cashboxRepo.GetByIdAsync(payment.CashboxId, ct);
            if (cashbox == null)
                throw new TreasuryDomainException("الخزنة غير موجودة.");

            if (!cashbox.AccountId.HasValue)
                throw new TreasuryDomainException(
                    "الخزنة ليس لها حساب GL مرتبط. يجب ربط الخزنة بحساب أولاً.");

            var cashboxAccount = await _accountRepo.GetByIdAsync(cashbox.AccountId.Value, ct);
            if (cashboxAccount == null || !cashboxAccount.CanReceivePostings())
                throw new TreasuryDomainException("حساب الخزنة المرتبط غير صالح للترحيل.");

            var contraAccount = await _accountRepo.GetByIdAsync(payment.AccountId, ct);
            if (contraAccount == null || !contraAccount.CanReceivePostings())
                throw new TreasuryDomainException("الحساب المقابل غير صالح للترحيل.");

            return (cashbox, cashboxAccount, contraAccount);
        }

        private async Task<JournalEntry> CreateJournalEntryAsync(
            CashPayment payment,
            (Cashbox cashbox, Account cashboxAccount, Account contraAccount) accounts,
            FiscalYear fiscalYear,
            FiscalPeriod period,
            DateTime now,
            CancellationToken ct)
        {
            var username = _currentUser.Username ?? "System";
            var lines = new[]
            {
                new JournalLineSpec(accounts.contraAccount.Id, payment.Amount, 0,
                    $"سند صرف {payment.PaymentNumber} — {accounts.contraAccount.AccountNameAr}"),
                new JournalLineSpec(accounts.cashboxAccount.Id, 0, payment.Amount,
                    $"صرف نقدي — {accounts.cashbox.NameAr}")
            };

            return await _journalFactory.CreateAndPostAsync(
                payment.PaymentDate,
                $"سند صرف رقم {payment.PaymentNumber} — {payment.Description}",
                SourceType.CashPayment,
                fiscalYear.Id,
                period.Id,
                lines,
                username,
                now,
                referenceNumber: payment.PaymentNumber,
                sourceId: payment.Id,
                ct: ct);
        }

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CancelAsync", "CashPayment", id);

            var payment = await _paymentRepo.GetWithDetailsTrackedAsync(id, ct);
            if (payment == null)
                return ServiceResult.Failure(PaymentNotFoundMessage);

            if (!payment.JournalEntryId.HasValue)
                return ServiceResult.Failure("لا يمكن إلغاء سند بدون قيد محاسبي.");

            var cancelContext = await _fiscalValidator.ValidateForCancelAsync(payment.PaymentDate, ct);

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // ── Reverse journal entry ──
                    var originalJournal = await _journalRepo.GetWithLinesAsync(payment.JournalEntryId.Value, ct);
                    if (originalJournal == null)
                        throw new TreasuryDomainException("القيد المحاسبي الأصلي غير موجود.");

                    var reversalEntry = originalJournal.CreateReversal(
                        cancelContext.Today, $"عكس سند صرف رقم {payment.PaymentNumber}",
                        cancelContext.FiscalYear.Id, cancelContext.Period.Id);

                    var reversalNumber = await _journalNumberGen.NextNumberAsync(cancelContext.FiscalYear.Id, ct);
                    reversalEntry.Post(reversalNumber, _currentUser.Username, _dateTime.UtcNow);
                    await _journalRepo.AddAsync(reversalEntry, ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                    originalJournal.MarkAsReversed(reversalEntry.Id);
                    _journalRepo.Update(originalJournal);

                    // ── Cancel the payment ──
                    payment.Cancel();

                    // CSH-03: Restore cashbox balance (payment cancelled — money returned)
                    var cashbox = await _cashboxRepo.GetByIdAsync(payment.CashboxId, ct)
                        ?? throw new TreasuryDomainException("الصندوق المرتبط بالسند غير موجود. لا يمكن إلغاء السند.");
                    cashbox.IncreaseBalance(payment.Amount);
                    _cashboxRepo.Update(cashbox);

                    // Reverse payment on linked invoice if present
                    if (payment.PurchaseInvoiceId.HasValue)
                    {
                        var invoice = await _invoiceRepo.GetWithLinesTrackedAsync(payment.PurchaseInvoiceId.Value, ct);
                        if (invoice != null && invoice.PaidAmount > 0)
                        {
                            var reversalAmount = Math.Min(payment.Amount, invoice.PaidAmount);
                            invoice.ReversePayment(reversalAmount);
                        }
                    }

                    await _unitOfWork.SaveChangesAsync(ct);

                }, IsolationLevel.Serializable, ct);

                return ServiceResult.Success();
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while cancelling cash payment.");
                return ServiceResult.Failure(
                    ErrorSanitizer.Sanitize(ex, "إلغاء سند الصرف"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult.Failure("تعذر إلغاء سند الصرف بسبب تعارض تحديث متزامن. يرجى إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult.Failure("تعذر إلغاء سند الصرف بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ غير متوقع أثناء إلغاء سند الصرف {PaymentId}", id);
                return ServiceResult.Failure("حدث خطأ غير متوقع أثناء إلغاء سند الصرف.");
            }
        }

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteDraftAsync", "CashPayment", id);

            var payment = await _paymentRepo.GetWithDetailsTrackedAsync(id, ct);
            if (payment == null)
                return ServiceResult.Failure(PaymentNotFoundMessage);

            if (payment.Status != InvoiceStatus.Draft)
                return ServiceResult.Failure("لا يمكن حذف إلا سندات الصرف المسودة.");

            payment.SoftDelete(_currentUser.Username, _dateTime.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        // ── Private Helpers ─────────────────────────────────────

        private async Task<bool> IsNegativeCashAllowedAsync(CancellationToken ct)
        {
            if (_featureService == null)
                return false;

            var result = await _featureService.IsEnabledAsync(FeatureKeys.AllowNegativeCash, ct);
            return result.IsSuccess && result.Data;
        }
    }

    public sealed class CashPaymentRepositories
    {
        public CashPaymentRepositories(
            ICashPaymentRepository paymentRepo,
            ICashboxRepository cashboxRepo,
            IJournalEntryRepository journalRepo,
            IAccountRepository accountRepo,
            IPurchaseInvoiceRepository invoiceRepo)
        {
            PaymentRepo = paymentRepo ?? throw new ArgumentNullException(nameof(paymentRepo));
            CashboxRepo = cashboxRepo ?? throw new ArgumentNullException(nameof(cashboxRepo));
            JournalRepo = journalRepo ?? throw new ArgumentNullException(nameof(journalRepo));
            AccountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
            InvoiceRepo = invoiceRepo ?? throw new ArgumentNullException(nameof(invoiceRepo));
        }

        public ICashPaymentRepository PaymentRepo { get; }
        public ICashboxRepository CashboxRepo { get; }
        public IJournalEntryRepository JournalRepo { get; }
        public IAccountRepository AccountRepo { get; }
        public IPurchaseInvoiceRepository InvoiceRepo { get; }
    }

    public sealed class CashPaymentServices
    {
        public CashPaymentServices(
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

    public sealed class CashPaymentValidators
    {
        public CashPaymentValidators(
            IValidator<CreateCashPaymentDto> createValidator,
            IValidator<UpdateCashPaymentDto> updateValidator)
        {
            CreateValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            UpdateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
        }

        public IValidator<CreateCashPaymentDto> CreateValidator { get; }
        public IValidator<UpdateCashPaymentDto> UpdateValidator { get; }
    }
}
