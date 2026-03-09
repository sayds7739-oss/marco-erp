using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Mappers.Sales;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Exceptions.Accounting;
using MarcoERP.Domain.Exceptions.Inventory;
using MarcoERP.Domain.Exceptions.Sales;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Sales
{
    public sealed partial class SalesInvoiceService
    {
        // ══════════════════════════════════════════════════════════
        //  POST — The critical operation
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Posts a draft sales invoice. This triggers:
        ///   1. Stock validation — every line must have sufficient warehouse stock.
        ///   2. Revenue journal — DR AR / CR Sales / CR VAT Output.
        ///   3. COGS journal — DR COGS / CR Inventory (per-line at current WAC).
        ///   4. Warehouse stock decrease + inventory movement records.
        ///   5. Invoice status → Posted.
        /// </summary>
        public async Task<ServiceResult<SalesInvoiceDto>> PostAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "PostAsync", "SalesInvoice", id);

            var invoice = await _invoiceRepo.GetWithLinesTrackedAsync(id, ct);
            var preCheck = ValidatePostPreconditions(invoice);
            if (preCheck != null) return preCheck;

            var allowNegativeStock = await IsNegativeStockAllowedAsync(ct);
            var warningMessages = new List<string>();

            SalesInvoice saved = null;

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var reloaded = await _invoiceRepo.GetWithLinesTrackedAsync(invoice.Id, ct);
                    var statusCheck = ValidatePostPreconditions(reloaded);
                    if (statusCheck != null)
                        throw new SalesInvoiceDomainException(statusCheck.ErrorMessage ?? "البيانات تغيرت أثناء الترحيل.");

                    // ── Credit Control Re-check inside transaction ──────
                    var creditError = await GetCreditControlErrorAsync(reloaded.CustomerId, reloaded.NetTotal, ct);
                    if (creditError != null)
                        throw new SalesInvoiceDomainException(creditError);

                    await ValidateStockAsync(reloaded, allowNegativeStock, warningMessages, ct);

                    var postingCtx = await _fiscalValidator.ValidateForPostingAsync(reloaded.InvoiceDate, ct);
                    var accounts = await ResolvePostingAccountsAsync(ct);

                    var revenueJournal = await CreateRevenueJournalAsync(
                        reloaded,
                        postingCtx.FiscalYear,
                        postingCtx.Period,
                        accounts,
                        postingCtx.Now,
                        postingCtx.Username,
                        ct);

                    var cogsResult = await CreateCogsJournalAsync(
                        reloaded,
                        postingCtx.FiscalYear,
                        postingCtx.Period,
                        accounts,
                        postingCtx.Now,
                        postingCtx.Username,
                        ct);

                    // ── Commission journal (COM-02/COM-03) ────────────
                    JournalEntry commissionJournal = null;
                    if (reloaded.SalesRepresentativeId.HasValue && reloaded.SalesRepresentative != null)
                    {
                        var rep = reloaded.SalesRepresentative;
                        if (rep.CommissionRate > 0)
                        {
                            var netSalesRevenue = reloaded.Subtotal - reloaded.DiscountTotal + reloaded.DeliveryFee;
                            decimal commissionBase = rep.CommissionBasedOn == CommissionBasis.Profit
                                ? netSalesRevenue - cogsResult.totalCogs
                                : netSalesRevenue;

                            var commission = Math.Round(commissionBase * rep.CommissionRate / 100m, 2);
                            if (commission > 0)
                            {
                                commissionJournal = await CreateCommissionJournalAsync(
                                    reloaded,
                                    commission,
                                    postingCtx.FiscalYear,
                                    postingCtx.Period,
                                    postingCtx.Now,
                                    postingCtx.Username,
                                    ct);
                            }
                        }
                    }

                    // Save journals (revenue always, COGS only when totalCogs > 0)
                    // to get DB-generated Ids before Post().
                    await _unitOfWork.SaveChangesAsync(ct);

                    await DeductStockAsync(reloaded, cogsResult.lineCosts, allowNegativeStock, ct);

                    reloaded.Post(revenueJournal.Id, cogsResult.journal?.Id, commissionJournal?.Id);
                    await _unitOfWork.SaveChangesAsync(ct);

                    saved = await _invoiceRepo.GetWithLinesAsync(reloaded.Id, ct);
                }, IsolationLevel.Serializable, ct);

                var dto = SalesInvoiceMapper.ToDto(saved);
                if (warningMessages.Count > 0)
                    dto.WarningMessage = string.Join(" | ", warningMessages);
                return ServiceResult<SalesInvoiceDto>.Success(dto);
            }
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult<SalesInvoiceDto>.Failure(ex.Message);
            }
            catch (JournalEntryDomainException ex)
            {
                return ServiceResult<SalesInvoiceDto>.Failure(ex.Message);
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult<SalesInvoiceDto>.Failure(ex.Message);
            }
            catch (AccountDomainException ex)
            {
                return ServiceResult<SalesInvoiceDto>.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while posting sales invoice {InvoiceId}.", invoice?.Id);
                return ServiceResult<SalesInvoiceDto>.Failure(
                    ErrorSanitizer.Sanitize(ex, "ترحيل فاتورة البيع"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<SalesInvoiceDto>.Failure("تعذر ترحيل الفاتورة بسبب تعارض تحديث متزامن. يرجى إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult<SalesInvoiceDto>.Failure("تعذر ترحيل الفاتورة بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post sales invoice {InvoiceId}.", invoice?.Id);
                return ServiceResult<SalesInvoiceDto>.Failure("حدث خطأ غير متوقع أثناء ترحيل الفاتورة.");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CANCEL
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CancelAsync", "SalesInvoice", id);

            var invoice = await _invoiceRepo.GetWithLinesTrackedAsync(id, ct);
            var preCheck = ValidateCancelPreconditions(invoice);
            if (preCheck != null) return preCheck;

            try
            {
                var cancelCtx = await _fiscalValidator.ValidateForCancelAsync(invoice.InvoiceDate, ct);

                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var reloaded = await _invoiceRepo.GetWithLinesTrackedAsync(invoice.Id, ct);
                    var statusCheck = ValidateCancelPreconditions(reloaded);
                    if (statusCheck != null) throw new SalesInvoiceDomainException(statusCheck.ErrorMessage);

                    await ReverseStockAsync(reloaded, cancelCtx.Today, ct);
                    await CreateRevenueReversalAsync(reloaded, cancelCtx.FiscalYear, cancelCtx.Period, cancelCtx.Today, ct);

                    if (reloaded.CogsJournalEntryId.HasValue)
                        await CreateCogsReversalAsync(reloaded, cancelCtx.FiscalYear, cancelCtx.Period, cancelCtx.Today, ct);

                    if (reloaded.CommissionJournalEntryId.HasValue)
                        await CreateCommissionReversalAsync(reloaded, cancelCtx.FiscalYear, cancelCtx.Period, cancelCtx.Today, ct);

                    reloaded.Cancel();
                    await _unitOfWork.SaveChangesAsync(ct);
                }, IsolationLevel.Serializable, ct);

                return ServiceResult.Success();
            }
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (JournalEntryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (AccountDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while cancelling sales invoice {InvoiceId}.", invoice?.Id);
                return ServiceResult.Failure(
                    ErrorSanitizer.Sanitize(ex, "إلغاء فاتورة البيع"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult.Failure("تعذر إلغاء الفاتورة بسبب تعارض تحديث متزامن. يرجى إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult.Failure("تعذر إلغاء الفاتورة بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel sales invoice {InvoiceId}.", invoice?.Id);
                return ServiceResult.Failure("حدث خطأ غير متوقع أثناء إلغاء فاتورة البيع.");
            }
        }

        // ── Precondition Validators ──────────────────────────────

        private static ServiceResult<SalesInvoiceDto> ValidatePostPreconditions(SalesInvoice invoice)
        {
            if (invoice == null)
                return ServiceResult<SalesInvoiceDto>.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Draft)
                return ServiceResult<SalesInvoiceDto>.Failure("لا يمكن ترحيل فاتورة مرحّلة بالفعل أو ملغاة.");

            if (!invoice.Lines.Any())
                return ServiceResult<SalesInvoiceDto>.Failure("لا يمكن ترحيل فاتورة بدون بنود.");

            if (invoice.NetTotal <= 0)
                return ServiceResult<SalesInvoiceDto>.Failure("صافي الفاتورة يجب أن يكون أكبر من صفر.");

            return null;
        }

        private static ServiceResult ValidateCancelPreconditions(SalesInvoice invoice)
        {
            if (invoice == null)
                return ServiceResult.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Posted)
                return ServiceResult.Failure("لا يمكن إلغاء إلا الفواتير المرحّلة.");

            if (!invoice.JournalEntryId.HasValue)
                return ServiceResult.Failure("لا يمكن إلغاء فاتورة بدون قيود محاسبية.");

            if (invoice.PaidAmount > 0)
                return ServiceResult.Failure(
                    $"لا يمكن إلغاء فاتورة عليها دفعات ({invoice.PaidAmount:N2}). يجب إلغاء سندات القبض المرتبطة أولاً.");

            return null;
        }

        // ── Journal Creation ─────────────────────────────────────

        private async Task<JournalEntry> CreateRevenueJournalAsync(
            SalesInvoice invoice,
            FiscalYear fiscalYear,
            FiscalPeriod period,
            (Account ar, Account sales, Account vatOutput, Account cogs, Account inventory) accounts,
            DateTime now,
            string username,
            CancellationToken ct)
        {
            var lines = new List<JournalLineSpec>();

            lines.Add(new JournalLineSpec(accounts.ar.Id, invoice.NetTotal, 0,
                $"عميل — فاتورة بيع {invoice.InvoiceNumber}"));

            var netSalesRevenue = invoice.Subtotal - invoice.DiscountTotal + invoice.DeliveryFee;
            if (netSalesRevenue > 0)
                lines.Add(new JournalLineSpec(accounts.sales.Id, 0, netSalesRevenue,
                    $"مبيعات — فاتورة بيع {invoice.InvoiceNumber}"));

            if (invoice.VatTotal > 0)
                lines.Add(new JournalLineSpec(accounts.vatOutput.Id, 0, invoice.VatTotal,
                    $"ضريبة مخرجات — فاتورة بيع {invoice.InvoiceNumber}"));

            return await _journalFactory.CreateAndPostAsync(
                invoice.InvoiceDate,
                $"فاتورة بيع رقم {invoice.InvoiceNumber}",
                SourceType.SalesInvoice,
                fiscalYear.Id,
                period.Id,
                lines,
                username,
                now,
                referenceNumber: invoice.InvoiceNumber,
                sourceId: invoice.Id,
                ct: ct);
        }

#nullable enable
        private async Task<(JournalEntry? journal, Dictionary<int, decimal> lineCosts, decimal totalCogs)> CreateCogsJournalAsync(
#nullable restore
            SalesInvoice invoice,
            FiscalYear fiscalYear,
            FiscalPeriod period,
            (Account ar, Account sales, Account vatOutput, Account cogs, Account inventory) accounts,
            DateTime now,
            string username,
            CancellationToken ct)
        {
            decimal totalCogs = 0;
            var lineCosts = new Dictionary<int, decimal>();

            foreach (var line in invoice.Lines)
            {
                var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                var costPerBaseUnit = product.WeightedAverageCost;
                var lineCost = Math.Round(line.BaseQuantity * costPerBaseUnit, 4);
                totalCogs += lineCost;
                lineCosts[line.Id] = costPerBaseUnit;
            }

            // When all products have zero WAC, skip COGS journal entirely
            // (JE-INV-04 forbids lines with both Debit=0 and Credit=0).
            if (totalCogs <= 0)
                return (null, lineCosts, 0);

            var lines = new List<JournalLineSpec>
            {
                new JournalLineSpec(accounts.cogs.Id, totalCogs, 0,
                    $"تكلفة بضاعة مباعة — فاتورة بيع {invoice.InvoiceNumber}"),
                new JournalLineSpec(accounts.inventory.Id, 0, totalCogs,
                    $"مخزون — تكلفة بضاعة مباعة {invoice.InvoiceNumber}")
            };

            var journal = await _journalFactory.CreateAndPostAsync(
                invoice.InvoiceDate,
                $"تكلفة بضاعة مباعة — فاتورة بيع {invoice.InvoiceNumber}",
                SourceType.SalesInvoice,
                fiscalYear.Id,
                period.Id,
                lines,
                username,
                now,
                referenceNumber: invoice.InvoiceNumber,
                sourceId: invoice.Id,
                ct: ct);

            return (journal, lineCosts, totalCogs);
        }

        // ── Journal Reversals ────────────────────────────────────

        private async Task CreateRevenueReversalAsync(
            SalesInvoice invoice,
            FiscalYear fiscalYear,
            FiscalPeriod period,
            DateTime today,
            CancellationToken ct)
        {
            var revenueJournal = await _journalRepo.GetWithLinesAsync(invoice.JournalEntryId.Value, ct);
            if (revenueJournal == null)
                throw new SalesInvoiceDomainException("قيد الإيراد الأصلي غير موجود.");

            var revenueReversal = revenueJournal.CreateReversal(
                today,
                $"عكس إيراد فاتورة بيع رقم {invoice.InvoiceNumber}",
                fiscalYear.Id,
                period.Id);

            var revenueNumber = await _journalNumberGen.NextNumberAsync(fiscalYear.Id, ct);
            revenueReversal.Post(revenueNumber, _currentUser.Username, _dateTime.UtcNow);
            await _journalRepo.AddAsync(revenueReversal, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            revenueJournal.MarkAsReversed(revenueReversal.Id);
            _journalRepo.Update(revenueJournal);
        }

        private async Task CreateCogsReversalAsync(
            SalesInvoice invoice,
            FiscalYear fiscalYear,
            FiscalPeriod period,
            DateTime today,
            CancellationToken ct)
        {
            var cogsJournal = await _journalRepo.GetWithLinesAsync(invoice.CogsJournalEntryId.Value, ct);
            if (cogsJournal == null)
                throw new SalesInvoiceDomainException("قيد التكلفة الأصلي غير موجود.");

            var cogsReversal = cogsJournal.CreateReversal(
                today,
                $"عكس تكلفة فاتورة بيع رقم {invoice.InvoiceNumber}",
                fiscalYear.Id,
                period.Id);

            var cogsNumber = await _journalNumberGen.NextNumberAsync(fiscalYear.Id, ct);
            cogsReversal.Post(cogsNumber, _currentUser.Username, _dateTime.UtcNow);
            await _journalRepo.AddAsync(cogsReversal, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            cogsJournal.MarkAsReversed(cogsReversal.Id);
            _journalRepo.Update(cogsJournal);
        }

        // ── Commission Journal ────────────────────────────────────

        private async Task<JournalEntry> CreateCommissionJournalAsync(
            SalesInvoice invoice,
            decimal commissionAmount,
            FiscalYear fiscalYear,
            FiscalPeriod period,
            DateTime now,
            string username,
            CancellationToken ct)
        {
            var expenseAccount = await _accountRepo.GetByCodeAsync(CommissionExpenseAccountCode, ct);
            var payableAccount = await _accountRepo.GetByCodeAsync(CommissionPayableAccountCode, ct);

            if (expenseAccount == null || payableAccount == null)
                throw new SalesInvoiceDomainException(
                    "حسابات العمولة (مصروف العمولات / العمولات المستحقة) غير موجودة. تأكد من تشغيل Seed.");

            var lines = new List<JournalLineSpec>
            {
                new JournalLineSpec(expenseAccount.Id, commissionAmount, 0,
                    $"مصروف عمولة — فاتورة بيع {invoice.InvoiceNumber}"),
                new JournalLineSpec(payableAccount.Id, 0, commissionAmount,
                    $"عمولة مستحقة — فاتورة بيع {invoice.InvoiceNumber}")
            };

            return await _journalFactory.CreateAndPostAsync(
                invoice.InvoiceDate,
                $"عمولة مندوب — فاتورة بيع رقم {invoice.InvoiceNumber}",
                SourceType.SalesInvoice,
                fiscalYear.Id,
                period.Id,
                lines,
                username,
                now,
                referenceNumber: invoice.InvoiceNumber,
                sourceId: invoice.Id,
                ct: ct);
        }

        private async Task CreateCommissionReversalAsync(
            SalesInvoice invoice,
            FiscalYear fiscalYear,
            FiscalPeriod period,
            DateTime today,
            CancellationToken ct)
        {
            var commissionJournal = await _journalRepo.GetWithLinesAsync(invoice.CommissionJournalEntryId.Value, ct);
            if (commissionJournal == null)
                throw new SalesInvoiceDomainException("قيد العمولة الأصلي غير موجود.");

            var commissionReversal = commissionJournal.CreateReversal(
                today,
                $"عكس عمولة فاتورة بيع رقم {invoice.InvoiceNumber}",
                fiscalYear.Id,
                period.Id);

            var commissionNumber = await _journalNumberGen.NextNumberAsync(fiscalYear.Id, ct);
            commissionReversal.Post(commissionNumber, _currentUser.Username, _dateTime.UtcNow);
            await _journalRepo.AddAsync(commissionReversal, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            commissionJournal.MarkAsReversed(commissionReversal.Id);
            _journalRepo.Update(commissionJournal);
        }
    }
}
