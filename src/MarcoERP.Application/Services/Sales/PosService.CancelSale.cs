using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using Microsoft.Extensions.Logging;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Sales;

namespace MarcoERP.Application.Services.Sales
{
    public sealed partial class PosService
    {
        // ══════════════════════════════════════════════════════════
        //  CANCEL SALE
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult> CancelSaleAsync(int salesInvoiceId, int sessionId, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CancelSaleAsync", "SalesInvoice", salesInvoiceId);
            var invoice = await _invoiceRepo.GetWithLinesAsync(salesInvoiceId, ct);
            if (invoice == null)
                return ServiceResult.Failure("فاتورة البيع غير موجودة.");

            if (invoice.Status != InvoiceStatus.Posted)
                return ServiceResult.Failure("لا يمكن إلغاء إلا الفواتير المرحّلة.");

            var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
            if (session == null)
                return ServiceResult.Failure("جلسة نقطة البيع غير موجودة.");

            if (!session.IsOpen)
                return ServiceResult.Failure("لا يمكن إلغاء فاتورة في جلسة مغلقة.");

            try
            {
                await ExecuteCancelSaleAsync(invoice, session, ct);
                return ServiceResult.Success();
            }
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CancelSaleAsync failed for PosSession.");
                return ServiceResult.Failure(ErrorSanitizer.SanitizeGeneric(ex, "إلغاء عملية البيع"));
            }
        }

        private async Task ExecuteCancelSaleAsync(SalesInvoice invoice, PosSession session, CancellationToken ct)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var context = await _fiscalValidator.ValidateForPosPostingAsync(_dateTime.Today, ct);
                var today = _dateTime.Today;

                await ReverseRevenueJournalAsync(invoice, context, today, ct);
                await ReverseCogsJournalAsync(invoice, context, today, ct);
                await ReverseStockAsync(invoice, session, today, ct);

                // Reverse any payment allocation before cancelling
                if (invoice.PaidAmount > 0)
                    invoice.ReversePayment(invoice.PaidAmount);

                invoice.Cancel();
                _invoiceRepo.Update(invoice);

                await ReverseSessionTotalsAsync(invoice, session, ct);

                await _unitOfWork.SaveChangesAsync(ct);

            }, IsolationLevel.Serializable, ct);
        }

        private async Task ReverseRevenueJournalAsync(
            SalesInvoice invoice,
            PostingContext context,
            DateTime reversalDate,
            CancellationToken ct)
        {
            if (!invoice.JournalEntryId.HasValue)
                return;

            var revenueJournal = await _journalRepo.GetWithLinesAsync(invoice.JournalEntryId.Value, ct);
            if (revenueJournal == null)
                return;

            var reversalRevenue = revenueJournal.CreateReversal(
                reversalDate,
                $"إلغاء POS — فاتورة {invoice.InvoiceNumber}",
                context.FiscalYear.Id,
                context.Period.Id);

            var revNumber = await _journalNumberGen.NextNumberAsync(context.FiscalYear.Id, ct);
            reversalRevenue.Post(revNumber, context.Username, context.Now);
            await _journalRepo.AddAsync(reversalRevenue, ct);

            revenueJournal.MarkAsReversed(reversalRevenue.Id);
            _journalRepo.Update(revenueJournal);
        }

        private async Task ReverseCogsJournalAsync(
            SalesInvoice invoice,
            PostingContext context,
            DateTime reversalDate,
            CancellationToken ct)
        {
            if (!invoice.CogsJournalEntryId.HasValue)
                return;

            var cogsJournal = await _journalRepo.GetWithLinesAsync(invoice.CogsJournalEntryId.Value, ct);
            if (cogsJournal == null)
                return;

            var reversalCogs = cogsJournal.CreateReversal(
                reversalDate,
                $"إلغاء تكلفة POS — فاتورة {invoice.InvoiceNumber}",
                context.FiscalYear.Id,
                context.Period.Id);

            var cogsRevNumber = await _journalNumberGen.NextNumberAsync(context.FiscalYear.Id, ct);
            reversalCogs.Post(cogsRevNumber, context.Username, context.Now);
            await _journalRepo.AddAsync(reversalCogs, ct);

            cogsJournal.MarkAsReversed(reversalCogs.Id);
            _journalRepo.Update(cogsJournal);
        }

        private async Task ReverseStockAsync(
            SalesInvoice invoice,
            PosSession session,
            DateTime today,
            CancellationToken ct)
        {
            // Track running totals per product for correct WAC recalculation
            // when multiple lines reference the same product.
            var runningTotals = new Dictionary<int, (Product product, decimal totalQty)>();

            foreach (var line in invoice.Lines)
            {
                var costPerBaseUnit = 0m;

                // ── WAC recalculation: returned stock re-enters at original cost ──
                if (!runningTotals.TryGetValue(line.ProductId, out var state))
                {
                    var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                    var existingTotalQty = await _stockManager.GetTotalStockAsync(line.ProductId, ct);
                    state = (product, existingTotalQty);
                }

                costPerBaseUnit = state.product.WeightedAverageCost;

                if (state.totalQty <= 0)
                {
                    state.product.SetWeightedAverageCost(costPerBaseUnit);
                }
                else
                {
                    state.product.UpdateWeightedAverageCost(state.totalQty, line.BaseQuantity, costPerBaseUnit);
                }

                // Track running total so next line for same product uses correct base qty
                state.totalQty += line.BaseQuantity;
                runningTotals[line.ProductId] = state;
                _productRepo.Update(state.product);

                await _stockManager.IncreaseAsync(new StockOperation
                {
                    ProductId = line.ProductId,
                    WarehouseId = invoice.WarehouseId,
                    UnitId = line.UnitId,
                    MovementType = MovementType.SalesReturn,
                    Quantity = line.Quantity,
                    BaseQuantity = line.BaseQuantity,
                    CostPerBaseUnit = costPerBaseUnit,
                    DocumentDate = today,
                    DocumentNumber = invoice.InvoiceNumber,
                    SourceType = SourceType.SalesInvoice,
                    SourceId = invoice.Id,
                    Notes = $"إلغاء POS — جلسة {session.SessionNumber}",
                }, ct);
            }
        }

        private async Task ReverseSessionTotalsAsync(SalesInvoice invoice, PosSession session, CancellationToken ct)
        {
            var payments = await _paymentRepo.GetByInvoiceAsync(invoice.Id, ct);
            var cashTotal = payments.Where(p => p.PaymentMethod == PaymentMethod.Cash).Sum(p => p.Amount);
            var cardReversed = payments.Where(p => p.PaymentMethod == PaymentMethod.Card).Sum(p => p.Amount);
            var onAccountReversed = payments.Where(p => p.PaymentMethod == PaymentMethod.OnAccount).Sum(p => p.Amount);

            // POS-08: Use net cash (after change), matching CompleteSale's session recording
            var netCash = Math.Min(cashTotal, invoice.NetTotal - cardReversed - onAccountReversed);

            session.ReverseSale(invoice.NetTotal, netCash, cardReversed, onAccountReversed);
            _sessionRepo.Update(session);
        }
    }
}
