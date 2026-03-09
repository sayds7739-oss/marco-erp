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
            {
                var receiptsBeforeQuery = _db.Set<Domain.Entities.Treasury.CashReceipt>()
                    .Where(r => r.Status == InvoiceStatus.Posted && r.ReceiptDate < fromDate);
                var paymentsBeforeQuery = _db.Set<Domain.Entities.Treasury.CashPayment>()
                    .Where(p => p.Status == InvoiceStatus.Posted && p.PaymentDate < fromDate);
                var transfersOutBeforeQuery = _db.Set<Domain.Entities.Treasury.CashTransfer>()
                    .Where(t => t.Status == InvoiceStatus.Posted && t.TransferDate < fromDate);
                var transfersInBeforeQuery = _db.Set<Domain.Entities.Treasury.CashTransfer>()
                    .Where(t => t.Status == InvoiceStatus.Posted && t.TransferDate < fromDate);

                if (cashboxId.HasValue)
                {
                    receiptsBeforeQuery = receiptsBeforeQuery.Where(r => r.CashboxId == cashboxId.Value);
                    paymentsBeforeQuery = paymentsBeforeQuery.Where(p => p.CashboxId == cashboxId.Value);
                    transfersOutBeforeQuery = transfersOutBeforeQuery.Where(t => t.SourceCashboxId == cashboxId.Value);
                    transfersInBeforeQuery = transfersInBeforeQuery.Where(t => t.TargetCashboxId == cashboxId.Value);
                }

                var receiptsBefore = await receiptsBeforeQuery.SumAsync(r => r.Amount, ct);
                var paymentsBefore = await paymentsBeforeQuery.SumAsync(p => p.Amount, ct);
                var transfersOutBefore = await transfersOutBeforeQuery.SumAsync(t => t.Amount, ct);
                var transfersInBefore = await transfersInBeforeQuery.SumAsync(t => t.Amount, ct);

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
                .Where(i => i.CustomerId.HasValue)
                .GroupBy(i => new { CustomerId = i.CustomerId.Value, i.Customer.Code, i.Customer.NameAr })
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
            // Sales VAT (output VAT from sales invoices)
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

            // Sales Return VAT (reduces output VAT — returns decrease what we owe in VAT)
            var salesReturnVat = await _db.Set<Domain.Entities.Sales.SalesReturnLine>()
                .AsNoTracking()
                .Join(_db.Set<Domain.Entities.Sales.SalesReturn>(),
                    line => line.SalesReturnId, ret => ret.Id,
                    (line, ret) => new { line, ret })
                .Where(x => x.ret.Status == InvoiceStatus.Posted
                          && x.ret.ReturnDate >= fromDate
                          && x.ret.ReturnDate <= toDate)
                .GroupBy(x => x.line.VatRate)
                .Select(g => new
                {
                    VatRate = g.Key,
                    Base = g.Sum(x => x.line.NetTotal),
                    Vat = g.Sum(x => x.line.VatAmount)
                }).ToListAsync(ct);

            // Purchase VAT (input VAT from purchase invoices)
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

            // Purchase Return VAT (reduces input VAT — returns decrease what we can reclaim)
            var purchaseReturnVat = await _db.Set<Domain.Entities.Purchases.PurchaseReturnLine>()
                .AsNoTracking()
                .Join(_db.Set<Domain.Entities.Purchases.PurchaseReturn>(),
                    line => line.PurchaseReturnId, ret => ret.Id,
                    (line, ret) => new { line, ret })
                .Where(x => x.ret.Status == InvoiceStatus.Posted
                          && x.ret.ReturnDate >= fromDate
                          && x.ret.ReturnDate <= toDate)
                .GroupBy(x => x.line.VatRate)
                .Select(g => new
                {
                    VatRate = g.Key,
                    Base = g.Sum(x => x.line.NetTotal),
                    Vat = g.Sum(x => x.line.VatAmount)
                }).ToListAsync(ct);

            var allRates = salesVat.Select(s => s.VatRate)
                .Union(purchaseVat.Select(p => p.VatRate))
                .Union(salesReturnVat.Select(r => r.VatRate))
                .Union(purchaseReturnVat.Select(r => r.VatRate))
                .Distinct()
                .OrderBy(r => r);

            var rows = allRates.Select(rate => new VatReportRowDto
            {
                VatRate = rate,
                SalesBase = (salesVat.FirstOrDefault(s => s.VatRate == rate)?.Base ?? 0)
                          - (salesReturnVat.FirstOrDefault(r => r.VatRate == rate)?.Base ?? 0),
                SalesVat = (salesVat.FirstOrDefault(s => s.VatRate == rate)?.Vat ?? 0)
                         - (salesReturnVat.FirstOrDefault(r => r.VatRate == rate)?.Vat ?? 0),
                PurchaseBase = (purchaseVat.FirstOrDefault(p => p.VatRate == rate)?.Base ?? 0)
                             - (purchaseReturnVat.FirstOrDefault(r => r.VatRate == rate)?.Base ?? 0),
                PurchaseVat = (purchaseVat.FirstOrDefault(p => p.VatRate == rate)?.Vat ?? 0)
                            - (purchaseReturnVat.FirstOrDefault(r => r.VatRate == rate)?.Vat ?? 0),
                NetVat = ((salesVat.FirstOrDefault(s => s.VatRate == rate)?.Vat ?? 0)
                        - (salesReturnVat.FirstOrDefault(r => r.VatRate == rate)?.Vat ?? 0))
                       - ((purchaseVat.FirstOrDefault(p => p.VatRate == rate)?.Vat ?? 0)
                        - (purchaseReturnVat.FirstOrDefault(r => r.VatRate == rate)?.Vat ?? 0))
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

    }
}
