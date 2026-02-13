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
using MarcoERP.Domain.Interfaces.Purchases;
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
        private readonly IFiscalYearRepository _fiscalYearRepo;
        private readonly IJournalNumberGenerator _journalNumberGen;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreateCashPaymentDto> _createValidator;
        private readonly IValidator<UpdateCashPaymentDto> _updateValidator;
        private const string PaymentNotFoundMessage = "سند الصرف غير موجود.";
        private readonly ILogger<CashPaymentService> _logger;

        public CashPaymentService(
            CashPaymentRepositories repos,
            CashPaymentServices services,
            CashPaymentValidators validators,
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

            _fiscalYearRepo = services.FiscalYearRepo;
            _journalNumberGen = services.JournalNumberGen;
            _unitOfWork = services.UnitOfWork;
            _currentUser = services.CurrentUser;
            _dateTime = services.DateTime;

            _createValidator = validators.CreateValidator;
            _updateValidator = validators.UpdateValidator;
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
            var authCheck = AuthorizationGuard.Check<CashPaymentDto>(_currentUser, PermissionKeys.TreasuryCreate);
            if (authCheck != null) return authCheck;

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
        }

        public async Task<ServiceResult<CashPaymentDto>> UpdateAsync(UpdateCashPaymentDto dto, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<CashPaymentDto>(_currentUser, PermissionKeys.TreasuryCreate);
            if (authCheck != null) return authCheck;

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<CashPaymentDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var payment = await _paymentRepo.GetWithDetailsAsync(dto.Id, ct);
            if (payment == null)
                return ServiceResult<CashPaymentDto>.Failure(PaymentNotFoundMessage);

            if (payment.Status != InvoiceStatus.Draft)
                return ServiceResult<CashPaymentDto>.Failure("لا يمكن تعديل سند صرف مرحّل أو ملغى.");

            try
            {
                payment.UpdateHeader(
                    dto.PaymentDate, dto.CashboxId, dto.AccountId,
                    dto.Amount, dto.Description, dto.SupplierId, dto.Notes);

                _paymentRepo.Update(payment);
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
            var authCheck = AuthorizationGuard.Check<CashPaymentDto>(_currentUser, PermissionKeys.TreasuryPost);
            if (authCheck != null) return authCheck;

            var payment = await _paymentRepo.GetWithDetailsAsync(id, ct);
            var preCheck = ValidatePostPreconditions(payment);
            if (preCheck != null) return preCheck;

            // CSH-03: Prevent cashbox balance from going negative
            var cashboxBalance = await _cashboxRepo.GetGLBalanceAsync(payment.CashboxId, ct);
            if (cashboxBalance < payment.Amount)
            {
                var cashbox = await _cashboxRepo.GetByIdAsync(payment.CashboxId, ct);
                return ServiceResult<CashPaymentDto>.Failure(
                    $"رصيد الخزنة ({cashbox?.NameAr ?? "غير معروفة"}) غير كافٍ. الرصيد الحالي: {cashboxBalance:N2}، المبلغ المطلوب: {payment.Amount:N2}");
            }

            CashPayment saved = null;

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var accounts = await GetPostingAccountsAsync(payment, ct);
                    var (fiscalYear, period) = await GetPostingPeriodAsync(payment, ct);
                    var now = _dateTime.UtcNow;

                    var journalEntry = await CreateJournalEntryAsync(
                        payment,
                        accounts,
                        fiscalYear,
                        period,
                        now,
                        ct);

                    await _unitOfWork.SaveChangesAsync(ct);

                    payment.Post(journalEntry.Id);
                    _paymentRepo.Update(payment);

                    // Apply payment to linked invoice if present
                    if (payment.PurchaseInvoiceId.HasValue)
                    {
                        var invoice = await _invoiceRepo.GetByIdAsync(payment.PurchaseInvoiceId.Value, ct);
                        if (invoice != null)
                        {
                            invoice.ApplyPayment(payment.Amount);
                            _invoiceRepo.Update(invoice);
                        }
                    }

                    await _unitOfWork.SaveChangesAsync(ct);

                    saved = await _paymentRepo.GetWithDetailsAsync(payment.Id, ct);
                }, IsolationLevel.Serializable, ct);

                return ServiceResult<CashPaymentDto>.Success(CashPaymentMapper.ToDto(saved));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<CashPaymentDto>.Failure(ex.Message);
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

        private async Task<(FiscalYear fiscalYear, FiscalPeriod period)> GetPostingPeriodAsync(
            CashPayment payment,
            CancellationToken ct)
        {
            var fiscalYear = await _fiscalYearRepo.GetActiveYearAsync(ct);
            if (fiscalYear == null)
                throw new TreasuryDomainException("لا توجد سنة مالية نشطة.");

            if (!fiscalYear.ContainsDate(payment.PaymentDate))
                throw new TreasuryDomainException(
                    $"تاريخ السند {payment.PaymentDate:yyyy-MM-dd} لا يقع ضمن السنة المالية النشطة.");

            var yearWithPeriods = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            var period = yearWithPeriods.GetPeriod(payment.PaymentDate.Month);
            if (period == null)
                throw new TreasuryDomainException("لا توجد فترة مالية للشهر المحدد.");
            if (!period.IsOpen)
                throw new TreasuryDomainException(
                    $"الفترة المالية ({period.PeriodNumber}/{period.Year}) مُقفلة.");

            return (fiscalYear, period);
        }

        private async Task<JournalEntry> CreateJournalEntryAsync(
            CashPayment payment,
            (Cashbox cashbox, Account cashboxAccount, Account contraAccount) accounts,
            FiscalYear fiscalYear,
            FiscalPeriod period,
            DateTime now,
            CancellationToken ct)
        {
            var journalEntry = JournalEntry.CreateDraft(
                payment.PaymentDate,
                $"سند صرف رقم {payment.PaymentNumber} — {payment.Description}",
                SourceType.CashPayment,
                fiscalYear.Id,
                period.Id,
                referenceNumber: payment.PaymentNumber,
                sourceId: payment.Id);

            journalEntry.AddLine(accounts.contraAccount.Id, payment.Amount, 0, now,
                $"سند صرف {payment.PaymentNumber} — {accounts.contraAccount.AccountNameAr}");

            journalEntry.AddLine(accounts.cashboxAccount.Id, 0, payment.Amount, now,
                $"صرف نقدي — {accounts.cashbox.NameAr}");

            var journalNumber = _journalNumberGen.NextNumber(fiscalYear.Id);
            var username = _currentUser.Username ?? "System";
            journalEntry.Post(journalNumber, username, now);

            await _journalRepo.AddAsync(journalEntry, ct);
            return journalEntry;
        }

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.TreasuryPost);
            if (authCheck != null) return authCheck;

            var payment = await _paymentRepo.GetWithDetailsAsync(id, ct);
            if (payment == null)
                return ServiceResult.Failure(PaymentNotFoundMessage);

            if (!payment.JournalEntryId.HasValue)
                return ServiceResult.Failure("لا يمكن إلغاء سند بدون قيد محاسبي.");

            var cancelContext = await GetCancelContextAsync(payment.PaymentDate, ct);

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

                    var reversalNumber = _journalNumberGen.NextNumber(cancelContext.FiscalYear.Id);
                    reversalEntry.Post(reversalNumber, _currentUser.Username, _dateTime.UtcNow);
                    await _journalRepo.AddAsync(reversalEntry, ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                    originalJournal.MarkAsReversed(reversalEntry.Id);
                    _journalRepo.Update(originalJournal);

                    // ── Cancel the payment ──
                    payment.Cancel();
                    _paymentRepo.Update(payment);

                    // Reverse payment on linked invoice if present
                    if (payment.PurchaseInvoiceId.HasValue)
                    {
                        var invoice = await _invoiceRepo.GetByIdAsync(payment.PurchaseInvoiceId.Value, ct);
                        if (invoice != null && invoice.PaidAmount > 0)
                        {
                            var reversalAmount = Math.Min(payment.Amount, invoice.PaidAmount);
                            invoice.ReversePayment(reversalAmount);
                            _invoiceRepo.Update(invoice);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ غير متوقع أثناء إلغاء سند الصرف {PaymentId}", id);
                return ServiceResult.Failure("حدث خطأ غير متوقع أثناء إلغاء سند الصرف.");
            }
        }

        private async Task<(FiscalYear FiscalYear, FiscalPeriod Period, DateTime Today)> GetCancelContextAsync(
            DateTime paymentDate,
            CancellationToken ct)
        {
            var fiscalYear = await _fiscalYearRepo.GetByYearAsync(paymentDate.Year, ct);
            if (fiscalYear == null)
                throw new TreasuryDomainException($"لا توجد سنة مالية للعام {paymentDate.Year}.");

            fiscalYear = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            if (fiscalYear.Status != FiscalYearStatus.Active)
                throw new TreasuryDomainException($"السنة المالية {fiscalYear.Year} ليست فعّالة.");

            var period = fiscalYear.GetPeriod(paymentDate.Month);
            if (period == null || !period.IsOpen)
                throw new TreasuryDomainException($"الفترة المالية لـ {paymentDate:yyyy-MM} مُقفلة.");

            return (fiscalYear, period, paymentDate);
        }

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.TreasuryCreate);
            if (authCheck != null) return authCheck;

            var payment = await _paymentRepo.GetByIdAsync(id, ct);
            if (payment == null)
                return ServiceResult.Failure(PaymentNotFoundMessage);

            if (payment.Status != InvoiceStatus.Draft)
                return ServiceResult.Failure("لا يمكن حذف إلا سندات الصرف المسودة.");

            payment.SoftDelete(_currentUser.Username, _dateTime.UtcNow);
            _paymentRepo.Update(payment);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
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
