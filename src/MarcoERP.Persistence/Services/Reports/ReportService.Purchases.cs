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
        //  SUPPLIER STATEMENT (كشف حساب مورد)
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<SupplierStatementDto>> GetSupplierStatementAsync(
            SupplierStatementRequestDto request, CancellationToken ct = default)
        {
            if (request == null)
                return ServiceResult<SupplierStatementDto>.Failure("بيانات الطلب مطلوبة.");

            var supplier = await _db.Set<Domain.Entities.Purchases.Supplier>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SupplierId && !s.IsDeleted, ct);

            if (supplier == null)
                return ServiceResult<SupplierStatementDto>.Failure("المورد غير موجود.");

            // ── Build unified transaction list ──

            var lines = new List<SupplierStatementLineDto>();

            // 1) Purchase Invoices (Posted) → Credit (we owe supplier)
            var purchaseInvoices = await _db.Set<Domain.Entities.Purchases.PurchaseInvoice>()
                .AsNoTracking()
                .Where(i => i.Status == InvoiceStatus.Posted
                         && i.SupplierId == request.SupplierId
                         && i.CounterpartyType == CounterpartyType.Supplier)
                .Select(i => new { i.InvoiceDate, i.InvoiceNumber, i.NetTotal })
                .ToListAsync(ct);

            foreach (var inv in purchaseInvoices)
            {
                lines.Add(new SupplierStatementLineDto
                {
                    Date = inv.InvoiceDate,
                    DocumentType = "فاتورة شراء",
                    DocumentNumber = inv.InvoiceNumber,
                    Description = $"فاتورة شراء رقم {inv.InvoiceNumber}",
                    Debit = 0,
                    Credit = inv.NetTotal
                });
            }

            // 2) Purchase Returns (Posted) → Debit (reduces what we owe)
            var purchaseReturns = await _db.Set<Domain.Entities.Purchases.PurchaseReturn>()
                .AsNoTracking()
                .Where(r => r.Status == InvoiceStatus.Posted
                         && r.SupplierId == request.SupplierId
                         && r.CounterpartyType == CounterpartyType.Supplier)
                .Select(r => new { r.ReturnDate, r.ReturnNumber, r.NetTotal })
                .ToListAsync(ct);

            foreach (var ret in purchaseReturns)
            {
                lines.Add(new SupplierStatementLineDto
                {
                    Date = ret.ReturnDate,
                    DocumentType = "مرتجع شراء",
                    DocumentNumber = ret.ReturnNumber,
                    Description = $"مرتجع شراء رقم {ret.ReturnNumber}",
                    Debit = ret.NetTotal,
                    Credit = 0
                });
            }

            // 3) Cash Payments (Posted) → Debit (we paid supplier)
            var cashPayments = await _db.Set<Domain.Entities.Treasury.CashPayment>()
                .AsNoTracking()
                .Where(p => p.Status == InvoiceStatus.Posted
                         && p.SupplierId == request.SupplierId)
                .Select(p => new { p.PaymentDate, p.PaymentNumber, p.Amount, p.Description })
                .ToListAsync(ct);

            foreach (var pay in cashPayments)
            {
                lines.Add(new SupplierStatementLineDto
                {
                    Date = pay.PaymentDate,
                    DocumentType = "سند صرف",
                    DocumentNumber = pay.PaymentNumber,
                    Description = pay.Description ?? $"سند صرف رقم {pay.PaymentNumber}",
                    Debit = pay.Amount,
                    Credit = 0
                });
            }

            // ── Split into opening balance (before FromDate) and period lines ──
            // Supplier balance convention: Credit - Debit = positive means we owe supplier

            var openingLines = lines.Where(l => l.Date < request.FromDate).ToList();
            var periodLines = lines
                .Where(l => l.Date >= request.FromDate && l.Date <= request.ToDate)
                .OrderBy(l => l.Date)
                .ThenBy(l => l.DocumentNumber)
                .ToList();

            decimal openingBalance = openingLines.Sum(l => l.Credit) - openingLines.Sum(l => l.Debit);

            // ── Calculate running balance ──
            // Positive = we owe supplier. Credit increases, Debit decreases.

            decimal running = openingBalance;
            foreach (var line in periodLines)
            {
                running += line.Credit - line.Debit;
                line.RunningBalance = running;
            }

            var result = new SupplierStatementDto
            {
                SupplierNameAr = supplier.NameAr,
                SupplierPhone = supplier.Phone,
                OpeningBalance = openingBalance,
                TotalDebit = periodLines.Sum(l => l.Debit),
                TotalCredit = periodLines.Sum(l => l.Credit),
                ClosingBalance = running,
                Lines = periodLines
            };

            return ServiceResult<SupplierStatementDto>.Success(result);
        }

    }
}
