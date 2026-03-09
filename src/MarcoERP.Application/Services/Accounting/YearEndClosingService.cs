using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Accounting;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Application.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Accounting
{
    /// <summary>
    /// Implements the year-end closing logic:
    /// 1. Closes all Revenue/COGS/Expense/OtherIncome/OtherExpense accounts to Retained Earnings.
    /// 2. Generates an opening balance journal for the new fiscal year.
    /// </summary>
    [Module(SystemModule.Accounting)]
    public sealed class YearEndClosingService : IYearEndClosingService
    {
        private readonly IAccountRepository _accountRepo;
        private readonly IJournalEntryRepository _journalRepo;
        private readonly IFiscalYearRepository _fiscalYearRepo;
        private readonly IJournalNumberGenerator _journalNumberGen;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly ILogger<YearEndClosingService> _logger;
        private readonly IFeatureService _featureService;

        /// <summary>System account code for Retained Earnings (الأرباح المحتجزة).</summary>
        private const string RetainedEarningsCode = "3121";

        public YearEndClosingService(
            IAccountRepository accountRepo,
            IJournalEntryRepository journalRepo,
            IFiscalYearRepository fiscalYearRepo,
            IJournalNumberGenerator journalNumberGen,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime,
            ILogger<YearEndClosingService> logger = null,
            IFeatureService featureService = null)
        {
            _accountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
            _journalRepo = journalRepo ?? throw new ArgumentNullException(nameof(journalRepo));
            _fiscalYearRepo = fiscalYearRepo ?? throw new ArgumentNullException(nameof(fiscalYearRepo));
            _journalNumberGen = journalNumberGen ?? throw new ArgumentNullException(nameof(journalNumberGen));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<YearEndClosingService>.Instance;
            _featureService = featureService;
        }

        /// <summary>
        /// Creates and posts the year-end closing journal entry.
        /// Debits all revenue/other-income accounts (which have credit balances)
        /// and credits all COGS/expense/other-expense accounts (which have debit balances),
        /// with the net difference posted to Retained Earnings.
        /// </summary>
        public async Task<ServiceResult> GenerateClosingEntryAsync(
            int fiscalYearId, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "GenerateClosingEntryAsync", "FiscalYear", fiscalYearId);
            // Feature Guard — block operation if Accounting module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync(_featureService, FeatureKeys.Accounting, ct);
                if (guard != null) return guard;
            }

            var fiscalYear = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYearId, ct);
            if (fiscalYear == null)
                return ServiceResult.Failure("السنة المالية غير موجودة.");

            // Must be active (about to be closed)
            if (fiscalYear.Status != FiscalYearStatus.Active)
                return ServiceResult.Failure("السنة المالية ليست نشطة.");

            if (fiscalYear.Periods == null || fiscalYear.Periods.Count == 0)
                return ServiceResult.Failure("لا توجد فترات مالية في هذه السنة.");

            // Use the last period (December) for the closing entry
            var lastPeriod = fiscalYear.Periods.OrderByDescending(p => p.PeriodNumber).First();
            if (lastPeriod == null)
                return ServiceResult.Failure("لا توجد فترات مالية في هذه السنة.");

            var closingDate = fiscalYear.EndDate;
            if (!fiscalYear.ContainsDate(closingDate))
                return ServiceResult.Failure("تاريخ قيد الإقفال لا يقع ضمن السنة المالية.");

            if (!lastPeriod.ContainsDate(closingDate))
                return ServiceResult.Failure("تاريخ قيد الإقفال لا يقع ضمن الفترة المالية الأخيرة.");

            // Idempotency guard — prevent double-closing
            var periodJournals = await _journalRepo.GetByPeriodAsync(lastPeriod.Id, ct);
            if (periodJournals != null && periodJournals.Any(j =>
                j.SourceType == SourceType.Closing && j.Status == JournalEntryStatus.Posted))
            {
                return ServiceResult.Failure("تم إنشاء قيد الإقفال لهذه السنة المالية مسبقاً.");
            }

            // Get retained earnings account
            var retainedEarnings = await _accountRepo.GetByCodeAsync(RetainedEarningsCode, ct);
            if (retainedEarnings == null)
                return ServiceResult.Failure(
                    $"حساب الأرباح المحتجزة ({RetainedEarningsCode}) غير موجود في دليل الحسابات.");

            if (!retainedEarnings.CanReceivePostings())
                return ServiceResult.Failure(
                    $"حساب الأرباح المحتجزة ({RetainedEarningsCode}) لا يقبل الترحيل.");

            // Get all posted journal entry lines for this fiscal year, grouped by account
            var postedLines = await _journalRepo.GetPostedLinesByYearAsync(fiscalYearId, ct);

            // Group by account and calculate net balance
            var accountBalances = postedLines
                .GroupBy(l => l.AccountId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    NetDebit = g.Sum(l => l.DebitAmount),
                    NetCredit = g.Sum(l => l.CreditAmount)
                })
                .ToList();

            // Load all accounts to filter by type — key by AccountId
            var allAccounts = await _accountRepo.GetAllAsync(ct);
            var accountTypeMap = allAccounts.GroupBy(a => a.Id)
                .ToDictionary(g => g.Key, g => g.First().AccountType);
            var accountMap = allAccounts.GroupBy(a => a.Id)
                .ToDictionary(g => g.Key, g => g.First());

            // Income statement account types to close
            var closingTypes = new HashSet<AccountType>
            {
                AccountType.Revenue,
                AccountType.COGS,
                AccountType.Expense,
                AccountType.OtherIncome,
                AccountType.OtherExpense
            };

            var closingLines = new List<(int AccountId, decimal Debit, decimal Credit)>();
            decimal totalClosingDebit = 0;
            decimal totalClosingCredit = 0;

            foreach (var ab in accountBalances)
            {
                if (!accountTypeMap.TryGetValue(ab.AccountId, out var accountType))
                    continue;

                if (!closingTypes.Contains(accountType))
                    continue;

                // Net balance for this account
                var netBalance = ab.NetDebit - ab.NetCredit;

                if (netBalance == 0) continue;

                // To close an account, we reverse its balance:
                // If account has net debit balance (Expenses/COGS) → Credit it to zero
                // If account has net credit balance (Revenue/Income) → Debit it to zero
                if (netBalance > 0)
                {
                    // Debit balance → Credit to close
                    closingLines.Add((ab.AccountId, 0, netBalance));
                    totalClosingCredit += netBalance;
                }
                else
                {
                    // Credit balance → Debit to close
                    var absBalance = Math.Abs(netBalance);
                    closingLines.Add((ab.AccountId, absBalance, 0));
                    totalClosingDebit += absBalance;
                }
            }

            if (closingLines.Count == 0)
                return ServiceResult.Success(); // No income/expense activity to close

            // The difference goes to Retained Earnings
            var retainedDiff = totalClosingDebit - totalClosingCredit;
            if (retainedDiff > 0)
            {
                // More debits (revenue > expenses) → Net profit → Credit Retained Earnings
                closingLines.Add((retainedEarnings.Id, 0, retainedDiff));
            }
            else if (retainedDiff < 0)
            {
                // More credits (expenses > revenue) → Net loss → Debit Retained Earnings  
                closingLines.Add((retainedEarnings.Id, Math.Abs(retainedDiff), 0));
            }

            foreach (var line in closingLines)
            {
                if (!accountMap.TryGetValue(line.AccountId, out var account))
                    return ServiceResult.Failure($"الحساب بالمعرّف {line.AccountId} غير موجود أثناء تكوين قيد الإقفال.");

                if (!account.CanReceivePostings())
                    return ServiceResult.Failure($"الحساب '{account.AccountCode} - {account.AccountNameAr}' لا يقبل الترحيل في قيد الإقفال.");
            }

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var now = _dateTime.UtcNow;
                    var username = _currentUser.Username ?? "System";

                    var journalEntry = JournalEntry.CreateDraft(
                        closingDate,
                        $"قيد إقفال السنة المالية {fiscalYear.Year} — نقل الأرباح والخسائر إلى الأرباح المحتجزة",
                        SourceType.Closing,
                        fiscalYearId,
                        lastPeriod.Id);

                    foreach (var line in closingLines)
                    {
                        journalEntry.AddLine(
                            line.AccountId,
                            line.Debit,
                            line.Credit,
                            now,
                            "قيد إقفال نهاية السنة");
                    }

                    var validationErrors = journalEntry.Validate();
                    if (validationErrors.Count > 0)
                        throw new InvalidOperationException(string.Join(" | ", validationErrors));

                    var journalNumber = await _journalNumberGen.NextNumberAsync(fiscalYearId, ct);
                    journalEntry.Post(journalNumber, username, now);

                    await _journalRepo.AddAsync(journalEntry, ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                }, IsolationLevel.Serializable, ct);

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateClosingEntryAsync failed for FiscalYear.");
                return ServiceResult.Failure(ErrorSanitizer.SanitizeGeneric(ex, "إنشاء قيد الإقفال"));
            }
        }
    }
}
