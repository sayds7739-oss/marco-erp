using System;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Treasury;

namespace MarcoERP.Domain.Entities.Treasury
{
    /// <summary>
    /// Represents a cash payment voucher (سند صرف).
    /// Money paid OUT from a cashbox to a supplier or any GL account.
    /// Lifecycle: Draft → Posted → Cancelled.
    /// On posting: auto-generates journal entry (DR Contra Account / CR Cashbox).
    /// </summary>
    public sealed class CashPayment : CompanyAwareEntity
    {
        // ── Constructors ─────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private CashPayment() { }

        /// <summary>
        /// Creates a new cash payment in Draft status.
        /// </summary>
        public CashPayment(CashPaymentDraft draft)
        {
            if (draft == null)
                throw new TreasuryDomainException("بيانات سند الصرف مطلوبة.");

            if (string.IsNullOrWhiteSpace(draft.PaymentNumber))
                throw new TreasuryDomainException("رقم سند الصرف مطلوب.");
            if (draft.CashboxId <= 0)
                throw new TreasuryDomainException("الخزنة مطلوبة.");
            if (draft.AccountId <= 0)
                throw new TreasuryDomainException("الحساب المقابل مطلوب.");
            if (draft.Amount <= 0)
                throw new TreasuryDomainException("مبلغ سند الصرف يجب أن يكون أكبر من صفر.");
            if (string.IsNullOrWhiteSpace(draft.Description))
                throw new TreasuryDomainException("وصف سند الصرف مطلوب.");

            PaymentNumber = draft.PaymentNumber.Trim();
            PaymentDate = draft.PaymentDate;
            CashboxId = draft.CashboxId;
            AccountId = draft.AccountId;
            Amount = draft.Amount;
            Description = draft.Description.Trim();
            SupplierId = draft.SupplierId;
            PurchaseInvoiceId = draft.PurchaseInvoiceId;
            Notes = draft.Notes?.Trim();
            Status = InvoiceStatus.Draft;
            Cashbox = null;
        }

        // ── Properties ───────────────────────────────────────────

        /// <summary>Auto-generated payment number (CP-YYYYMM-####).</summary>
        public string PaymentNumber { get; private set; }

        /// <summary>Payment date.</summary>
        public DateTime PaymentDate { get; private set; }

        /// <summary>FK to Cashbox where money leaves.</summary>
        public int CashboxId { get; private set; }

        /// <summary>Navigation to Cashbox.</summary>
        public Cashbox Cashbox { get; private set; }

        /// <summary>Contra GL account (e.g. 2111 AP for supplier payment, or any postable account).</summary>
        public int AccountId { get; private set; }

        /// <summary>Navigation to contra GL Account.</summary>
        public Account Account { get; private set; }

        /// <summary>Optional FK to Supplier (if this is a supplier payment).</summary>
        public int? SupplierId { get; private set; }

        /// <summary>Navigation to linked Supplier.</summary>
        public Supplier Supplier { get; private set; }

        /// <summary>Optional FK to PurchaseInvoice when payment is created from a purchase invoice.</summary>
        public int? PurchaseInvoiceId { get; private set; }

        /// <summary>Payment amount.</summary>
        public decimal Amount { get; private set; }

        /// <summary>Required description of the payment.</summary>
        public string Description { get; private set; }

        /// <summary>Optional notes.</summary>
        public string Notes { get; private set; }

        /// <summary>Document status: Draft → Posted → Cancelled.</summary>
        public InvoiceStatus Status { get; private set; }

        /// <summary>FK to the auto-generated journal entry (set on posting).</summary>
        public int? JournalEntryId { get; private set; }

        // ── Domain Methods ───────────────────────────────────────

        /// <summary>
        /// Updates the payment header. Only allowed while Draft.
        /// </summary>
        public void UpdateHeader(
            DateTime paymentDate,
            int cashboxId,
            int accountId,
            decimal amount,
            string description,
            int? supplierId,
            int? purchaseInvoiceId,
            string notes)
        {
            EnsureDraft("لا يمكن تعديل سند صرف مرحّل أو ملغى.");

            if (cashboxId <= 0)
                throw new TreasuryDomainException("الخزنة مطلوبة.");
            if (accountId <= 0)
                throw new TreasuryDomainException("الحساب المقابل مطلوب.");
            if (amount <= 0)
                throw new TreasuryDomainException("مبلغ سند الصرف يجب أن يكون أكبر من صفر.");
            if (string.IsNullOrWhiteSpace(description))
                throw new TreasuryDomainException("وصف سند الصرف مطلوب.");

            PaymentDate = paymentDate;
            CashboxId = cashboxId;
            AccountId = accountId;
            Amount = amount;
            Description = description.Trim();
            SupplierId = supplierId;
            PurchaseInvoiceId = purchaseInvoiceId;
            Notes = notes?.Trim();
        }

        /// <summary>
        /// Posts the cash payment. Journal entry ID is assigned.
        /// </summary>
        public void Post(int journalEntryId)
        {
            EnsureDraft("لا يمكن ترحيل سند صرف مرحّل بالفعل أو ملغى.");

            if (journalEntryId <= 0)
                throw new TreasuryDomainException("معرف القيد المحاسبي غير صالح.");

            Status = InvoiceStatus.Posted;
            JournalEntryId = journalEntryId;
        }

        /// <summary>
        /// Cancels a posted cash payment.
        /// </summary>
        public void Cancel()
        {
            if (Status != InvoiceStatus.Posted)
                throw new TreasuryDomainException("لا يمكن إلغاء إلا سندات الصرف المرحّلة.");
            Status = InvoiceStatus.Cancelled;
        }

        // ── Soft Delete Override ────────────────────────────────

        /// <summary>
        /// Only draft payments can be soft-deleted.
        /// Posted/Cancelled payments are immutable.
        /// </summary>
        public override void SoftDelete(string deletedBy, DateTime deletedAt)
        {
            if (Status != InvoiceStatus.Draft)
                throw new TreasuryDomainException("لا يمكن حذف سند صرف مرحّل أو ملغى — استخدم الإلغاء.");

            base.SoftDelete(deletedBy, deletedAt);
        }

        // ── Private Helpers ──────────────────────────────────────

        private void EnsureDraft(string errorMessage)
        {
            if (Status != InvoiceStatus.Draft)
                throw new TreasuryDomainException(errorMessage);
        }
    }

    public sealed class CashPaymentDraft
    {
        public string PaymentNumber { get; init; }
        public DateTime PaymentDate { get; init; }
        public int CashboxId { get; init; }
        public int AccountId { get; init; }
        public decimal Amount { get; init; }
        public string Description { get; init; }
        public int? SupplierId { get; init; }
        public int? PurchaseInvoiceId { get; init; }
        public string Notes { get; init; }
    }
}
