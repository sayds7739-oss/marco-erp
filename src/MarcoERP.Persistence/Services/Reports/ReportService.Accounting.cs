using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Reports;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Reports;
using MarcoERP.Domain.Enums;
using MarcoERP.Persistence;

namespace MarcoERP.Persistence.Services.Reports
{
    public sealed partial class ReportService
    {
        // ════════════════════════════════════════════════════════
        //  TRIAL BALANCE
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<IReadOnlyList<TrialBalanceRowDto>>> GetTrialBalanceAsync(
            DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        {
            var rows = await (
                from line in _db.Set<Domain.Entities.Accounting.JournalEntryLine>()
                join je in _db.Set<Domain.Entities.Accounting.JournalEntry>()
                    on line.JournalEntryId equals je.Id
                join acc in _db.Set<Domain.Entities.Accounting.Account>()
                    on line.AccountId equals acc.Id
                where je.Status == JournalEntryStatus.Posted
                      && je.JournalDate >= fromDate
                      && je.JournalDate <= toDate
                      && !je.IsDeleted
                group new { line, acc } by new
                {
                    acc.Id,
                    acc.AccountCode,
                    acc.AccountNameAr,
                    acc.AccountType,
                    acc.NormalBalance,
                    acc.Level
                } into g
                orderby g.Key.AccountCode
                select new TrialBalanceRowDto
                {
                    AccountId = g.Key.Id,
                    AccountCode = g.Key.AccountCode,
                    AccountNameAr = g.Key.AccountNameAr,
                    AccountTypeName = GetAccountTypeName(g.Key.AccountType),
                    Level = g.Key.Level,
                    TotalDebit = g.Sum(x => x.line.DebitAmount),
                    TotalCredit = g.Sum(x => x.line.CreditAmount),
                    Balance = g.Key.NormalBalance == NormalBalance.Debit
                        ? g.Sum(x => x.line.DebitAmount) - g.Sum(x => x.line.CreditAmount)
                        : g.Sum(x => x.line.CreditAmount) - g.Sum(x => x.line.DebitAmount),
                    BalanceSide = (g.Key.NormalBalance == NormalBalance.Debit
                        ? g.Sum(x => x.line.DebitAmount) - g.Sum(x => x.line.CreditAmount)
                        : g.Sum(x => x.line.CreditAmount) - g.Sum(x => x.line.DebitAmount)) >= 0
                        ? "مدين" : "دائن"
                }
            ).ToListAsync(ct);

            return ServiceResult<IReadOnlyList<TrialBalanceRowDto>>.Success(rows);
        }

        // ════════════════════════════════════════════════════════
        //  ACCOUNT STATEMENT
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<AccountStatementReportDto>> GetAccountStatementAsync(
            int accountId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        {
            var account = await _db.Set<Domain.Entities.Accounting.Account>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted, ct);

            if (account == null)
                return ServiceResult<AccountStatementReportDto>.Failure("الحساب غير موجود.");

            // Opening balance = sum of all posted entries BEFORE fromDate
            var openingBalance = await (
                from line in _db.Set<Domain.Entities.Accounting.JournalEntryLine>()
                join je in _db.Set<Domain.Entities.Accounting.JournalEntry>()
                    on line.JournalEntryId equals je.Id
                where line.AccountId == accountId
                      && je.Status == JournalEntryStatus.Posted
                      && je.JournalDate < fromDate
                      && !je.IsDeleted
                select line.DebitAmount - line.CreditAmount
            ).SumAsync(ct);

            // Adjust sign based on normal balance
            if (account.NormalBalance == NormalBalance.Credit)
                openingBalance = -openingBalance;

            // Get movements in period
            var movements = await (
                from line in _db.Set<Domain.Entities.Accounting.JournalEntryLine>()
                join je in _db.Set<Domain.Entities.Accounting.JournalEntry>()
                    on line.JournalEntryId equals je.Id
                where line.AccountId == accountId
                      && je.Status == JournalEntryStatus.Posted
                      && je.JournalDate >= fromDate
                      && je.JournalDate <= toDate
                      && !je.IsDeleted
                orderby je.JournalDate, je.JournalNumber
                select new AccountStatementRowDto
                {
                    Date = je.JournalDate,
                    JournalNumber = je.JournalNumber ?? je.DraftCode,
                    Description = je.Description,
                    SourceTypeName = GetSourceTypeName(je.SourceType),
                    DebitAmount = line.DebitAmount,
                    CreditAmount = line.CreditAmount
                }
            ).ToListAsync(ct);

            // Calculate running balance
            decimal running = openingBalance;
            foreach (var row in movements)
            {
                if (account.NormalBalance == NormalBalance.Debit)
                    running += row.DebitAmount - row.CreditAmount;
                else
                    running += row.CreditAmount - row.DebitAmount;

                row.RunningBalance = running;
            }

            var report = new AccountStatementReportDto
            {
                AccountId = account.Id,
                AccountCode = account.AccountCode,
                AccountNameAr = account.AccountNameAr,
                AccountTypeName = GetAccountTypeName(account.AccountType),
                OpeningBalance = openingBalance,
                TotalDebit = movements.Sum(m => m.DebitAmount),
                TotalCredit = movements.Sum(m => m.CreditAmount),
                ClosingBalance = running,
                Rows = movements
            };

            return ServiceResult<AccountStatementReportDto>.Success(report);
        }

        // ════════════════════════════════════════════════════════
        //  INCOME STATEMENT (P&L)
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<IncomeStatementDto>> GetIncomeStatementAsync(
            DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        {
            var accountBalances = await (
                from line in _db.Set<Domain.Entities.Accounting.JournalEntryLine>()
                join je in _db.Set<Domain.Entities.Accounting.JournalEntry>()
                    on line.JournalEntryId equals je.Id
                join acc in _db.Set<Domain.Entities.Accounting.Account>()
                    on line.AccountId equals acc.Id
                where je.Status == JournalEntryStatus.Posted
                      && je.JournalDate >= fromDate
                      && je.JournalDate <= toDate
                      && !je.IsDeleted
                      && (acc.AccountType == AccountType.Revenue
                          || acc.AccountType == AccountType.COGS
                          || acc.AccountType == AccountType.Expense
                          || acc.AccountType == AccountType.OtherIncome
                          || acc.AccountType == AccountType.OtherExpense)
                group line by new
                {
                    acc.AccountCode,
                    acc.AccountNameAr,
                    acc.AccountType,
                    acc.NormalBalance
                } into g
                select new
                {
                    g.Key.AccountCode,
                    g.Key.AccountNameAr,
                    g.Key.AccountType,
                    g.Key.NormalBalance,
                    Amount = g.Key.NormalBalance == NormalBalance.Debit
                        ? g.Sum(x => x.DebitAmount) - g.Sum(x => x.CreditAmount)
                        : g.Sum(x => x.CreditAmount) - g.Sum(x => x.DebitAmount)
                }
            ).ToListAsync(ct);

            var result = new IncomeStatementDto();

            foreach (var ab in accountBalances.OrderBy(a => a.AccountCode))
            {
                var row = new IncomeStatementRowDto
                {
                    AccountCode = ab.AccountCode,
                    AccountNameAr = ab.AccountNameAr,
                    AccountTypeName = GetAccountTypeName(ab.AccountType),
                    Amount = ab.Amount
                };

                switch (ab.AccountType)
                {
                    case AccountType.Revenue:
                        result.RevenueRows.Add(row);
                        break;
                    case AccountType.COGS:
                        result.CogsRows.Add(row);
                        break;
                    case AccountType.Expense:
                        result.ExpenseRows.Add(row);
                        break;
                    case AccountType.OtherIncome:
                        result.OtherIncomeRows.Add(row);
                        break;
                    case AccountType.OtherExpense:
                        result.OtherExpenseRows.Add(row);
                        break;
                }
            }

            result.TotalRevenue = result.RevenueRows.Sum(r => r.Amount);
            result.TotalCogs = result.CogsRows.Sum(r => r.Amount);
            result.GrossProfit = result.TotalRevenue - result.TotalCogs;
            result.TotalExpenses = result.ExpenseRows.Sum(r => r.Amount);
            result.TotalOtherIncome = result.OtherIncomeRows.Sum(r => r.Amount);
            result.TotalOtherExpenses = result.OtherExpenseRows.Sum(r => r.Amount);
            result.NetProfit = result.GrossProfit - result.TotalExpenses
                             + result.TotalOtherIncome - result.TotalOtherExpenses;

            return ServiceResult<IncomeStatementDto>.Success(result);
        }

        // ════════════════════════════════════════════════════════
        //  BALANCE SHEET
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<BalanceSheetDto>> GetBalanceSheetAsync(
            DateTime asOfDate, CancellationToken ct = default)
        {
            var accountBalances = await (
                from line in _db.Set<Domain.Entities.Accounting.JournalEntryLine>()
                join je in _db.Set<Domain.Entities.Accounting.JournalEntry>()
                    on line.JournalEntryId equals je.Id
                join acc in _db.Set<Domain.Entities.Accounting.Account>()
                    on line.AccountId equals acc.Id
                where je.Status == JournalEntryStatus.Posted
                      && je.JournalDate <= asOfDate
                      && !je.IsDeleted
                      && (acc.AccountType == AccountType.Asset
                          || acc.AccountType == AccountType.Liability
                          || acc.AccountType == AccountType.Equity)
                group line by new
                {
                    acc.AccountCode,
                    acc.AccountNameAr,
                    acc.AccountType,
                    acc.NormalBalance,
                    acc.Level
                } into g
                select new
                {
                    g.Key.AccountCode,
                    g.Key.AccountNameAr,
                    g.Key.AccountType,
                    g.Key.NormalBalance,
                    g.Key.Level,
                    Balance = g.Key.NormalBalance == NormalBalance.Debit
                        ? g.Sum(x => x.DebitAmount) - g.Sum(x => x.CreditAmount)
                        : g.Sum(x => x.CreditAmount) - g.Sum(x => x.DebitAmount)
                }
            ).ToListAsync(ct);

            // Also calculate retained earnings from income statement accounts (server-side aggregation)
            var retainedEarningsQuery =
                from line in _db.Set<Domain.Entities.Accounting.JournalEntryLine>()
                join je in _db.Set<Domain.Entities.Accounting.JournalEntry>()
                    on line.JournalEntryId equals je.Id
                join acc in _db.Set<Domain.Entities.Accounting.Account>()
                    on line.AccountId equals acc.Id
                where je.Status == JournalEntryStatus.Posted
                      && je.JournalDate <= asOfDate
                      && !je.IsDeleted
                      && (acc.AccountType == AccountType.Revenue
                          || acc.AccountType == AccountType.COGS
                          || acc.AccountType == AccountType.Expense
                          || acc.AccountType == AccountType.OtherIncome
                          || acc.AccountType == AccountType.OtherExpense)
                group new { line.DebitAmount, line.CreditAmount, acc.NormalBalance }
                    by acc.NormalBalance into g
                select new
                {
                    NormalBalance = g.Key,
                    TotalDebit = g.Sum(x => x.DebitAmount),
                    TotalCredit = g.Sum(x => x.CreditAmount)
                };

            var retainedEarningGroups = await retainedEarningsQuery.ToListAsync(ct);

            decimal retainedEarningsAmount = retainedEarningGroups.Sum(r =>
                r.NormalBalance == NormalBalance.Credit
                    ? r.TotalCredit - r.TotalDebit
                    : -(r.TotalDebit - r.TotalCredit));

            var result = new BalanceSheetDto();

            foreach (var ab in accountBalances.OrderBy(a => a.AccountCode))
            {
                var row = new BalanceSheetRowDto
                {
                    AccountCode = ab.AccountCode,
                    AccountNameAr = ab.AccountNameAr,
                    AccountTypeName = GetAccountTypeName(ab.AccountType),
                    Level = ab.Level,
                    Balance = ab.Balance
                };

                switch (ab.AccountType)
                {
                    case AccountType.Asset:
                        result.AssetRows.Add(row);
                        break;
                    case AccountType.Liability:
                        result.LiabilityRows.Add(row);
                        break;
                    case AccountType.Equity:
                        result.EquityRows.Add(row);
                        break;
                }
            }

            result.TotalAssets = result.AssetRows.Sum(r => r.Balance);
            result.TotalLiabilities = result.LiabilityRows.Sum(r => r.Balance);
            result.TotalEquity = result.EquityRows.Sum(r => r.Balance);
            result.RetainedEarnings = retainedEarningsAmount;
            result.TotalLiabilitiesAndEquity = result.TotalLiabilities
                                             + result.TotalEquity
                                             + result.RetainedEarnings;

            return ServiceResult<BalanceSheetDto>.Success(result);
        }


        // ════════════════════════════════════════════════════════
        //  JOURNAL REGISTER (سجل القيود المحاسبية)
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<JournalRegisterDto>> GetJournalRegisterAsync(
            JournalRegisterRequestDto request, CancellationToken ct = default)
        {
            if (request == null)
                return ServiceResult<JournalRegisterDto>.Failure("بيانات الطلب مطلوبة.");

            var rows = await _db.Set<Domain.Entities.Accounting.JournalEntry>()
                .AsNoTracking()
                .Where(je => !je.IsDeleted
                          && je.JournalDate >= request.FromDate
                          && je.JournalDate <= request.ToDate)
                .OrderBy(je => je.JournalDate)
                .ThenBy(je => je.JournalNumber ?? je.DraftCode)
                .Select(je => new JournalRegisterRowDto
                {
                    JournalEntryId = je.Id,
                    EntryNumber = je.JournalNumber ?? je.DraftCode,
                    Date = je.JournalDate,
                    Description = je.Description,
                    TotalDebit = je.TotalDebit,
                    TotalCredit = je.TotalCredit,
                    Status = je.Status == JournalEntryStatus.Draft ? "مسودة"
                           : je.Status == JournalEntryStatus.Posted ? "مرحّل"
                           : je.Status == JournalEntryStatus.Reversed ? "معكوس"
                           : je.Status.ToString(),
                    SourceType = GetSourceTypeName(je.SourceType)
                })
                .ToListAsync(ct);

            var result = new JournalRegisterDto
            {
                EntryCount = rows.Count,
                TotalDebit = rows.Sum(r => r.TotalDebit),
                TotalCredit = rows.Sum(r => r.TotalCredit),
                Rows = rows
            };

            return ServiceResult<JournalRegisterDto>.Success(result);
        }

        // ════════════════════════════════════════════════════════
        //  DASHBOARD
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<DashboardSummaryDto>> GetDashboardSummaryAsync(CancellationToken ct = default)
        {
            var today = _dateTime.Today;
            var tomorrow = today.AddDays(1);
            var yesterday = today.AddDays(-1);
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var lastMonthStart = monthStart.AddMonths(-1);

            var dto = new DashboardSummaryDto();

            // ── Today's sales ──
            dto.TodaySales = await _db.Set<Domain.Entities.Sales.SalesInvoice>()
                .Where(i => i.Status == InvoiceStatus.Posted && i.InvoiceDate >= today && i.InvoiceDate < tomorrow)
                .SumAsync(i => i.NetTotal, ct);
            dto.TodaySalesCount = await _db.Set<Domain.Entities.Sales.SalesInvoice>()
                .CountAsync(i => i.Status == InvoiceStatus.Posted && i.InvoiceDate >= today && i.InvoiceDate < tomorrow, ct);

            // ── Today's purchases ──
            dto.TodayPurchases = await _db.Set<Domain.Entities.Purchases.PurchaseInvoice>()
                .Where(i => i.Status == InvoiceStatus.Posted && i.InvoiceDate >= today && i.InvoiceDate < tomorrow)
                .SumAsync(i => i.NetTotal, ct);
            dto.TodayPurchasesCount = await _db.Set<Domain.Entities.Purchases.PurchaseInvoice>()
                .CountAsync(i => i.Status == InvoiceStatus.Posted && i.InvoiceDate >= today && i.InvoiceDate < tomorrow, ct);

            // ── Today's receipts & payments ──
            dto.TodayReceipts = await _db.Set<Domain.Entities.Treasury.CashReceipt>()
                .Where(r => r.Status == InvoiceStatus.Posted && r.ReceiptDate >= today && r.ReceiptDate < tomorrow)
                .SumAsync(r => r.Amount, ct);
            dto.TodayPayments = await _db.Set<Domain.Entities.Treasury.CashPayment>()
                .Where(p => p.Status == InvoiceStatus.Posted && p.PaymentDate >= today && p.PaymentDate < tomorrow)
                .SumAsync(p => p.Amount, ct);

            dto.DailyNetProfit = dto.TodaySales - dto.TodayPurchases;

            // ── Yesterday deltas ──
            var yesterdaySales = await _db.Set<Domain.Entities.Sales.SalesInvoice>()
                .Where(i => i.Status == InvoiceStatus.Posted && i.InvoiceDate >= yesterday && i.InvoiceDate < today)
                .SumAsync(i => i.NetTotal, ct);
            var yesterdayPurchases = await _db.Set<Domain.Entities.Purchases.PurchaseInvoice>()
                .Where(i => i.Status == InvoiceStatus.Posted && i.InvoiceDate >= yesterday && i.InvoiceDate < today)
                .SumAsync(i => i.NetTotal, ct);
            var yesterdayReceipts = await _db.Set<Domain.Entities.Treasury.CashReceipt>()
                .Where(r => r.Status == InvoiceStatus.Posted && r.ReceiptDate >= yesterday && r.ReceiptDate < today)
                .SumAsync(r => r.Amount, ct);
            var yesterdayPayments = await _db.Set<Domain.Entities.Treasury.CashPayment>()
                .Where(p => p.Status == InvoiceStatus.Posted && p.PaymentDate >= yesterday && p.PaymentDate < today)
                .SumAsync(p => p.Amount, ct);

            dto.TodaySalesDelta = dto.TodaySales - yesterdaySales;
            dto.TodayPurchasesDelta = dto.TodayPurchases - yesterdayPurchases;
            dto.TodayReceiptsDelta = dto.TodayReceipts - yesterdayReceipts;
            dto.TodayPaymentsDelta = dto.TodayPayments - yesterdayPayments;

            // ── Month totals ──
            dto.MonthSales = await _db.Set<Domain.Entities.Sales.SalesInvoice>()
                .Where(i => i.Status == InvoiceStatus.Posted && i.InvoiceDate >= monthStart && i.InvoiceDate < tomorrow)
                .SumAsync(i => i.NetTotal, ct);
            dto.MonthPurchases = await _db.Set<Domain.Entities.Purchases.PurchaseInvoice>()
                .Where(i => i.Status == InvoiceStatus.Posted && i.InvoiceDate >= monthStart && i.InvoiceDate < tomorrow)
                .SumAsync(i => i.NetTotal, ct);
            dto.MonthReceipts = await _db.Set<Domain.Entities.Treasury.CashReceipt>()
                .Where(r => r.Status == InvoiceStatus.Posted && r.ReceiptDate >= monthStart && r.ReceiptDate < tomorrow)
                .SumAsync(r => r.Amount, ct);
            dto.MonthPayments = await _db.Set<Domain.Entities.Treasury.CashPayment>()
                .Where(p => p.Status == InvoiceStatus.Posted && p.PaymentDate >= monthStart && p.PaymentDate < tomorrow)
                .SumAsync(p => p.Amount, ct);

            // ── Last month deltas ──
            var lastMonthSales = await _db.Set<Domain.Entities.Sales.SalesInvoice>()
                .Where(i => i.Status == InvoiceStatus.Posted && i.InvoiceDate >= lastMonthStart && i.InvoiceDate < monthStart)
                .SumAsync(i => i.NetTotal, ct);
            var lastMonthPurchases = await _db.Set<Domain.Entities.Purchases.PurchaseInvoice>()
                .Where(i => i.Status == InvoiceStatus.Posted && i.InvoiceDate >= lastMonthStart && i.InvoiceDate < monthStart)
                .SumAsync(i => i.NetTotal, ct);
            var lastMonthReceipts = await _db.Set<Domain.Entities.Treasury.CashReceipt>()
                .Where(r => r.Status == InvoiceStatus.Posted && r.ReceiptDate >= lastMonthStart && r.ReceiptDate < monthStart)
                .SumAsync(r => r.Amount, ct);
            var lastMonthPayments = await _db.Set<Domain.Entities.Treasury.CashPayment>()
                .Where(p => p.Status == InvoiceStatus.Posted && p.PaymentDate >= lastMonthStart && p.PaymentDate < monthStart)
                .SumAsync(p => p.Amount, ct);

            dto.MonthSalesDelta = dto.MonthSales - lastMonthSales;
            dto.MonthPurchasesDelta = dto.MonthPurchases - lastMonthPurchases;
            dto.MonthReceiptsDelta = dto.MonthReceipts - lastMonthReceipts;
            dto.MonthPaymentsDelta = dto.MonthPayments - lastMonthPayments;

            // ── Low stock count ──
            dto.LowStockCount = await _db.Set<Domain.Entities.Inventory.WarehouseProduct>()
                .Include(wp => wp.Product)
                .CountAsync(wp => wp.Quantity < wp.Product.MinimumStock && !wp.Product.IsDeleted, ct);
            dto.TotalProducts = await _db.Set<Domain.Entities.Inventory.Product>()
                .CountAsync(p => !p.IsDeleted, ct);

            // ── Pending drafts ──
            dto.PendingSalesInvoices = await _db.Set<Domain.Entities.Sales.SalesInvoice>()
                .CountAsync(i => i.Status == InvoiceStatus.Draft, ct);
            dto.PendingPurchaseInvoices = await _db.Set<Domain.Entities.Purchases.PurchaseInvoice>()
                .CountAsync(i => i.Status == InvoiceStatus.Draft, ct);
            dto.PendingJournalEntries = await _db.Set<Domain.Entities.Accounting.JournalEntry>()
                .CountAsync(j => j.Status == JournalEntryStatus.Draft && !j.IsDeleted, ct);

            // ── Running Balances (from posted GL entries) ──
            var postedJournalIds = _db.Set<Domain.Entities.Accounting.JournalEntry>()
                .Where(j => j.Status == JournalEntryStatus.Posted && !j.IsDeleted)
                .Select(j => j.Id);

            var postedLines = _db.Set<Domain.Entities.Accounting.JournalEntryLine>()
                .Where(l => postedJournalIds.Contains(l.JournalEntryId));

            // Cash Balance: all accounts under "111" (Cash & Banks)
            var cashAccountIds = await _db.Set<Domain.Entities.Accounting.Account>()
                .Where(a => a.AccountCode.StartsWith("111"))
                .Select(a => a.Id).ToListAsync(ct);
            dto.CashBalance = await postedLines
                .Where(l => cashAccountIds.Contains(l.AccountId))
                .SumAsync(l => l.DebitAmount - l.CreditAmount, ct);

            // Customer AR: all accounts under "112" (Receivables)
            var arAccountIds = await _db.Set<Domain.Entities.Accounting.Account>()
                .Where(a => a.AccountCode.StartsWith("112"))
                .Select(a => a.Id).ToListAsync(ct);
            dto.TotalCustomerBalance = await postedLines
                .Where(l => arAccountIds.Contains(l.AccountId))
                .SumAsync(l => l.DebitAmount - l.CreditAmount, ct);

            // Supplier AP: all accounts under "211" (Payables) — shown as positive amount owed
            var apAccountIds = await _db.Set<Domain.Entities.Accounting.Account>()
                .Where(a => a.AccountCode.StartsWith("211"))
                .Select(a => a.Id).ToListAsync(ct);
            dto.TotalSupplierBalance = await postedLines
                .Where(l => apAccountIds.Contains(l.AccountId))
                .SumAsync(l => l.CreditAmount - l.DebitAmount, ct);

            // Month Gross Profit: Sales - Purchases (commercial view)
            dto.MonthGrossProfit = dto.MonthSales - dto.MonthPurchases;
            dto.GrossMarginPercent = dto.MonthSales == 0m
                ? 0m
                : Math.Round((dto.MonthGrossProfit / dto.MonthSales) * 100m, 2);

            return ServiceResult<DashboardSummaryDto>.Success(dto);
        }
    }
}
