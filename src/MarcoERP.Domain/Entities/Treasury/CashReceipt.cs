using System;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Treasury;

namespace MarcoERP.Domain.Entities.Treasury
{
    /// <summary>
    /// Represents a cash receipt voucher (سند قبض).
    /// Money received INTO a cashbox from a customer or any GL account.
    /// Lifecycle: Draft → Posted → Cancelled.
    /// On posting: auto-generates journal entry (DR Cashbox / CR Contra Account).
    /// </summary>
    public sealed class CashReceipt : CompanyAwareEntity
    {
        // ── Constructors ─────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private CashReceipt() { }

        /// <summary>
        /// Creates a new cash receipt in Draft status.
        /// </summary>
        public CashReceipt(CashReceiptDraft draft)
        {
            if (draft == null)
                throw new TreasuryDomainException("بيانات سند القبض مطلوبة.");

            if (string.IsNullOrWhiteSpace(draft.ReceiptNumber))
                throw new TreasuryDomainException("رقم سند القبض مطلوب.");
            if (draft.CashboxId <= 0)
                throw new TreasuryDomainException("الخزنة مطلوبة.");
            if (draft.AccountId <= 0)
                throw new TreasuryDomainException("الحساب المقابل مطلوب.");
            if (draft.Amount <= 0)
                throw new TreasuryDomainException("مبلغ سند القبض يجب أن يكون أكبر من صفر.");
            if (string.IsNullOrWhiteSpace(draft.Description))
                throw new TreasuryDomainException("وصف سند القبض مطلوب.");

            ReceiptNumber = draft.ReceiptNumber.Trim();
            ReceiptDate = draft.ReceiptDate;
            CashboxId = draft.CashboxId;
            AccountId = draft.AccountId;
            Amount = draft.Amount;
            Description = draft.Description.Trim();
            CustomerId = draft.CustomerId;
            SalesInvoiceId = draft.SalesInvoiceId;
            Notes = draft.Notes?.Trim();
            Status = InvoiceStatus.Draft;
            Cashbox = null;
        }

        // ── Properties ───────────────────────────────────────────

        /// <summary>Auto-generated receipt number (CR-YYYYMM-####).</summary>
        public string ReceiptNumber { get; private set; }

        /// <summary>Receipt date.</summary>
        public DateTime ReceiptDate { get; private set; }

        /// <summary>FK to Cashbox where money is received.</summary>
        public int CashboxId { get; private set; }

        /// <summary>Navigation to Cashbox.</summary>
        public Cashbox Cashbox { get; private set; }

        /// <summary>Contra GL account (e.g. 1121 AR for customer payment, or any postable account).</summary>
        public int AccountId { get; private set; }

        /// <summary>Navigation to contra GL Account.</summary>
        public Account Account { get; private set; }

        /// <summary>Optional FK to Customer (if this is a customer payment).</summary>
        public int? CustomerId { get; private set; }

        /// <summary>Navigation to linked Customer.</summary>
        public Customer Customer { get; private set; }

        /// <summary>Optional FK to SalesInvoice when receipt is created from a sales invoice.</summary>
        public int? SalesInvoiceId { get; private set; }

        /// <summary>Receipt amount.</summary>
        public decimal Amount { get; private set; }

        /// <summary>Required description of the receipt.</summary>
        public string Description { get; private set; }

        /// <summary>Optional notes.</summary>
        public string Notes { get; private set; }

        /// <summary>Document status: Draft → Posted → Cancelled.</summary>
        public InvoiceStatus Status { get; private set; }

        /// <summary>FK to the auto-generated journal entry (set on posting).</summary>
        public int? JournalEntryId { get; private set; }

        // ── Domain Methods ───────────────────────────────────────

        /// <summary>
        /// Updates the receipt header. Only allowed while Draft.
        /// </summary>
        public void UpdateHeader(
            DateTime receiptDate,
            int cashboxId,
            int accountId,
            decimal amount,
            string description,
            int? customerId,
            int? salesInvoiceId,
            string notes)
        {
            EnsureDraft("لا يمكن تعديل سند قبض مرحّل أو ملغى.");

            if (cashboxId <= 0)
                throw new TreasuryDomainException("الخزنة مطلوبة.");
            if (accountId <= 0)
                throw new TreasuryDomainException("الحساب المقابل مطلوب.");
            if (amount <= 0)
                throw new TreasuryDomainException("مبلغ سند القبض يجب أن يكون أكبر من صفر.");
            if (string.IsNullOrWhiteSpace(description))
                throw new TreasuryDomainException("وصف سند القبض مطلوب.");

            ReceiptDate = receiptDate;
            CashboxId = cashboxId;
            AccountId = accountId;
            Amount = amount;
            Description = description.Trim();
            CustomerId = customerId;
            SalesInvoiceId = salesInvoiceId;
            Notes = notes?.Trim();
        }

        /// <summary>
        /// Posts the cash receipt. Journal entry ID is assigned.
        /// </summary>
        public void Post(int journalEntryId)
        {
            EnsureDraft("لا يمكن ترحيل سند قبض مرحّل بالفعل أو ملغى.");

            if (journalEntryId <= 0)
                throw new TreasuryDomainException("معرف القيد المحاسبي غير صالح.");

            Status = InvoiceStatus.Posted;
            JournalEntryId = journalEntryId;
        }

        /// <summary>
        /// Cancels a posted cash receipt.
        /// </summary>
        public void Cancel()
        {
            if (Status != InvoiceStatus.Posted)
                throw new TreasuryDomainException("لا يمكن إلغاء إلا سندات القبض المرحّلة.");
            Status = InvoiceStatus.Cancelled;
        }

        // ── Soft Delete Override ────────────────────────────────

        /// <summary>
        /// Only draft receipts can be soft-deleted.
        /// Posted/Cancelled receipts are immutable.
        /// </summary>
        public override void SoftDelete(string deletedBy, DateTime deletedAt)
        {
            if (Status != InvoiceStatus.Draft)
                throw new TreasuryDomainException("لا يمكن حذف سند قبض مرحّل أو ملغى — استخدم الإلغاء.");

            base.SoftDelete(deletedBy, deletedAt);
        }

        // ── Private Helpers ──────────────────────────────────────

        private void EnsureDraft(string errorMessage)
        {
            if (Status != InvoiceStatus.Draft)
                throw new TreasuryDomainException(errorMessage);
        }
    }

    public sealed class CashReceiptDraft
    {
        public string ReceiptNumber { get; init; }
        public DateTime ReceiptDate { get; init; }
        public int CashboxId { get; init; }
        public int AccountId { get; init; }
        public decimal Amount { get; init; }
        public string Description { get; init; }
        public int? CustomerId { get; init; }
        public int? SalesInvoiceId { get; init; }
        public string Notes { get; init; }
    }
}
