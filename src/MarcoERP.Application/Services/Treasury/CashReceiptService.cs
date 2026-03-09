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
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Exceptions.Treasury;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Domain.Interfaces.Settings;
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
        private readonly ISystemSettingRepository _systemSettingRepository;
        private readonly JournalEntryFactory _journalFactory;
        private readonly FiscalPeriodValidator _fiscalValidator;
        private readonly IFeatureService _featureService;

        private const string ReceiptNotFoundMessage = "سند القبض غير موجود.";

        public CashReceiptService(
            CashReceiptRepositories repos,
            CashReceiptServices services,
            CashReceiptValidators validators,
            JournalEntryFactory journalFactory,
            FiscalPeriodValidator fiscalValidator,
            ILogger<CashReceiptService> logger,
            IFeatureService featureService = null)
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
            _systemSettingRepository = services.SystemSettingRepo;

            _createValidator = validators.CreateValidator;
            _updateValidator = validators.UpdateValidator;
            _journalFactory = journalFactory ?? throw new ArgumentNullException(nameof(journalFactory));
            _fiscalValidator = fiscalValidator ?? throw new ArgumentNullException(nameof(fiscalValidator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _featureService = featureService;
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
            // Feature Guard — block operation if Treasury module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<CashReceiptDto>(_featureService, FeatureKeys.Treasury, ct);
                if (guard != null) return guard;
            }

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "CashReceipt", 0);

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
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<CashReceiptDto>.Failure("تم تعديل سند القبض بواسطة مستخدم آخر. يرجى إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult<CashReceiptDto>.Failure("تعذر حفظ سند القبض بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
            }
        }

        public async Task<ServiceResult<CashReceiptDto>> UpdateAsync(UpdateCashReceiptDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "CashReceipt", dto.Id);

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<CashReceiptDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var receipt = await _receiptRepo.GetWithDetailsTrackedAsync(dto.Id, ct);
            if (receipt == null)
                return ServiceResult<CashReceiptDto>.Failure(ReceiptNotFoundMessage);

            if (receipt.Status != InvoiceStatus.Draft)
                return ServiceResult<CashReceiptDto>.Failure("لا يمكن تعديل سند قبض مرحّل أو ملغى.");

            try
            {
                receipt.UpdateHeader(
                    dto.ReceiptDate, dto.CashboxId, dto.AccountId,
                    dto.Amount, dto.Description, dto.CustomerId, dto.SalesInvoiceId, dto.Notes);

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
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "PostAsync", "CashReceipt", id);

            var receipt = await _receiptRepo.GetWithDetailsTrackedAsync(id, ct);
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
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while posting cash receipt.");
                return ServiceResult<CashReceiptDto>.Failure(
                    ErrorSanitizer.Sanitize(ex, "ترحيل سند القبض"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<CashReceiptDto>.Failure("تعذر ترحيل سند القبض بسبب تعارض تحديث متزامن. يرجى إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult<CashReceiptDto>.Failure("تعذر ترحيل سند القبض بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ غير متوقع أثناء ترحيل سند القبض {ReceiptId}", id);
                return ServiceResult<CashReceiptDto>.Failure("حدث خطأ غير متوقع أثناء ترحيل سند القبض.");
            }
        }

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CancelAsync", "CashReceipt", id);

            var receipt = await _receiptRepo.GetWithDetailsTrackedAsync(id, ct);
            if (receipt == null)
                return ServiceResult.Failure(ReceiptNotFoundMessage);

            if (!receipt.JournalEntryId.HasValue)
                return ServiceResult.Failure("لا يمكن إلغاء سند بدون قيد محاسبي.");

            try
            {
                var context = await _fiscalValidator.ValidateForCancelAsync(receipt.ReceiptDate, ct);
                await ExecuteCancelAsync(receipt, context, ct);
                return ServiceResult.Success();
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while cancelling cash receipt.");
                return ServiceResult.Failure(
                    ErrorSanitizer.Sanitize(ex, "إلغاء سند القبض"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult.Failure("تعذر إلغاء سند القبض بسبب تعارض تحديث متزامن. يرجى إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult.Failure("تعذر إلغاء سند القبض بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
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
                var reloaded = await _receiptRepo.GetWithDetailsTrackedAsync(receipt.Id, ct);
                if (reloaded == null)
                    throw new TreasuryDomainException(ReceiptNotFoundMessage);
                if (reloaded.Status != InvoiceStatus.Draft)
                    throw new TreasuryDomainException("لا يمكن ترحيل سند قبض مرحّل بالفعل أو ملغى.");

                // CRITICAL: Re-validate invoice balance inside transaction to prevent double-payment
                if (reloaded.SalesInvoiceId.HasValue)
                {
                    var linkedInvoice = await _invoiceRepo.GetWithLinesTrackedAsync(reloaded.SalesInvoiceId.Value, ct);
                    if (linkedInvoice == null)
                        throw new TreasuryDomainException("الفاتورة المرتبطة غير موجودة.");
                    if (linkedInvoice.Status != InvoiceStatus.Posted)
                        throw new TreasuryDomainException("الفاتورة المرتبطة غير مرحلة.");
                    if (reloaded.Amount > linkedInvoice.BalanceDue)
                        throw new TreasuryDomainException(
                            $"مبلغ سند القبض ({reloaded.Amount:N2}) يتجاوز الرصيد المستحق على الفاتورة ({linkedInvoice.BalanceDue:N2}).");
                }

                var accounts = await ResolveAccountsAsync(reloaded, ct);
                var context = await _fiscalValidator.ValidateForPostingAsync(reloaded.ReceiptDate, ct);

                var journalEntry = await CreateJournalEntryAsync(reloaded, accounts, context, ct);

                await _unitOfWork.SaveChangesAsync(ct);

                reloaded.Post(journalEntry.Id);

                // CSH-03: Increase cashbox balance (money received)
                accounts.Cashbox.IncreaseBalance(reloaded.Amount);
                _cashboxRepo.Update(accounts.Cashbox);

                // Apply payment to linked invoice if present
                if (reloaded.SalesInvoiceId.HasValue)
                {
                    var invoice = await _invoiceRepo.GetWithLinesTrackedAsync(reloaded.SalesInvoiceId.Value, ct);
                    if (invoice != null)
                    {
                        invoice.ApplyPayment(reloaded.Amount);
                    }
                }

                await _unitOfWork.SaveChangesAsync(ct);

                saved = await _receiptRepo.GetWithDetailsAsync(reloaded.Id, ct);
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

        private async Task<JournalEntry> CreateJournalEntryAsync(
            CashReceipt receipt,
            CashReceiptAccounts accounts,
            PostingContext context,
            CancellationToken ct)
        {
            var lines = new[]
            {
                new JournalLineSpec(accounts.CashboxAccount.Id, receipt.Amount, 0,
                    $"قبض نقدي — {accounts.Cashbox.NameAr}"),
                new JournalLineSpec(accounts.ContraAccount.Id, 0, receipt.Amount,
                    $"سند قبض {receipt.ReceiptNumber} — {accounts.ContraAccount.AccountNameAr}")
            };

            return await _journalFactory.CreateAndPostAsync(
                receipt.ReceiptDate,
                $"سند قبض رقم {receipt.ReceiptNumber} — {receipt.Description}",
                SourceType.CashReceipt,
                context.FiscalYear.Id,
                context.Period.Id,
                lines,
                context.Username,
                context.Now,
                referenceNumber: receipt.ReceiptNumber,
                sourceId: receipt.Id,
                ct: ct);
        }

        private async Task ExecuteCancelAsync(CashReceipt receipt, CancelContext context, CancellationToken ct)
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

                // CSH-03: Decrease cashbox balance (receipt cancelled — money reversed)
                var cashbox = await _cashboxRepo.GetByIdAsync(receipt.CashboxId, ct)
                    ?? throw new TreasuryDomainException("الصندوق المرتبط بالسند غير موجود. لا يمكن إلغاء السند.");
                cashbox.DecreaseBalanceAllowNegative(receipt.Amount);
                _cashboxRepo.Update(cashbox);

                // Reverse payment on linked invoice if present
                if (receipt.SalesInvoiceId.HasValue)
                {
                    var invoice = await _invoiceRepo.GetWithLinesTrackedAsync(receipt.SalesInvoiceId.Value, ct);
                    if (invoice != null && invoice.PaidAmount > 0)
                    {
                        // Use Math.Min to handle partial-reversal edge cases safely
                        var reversalAmount = Math.Min(receipt.Amount, invoice.PaidAmount);
                        invoice.ReversePayment(reversalAmount);
                    }
                }

                await _unitOfWork.SaveChangesAsync(ct);

            }, IsolationLevel.Serializable, ct);
        }

        private async Task ReverseJournalAsync(
            int journalId,
            string description,
            string notFoundMessage,
            CancelContext context,
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

            var reversalNumber = await _journalNumberGen.NextNumberAsync(context.FiscalYear.Id, ct);
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
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteDraftAsync", "CashReceipt", id);

            var receipt = await _receiptRepo.GetWithDetailsTrackedAsync(id, ct);
            if (receipt == null)
                return ServiceResult.Failure(ReceiptNotFoundMessage);

            if (receipt.Status != InvoiceStatus.Draft)
                return ServiceResult.Failure("لا يمكن حذف إلا سندات القبض المسودة.");

            receipt.SoftDelete(_currentUser.Username, _dateTime.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        private sealed class CashReceiptAccounts
        {
            public Cashbox Cashbox { get; init; } = default!;
            public Account CashboxAccount { get; init; } = default!;
            public Account ContraAccount { get; init; } = default!;
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
            IDateTimeProvider dateTime,
            ISystemSettingRepository systemSettingRepo)
        {
            FiscalYearRepo = fiscalYearRepo ?? throw new ArgumentNullException(nameof(fiscalYearRepo));
            JournalNumberGen = journalNumberGen ?? throw new ArgumentNullException(nameof(journalNumberGen));
            UnitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            CurrentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            DateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            SystemSettingRepo = systemSettingRepo ?? throw new ArgumentNullException(nameof(systemSettingRepo));
        }

        public IFiscalYearRepository FiscalYearRepo { get; }
        public IJournalNumberGenerator JournalNumberGen { get; }
        public IUnitOfWork UnitOfWork { get; }
        public ICurrentUserService CurrentUser { get; }
        public IDateTimeProvider DateTime { get; }
        public ISystemSettingRepository SystemSettingRepo { get; }
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
