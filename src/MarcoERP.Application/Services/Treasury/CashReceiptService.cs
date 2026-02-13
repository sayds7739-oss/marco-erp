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
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Domain.Interfaces.Treasury;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Treasury
{
    /// <summary>
    /// Application service for CashReceipt (سند قبض) lifecycle.
    /// On Post: auto-generates journal entry:
    ///   DR: Cashbox GL Account (e.g. 1111 الصندوق الرئيسي)
    ///   CR: Contra Account (e.g. 1121 ذمم تجارية — customer payment)
    /// SourceType: CashReceipt (3)
    /// </summary>
    [Module(SystemModule.Treasury)]
    public sealed class CashReceiptService : ICashReceiptService
    {
        private readonly ICashReceiptRepository _receiptRepo;
        private readonly ICashboxRepository _cashboxRepo;
        private readonly IJournalEntryRepository _journalRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly ISalesInvoiceRepository _invoiceRepo;
        private readonly IFiscalYearRepository _fiscalYearRepo;
        private readonly IJournalNumberGenerator _journalNumberGen;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreateCashReceiptDto> _createValidator;
        private readonly IValidator<UpdateCashReceiptDto> _updateValidator;
        private readonly ILogger<CashReceiptService> _logger;

        private const string ReceiptNotFoundMessage = "سند القبض غير موجود.";

        public CashReceiptService(
            CashReceiptRepositories repos,
            CashReceiptServices services,
            CashReceiptValidators validators,
            ILogger<CashReceiptService> logger)
        {
            if (repos == null) throw new ArgumentNullException(nameof(repos));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (validators == null) throw new ArgumentNullException(nameof(validators));

            _receiptRepo = repos.ReceiptRepo;
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

        public async Task<ServiceResult<IReadOnlyList<CashReceiptListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _receiptRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<CashReceiptListDto>>.Success(
                entities.Select(CashReceiptMapper.ToListDto).ToList());
        }

        public async Task<ServiceResult<CashReceiptDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _receiptRepo.GetWithDetailsAsync(id, ct);
            if (entity == null)
                return ServiceResult<CashReceiptDto>.Failure(ReceiptNotFoundMessage);
            return ServiceResult<CashReceiptDto>.Success(CashReceiptMapper.ToDto(entity));
        }

        public async Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default)
        {
            var next = await _receiptRepo.GetNextNumberAsync(ct);
            return ServiceResult<string>.Success(next);
        }

        // ── Commands ────────────────────────────────────────────

        public async Task<ServiceResult<CashReceiptDto>> CreateAsync(CreateCashReceiptDto dto, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<CashReceiptDto>(_currentUser, PermissionKeys.TreasuryCreate);
            if (authCheck != null) return authCheck;

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<CashReceiptDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            // Validate cashbox exists and is active
            var cashbox = await _cashboxRepo.GetByIdAsync(dto.CashboxId, ct);
            if (cashbox == null)
                return ServiceResult<CashReceiptDto>.Failure("الخزنة غير موجودة.");
            if (!cashbox.IsActive)
                return ServiceResult<CashReceiptDto>.Failure("الخزنة غير نشطة.");

            // Validate contra account exists and is postable
            var account = await _accountRepo.GetByIdAsync(dto.AccountId, ct);
            if (account == null)
                return ServiceResult<CashReceiptDto>.Failure("الحساب المقابل غير موجود.");
            if (!account.CanReceivePostings())
                return ServiceResult<CashReceiptDto>.Failure(
                    $"الحساب '{account.AccountCode} - {account.AccountNameAr}' لا يقبل الترحيل.");

            // Validate receipt amount does not exceed invoice balance due
            if (dto.SalesInvoiceId.HasValue)
            {
                var linkedInvoice = await _invoiceRepo.GetByIdAsync(dto.SalesInvoiceId.Value, ct);
                if (linkedInvoice == null)
                    return ServiceResult<CashReceiptDto>.Failure("الفاتورة المرتبطة غير موجودة.");
                if (linkedInvoice.Status != InvoiceStatus.Posted)
                    return ServiceResult<CashReceiptDto>.Failure("الفاتورة المرتبطة غير مرحلة.");
                if (dto.Amount > linkedInvoice.BalanceDue)
                    return ServiceResult<CashReceiptDto>.Failure(
                        $"مبلغ سند القبض ({dto.Amount:N2}) يتجاوز الرصيد المستحق على الفاتورة ({linkedInvoice.BalanceDue:N2}).");
            }

            try
            {
                var receiptNumber = await _receiptRepo.GetNextNumberAsync(ct);

                var receipt = new CashReceipt(new CashReceiptDraft
                {
                    ReceiptNumber = receiptNumber,
                    ReceiptDate = dto.ReceiptDate,
                    CashboxId = dto.CashboxId,
                    AccountId = dto.AccountId,
                    Amount = dto.Amount,
                    Description = dto.Description,
                    CustomerId = dto.CustomerId,
                    SalesInvoiceId = dto.SalesInvoiceId,
                    Notes = dto.Notes
                });

                await _receiptRepo.AddAsync(receipt, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _receiptRepo.GetWithDetailsAsync(receipt.Id, ct);
                return ServiceResult<CashReceiptDto>.Success(CashReceiptMapper.ToDto(saved));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<CashReceiptDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<CashReceiptDto>> UpdateAsync(UpdateCashReceiptDto dto, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<CashReceiptDto>(_currentUser, PermissionKeys.TreasuryCreate);
            if (authCheck != null) return authCheck;

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<CashReceiptDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var receipt = await _receiptRepo.GetWithDetailsAsync(dto.Id, ct);
            if (receipt == null)
                return ServiceResult<CashReceiptDto>.Failure(ReceiptNotFoundMessage);

            if (receipt.Status != InvoiceStatus.Draft)
                return ServiceResult<CashReceiptDto>.Failure("لا يمكن تعديل سند قبض مرحّل أو ملغى.");

            try
            {
                receipt.UpdateHeader(
                    dto.ReceiptDate, dto.CashboxId, dto.AccountId,
                    dto.Amount, dto.Description, dto.CustomerId, dto.Notes);

                _receiptRepo.Update(receipt);
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _receiptRepo.GetWithDetailsAsync(receipt.Id, ct);
                return ServiceResult<CashReceiptDto>.Success(CashReceiptMapper.ToDto(saved));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<CashReceiptDto>.Failure(ex.Message);
            }
        }

        /// <summary>
        /// Posts a draft cash receipt. Auto-generates journal entry:
        ///   DR: Cashbox GL Account (via Cashbox.AccountId)
        ///   CR: Contra Account (receipt.AccountId)
        /// </summary>
        public async Task<ServiceResult<CashReceiptDto>> PostAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<CashReceiptDto>(_currentUser, PermissionKeys.TreasuryPost);
            if (authCheck != null) return authCheck;

            var receipt = await _receiptRepo.GetWithDetailsAsync(id, ct);
            if (receipt == null)
                return ServiceResult<CashReceiptDto>.Failure(ReceiptNotFoundMessage);

            if (receipt.Status != InvoiceStatus.Draft)
                return ServiceResult<CashReceiptDto>.Failure("لا يمكن ترحيل سند قبض مرحّل بالفعل أو ملغى.");

            try
            {
                var saved = await ExecutePostAsync(receipt, ct);
                return ServiceResult<CashReceiptDto>.Success(CashReceiptMapper.ToDto(saved));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<CashReceiptDto>.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ غير متوقع أثناء ترحيل سند القبض {ReceiptId}", id);
                return ServiceResult<CashReceiptDto>.Failure("حدث خطأ غير متوقع أثناء ترحيل سند القبض.");
            }
        }

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.TreasuryPost);
            if (authCheck != null) return authCheck;

            var receipt = await _receiptRepo.GetWithDetailsAsync(id, ct);
            if (receipt == null)
                return ServiceResult.Failure(ReceiptNotFoundMessage);

            if (!receipt.JournalEntryId.HasValue)
                return ServiceResult.Failure("لا يمكن إلغاء سند بدون قيد محاسبي.");

            try
            {
                var context = await GetCancelContextAsync(receipt.ReceiptDate, ct);
                await ExecuteCancelAsync(receipt, context, ct);
                return ServiceResult.Success();
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ غير متوقع أثناء إلغاء سند القبض {ReceiptId}", id);
                return ServiceResult.Failure("حدث خطأ غير متوقع أثناء إلغاء سند القبض.");
            }
        }

        private async Task<CashReceipt> ExecutePostAsync(CashReceipt receipt, CancellationToken ct)
        {
            CashReceipt saved = null;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var accounts = await ResolveAccountsAsync(receipt, ct);
                var context = await GetPostingContextAsync(receipt.ReceiptDate, ct);

                var journalEntry = await CreateJournalEntryAsync(receipt, accounts, context, ct);

                await _unitOfWork.SaveChangesAsync(ct);

                receipt.Post(journalEntry.Id);
                _receiptRepo.Update(receipt);

                // Apply payment to linked invoice if present
                if (receipt.SalesInvoiceId.HasValue)
                {
                    var invoice = await _invoiceRepo.GetByIdAsync(receipt.SalesInvoiceId.Value, ct);
                    if (invoice != null)
                    {
                        invoice.ApplyPayment(receipt.Amount);
                        _invoiceRepo.Update(invoice);
                    }
                }

                await _unitOfWork.SaveChangesAsync(ct);

                saved = await _receiptRepo.GetWithDetailsAsync(receipt.Id, ct);
            }, IsolationLevel.Serializable, ct);

            return saved ?? receipt;
        }

        private async Task<CashReceiptAccounts> ResolveAccountsAsync(CashReceipt receipt, CancellationToken ct)
        {
            var cashbox = await _cashboxRepo.GetByIdAsync(receipt.CashboxId, ct);
            if (cashbox == null)
                throw new TreasuryDomainException("الخزنة غير موجودة.");
            if (!cashbox.AccountId.HasValue)
                throw new TreasuryDomainException(
                    "الخزنة ليس لها حساب GL مرتبط. يجب ربط الخزنة بحساب أولاً.");

            var cashboxAccount = await _accountRepo.GetByIdAsync(cashbox.AccountId.Value, ct);
            if (cashboxAccount == null || !cashboxAccount.CanReceivePostings())
                throw new TreasuryDomainException(
                    "حساب الخزنة المرتبط غير صالح للترحيل.");

            var contraAccount = await _accountRepo.GetByIdAsync(receipt.AccountId, ct);
            if (contraAccount == null || !contraAccount.CanReceivePostings())
                throw new TreasuryDomainException(
                    "الحساب المقابل غير صالح للترحيل.");

            return new CashReceiptAccounts
            {
                Cashbox = cashbox,
                CashboxAccount = cashboxAccount,
                ContraAccount = contraAccount
            };
        }

        private async Task<CashReceiptPostingContext> GetPostingContextAsync(DateTime receiptDate, CancellationToken ct)
        {
            var fiscalYear = await _fiscalYearRepo.GetActiveYearAsync(ct);
            if (fiscalYear == null)
                throw new TreasuryDomainException("لا توجد سنة مالية نشطة.");

            if (!fiscalYear.ContainsDate(receiptDate))
                throw new TreasuryDomainException(
                    $"تاريخ السند {receiptDate:yyyy-MM-dd} لا يقع ضمن السنة المالية النشطة.");

            var yearWithPeriods = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            var period = yearWithPeriods.GetPeriod(receiptDate.Month);
            if (period == null)
                throw new TreasuryDomainException("لا توجد فترة مالية للشهر المحدد.");
            if (!period.IsOpen)
                throw new TreasuryDomainException(
                    $"الفترة المالية ({period.PeriodNumber}/{period.Year}) مُقفلة.");

            return new CashReceiptPostingContext
            {
                FiscalYear = yearWithPeriods,
                Period = period,
                Now = _dateTime.UtcNow,
                Username = _currentUser.Username ?? "System"
            };
        }

        private async Task<JournalEntry> CreateJournalEntryAsync(
            CashReceipt receipt,
            CashReceiptAccounts accounts,
            CashReceiptPostingContext context,
            CancellationToken ct)
        {
            var journalEntry = JournalEntry.CreateDraft(
                receipt.ReceiptDate,
                $"سند قبض رقم {receipt.ReceiptNumber} — {receipt.Description}",
                SourceType.CashReceipt,
                context.FiscalYear.Id,
                context.Period.Id,
                referenceNumber: receipt.ReceiptNumber,
                sourceId: receipt.Id);

            journalEntry.AddLine(accounts.CashboxAccount.Id, receipt.Amount, 0, context.Now,
                $"قبض نقدي — {accounts.Cashbox.NameAr}");

            journalEntry.AddLine(accounts.ContraAccount.Id, 0, receipt.Amount, context.Now,
                $"سند قبض {receipt.ReceiptNumber} — {accounts.ContraAccount.AccountNameAr}");

            var journalNumber = _journalNumberGen.NextNumber(context.FiscalYear.Id);
            journalEntry.Post(journalNumber, context.Username, context.Now);

            await _journalRepo.AddAsync(journalEntry, ct);
            return journalEntry;
        }

        private async Task<CashReceiptCancelContext> GetCancelContextAsync(DateTime receiptDate, CancellationToken ct)
        {
            var fiscalYear = await _fiscalYearRepo.GetByYearAsync(receiptDate.Year, ct);
            if (fiscalYear == null)
                throw new TreasuryDomainException($"لا توجد سنة مالية للعام {receiptDate.Year}.");

            fiscalYear = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            if (fiscalYear.Status != FiscalYearStatus.Active)
                throw new TreasuryDomainException($"السنة المالية {fiscalYear.Year} ليست فعّالة.");

            var period = fiscalYear.GetPeriod(receiptDate.Month);
            if (period == null || !period.IsOpen)
                throw new TreasuryDomainException($"الفترة المالية لـ {receiptDate:yyyy-MM} مُقفلة.");

            return new CashReceiptCancelContext
            {
                FiscalYear = fiscalYear,
                Period = period,
                Today = receiptDate
            };
        }

        private async Task ExecuteCancelAsync(CashReceipt receipt, CashReceiptCancelContext context, CancellationToken ct)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await ReverseJournalAsync(
                    receipt.JournalEntryId.Value,
                    $"عكس سند قبض رقم {receipt.ReceiptNumber}",
                    "القيد المحاسبي الأصلي غير موجود.",
                    context,
                    ct);

                receipt.Cancel();
                _receiptRepo.Update(receipt);

                // Reverse payment on linked invoice if present
                if (receipt.SalesInvoiceId.HasValue)
                {
                    var invoice = await _invoiceRepo.GetByIdAsync(receipt.SalesInvoiceId.Value, ct);
                    if (invoice != null && invoice.PaidAmount > 0)
                    {
                        // Use Math.Min to handle partial-reversal edge cases safely
                        var reversalAmount = Math.Min(receipt.Amount, invoice.PaidAmount);
                        invoice.ReversePayment(reversalAmount);
                        _invoiceRepo.Update(invoice);
                    }
                }

                await _unitOfWork.SaveChangesAsync(ct);

            }, IsolationLevel.Serializable, ct);
        }

        private async Task ReverseJournalAsync(
            int journalId,
            string description,
            string notFoundMessage,
            CashReceiptCancelContext context,
            CancellationToken ct)
        {
            var originalJournal = await _journalRepo.GetWithLinesAsync(journalId, ct);
            if (originalJournal == null)
                throw new TreasuryDomainException(notFoundMessage);

            var reversalEntry = originalJournal.CreateReversal(
                context.Today,
                description,
                context.FiscalYear.Id,
                context.Period.Id);

            var reversalNumber = _journalNumberGen.NextNumber(context.FiscalYear.Id);
            var username = _currentUser.Username ?? "System";
            var now = _dateTime.UtcNow;
            reversalEntry.Post(reversalNumber, username, now);
            await _journalRepo.AddAsync(reversalEntry, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            originalJournal.MarkAsReversed(reversalEntry.Id);
            _journalRepo.Update(originalJournal);
        }

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.TreasuryCreate);
            if (authCheck != null) return authCheck;

            var receipt = await _receiptRepo.GetByIdAsync(id, ct);
            if (receipt == null)
                return ServiceResult.Failure(ReceiptNotFoundMessage);

            if (receipt.Status != InvoiceStatus.Draft)
                return ServiceResult.Failure("لا يمكن حذف إلا سندات القبض المسودة.");

            receipt.SoftDelete(_currentUser.Username, _dateTime.UtcNow);
            _receiptRepo.Update(receipt);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        private sealed class CashReceiptPostingContext
        {
            public FiscalYear FiscalYear { get; init; } = default!;
            public FiscalPeriod Period { get; init; } = default!;
            public DateTime Now { get; init; }
            public string Username { get; init; } = string.Empty;
        }

        private sealed class CashReceiptAccounts
        {
            public Cashbox Cashbox { get; init; } = default!;
            public Account CashboxAccount { get; init; } = default!;
            public Account ContraAccount { get; init; } = default!;
        }

        private sealed class CashReceiptCancelContext
        {
            public FiscalYear FiscalYear { get; init; } = default!;
            public FiscalPeriod Period { get; init; } = default!;
            public DateTime Today { get; init; }
        }
    }

    public sealed class CashReceiptRepositories
    {
        public CashReceiptRepositories(
            ICashReceiptRepository receiptRepo,
            ICashboxRepository cashboxRepo,
            IJournalEntryRepository journalRepo,
            IAccountRepository accountRepo,
            ISalesInvoiceRepository invoiceRepo)
        {
            ReceiptRepo = receiptRepo ?? throw new ArgumentNullException(nameof(receiptRepo));
            CashboxRepo = cashboxRepo ?? throw new ArgumentNullException(nameof(cashboxRepo));
            JournalRepo = journalRepo ?? throw new ArgumentNullException(nameof(journalRepo));
            AccountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
            InvoiceRepo = invoiceRepo ?? throw new ArgumentNullException(nameof(invoiceRepo));
        }

        public ICashReceiptRepository ReceiptRepo { get; }
        public ICashboxRepository CashboxRepo { get; }
        public IJournalEntryRepository JournalRepo { get; }
        public IAccountRepository AccountRepo { get; }
        public ISalesInvoiceRepository InvoiceRepo { get; }
    }

    public sealed class CashReceiptServices
    {
        public CashReceiptServices(
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

    public sealed class CashReceiptValidators
    {
        public CashReceiptValidators(
            IValidator<CreateCashReceiptDto> createValidator,
            IValidator<UpdateCashReceiptDto> updateValidator)
        {
            CreateValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            UpdateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
        }

        public IValidator<CreateCashReceiptDto> CreateValidator { get; }
        public IValidator<UpdateCashReceiptDto> UpdateValidator { get; }
    }
}
