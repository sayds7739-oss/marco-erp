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
                group line by new { prod.Id, prod.Code, prod.NameAr } into g
                select new
                {
                    ProductId = g.Key.Id,
                    ProductCode = g.Key.Code,
                    ProductName = g.Key.NameAr,
                    TotalQuantity = g.Sum(x => x.BaseQuantity),
                    TotalSalesAmount = g.Sum(x => x.NetTotal)
                }
            ).ToListAsync(ct);

            // Historical cost per product from InventoryMovement (actual cost at time of sale,
            // not the current Product.WeightedAverageCost which changes with every purchase).
            // SalesOut movements are created when a sales invoice is posted and record UnitCost/TotalCost
            // at the WAC that was current at that moment, giving us true historical COGS.
            var historicalCosts = await (
                from mov in _db.Set<Domain.Entities.Inventory.InventoryMovement>()
                join inv in _db.Set<Domain.Entities.Sales.SalesInvoice>()
                    on mov.SourceId equals (int?)inv.Id
                where mov.MovementType == MovementType.SalesOut
                      && mov.SourceType == SourceType.SalesInvoice
                      && inv.Status == InvoiceStatus.Posted
                      && inv.InvoiceDate >= fromDate
                      && inv.InvoiceDate <= toDate
                group mov by mov.ProductId into g
                select new
                {
                    ProductId = g.Key,
                    TotalCost = g.Sum(x => x.TotalCost)
                }
            ).ToListAsync(ct);

            var costByProduct = historicalCosts.ToDictionary(c => c.ProductId, c => c.TotalCost);

            var rows = salesData.Select(s =>
            {
                var historicalCost = costByProduct.GetValueOrDefault(s.ProductId, 0m);
                return new ProfitReportRowDto
                {
                    ProductId = s.ProductId,
                    ProductCode = s.ProductCode,
                    ProductName = s.ProductName,
                    TotalSalesQuantity = s.TotalQuantity,
                    TotalSalesAmount = s.TotalSalesAmount,
                    TotalCostAmount = historicalCost,
                    GrossProfit = s.TotalSalesAmount - historicalCost,
                    ProfitMarginPercent = s.TotalSalesAmount != 0
                        ? Math.Round((s.TotalSalesAmount - historicalCost) / s.TotalSalesAmount * 100, 2)
                        : 0
                };
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
        //  CUSTOMER STATEMENT (كشف حساب عميل)
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<CustomerStatementDto>> GetCustomerStatementAsync(
            CustomerStatementRequestDto request, CancellationToken ct = default)
        {
            if (request == null)
                return ServiceResult<CustomerStatementDto>.Failure("بيانات الطلب مطلوبة.");

            var customer = await _db.Set<Domain.Entities.Sales.Customer>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CustomerId && !c.IsDeleted, ct);

            if (customer == null)
                return ServiceResult<CustomerStatementDto>.Failure("العميل غير موجود.");

            // ── Build unified transaction list ──

            var lines = new List<CustomerStatementLineDto>();

            // 1) Sales Invoices (Posted) → Debit (customer owes us)
            var salesInvoices = await _db.Set<Domain.Entities.Sales.SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.Status == InvoiceStatus.Posted
                         && i.CustomerId == request.CustomerId
                         && i.CounterpartyType == CounterpartyType.Customer)
                .Select(i => new { i.InvoiceDate, i.InvoiceNumber, i.NetTotal })
                .ToListAsync(ct);

            foreach (var inv in salesInvoices)
            {
                lines.Add(new CustomerStatementLineDto
                {
                    Date = inv.InvoiceDate,
                    DocumentType = "فاتورة بيع",
                    DocumentNumber = inv.InvoiceNumber,
                    Description = $"فاتورة بيع رقم {inv.InvoiceNumber}",
                    Debit = inv.NetTotal,
                    Credit = 0
                });
            }

            // 2) Sales Returns (Posted) → Credit (reduces what customer owes)
            var salesReturns = await _db.Set<Domain.Entities.Sales.SalesReturn>()
                .AsNoTracking()
                .Where(r => r.Status == InvoiceStatus.Posted
                         && r.CustomerId == request.CustomerId
                         && r.CounterpartyType == CounterpartyType.Customer)
                .Select(r => new { r.ReturnDate, r.ReturnNumber, r.NetTotal })
                .ToListAsync(ct);

            foreach (var ret in salesReturns)
            {
                lines.Add(new CustomerStatementLineDto
                {
                    Date = ret.ReturnDate,
                    DocumentType = "مرتجع بيع",
                    DocumentNumber = ret.ReturnNumber,
                    Description = $"مرتجع بيع رقم {ret.ReturnNumber}",
                    Debit = 0,
                    Credit = ret.NetTotal
                });
            }

            // 3) Cash Receipts (Posted) → Credit (customer paid us)
            var cashReceipts = await _db.Set<Domain.Entities.Treasury.CashReceipt>()
                .AsNoTracking()
                .Where(r => r.Status == InvoiceStatus.Posted
                         && r.CustomerId == request.CustomerId)
                .Select(r => new { r.ReceiptDate, r.ReceiptNumber, r.Amount, r.Description })
                .ToListAsync(ct);

            foreach (var rec in cashReceipts)
            {
                lines.Add(new CustomerStatementLineDto
                {
                    Date = rec.ReceiptDate,
                    DocumentType = "سند قبض",
                    DocumentNumber = rec.ReceiptNumber,
                    Description = rec.Description ?? $"سند قبض رقم {rec.ReceiptNumber}",
                    Debit = 0,
                    Credit = rec.Amount
                });
            }

            // ── Split into opening balance (before FromDate) and period lines ──

            var openingLines = lines.Where(l => l.Date < request.FromDate).ToList();
            var periodLines = lines
                .Where(l => l.Date >= request.FromDate && l.Date <= request.ToDate)
                .OrderBy(l => l.Date)
                .ThenBy(l => l.DocumentNumber)
                .ToList();

            decimal openingBalance = openingLines.Sum(l => l.Debit) - openingLines.Sum(l => l.Credit);

            // ── Calculate running balance ──

            decimal running = openingBalance;
            foreach (var line in periodLines)
            {
                running += line.Debit - line.Credit;
                line.RunningBalance = running;
            }

            var result = new CustomerStatementDto
            {
                CustomerNameAr = customer.NameAr,
                CustomerPhone = customer.Phone,
                OpeningBalance = openingBalance,
                TotalDebit = periodLines.Sum(l => l.Debit),
                TotalCredit = periodLines.Sum(l => l.Credit),
                ClosingBalance = running,
                Lines = periodLines
            };

            return ServiceResult<CustomerStatementDto>.Success(result);
        }

        // ════════════════════════════════════════════════════════
        //  DASHBOARD CHART DATA
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<IReadOnlyList<DailyTrendPoint>>> GetWeeklySalesTrendAsync(
            int weeks = 4, CancellationToken ct = default)
        {
            var today = _dateTime.UtcNow.Date;
            var startDate = today.AddDays(-(weeks * 7) + 1);

            var salesByDay = await _db.Set<Domain.Entities.Sales.SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.Status == InvoiceStatus.Posted
                         && i.InvoiceDate >= startDate
                         && i.InvoiceDate <= today)
                .GroupBy(i => i.InvoiceDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(i => i.NetTotal) })
                .ToListAsync(ct);

            var purchasesByDay = await _db.Set<Domain.Entities.Purchases.PurchaseInvoice>()
                .AsNoTracking()
                .Where(i => i.Status == InvoiceStatus.Posted
                         && i.InvoiceDate >= startDate
                         && i.InvoiceDate <= today)
                .GroupBy(i => i.InvoiceDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(i => i.NetTotal) })
                .ToListAsync(ct);

            var salesLookup = salesByDay.ToDictionary(x => x.Date, x => x.Total);
            var purchaseLookup = purchasesByDay.ToDictionary(x => x.Date, x => x.Total);

            var points = new List<DailyTrendPoint>();
            for (var d = startDate; d <= today; d = d.AddDays(1))
            {
                points.Add(new DailyTrendPoint
                {
                    Date = d,
                    SalesTotal = salesLookup.TryGetValue(d, out var s) ? s : 0m,
                    PurchaseTotal = purchaseLookup.TryGetValue(d, out var p) ? p : 0m
                });
            }

            return ServiceResult<IReadOnlyList<DailyTrendPoint>>.Success(points);
        }

        public async Task<ServiceResult<IReadOnlyList<TopProductDto>>> GetTopProductsAsync(
            int count = 5, CancellationToken ct = default)
        {
            var today = _dateTime.UtcNow.Date;
            var monthStart = new DateTime(today.Year, today.Month, 1);

            // Join through SalesInvoice to filter by status and date
            var topProducts = await (
                from line in _db.Set<Domain.Entities.Sales.SalesInvoiceLine>().AsNoTracking()
                join inv in _db.Set<Domain.Entities.Sales.SalesInvoice>().AsNoTracking()
                    on line.SalesInvoiceId equals inv.Id
                join prod in _db.Set<Domain.Entities.Inventory.Product>().AsNoTracking()
                    on line.ProductId equals prod.Id
                where inv.Status == InvoiceStatus.Posted
                   && inv.InvoiceDate >= monthStart
                   && inv.InvoiceDate <= today
                group line by new { line.ProductId, prod.NameAr } into g
                select new TopProductDto
                {
                    Label = g.Key.NameAr,
                    Value = g.Sum(l => l.NetTotal)
                })
                .OrderByDescending(x => x.Value)
                .Take(count)
                .ToListAsync(ct);

            return ServiceResult<IReadOnlyList<TopProductDto>>.Success(topProducts);
        }

    }
}
