using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Application.Mappers.Sales;
using Microsoft.Extensions.Logging;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Sales;

namespace MarcoERP.Application.Services.Sales
{
    public sealed partial class PosService
    {
        // ══════════════════════════════════════════════════════════
        //  COMPLETE SALE — The critical atomic operation
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Completes a POS sale in one Serializable transaction:
        ///   1. Validate fiscal period open
        ///   2. Validate stock for all lines
        ///   3. Create SalesInvoice (Draft) with lines
        ///   4. Generate Revenue Journal (DR Cash/Bank/AR, CR Sales, CR VAT Output)
        ///   5. Generate COGS Journal (DR COGS, CR Inventory)
        ///   6. Deduct stock &amp; create InventoryMovements
        ///   7. Post the invoice
        ///   8. Record POS payments
        ///   9. Update session totals
        ///   10. Commit — full rollback on any failure
        /// </summary>
        public async Task<ServiceResult<SalesInvoiceDto>> CompleteSaleAsync(CompletePoseSaleDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CompleteSaleAsync", "PosSession", dto.SessionId);

            // G-01 fix: FeatureGuard — block POS operations when POS feature is disabled
            var guard = await FeatureGuard.CheckAsync<SalesInvoiceDto>(_featureService, FeatureKeys.POS, ct);
            if (guard != null) return guard;

            var vr = await _completeSaleValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<SalesInvoiceDto>.Failure(string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var sessionResult = await GetOpenSessionAsync(dto.SessionId, ct);
            if (!sessionResult.IsSuccess)
                return ServiceResult<SalesInvoiceDto>.Failure(sessionResult.ErrorMessage);

            var paymentResult = ParsePayments(dto);
            if (!paymentResult.IsSuccess)
                return ServiceResult<SalesInvoiceDto>.Failure(paymentResult.ErrorMessage);

            try
            {
                var allowNegativeStock = await IsNegativeStockAllowedAsync(ct);
                var receiptPrintingEnabled = await IsReceiptPrintingEnabledAsync(ct);

                var result = await ExecuteCompleteSaleAsync(
                    dto,
                    sessionResult.Data,
                    paymentResult.Data,
                    allowNegativeStock,
                    ct);

                if (receiptPrintingEnabled && _receiptPrinterService != null && _receiptPrinterService.IsAvailable())
                {
                    try
                    {
                        await _receiptPrinterService.PrintReceiptAsync(result.Receipt, ct);
                    }
                    catch
                    {
                        // Receipt printing failures should not block POS sales.
                    }
                }

                result.Invoice.WarningMessage = result.WarningMessage;
                return ServiceResult<SalesInvoiceDto>.Success(result.Invoice);
            }
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult<SalesInvoiceDto>.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CompleteSaleAsync failed for PosSession.");
                return ServiceResult<SalesInvoiceDto>.Failure(ErrorSanitizer.SanitizeGeneric(ex, "إتمام عملية البيع"));
            }
        }

        private async Task<ServiceResult<PosSession>> GetOpenSessionAsync(int sessionId, CancellationToken ct)
        {
            var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
            if (session == null)
                return ServiceResult<PosSession>.Failure("جلسة نقطة البيع غير موجودة.");

            if (!session.IsOpen)
                return ServiceResult<PosSession>.Failure("جلسة نقطة البيع مغلقة.");

            return ServiceResult<PosSession>.Success(session);
        }

        private ServiceResult<PosPaymentBreakdown> ParsePayments(CompletePoseSaleDto dto)
        {
            decimal totalCash = 0;
            decimal totalCard = 0;
            decimal totalOnAccount = 0;
            var parsedPayments = new List<PosParsedPayment>();

            foreach (var p in dto.Payments)
            {
                if (!Enum.TryParse<PaymentMethod>(p.PaymentMethod, true, out var method))
                    return ServiceResult<PosPaymentBreakdown>.Failure($"طريقة الدفع غير صالحة: {p.PaymentMethod}");

                parsedPayments.Add(new PosParsedPayment(method, p.Amount, p.ReferenceNumber));

                switch (method)
                {
                    case PaymentMethod.Cash:
                        totalCash += p.Amount;
                        break;
                    case PaymentMethod.Card:
                        totalCard += p.Amount;
                        break;
                    case PaymentMethod.OnAccount:
                        totalOnAccount += p.Amount;
                        break;
                }
            }

            var customerId = dto.CustomerId ?? 1;
            if (totalOnAccount > 0 && (dto.CustomerId == null || dto.CustomerId <= 0))
                return ServiceResult<PosPaymentBreakdown>.Failure("البيع بالآجل يتطلب تحديد عميل.");

            return ServiceResult<PosPaymentBreakdown>.Success(new PosPaymentBreakdown
            {
                TotalCash = totalCash,
                TotalCard = totalCard,
                TotalOnAccount = totalOnAccount,
                CustomerId = customerId,
                Payments = parsedPayments
            });
        }

        private async Task<PosSaleResult> ExecuteCompleteSaleAsync(
            CompletePoseSaleDto dto,
            PosSession session,
            PosPaymentBreakdown payments,
            bool allowNegativeStock,
            CancellationToken ct)
        {
            SalesInvoiceDto result = null;
            ReceiptDto receipt = null;
            var warningMessages = new List<string>();

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var today = _dateTime.Today;

                var journalContext = await _fiscalValidator.ValidateForPosPostingAsync(today, ct);
                var username = journalContext.Username;

                var lineProducts = await LoadLineProductsAsync(session, dto.Lines, allowNegativeStock, warningMessages, ct);

                var invoice = await CreateDraftInvoiceAsync(dto, session, payments.CustomerId, today, lineProducts, ct);

                if (warningMessages.Count > 0 && _auditLogger != null)
                {
                    foreach (var warning in warningMessages)
                    {
                        await _auditLogger.LogAsync(
                            "SalesInvoice",
                            invoice.Id,
                            "RiskOperation",
                            username,
                            warning,
                            ct);
                    }
                }

                EnsurePaymentTotal(payments.TotalPaid, invoice.NetTotal);

                // POS-01: Prevent journal imbalance — card + on-account must not exceed net total
                if (payments.TotalCard + payments.TotalOnAccount > invoice.NetTotal)
                    throw new SalesInvoiceDomainException(
                        "مجموع الدفع بالبطاقة والآجل لا يمكن أن يتجاوز صافي الفاتورة.");

                var accounts = await ResolvePosAccountsAsync(ct);

                var revenueJournal = await CreateRevenueJournalAsync(
                    invoice,
                    payments,
                    accounts,
                    journalContext,
                    ct);

                var cogsResult = await CreateCogsJournalAsync(
                    invoice,
                    lineProducts,
                    accounts,
                    journalContext,
                    ct);

                await _unitOfWork.SaveChangesAsync(ct);

                await DeductStockAsync(invoice, session, cogsResult.lineCosts, today, allowNegativeStock, ct);

                invoice.Post(revenueJournal.Id, cogsResult.journal?.Id);

                // Mark invoice payment based on POS payment methods
                // Cap to NetTotal so overpayment (change scenario) doesn't exceed invoice amount
                var paidNow = payments.TotalCash + payments.TotalCard;
                var actualPaid = Math.Min(paidNow, invoice.NetTotal);
                if (actualPaid > 0)
                    invoice.ApplyPayment(actualPaid);

                _invoiceRepo.Update(invoice);

                await RecordPosPaymentsAsync(invoice, session, payments, ct);

                // Pass net cash (after change) to session, not the gross cash tendered
                var netCash = Math.Min(payments.TotalCash, invoice.NetTotal - payments.TotalCard - payments.TotalOnAccount);
                session.RecordSale(invoice.NetTotal, netCash, payments.TotalCard, payments.TotalOnAccount);
                _sessionRepo.Update(session);

                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _invoiceRepo.GetWithLinesAsync(invoice.Id, ct);
                result = SalesInvoiceMapper.ToDto(saved);
                receipt = BuildReceiptDto(result, payments, lineProducts, journalContext.Now, username);

            }, IsolationLevel.Serializable, ct);

            return new PosSaleResult
            {
                Invoice = result,
                Receipt = receipt,
                WarningMessage = warningMessages.Count > 0 ? string.Join(" | ", warningMessages) : null
            };
        }



        private async Task<Dictionary<int, Product>> LoadLineProductsAsync(
            PosSession session,
            IReadOnlyList<PosSaleLineDto> lines,
            bool allowNegativeStock,
            List<string> warnings,
            CancellationToken ct)
        {
            var lineProducts = new Dictionary<int, Product>();
            var cumulativeQty = new Dictionary<int, decimal>();

            foreach (var line in lines)
            {
                var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                if (product == null)
                    throw new SalesInvoiceDomainException($"الصنف برقم {line.ProductId} غير موجود.");

                if (product.Status != ProductStatus.Active)
                    throw new SalesInvoiceDomainException($"الصنف ({product.NameAr}) غير نشط.");

                var pu = product.ProductUnits.FirstOrDefault(u => u.UnitId == line.UnitId);
                if (pu == null)
                    throw new SalesInvoiceDomainException($"الوحدة المحددة غير مرتبطة بالصنف ({product.NameAr}).");

                var baseQty = Math.Round(line.Quantity * pu.ConversionFactor, 4);

                // POS-04: Track cumulative requested quantity per product across all lines
                cumulativeQty.TryGetValue(line.ProductId, out var prev);
                cumulativeQty[line.ProductId] = prev + baseQty;

                var whProduct = await _whProductRepo.GetAsync(session.WarehouseId, line.ProductId, ct);
                if (whProduct == null || whProduct.Quantity < cumulativeQty[line.ProductId])
                {
                    var available = whProduct?.Quantity ?? 0;
                    if (!allowNegativeStock)
                    {
                        throw new SalesInvoiceDomainException(
                            $"الكمية المتاحة للصنف ({product.NameAr}) = {available:N2} أقل من المطلوب ({cumulativeQty[line.ProductId]:N2}).");
                    }

                    warnings?.Add($"Negative stock allowed for product {product.NameAr}");
                }

                lineProducts[line.ProductId] = product;
            }

            return lineProducts;
        }

        private async Task<SalesInvoice> CreateDraftInvoiceAsync(
            CompletePoseSaleDto dto,
            PosSession session,
            int customerId,
            DateTime today,
            IReadOnlyDictionary<int, Product> lineProducts,
            CancellationToken ct)
        {
            var invoiceNumber = await _invoiceRepo.GetNextNumberAsync(ct);

            var invoice = new SalesInvoice(
                invoiceNumber,
                today,
                customerId,
                session.WarehouseId,
                dto.Notes ?? $"POS - جلسة {session.SessionNumber}");

            foreach (var lineDto in dto.Lines)
            {
                var product = lineProducts[lineDto.ProductId];
                var pu = product.ProductUnits.FirstOrDefault(u => u.UnitId == lineDto.UnitId);
                if (pu == null)
                    throw new SalesInvoiceDomainException(
                        $"الوحدة المحددة (ID={lineDto.UnitId}) غير مرتبطة بالصنف ({product.NameAr}).");

                invoice.AddLine(
                    lineDto.ProductId,
                    lineDto.UnitId,
                    lineDto.Quantity,
                    lineDto.UnitPrice,
                    pu.ConversionFactor,
                    lineDto.DiscountPercent,
                    product.VatRate);
            }

            await _invoiceRepo.AddAsync(invoice, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return invoice;
        }

        private static void EnsurePaymentTotal(decimal totalPaid, decimal netTotal)
        {
            if (totalPaid < netTotal)
                throw new SalesInvoiceDomainException(
                    $"إجمالي المدفوع ({totalPaid:N2}) أقل من إجمالي الفاتورة ({netTotal:N2}).");
        }

        private async Task<(Account cash, Account card, Account ar, Account sales, Account vat, Account cogs, Account inventory)>
            ResolvePosAccountsAsync(CancellationToken ct)
        {
            var cashAccount = await _accountRepo.GetByCodeAsync(CashAccountCode, ct);
            var cardAccount = await _accountRepo.GetByCodeAsync(CardAccountCode, ct);
            var arAccount = await _accountRepo.GetByCodeAsync(ArAccountCode, ct);
            var salesAccount = await _accountRepo.GetByCodeAsync(SalesAccountCode, ct);
            var vatOutAccount = await _accountRepo.GetByCodeAsync(VatOutputAccountCode, ct);
            var cogsAccount = await _accountRepo.GetByCodeAsync(CogsAccountCode, ct);
            var invAccount = await _accountRepo.GetByCodeAsync(InventoryAccountCode, ct);

            if (cashAccount == null || arAccount == null || salesAccount == null
                || vatOutAccount == null || cogsAccount == null || invAccount == null)
            {
                throw new SalesInvoiceDomainException("حسابات النظام المطلوبة غير موجودة. تأكد من تشغيل Seed.");
            }

            // Card account is optional — falls back to cash if not seeded
            cardAccount ??= cashAccount;

            return (cashAccount, cardAccount, arAccount, salesAccount, vatOutAccount, cogsAccount, invAccount);
        }

        private async Task<JournalEntry> CreateRevenueJournalAsync(
            SalesInvoice invoice,
            PosPaymentBreakdown payments,
            (Account cash, Account card, Account ar, Account sales, Account vat, Account cogs, Account inventory) accounts,
            PostingContext context,
            CancellationToken ct)
        {
            var lines = new List<JournalLineSpec>();

            // Cap cash debit to actual cash retained (exclude change given back to customer)
            var actualCash = Math.Min(payments.TotalCash, invoice.NetTotal - payments.TotalCard - payments.TotalOnAccount);
            if (actualCash > 0)
                lines.Add(new JournalLineSpec(accounts.cash.Id, actualCash, 0,
                    $"نقدي — POS {invoice.InvoiceNumber}"));

            if (payments.TotalCard > 0)
                lines.Add(new JournalLineSpec(accounts.card.Id, payments.TotalCard, 0,
                    $"بطاقة — POS {invoice.InvoiceNumber}"));

            if (payments.TotalOnAccount > 0)
                lines.Add(new JournalLineSpec(accounts.ar.Id, payments.TotalOnAccount, 0,
                    $"آجل — POS {invoice.InvoiceNumber}"));

            var netSalesRevenue = invoice.Subtotal - invoice.DiscountTotal;
            if (netSalesRevenue > 0)
                lines.Add(new JournalLineSpec(accounts.sales.Id, 0, netSalesRevenue,
                    $"مبيعات — POS {invoice.InvoiceNumber}"));

            if (invoice.VatTotal > 0)
                lines.Add(new JournalLineSpec(accounts.vat.Id, 0, invoice.VatTotal,
                    $"ضريبة مخرجات — POS {invoice.InvoiceNumber}"));

            return await _journalFactory.CreateAndPostAsync(
                invoice.InvoiceDate,
                $"نقطة بيع — فاتورة {invoice.InvoiceNumber}",
                SourceType.SalesInvoice,
                context.FiscalYear.Id,
                context.Period.Id,
                lines,
                context.Username,
                context.Now,
                referenceNumber: invoice.InvoiceNumber,
                sourceId: invoice.Id,
                ct: ct);
        }

        private async Task<(JournalEntry journal, Dictionary<int, decimal> lineCosts)> CreateCogsJournalAsync(
            SalesInvoice invoice,
            IReadOnlyDictionary<int, Product> lineProducts,
            (Account cash, Account card, Account ar, Account sales, Account vat, Account cogs, Account inventory) accounts,
            PostingContext context,
            CancellationToken ct)
        {
            decimal totalCogs = 0;
            var lineCosts = new Dictionary<int, decimal>();

            foreach (var line in invoice.Lines)
            {
                var product = lineProducts[line.ProductId];
                var costPerBaseUnit = product.WeightedAverageCost;
                var lineCost = Math.Round(line.BaseQuantity * costPerBaseUnit, 4);
                totalCogs += lineCost;
                lineCosts[line.Id] = costPerBaseUnit;
            }

            var journalLines = new List<JournalLineSpec>();
            if (totalCogs > 0)
            {
                journalLines.Add(new JournalLineSpec(accounts.cogs.Id, totalCogs, 0,
                    $"تكلفة بضاعة — POS {invoice.InvoiceNumber}"));
                journalLines.Add(new JournalLineSpec(accounts.inventory.Id, 0, totalCogs,
                    $"مخزون — تكلفة POS {invoice.InvoiceNumber}"));
            }

            // Guard: skip journal creation when total cost is zero (e.g. new system before first purchase invoice).
            // Mirrors the same pattern in SalesInvoiceService.Posting.cs.
            if (totalCogs <= 0)
                return (null, lineCosts);

            var journal = await _journalFactory.CreateAndPostAsync(
                invoice.InvoiceDate,
                $"تكلفة بضاعة مباعة — POS {invoice.InvoiceNumber}",
                SourceType.SalesInvoice,
                context.FiscalYear.Id,
                context.Period.Id,
                journalLines,
                context.Username,
                context.Now,
                referenceNumber: invoice.InvoiceNumber,
                sourceId: invoice.Id,
                ct: ct);

            return (journal, lineCosts);
        }

        private async Task DeductStockAsync(
            SalesInvoice invoice,
            PosSession session,
            IReadOnlyDictionary<int, decimal> lineCosts,
            DateTime today,
            bool allowNegativeStock,
            CancellationToken ct)
        {
            foreach (var line in invoice.Lines)
            {
                var costPerBaseUnit = lineCosts.TryGetValue(line.Id, out var unitCost) ? unitCost : 0;

                await _stockManager.DecreaseAsync(new StockOperation
                {
                    ProductId = line.ProductId,
                    WarehouseId = session.WarehouseId,
                    UnitId = line.UnitId,
                    MovementType = MovementType.SalesOut,
                    Quantity = line.Quantity,
                    BaseQuantity = line.BaseQuantity,
                    CostPerBaseUnit = costPerBaseUnit,
                    DocumentDate = today,
                    DocumentNumber = invoice.InvoiceNumber,
                    SourceType = SourceType.SalesInvoice,
                    SourceId = invoice.Id,
                    Notes = $"POS — جلسة {session.SessionNumber}",
                    AllowCreate = allowNegativeStock,
                    AllowNegativeStock = allowNegativeStock,
                }, ct);
            }
        }

        private static ReceiptDto BuildReceiptDto(
            SalesInvoiceDto invoice,
            PosPaymentBreakdown payments,
            IReadOnlyDictionary<int, Product> lineProducts,
            DateTime now,
            string cashierName)
        {
            var receipt = new ReceiptDto
            {
                InvoiceNumber = invoice.InvoiceNumber,
                DateTime = now,
                Subtotal = invoice.Subtotal,
                Discount = invoice.DiscountTotal,
                Vat = invoice.VatTotal,
                NetTotal = invoice.NetTotal,
                PaidAmount = payments.TotalPaid,
                Change = payments.TotalPaid > invoice.NetTotal ? payments.TotalPaid - invoice.NetTotal : 0,
                CashierName = cashierName
            };

            if (invoice.Lines != null)
            {
                foreach (var line in invoice.Lines)
                {
                    var name = lineProducts.TryGetValue(line.ProductId, out var product)
                        ? product.NameAr
                        : $"#{line.ProductId}";

                    receipt.Items.Add(new ReceiptItemDto
                    {
                        Name = name,
                        Qty = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        Total = line.TotalWithVat
                    });
                }
            }

            return receipt;
        }

        private async Task<bool> IsNegativeStockAllowedAsync(CancellationToken ct)
        {
            if (_featureService == null)
                return false;

            var result = await _featureService.IsEnabledAsync(FeatureKeys.AllowNegativeStock, ct);
            return result.IsSuccess && result.Data;
        }

        private async Task<bool> IsReceiptPrintingEnabledAsync(CancellationToken ct)
        {
            if (_featureService == null)
                return false;

            var result = await _featureService.IsEnabledAsync(FeatureKeys.ReceiptPrinting, ct);
            return result.IsSuccess && result.Data;
        }

        private async Task RecordPosPaymentsAsync(
            SalesInvoice invoice,
            PosSession session,
            PosPaymentBreakdown payments,
            CancellationToken ct)
        {
            foreach (var payment in payments.Payments)
            {
                var posPayment = new PosPayment(
                    invoice.Id,
                    session.Id,
                    payment.Method,
                    payment.Amount,
                    _dateTime.UtcNow,
                    payment.Reference);

                await _paymentRepo.AddAsync(posPayment, ct);
            }
        }
    }
}
