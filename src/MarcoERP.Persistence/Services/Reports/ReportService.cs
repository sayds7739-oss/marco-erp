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
    /// <summary>
    /// Implementation of IReportService using direct EF Core queries
    /// against the MarcoDbContext for efficient aggregation.
    /// </summary>
    public sealed class ReportService : IReportService
    {
        private readonly MarcoDbContext _db;
        private readonly IDateTimeProvider _dateTime;

        public ReportService(MarcoDbContext db, IDateTimeProvider dateTime)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
        }

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
                    Amount = Math.Abs(ab.Amount)
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
        //  SALES REPORT
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<SalesReportDto>> GetSalesReportAsync(
            DateTime fromDate, DateTime toDate, int? customerId = null, CancellationToken ct = default)
        {
            var query = _db.Set<Domain.Entities.Sales.SalesInvoice>()
                .AsNoTracking()
                .Include(i => i.Customer)
                .Where(i => i.Status == InvoiceStatus.Posted
                         && i.InvoiceDate >= fromDate
                         && i.InvoiceDate <= toDate);

            if (customerId.HasValue)
                query = query.Where(i => i.CustomerId == customerId.Value);

            var invoices = await query.OrderByDescending(i => i.InvoiceDate)
                .Select(i => new SalesReportRowDto
                {
                    InvoiceId = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDate = i.InvoiceDate,
                    CustomerName = i.Customer.NameAr,
                    Status = "مرحّل",
                    Subtotal = i.Subtotal,
                    DiscountTotal = i.DiscountTotal,
                    VatTotal = i.VatTotal,
                    NetTotal = i.NetTotal
                }).ToListAsync(ct);

            var result = new SalesReportDto
            {
                Rows = invoices,
                InvoiceCount = invoices.Count,
                TotalSubtotal = invoices.Sum(r => r.Subtotal),
                TotalDiscount = invoices.Sum(r => r.DiscountTotal),
                TotalVat = invoices.Sum(r => r.VatTotal),
                TotalNet = invoices.Sum(r => r.NetTotal)
            };

            return ServiceResult<SalesReportDto>.Success(result);
        }

        // ════════════════════════════════════════════════════════
        //  PURCHASE REPORT
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<PurchaseReportDto>> GetPurchaseReportAsync(
            DateTime fromDate, DateTime toDate, int? supplierId = null, CancellationToken ct = default)
        {
            var query = _db.Set<Domain.Entities.Purchases.PurchaseInvoice>()
                .AsNoTracking()
                .Include(i => i.Supplier)
                .Where(i => i.Status == InvoiceStatus.Posted
                         && i.InvoiceDate >= fromDate
                         && i.InvoiceDate <= toDate);

            if (supplierId.HasValue)
                query = query.Where(i => i.SupplierId == supplierId.Value);

            var invoices = await query.OrderByDescending(i => i.InvoiceDate)
                .Select(i => new PurchaseReportRowDto
                {
                    InvoiceId = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDate = i.InvoiceDate,
                    SupplierName = i.Supplier.NameAr,
                    Status = "مرحّل",
                    Subtotal = i.Subtotal,
                    DiscountTotal = i.DiscountTotal,
                    VatTotal = i.VatTotal,
                    NetTotal = i.NetTotal
                }).ToListAsync(ct);

            var result = new PurchaseReportDto
            {
                Rows = invoices,
                InvoiceCount = invoices.Count,
                TotalSubtotal = invoices.Sum(r => r.Subtotal),
                TotalDiscount = invoices.Sum(r => r.DiscountTotal),
                TotalVat = invoices.Sum(r => r.VatTotal),
                TotalNet = invoices.Sum(r => r.NetTotal)
            };

            return ServiceResult<PurchaseReportDto>.Success(result);
        }

        // ════════════════════════════════════════════════════════
        //  PROFIT REPORT
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<ProfitReportDto>> GetProfitReportAsync(
            DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        {
            // Sales per product (from posted sales invoices)
            var salesData = await (
                from line in _db.Set<Domain.Entities.Sales.SalesInvoiceLine>()
                join inv in _db.Set<Domain.Entities.Sales.SalesInvoice>()
                    on line.SalesInvoiceId equals inv.Id
                join prod in _db.Set<Domain.Entities.Inventory.Product>()
                    on line.ProductId equals prod.Id
                where inv.Status == InvoiceStatus.Posted
                      && inv.InvoiceDate >= fromDate
                      && inv.InvoiceDate <= toDate
                group line by new { prod.Id, prod.Code, prod.NameAr, prod.WeightedAverageCost } into g
                select new
                {
                    ProductId = g.Key.Id,
                    ProductCode = g.Key.Code,
                    ProductName = g.Key.NameAr,
                    WACost = g.Key.WeightedAverageCost,
                    TotalQuantity = g.Sum(x => x.BaseQuantity),
                    TotalSalesAmount = g.Sum(x => x.NetTotal)
                }
            ).ToListAsync(ct);

            var rows = salesData.Select(s => new ProfitReportRowDto
            {
                ProductId = s.ProductId,
                ProductCode = s.ProductCode,
                ProductName = s.ProductName,
                TotalSalesQuantity = s.TotalQuantity,
                TotalSalesAmount = s.TotalSalesAmount,
                TotalCostAmount = s.TotalQuantity * s.WACost,
                GrossProfit = s.TotalSalesAmount - (s.TotalQuantity * s.WACost),
                ProfitMarginPercent = s.TotalSalesAmount != 0
                    ? Math.Round((s.TotalSalesAmount - (s.TotalQuantity * s.WACost)) / s.TotalSalesAmount * 100, 2)
                    : 0
            }).OrderByDescending(r => r.GrossProfit).ToList();

            var result = new ProfitReportDto
            {
                Rows = rows,
                TotalSales = rows.Sum(r => r.TotalSalesAmount),
                TotalCost = rows.Sum(r => r.TotalCostAmount),
                TotalProfit = rows.Sum(r => r.GrossProfit),
                OverallMarginPercent = rows.Sum(r => r.TotalSalesAmount) != 0
                    ? Math.Round(rows.Sum(r => r.GrossProfit) / rows.Sum(r => r.TotalSalesAmount) * 100, 2)
                    : 0
            };

            return ServiceResult<ProfitReportDto>.Success(result);
        }

        // ════════════════════════════════════════════════════════
        //  INVENTORY REPORT
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<IReadOnlyList<InventoryReportRowDto>>> GetInventoryReportAsync(
            int? warehouseId = null, CancellationToken ct = default)
        {
            var query = _db.Set<Domain.Entities.Inventory.WarehouseProduct>()
                .AsNoTracking()
                .Include(wp => wp.Product).ThenInclude(p => p.Category)
                .Include(wp => wp.Product).ThenInclude(p => p.BaseUnit)
                .Include(wp => wp.Warehouse)
                .Where(wp => !wp.Product.IsDeleted && wp.Quantity != 0);

            if (warehouseId.HasValue)
                query = query.Where(wp => wp.WarehouseId == warehouseId.Value);

            var rows = await query
                .OrderBy(wp => wp.Product.Code)
                .ThenBy(wp => wp.Warehouse.NameAr)
                .Select(wp => new InventoryReportRowDto
                {
                    ProductId = wp.ProductId,
                    ProductCode = wp.Product.Code,
                    ProductName = wp.Product.NameAr,
                    CategoryName = wp.Product.Category.NameAr,
                    WarehouseName = wp.Warehouse.NameAr,
                    UnitName = wp.Product.BaseUnit.NameAr,
                    Quantity = wp.Quantity,
                    CostPrice = wp.Product.WeightedAverageCost,
                    TotalValue = wp.Quantity * wp.Product.WeightedAverageCost,
                    MinimumStock = wp.Product.MinimumStock,
                    IsBelowMinimum = wp.Quantity < wp.Product.MinimumStock
                }).ToListAsync(ct);

            return ServiceResult<IReadOnlyList<InventoryReportRowDto>>.Success(rows);
        }

        // ════════════════════════════════════════════════════════
        //  STOCK CARD
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<StockCardReportDto>> GetStockCardAsync(
            int productId, int? warehouseId, DateTime fromDate, DateTime toDate,
            CancellationToken ct = default)
        {
            var product = await _db.Set<Domain.Entities.Inventory.Product>()
                .AsNoTracking()
                .Include(p => p.BaseUnit)
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, ct);

            if (product == null)
                return ServiceResult<StockCardReportDto>.Failure("الصنف غير موجود.");

            var movQuery = _db.Set<Domain.Entities.Inventory.InventoryMovement>()
                .AsNoTracking()
                .Include(m => m.Warehouse)
                .Where(m => m.ProductId == productId);

            if (warehouseId.HasValue)
                movQuery = movQuery.Where(m => m.WarehouseId == warehouseId.Value);

            // Opening balance = server-side sum of movements before fromDate
            var incomingTypes = new[]
            {
                Domain.Enums.MovementType.PurchaseIn,
                Domain.Enums.MovementType.SalesReturn,
                Domain.Enums.MovementType.AdjustmentIn,
                Domain.Enums.MovementType.TransferIn,
                Domain.Enums.MovementType.OpeningBalance
            };

            decimal openingBalance = await movQuery
                .Where(m => m.MovementDate < fromDate)
                .SumAsync(m => incomingTypes.Contains(m.MovementType)
                    ? m.QuantityInBaseUnit
                    : -m.QuantityInBaseUnit, ct);

            // Movements in range — projected to avoid materializing full entities
            var movements = await movQuery
                .Where(m => m.MovementDate >= fromDate && m.MovementDate <= toDate)
                .OrderBy(m => m.MovementDate)
                .ThenBy(m => m.Id)
                .Select(m => new
                {
                    m.MovementDate,
                    m.MovementType,
                    m.ReferenceNumber,
                    m.SourceType,
                    WarehouseName = m.Warehouse.NameAr,
                    m.QuantityInBaseUnit,
                    m.UnitCost
                })
                .ToListAsync(ct);

            var rows = new List<StockCardRowDto>();
            decimal running = openingBalance;

            foreach (var m in movements)
            {
                bool isIncoming = incomingTypes.Contains(m.MovementType);
                decimal qIn = isIncoming ? m.QuantityInBaseUnit : 0;
                decimal qOut = !isIncoming ? m.QuantityInBaseUnit : 0;
                running += qIn - qOut;

                rows.Add(new StockCardRowDto
                {
                    MovementDate = m.MovementDate,
                    MovementTypeName = GetMovementTypeName(m.MovementType),
                    ReferenceNumber = m.ReferenceNumber,
                    SourceTypeName = GetSourceTypeName(m.SourceType),
                    WarehouseName = m.WarehouseName,
                    QuantityIn = qIn,
                    QuantityOut = qOut,
                    UnitCost = m.UnitCost,
                    BalanceAfter = running
                });
            }

            var report = new StockCardReportDto
            {
                ProductId = product.Id,
                ProductCode = product.Code,
                ProductName = product.NameAr,
                UnitName = product.BaseUnit?.NameAr,
                OpeningBalance = openingBalance,
                TotalIn = rows.Sum(r => r.QuantityIn),
                TotalOut = rows.Sum(r => r.QuantityOut),
                ClosingBalance = running,
                Rows = rows
            };

            return ServiceResult<StockCardReportDto>.Success(report);
        }

        // ════════════════════════════════════════════════════════
        //  CASHBOX MOVEMENT
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<CashboxMovementReportDto>> GetCashboxMovementAsync(
            int? cashboxId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        {
            string cashboxName = "جميع الخزن";
            if (cashboxId.HasValue)
            {
                var cashbox = await _db.Set<Domain.Entities.Treasury.Cashbox>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == cashboxId.Value, ct);
                if (cashbox != null) cashboxName = cashbox.NameAr;
            }

            var movements = new List<CashboxMovementRowDto>();

            // Cash Receipts (IN)
            {
                var query = _db.Set<Domain.Entities.Treasury.CashReceipt>()
                    .AsNoTracking()
                    .Where(r => r.Status == InvoiceStatus.Posted
                             && r.ReceiptDate >= fromDate
                             && r.ReceiptDate <= toDate);

                if (cashboxId.HasValue)
                    query = query.Where(r => r.CashboxId == cashboxId.Value);

                var receipts = await query.OrderBy(r => r.ReceiptDate).ToListAsync(ct);

                movements.AddRange(receipts.Select(r => new CashboxMovementRowDto
                {
                    Date = r.ReceiptDate,
                    DocumentType = "سند قبض",
                    DocumentNumber = r.ReceiptNumber,
                    Description = r.Description,
                    CounterpartyName = "",
                    AmountIn = r.Amount,
                    AmountOut = 0
                }));
            }

            // Cash Payments (OUT)
            {
                var query = _db.Set<Domain.Entities.Treasury.CashPayment>()
                    .AsNoTracking()
                    .Where(p => p.Status == InvoiceStatus.Posted
                             && p.PaymentDate >= fromDate
                             && p.PaymentDate <= toDate);

                if (cashboxId.HasValue)
                    query = query.Where(p => p.CashboxId == cashboxId.Value);

                var payments = await query.OrderBy(p => p.PaymentDate).ToListAsync(ct);

                movements.AddRange(payments.Select(p => new CashboxMovementRowDto
                {
                    Date = p.PaymentDate,
                    DocumentType = "سند صرف",
                    DocumentNumber = p.PaymentNumber,
                    Description = p.Description,
                    CounterpartyName = "",
                    AmountIn = 0,
                    AmountOut = p.Amount
                }));
            }

            // Cash Transfers
            {
                var queryOut = _db.Set<Domain.Entities.Treasury.CashTransfer>()
                    .AsNoTracking()
                    .Where(t => t.Status == InvoiceStatus.Posted
                             && t.TransferDate >= fromDate
                             && t.TransferDate <= toDate);

                if (cashboxId.HasValue)
                {
                    // Transfers OUT from this cashbox
                    var transfersOut = await queryOut
                        .Where(t => t.SourceCashboxId == cashboxId.Value)
                        .OrderBy(t => t.TransferDate).ToListAsync(ct);

                    movements.AddRange(transfersOut.Select(t => new CashboxMovementRowDto
                    {
                        Date = t.TransferDate,
                        DocumentType = "تحويل صادر",
                        DocumentNumber = t.TransferNumber,
                        Description = t.Description,
                        AmountIn = 0,
                        AmountOut = t.Amount
                    }));

                    // Transfers IN to this cashbox
                    var transfersIn = await _db.Set<Domain.Entities.Treasury.CashTransfer>()
                        .AsNoTracking()
                        .Where(t => t.Status == InvoiceStatus.Posted
                                 && t.TransferDate >= fromDate
                                 && t.TransferDate <= toDate
                                 && t.TargetCashboxId == cashboxId.Value)
                        .OrderBy(t => t.TransferDate).ToListAsync(ct);

                    movements.AddRange(transfersIn.Select(t => new CashboxMovementRowDto
                    {
                        Date = t.TransferDate,
                        DocumentType = "تحويل وارد",
                        DocumentNumber = t.TransferNumber,
                        Description = t.Description,
                        AmountIn = t.Amount,
                        AmountOut = 0
                    }));
                }
            }

            // Sort by date and calculate running balance
            movements = movements.OrderBy(m => m.Date).ThenBy(m => m.DocumentNumber).ToList();

            // Calculate opening balance from movements before fromDate
            decimal opening = 0;
            if (cashboxId.HasValue)
            {
                var receiptsBefore = await _db.Set<Domain.Entities.Treasury.CashReceipt>()
                    .Where(r => r.Status == InvoiceStatus.Posted && r.CashboxId == cashboxId.Value && r.ReceiptDate < fromDate)
                    .SumAsync(r => r.Amount, ct);
                var paymentsBefore = await _db.Set<Domain.Entities.Treasury.CashPayment>()
                    .Where(p => p.Status == InvoiceStatus.Posted && p.CashboxId == cashboxId.Value && p.PaymentDate < fromDate)
                    .SumAsync(p => p.Amount, ct);
                var transfersOutBefore = await _db.Set<Domain.Entities.Treasury.CashTransfer>()
                    .Where(t => t.Status == InvoiceStatus.Posted && t.SourceCashboxId == cashboxId.Value && t.TransferDate < fromDate)
                    .SumAsync(t => t.Amount, ct);
                var transfersInBefore = await _db.Set<Domain.Entities.Treasury.CashTransfer>()
                    .Where(t => t.Status == InvoiceStatus.Posted && t.TargetCashboxId == cashboxId.Value && t.TransferDate < fromDate)
                    .SumAsync(t => t.Amount, ct);

                opening = receiptsBefore - paymentsBefore - transfersOutBefore + transfersInBefore;
            }

            decimal running = opening;
            foreach (var m in movements)
            {
                running += m.AmountIn - m.AmountOut;
                m.RunningBalance = running;
            }

            var report = new CashboxMovementReportDto
            {
                CashboxId = cashboxId,
                CashboxName = cashboxName,
                OpeningBalance = opening,
                TotalIn = movements.Sum(m => m.AmountIn),
                TotalOut = movements.Sum(m => m.AmountOut),
                ClosingBalance = running,
                Rows = movements
            };

            return ServiceResult<CashboxMovementReportDto>.Success(report);
        }

        // ════════════════════════════════════════════════════════
        //  AGING REPORT
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<AgingReportDto>> GetAgingReportAsync(CancellationToken ct = default)
        {
            var today = _dateTime.Today;
            var result = new AgingReportDto();

            // Customer aging based on posted sales invoices with outstanding balance
            var salesInvoices = await _db.Set<Domain.Entities.Sales.SalesInvoice>()
                .AsNoTracking()
                .Include(i => i.Customer)
                .Where(i => i.Status == InvoiceStatus.Posted
                            && i.CounterpartyType == CounterpartyType.Customer
                            && (i.NetTotal - i.PaidAmount) > 0)
                .ToListAsync(ct);

            // Load posted sales returns (only for customers with outstanding invoices)
            var customerIds = salesInvoices.Select(i => i.CustomerId).Distinct().ToList();
            var salesReturns = await _db.Set<Domain.Entities.Sales.SalesReturn>()
                .AsNoTracking()
                .Where(r => r.Status == InvoiceStatus.Posted
                            && r.CounterpartyType == CounterpartyType.Customer
                            && r.CustomerId.HasValue
                            && customerIds.Contains(r.CustomerId.Value))
                .ToListAsync(ct);

            var returnsByCustomer = salesReturns
                .GroupBy(r => r.CustomerId.Value)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.NetTotal));

            var customerGroups = salesInvoices
                .GroupBy(i => new { i.CustomerId, i.Customer.Code, i.Customer.NameAr })
                .Select(g =>
                {
                    var row = new AgingRowDto
                    {
                        EntityId = g.Key.CustomerId,
                        Code = g.Key.Code,
                        Name = g.Key.NameAr
                    };

                    foreach (var inv in g)
                    {
                        var balance = inv.NetTotal - inv.PaidAmount;
                        if (balance <= 0) continue;

                        var days = (today - inv.InvoiceDate).Days;
                        if (days <= 30) row.Current += balance;
                        else if (days <= 60) row.Days30 += balance;
                        else if (days <= 90) row.Days60 += balance;
                        else if (days <= 120) row.Days90 += balance;
                        else row.Days120Plus += balance;
                    }

                    row.Total = row.Current + row.Days30 + row.Days60 + row.Days90 + row.Days120Plus;

                    // Subtract returns from oldest bucket first to keep buckets consistent
                    if (returnsByCustomer.TryGetValue(g.Key.CustomerId, out var returnTotal) && returnTotal > 0)
                    {
                        var remaining = returnTotal;
                        // Reduce from oldest to newest
                        var reduce120 = Math.Min(remaining, row.Days120Plus); row.Days120Plus -= reduce120; remaining -= reduce120;
                        var reduce90 = Math.Min(remaining, row.Days90); row.Days90 -= reduce90; remaining -= reduce90;
                        var reduce60 = Math.Min(remaining, row.Days60); row.Days60 -= reduce60; remaining -= reduce60;
                        var reduce30 = Math.Min(remaining, row.Days30); row.Days30 -= reduce30; remaining -= reduce30;
                        var reduceCur = Math.Min(remaining, row.Current); row.Current -= reduceCur; remaining -= reduceCur;
                        row.Total = row.Current + row.Days30 + row.Days60 + row.Days90 + row.Days120Plus;
                    }

                    return row;
                })
                .Where(r => r.Total != 0)
                .OrderByDescending(r => r.Total)
                .ToList();

            result.CustomerAging = customerGroups;
            result.TotalCustomerBalance = customerGroups.Sum(r => r.Total);

            // Supplier aging based on posted purchase invoices with outstanding balance
            var purchaseInvoices = await _db.Set<Domain.Entities.Purchases.PurchaseInvoice>()
                .AsNoTracking()
                .Include(i => i.Supplier)
                .Where(i => i.Status == InvoiceStatus.Posted
                            && i.CounterpartyType == CounterpartyType.Supplier
                            && i.SupplierId.HasValue
                            && (i.NetTotal - i.PaidAmount) > 0)
                .ToListAsync(ct);

            // Load posted purchase returns (only for suppliers with outstanding invoices)
            var supplierIds = purchaseInvoices.Select(i => i.SupplierId.Value).Distinct().ToList();
            var purchaseReturns = await _db.Set<Domain.Entities.Purchases.PurchaseReturn>()
                .AsNoTracking()
                .Where(r => r.Status == InvoiceStatus.Posted
                            && r.CounterpartyType == CounterpartyType.Supplier
                            && r.SupplierId.HasValue
                            && supplierIds.Contains(r.SupplierId.Value))
                .ToListAsync(ct);

            var returnsBySup = purchaseReturns
                .GroupBy(r => r.SupplierId.Value)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.NetTotal));

            var supplierGroups = purchaseInvoices
                .GroupBy(i => new { SupplierId = i.SupplierId.Value, i.Supplier.Code, i.Supplier.NameAr })
                .Select(g =>
                {
                    var row = new AgingRowDto
                    {
                        EntityId = g.Key.SupplierId,
                        Code = g.Key.Code,
                        Name = g.Key.NameAr
                    };

                    foreach (var inv in g)
                    {
                        var balance = inv.NetTotal - inv.PaidAmount;
                        if (balance <= 0) continue;

                        var days = (today - inv.InvoiceDate).Days;
                        if (days <= 30) row.Current += balance;
                        else if (days <= 60) row.Days30 += balance;
                        else if (days <= 90) row.Days60 += balance;
                        else if (days <= 120) row.Days90 += balance;
                        else row.Days120Plus += balance;
                    }

                    row.Total = row.Current + row.Days30 + row.Days60 + row.Days90 + row.Days120Plus;

                    if (returnsBySup.TryGetValue(g.Key.SupplierId, out var returnTotal) && returnTotal > 0)
                    {
                        var remaining = returnTotal;
                        var reduce120 = Math.Min(remaining, row.Days120Plus); row.Days120Plus -= reduce120; remaining -= reduce120;
                        var reduce90 = Math.Min(remaining, row.Days90); row.Days90 -= reduce90; remaining -= reduce90;
                        var reduce60 = Math.Min(remaining, row.Days60); row.Days60 -= reduce60; remaining -= reduce60;
                        var reduce30 = Math.Min(remaining, row.Days30); row.Days30 -= reduce30; remaining -= reduce30;
                        var reduceCur = Math.Min(remaining, row.Current); row.Current -= reduceCur; remaining -= reduceCur;
                        row.Total = row.Current + row.Days30 + row.Days60 + row.Days90 + row.Days120Plus;
                    }

                    return row;
                })
                .Where(r => r.Total != 0)
                .OrderByDescending(r => r.Total)
                .ToList();

            result.SupplierAging = supplierGroups;
            result.TotalSupplierBalance = supplierGroups.Sum(r => r.Total);

            return ServiceResult<AgingReportDto>.Success(result);
        }

        // ════════════════════════════════════════════════════════
        //  VAT REPORT
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<VatReportDto>> GetVatReportAsync(
            DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        {
            // Sales VAT
            var salesVat = await _db.Set<Domain.Entities.Sales.SalesInvoiceLine>()
                .AsNoTracking()
                .Join(_db.Set<Domain.Entities.Sales.SalesInvoice>(),
                    line => line.SalesInvoiceId, inv => inv.Id,
                    (line, inv) => new { line, inv })
                .Where(x => x.inv.Status == InvoiceStatus.Posted
                          && x.inv.InvoiceDate >= fromDate
                          && x.inv.InvoiceDate <= toDate)
                .GroupBy(x => x.line.VatRate)
                .Select(g => new
                {
                    VatRate = g.Key,
                    Base = g.Sum(x => x.line.NetTotal),
                    Vat = g.Sum(x => x.line.VatAmount)
                }).ToListAsync(ct);

            // Purchase VAT
            var purchaseVat = await _db.Set<Domain.Entities.Purchases.PurchaseInvoiceLine>()
                .AsNoTracking()
                .Join(_db.Set<Domain.Entities.Purchases.PurchaseInvoice>(),
                    line => line.PurchaseInvoiceId, inv => inv.Id,
                    (line, inv) => new { line, inv })
                .Where(x => x.inv.Status == InvoiceStatus.Posted
                          && x.inv.InvoiceDate >= fromDate
                          && x.inv.InvoiceDate <= toDate)
                .GroupBy(x => x.line.VatRate)
                .Select(g => new
                {
                    VatRate = g.Key,
                    Base = g.Sum(x => x.line.NetTotal),
                    Vat = g.Sum(x => x.line.VatAmount)
                }).ToListAsync(ct);

            var allRates = salesVat.Select(s => s.VatRate)
                .Union(purchaseVat.Select(p => p.VatRate))
                .Distinct()
                .OrderBy(r => r);

            var rows = allRates.Select(rate => new VatReportRowDto
            {
                VatRate = rate,
                SalesBase = salesVat.FirstOrDefault(s => s.VatRate == rate)?.Base ?? 0,
                SalesVat = salesVat.FirstOrDefault(s => s.VatRate == rate)?.Vat ?? 0,
                PurchaseBase = purchaseVat.FirstOrDefault(p => p.VatRate == rate)?.Base ?? 0,
                PurchaseVat = purchaseVat.FirstOrDefault(p => p.VatRate == rate)?.Vat ?? 0,
                NetVat = (salesVat.FirstOrDefault(s => s.VatRate == rate)?.Vat ?? 0)
                       - (purchaseVat.FirstOrDefault(p => p.VatRate == rate)?.Vat ?? 0)
            }).ToList();

            var result = new VatReportDto
            {
                Rows = rows,
                TotalSalesBase = rows.Sum(r => r.SalesBase),
                TotalSalesVat = rows.Sum(r => r.SalesVat),
                TotalPurchaseBase = rows.Sum(r => r.PurchaseBase),
                TotalPurchaseVat = rows.Sum(r => r.PurchaseVat),
                NetVatPayable = rows.Sum(r => r.NetVat)
            };

            return ServiceResult<VatReportDto>.Success(result);
        }

        // ════════════════════════════════════════════════════════
        //  DASHBOARD
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<DashboardSummaryDto>> GetDashboardSummaryAsync(CancellationToken ct = default)
        {
            var today = _dateTime.Today;
            var tomorrow = today.AddDays(1);
            var monthStart = new DateTime(today.Year, today.Month, 1);

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

            return ServiceResult<DashboardSummaryDto>.Success(dto);
        }

        // ════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════

        private static string GetAccountTypeName(AccountType type) => type switch
        {
            AccountType.Asset => "أصول",
            AccountType.Liability => "التزامات",
            AccountType.Equity => "حقوق ملكية",
            AccountType.Revenue => "إيرادات",
            AccountType.COGS => "تكلفة المبيعات",
            AccountType.Expense => "مصروفات",
            AccountType.OtherIncome => "إيرادات أخرى",
            AccountType.OtherExpense => "مصروفات أخرى",
            _ => type.ToString()
        };

        private static string GetSourceTypeName(SourceType type) => type switch
        {
            SourceType.Manual => "يدوي",
            SourceType.SalesInvoice => "فاتورة بيع",
            SourceType.PurchaseInvoice => "فاتورة شراء",
            SourceType.CashReceipt => "سند قبض",
            SourceType.CashPayment => "سند صرف",
            SourceType.Inventory => "مخزون",
            SourceType.Adjustment => "تسوية",
            SourceType.Opening => "رصيد افتتاحي",
            SourceType.Closing => "إقفال",
            SourceType.PurchaseReturn => "مرتجع شراء",
            SourceType.SalesReturn => "مرتجع بيع",
            SourceType.CashTransfer => "تحويل بين خزن",
            _ => type.ToString()
        };

        private static string GetMovementTypeName(MovementType type) => type switch
        {
            MovementType.PurchaseIn => "شراء",
            MovementType.SalesOut => "بيع",
            MovementType.SalesReturn => "مرتجع بيع",
            MovementType.PurchaseReturn => "مرتجع شراء",
            MovementType.AdjustmentIn => "تسوية +",
            MovementType.AdjustmentOut => "تسوية -",
            MovementType.TransferOut => "تحويل صادر",
            MovementType.TransferIn => "تحويل وارد",
            MovementType.OpeningBalance => "رصيد افتتاحي",
            _ => type.ToString()
        };
    }
}
