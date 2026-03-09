using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Mappers.Sales;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.Services.Sales
{
    public sealed partial class PosService
    {
        // ══════════════════════════════════════════════════════════
        //  REPORTS
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<PosDailyReportDto>> GetDailyReportAsync(DateTime date, CancellationToken ct = default)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            var sessions = await _sessionRepo.GetByDateRangeAsync(startOfDay, endOfDay, ct);

            // C-18 fix: compute actual COGS from SalesOut inventory movements
            var salesOutMovements = await _movementRepo.GetByDateRangeAndTypeAsync(
                startOfDay, endOfDay, MovementType.SalesOut, ct);
            var totalCogs = salesOutMovements.Sum(m => m.TotalCost);
            var totalSales = sessions.Sum(s => s.TotalSales);

            var report = new PosDailyReportDto
            {
                Date = date.Date,
                TotalTransactions = sessions.Sum(s => s.TransactionCount),
                TotalSales = totalSales,
                TotalCash = sessions.Sum(s => s.TotalCashReceived),
                TotalCard = sessions.Sum(s => s.TotalCardReceived),
                TotalOnAccount = sessions.Sum(s => s.TotalOnAccount),
                TotalCogs = totalCogs,
                GrossProfit = totalSales - totalCogs
            };

            return ServiceResult<PosDailyReportDto>.Success(report);
        }

        public async Task<ServiceResult<PosSessionReportDto>> GetSessionReportAsync(int sessionId, CancellationToken ct = default)
        {
            var session = await _sessionRepo.GetWithPaymentsAsync(sessionId, ct);
            if (session == null)
                return ServiceResult<PosSessionReportDto>.Failure("الجلسة غير موجودة.");

            var payments = await _paymentRepo.GetBySessionAsync(sessionId, ct);
            var invoiceIds = payments.Select(p => p.SalesInvoiceId).Distinct().ToList();

            var sales = new List<PosSessionSaleDto>();
            foreach (var invoiceId in invoiceIds)
            {
                var invoice = await _invoiceRepo.GetWithLinesAsync(invoiceId, ct);
                if (invoice == null) continue;

                var invoicePayments = payments.Where(p => p.SalesInvoiceId == invoiceId).ToList();
                var methodNames = string.Join(" + ", invoicePayments.Select(p => p.PaymentMethod.ToString()).Distinct());

                sales.Add(new PosSessionSaleDto
                {
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate,
                    CustomerNameAr = invoice.Customer?.NameAr ?? "عميل نقدي",
                    NetTotal = invoice.NetTotal,
                    PaymentMethods = methodNames
                });
            }

            var report = new PosSessionReportDto
            {
                Session = PosMapper.ToSessionDto(session),
                Sales = sales
            };

            return ServiceResult<PosSessionReportDto>.Success(report);
        }

        public async Task<ServiceResult<PosProfitReportDto>> GetProfitReportAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        {
            var endDate = toDate.AddDays(1);

            var sessions = await _sessionRepo.GetByDateRangeAsync(fromDate, endDate, ct);
            var totalRevenue = sessions.Sum(s => s.TotalSales);

            // C-18 fix: compute actual COGS from SalesOut inventory movements
            var salesOutMovements = await _movementRepo.GetByDateRangeAndTypeAsync(
                fromDate, endDate, MovementType.SalesOut, ct);
            var totalCogs = salesOutMovements.Sum(m => m.TotalCost);
            var grossProfit = totalRevenue - totalCogs;

            // Load actual invoice lines to compute per-product revenue
            var invoiceIds = salesOutMovements
                .Where(m => m.SourceId.HasValue)
                .Select(m => m.SourceId.Value)
                .Distinct()
                .ToList();

            // Build per-product revenue lookup from invoice lines
            var productRevenueLookup = new Dictionary<int, decimal>();
            var productInfoLookup = new Dictionary<int, (string Code, string NameAr)>();
            foreach (var invoiceId in invoiceIds)
            {
                var invoice = await _invoiceRepo.GetWithLinesAsync(invoiceId, ct);
                if (invoice == null || invoice.Status == InvoiceStatus.Cancelled)
                    continue;

                foreach (var line in invoice.Lines)
                {
                    if (!productRevenueLookup.ContainsKey(line.ProductId))
                        productRevenueLookup[line.ProductId] = 0;
                    productRevenueLookup[line.ProductId] += line.NetTotal;

                    if (!productInfoLookup.ContainsKey(line.ProductId))
                    {
                        productInfoLookup[line.ProductId] = (
                            line.Product?.Code ?? "",
                            line.Product?.NameAr ?? "");
                    }
                }
            }

            // Build per-product profit lines combining COGS from movements and revenue from invoice lines
            var costByProduct = salesOutMovements
                .GroupBy(m => m.ProductId)
                .ToDictionary(g => g.Key, g => (Cost: g.Sum(m => m.TotalCost), Qty: g.Sum(m => m.QuantityInBaseUnit)));

            var allProductIds = costByProduct.Keys.Union(productRevenueLookup.Keys).Distinct();

            var lines = allProductIds.Select(productId =>
            {
                var cost = costByProduct.ContainsKey(productId) ? costByProduct[productId].Cost : 0;
                var qty = costByProduct.ContainsKey(productId) ? costByProduct[productId].Qty : 0;
                var revenue = productRevenueLookup.ContainsKey(productId) ? productRevenueLookup[productId] : 0;
                var profit = revenue - cost;

                // Determine product info from invoice lines or movements
                string code = "", nameAr = "";
                if (productInfoLookup.ContainsKey(productId))
                {
                    code = productInfoLookup[productId].Code;
                    nameAr = productInfoLookup[productId].NameAr;
                }
                else
                {
                    var movement = salesOutMovements.FirstOrDefault(m => m.ProductId == productId);
                    code = movement?.Product?.Code ?? "";
                    nameAr = movement?.Product?.NameAr ?? "";
                }

                return new PosProfitLineDto
                {
                    ProductCode = code,
                    ProductNameAr = nameAr,
                    QuantitySold = qty,
                    Cost = cost,
                    Revenue = revenue,
                    Profit = profit,
                    ProfitMarginPercent = revenue != 0 ? Math.Round(profit / revenue * 100, 2) : 0
                };
            }).ToList();

            var report = new PosProfitReportDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                TotalRevenue = totalRevenue,
                TotalCogs = totalCogs,
                GrossProfit = grossProfit,
                GrossProfitMarginPercent = totalRevenue != 0 ? Math.Round(grossProfit / totalRevenue * 100, 2) : 0,
                Lines = lines
            };

            return ServiceResult<PosProfitReportDto>.Success(report);
        }

        public async Task<ServiceResult<CashVarianceReportDto>> GetCashVarianceReportAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        {
            var sessions = await _sessionRepo.GetByDateRangeAsync(fromDate, toDate.AddDays(1), ct);

            var lines = sessions
                .Where(s => s.Status == PosSessionStatus.Closed)
                .Select(s => new CashVarianceLineDto
                {
                    SessionNumber = s.SessionNumber,
                    OpenedAt = s.OpenedAt,
                    ClosedAt = s.ClosedAt,
                    OpeningBalance = s.OpeningBalance,
                    TotalCashReceived = s.TotalCashReceived,
                    ExpectedBalance = s.OpeningBalance + s.TotalCashReceived,
                    ClosingBalance = s.ClosingBalance,
                    Variance = s.Variance
                })
                .ToList();

            var report = new CashVarianceReportDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                Lines = lines,
                TotalVariance = lines.Sum(l => l.Variance)
            };

            return ServiceResult<CashVarianceReportDto>.Success(report);
        }
    }
}
