using System;
using System.Collections.Generic;
using System.Linq;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Sales;
using MarcoERP.Domain.Entities.Purchases;

namespace MarcoERP.Domain.Entities.Sales
{
    /// <summary>
    /// Represents a sales return header (مرتجع بيع).
    /// Lifecycle: Draft → Posted → (optionally) Cancelled.
    /// On posting: reversal revenue journal + reversal COGS journal, stock re-addition.
    /// </summary>
    public sealed class SalesReturn : CompanyAwareEntity
    {
        private readonly List<SalesReturnLine> _lines = new();

        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private SalesReturn() { }

        /// <summary>
        /// Creates a new sales return in Draft status.
        /// </summary>
        public SalesReturn(
            string returnNumber,
            DateTime returnDate,
            int? customerId,
            int warehouseId,
            int? originalInvoiceId,
            string notes,
            int? salesRepresentativeId = null,
            CounterpartyType counterpartyType = CounterpartyType.Customer,
            int? supplierId = null)
        {
            if (string.IsNullOrWhiteSpace(returnNumber))
                throw new SalesReturnDomainException("رقم مرتجع البيع مطلوب.");

            if (counterpartyType == CounterpartyType.Customer && (!customerId.HasValue || customerId <= 0))
                throw new SalesReturnDomainException("العميل مطلوب.");

            if (warehouseId <= 0)
                throw new SalesReturnDomainException("المستودع مطلوب.");

            if (counterpartyType == CounterpartyType.Supplier && (!supplierId.HasValue || supplierId <= 0))
                throw new SalesReturnDomainException("المورد مطلوب عند اختيار نوع الطرف (مورد).");

            ReturnNumber = returnNumber.Trim();
            ReturnDate = returnDate;
            CounterpartyType = counterpartyType;
            CustomerId = counterpartyType == CounterpartyType.Customer ? customerId : null;
            SupplierId = counterpartyType == CounterpartyType.Supplier ? supplierId : null;
            WarehouseId = warehouseId;
            OriginalInvoiceId = originalInvoiceId;
            SalesRepresentativeId = salesRepresentativeId;
            Status = InvoiceStatus.Draft;
            Notes = notes?.Trim();

            Subtotal = 0;
            DiscountTotal = 0;
            VatTotal = 0;
            NetTotal = 0;
            Customer = null;
            OriginalInvoice = null;
        }

        // ── Properties ──────────────────────────────────────────

        /// <summary>Unique auto-generated return number (SR-YYYYMM-####).</summary>
        public string ReturnNumber { get; private set; }

        /// <summary>Return date.</summary>
        public DateTime ReturnDate { get; private set; }

        /// <summary>FK to Customer.</summary>
        public int? CustomerId { get; private set; }

        /// <summary>Navigation property.</summary>
        public Customer Customer { get; private set; }

        /// <summary>Counterparty type — who the return is from.</summary>
        public CounterpartyType CounterpartyType { get; private set; }

        /// <summary>FK to Supplier (when CounterpartyType = Supplier — مرتجع من مورد).</summary>
        public int? SupplierId { get; private set; }

        /// <summary>Navigation property to Supplier.</summary>
        public Supplier CounterpartySupplier { get; private set; }

        /// <summary>FK to Warehouse the goods are returned to.</summary>
        public int WarehouseId { get; private set; }

        /// <summary>FK to SalesRepresentative (optional — مندوب المبيعات).</summary>
        public int? SalesRepresentativeId { get; private set; }

        /// <summary>Navigation property to SalesRepresentative.</summary>
        public SalesRepresentative SalesRepresentative { get; private set; }

        /// <summary>Optional FK to the original sales invoice being returned against.</summary>
        public int? OriginalInvoiceId { get; private set; }

        /// <summary>Navigation to original sales invoice.</summary>
        public SalesInvoice OriginalInvoice { get; private set; }

        /// <summary>Current lifecycle status.</summary>
        public InvoiceStatus Status { get; private set; }

        /// <summary>Sum of all line subtotals (before discount, before VAT).</summary>
        public decimal Subtotal { get; private set; }

        /// <summary>Total discount across all lines.</summary>
        public decimal DiscountTotal { get; private set; }

        /// <summary>Total VAT across all lines.</summary>
        public decimal VatTotal { get; private set; }

        /// <summary>Net total = Subtotal - DiscountTotal + VatTotal.</summary>
        public decimal NetTotal { get; private set; }

        /// <summary>Optional notes.</summary>
        public string Notes { get; private set; }

        /// <summary>FK to auto-generated reversal revenue journal entry (set on posting).</summary>
        public int? JournalEntryId { get; private set; }

        /// <summary>FK to auto-generated reversal COGS journal entry (set on posting).</summary>
        public int? CogsJournalEntryId { get; private set; }

        /// <summary>Return line items.</summary>
        public IReadOnlyCollection<SalesReturnLine> Lines => _lines.AsReadOnly();

        // ── Domain Methods ──────────────────────────────────────

        /// <summary>Adds a return line. Only allowed in Draft status.</summary>
        public SalesReturnLine AddLine(
            int productId,
            int unitId,
            decimal quantity,
            decimal unitPrice,
            decimal conversionFactor,
            decimal discountPercent,
            decimal vatRate)
        {
            EnsureDraft("لا يمكن إضافة بنود لمرتجع مرحّل أو ملغى.");

            if (quantity <= 0)
                throw new SalesReturnDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitPrice < 0)
                throw new SalesReturnDomainException("سعر الوحدة لا يمكن أن يكون سالباً.");
            if (conversionFactor <= 0)
                throw new SalesReturnDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");
            if (discountPercent < 0 || discountPercent > 100)
                throw new SalesReturnDomainException("نسبة الخصم يجب أن تكون بين 0 و 100.");
            if (vatRate < 0 || vatRate > 100)
                throw new SalesReturnDomainException("نسبة الضريبة يجب أن تكون بين 0 و 100.");

            var line = new SalesReturnLine(
                productId, unitId, quantity, unitPrice,
                conversionFactor, discountPercent, vatRate);

            _lines.Add(line);
            RecalculateTotals();
            return line;
        }

        /// <summary>Removes a line item. Only allowed in Draft status.</summary>
        public void RemoveLine(SalesReturnLine line)
        {
            EnsureDraft("لا يمكن حذف بنود من مرتجع مرحّل أو ملغى.");

            if (!_lines.Remove(line))
                throw new SalesReturnDomainException("البند غير موجود في المرتجع.");

            RecalculateTotals();
        }

        /// <summary>Updates the return header. Only allowed in Draft status.</summary>
        public void UpdateHeader(
            DateTime returnDate,
            int? customerId,
            int warehouseId,
            int? originalInvoiceId,
            string notes,
            int? salesRepresentativeId = null,
            CounterpartyType counterpartyType = CounterpartyType.Customer,
            int? supplierId = null)
        {
            EnsureDraft("لا يمكن تعديل مرتجع مرحّل أو ملغى.");

            if (counterpartyType == CounterpartyType.Customer && (!customerId.HasValue || customerId <= 0))
                throw new SalesReturnDomainException("العميل مطلوب.");
            if (warehouseId <= 0)
                throw new SalesReturnDomainException("المستودع مطلوب.");
            if (counterpartyType == CounterpartyType.Supplier && (!supplierId.HasValue || supplierId <= 0))
                throw new SalesReturnDomainException("المورد مطلوب عند اختيار نوع الطرف (مورد).");

            ReturnDate = returnDate;
            CounterpartyType = counterpartyType;
            CustomerId = counterpartyType == CounterpartyType.Customer ? customerId : null;
            SupplierId = counterpartyType == CounterpartyType.Supplier ? supplierId : null;
            WarehouseId = warehouseId;
            OriginalInvoiceId = originalInvoiceId;
            SalesRepresentativeId = salesRepresentativeId;
            Notes = notes?.Trim();
        }

        /// <summary>
        /// Posts the return. Sets both revenue reversal and COGS reversal journal entry IDs.
        /// </summary>
        public void Post(int journalEntryId, int cogsJournalEntryId)
        {
            EnsureDraft("لا يمكن ترحيل مرتجع مرحّل بالفعل أو ملغى.");

            if (!_lines.Any())
                throw new SalesReturnDomainException("لا يمكن ترحيل مرتجع بدون بنود.");

            if (journalEntryId <= 0)
                throw new SalesReturnDomainException("معرف القيد المحاسبي (عكس الإيرادات) غير صالح.");

            if (cogsJournalEntryId <= 0)
                throw new SalesReturnDomainException("معرف قيد عكس تكلفة البضاعة المباعة غير صالح.");

            Status = InvoiceStatus.Posted;
            JournalEntryId = journalEntryId;
            CogsJournalEntryId = cogsJournalEntryId;
        }

        /// <summary>Cancels a posted return.</summary>
        public void Cancel()
        {
            if (Status != InvoiceStatus.Posted)
                throw new SalesReturnDomainException("لا يمكن إلغاء إلا المرتجعات المرحّلة.");

            Status = InvoiceStatus.Cancelled;
        }

        /// <summary>Replaces all lines at once.</summary>
        public void ReplaceLines(IEnumerable<SalesReturnLine> newLines)
        {
            EnsureDraft("لا يمكن تعديل بنود مرتجع مرحّل أو ملغى.");

            _lines.Clear();
            if (newLines != null)
                _lines.AddRange(newLines);

            RecalculateTotals();
        }

        // ── Soft Delete Override ────────────────────────────────

        /// <summary>
        /// Only draft returns can be soft-deleted.
        /// Posted/Cancelled returns are immutable.
        /// </summary>
        public override void SoftDelete(string deletedBy, DateTime deletedAt)
        {
            if (Status != InvoiceStatus.Draft)
                throw new SalesReturnDomainException("لا يمكن حذف مرتجع بيع مرحّل أو ملغى — استخدم الإلغاء.");

            base.SoftDelete(deletedBy, deletedAt);
        }

        // ── Private Helpers ─────────────────────────────────────

        private void EnsureDraft(string errorMessage)
        {
            if (Status != InvoiceStatus.Draft)
                throw new SalesReturnDomainException(errorMessage);
        }

        private void RecalculateTotals()
        {
            Subtotal = _lines.Sum(l => l.SubTotal);
            DiscountTotal = _lines.Sum(l => l.DiscountAmount);
            VatTotal = _lines.Sum(l => l.VatAmount);
            NetTotal = _lines.Sum(l => l.TotalWithVat);
        }
    }
}
