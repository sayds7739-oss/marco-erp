using System;
using System.Collections.Generic;
using System.Linq;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Purchases;

namespace MarcoERP.Domain.Entities.Purchases
{
    /// <summary>
    /// Represents a purchase return header (مرتجع شراء).
    /// Lifecycle: Draft → Posted → (optionally) Cancelled.
    /// On posting: reversal journal entry, stock deduction, WAC recalculation.
    /// </summary>
    public sealed class PurchaseReturn : CompanyAwareEntity
    {
        private readonly List<PurchaseReturnLine> _lines = new();

        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private PurchaseReturn() { }

        /// <summary>
        /// Creates a new purchase return in Draft status.
        /// </summary>
        public PurchaseReturn(
            string returnNumber,
            DateTime returnDate,
            int? supplierId,
            int warehouseId,
            int? originalInvoiceId,
            string notes,
            int? salesRepresentativeId = null,
            CounterpartyType counterpartyType = CounterpartyType.Supplier,
            int? customerId = null)
        {
            if (string.IsNullOrWhiteSpace(returnNumber))
                throw new PurchaseReturnDomainException("رقم مرتجع الشراء مطلوب.");

            if (counterpartyType == CounterpartyType.Supplier && (!supplierId.HasValue || supplierId <= 0))
                throw new PurchaseReturnDomainException("المورد مطلوب.");

            if (warehouseId <= 0)
                throw new PurchaseReturnDomainException("المستودع مطلوب.");

            if (counterpartyType == CounterpartyType.Customer && (!customerId.HasValue || customerId <= 0))
                throw new PurchaseReturnDomainException("العميل مطلوب عند اختيار نوع الطرف (عميل).");

            ReturnNumber = returnNumber.Trim();
            ReturnDate = returnDate;
            CounterpartyType = counterpartyType;
            SupplierId = counterpartyType == CounterpartyType.Supplier ? supplierId : null;
            CounterpartyCustomerId = counterpartyType == CounterpartyType.Customer ? customerId : null;
            WarehouseId = warehouseId;
            OriginalInvoiceId = originalInvoiceId;
            SalesRepresentativeId = salesRepresentativeId;
            Status = InvoiceStatus.Draft;
            Notes = notes?.Trim();

            Subtotal = 0;
            DiscountTotal = 0;
            VatTotal = 0;
            NetTotal = 0;
            Supplier = null;
            OriginalInvoice = null;
        }

        // ── Properties ──────────────────────────────────────────

        /// <summary>Unique auto-generated return number (PR-YYYYMM-####).</summary>
        public string ReturnNumber { get; private set; }

        /// <summary>Return date.</summary>
        public DateTime ReturnDate { get; private set; }

        /// <summary>FK to Supplier.</summary>
        public int? SupplierId { get; private set; }

        /// <summary>Navigation property.</summary>
        public Supplier Supplier { get; private set; }

        /// <summary>Counterparty type — who the return is to.</summary>
        public CounterpartyType CounterpartyType { get; private set; }

        /// <summary>FK to Customer (when CounterpartyType = Customer — مرتجع لمشتريات من عميل).</summary>
        public int? CounterpartyCustomerId { get; private set; }

        /// <summary>Navigation property to Customer.</summary>
        public Sales.Customer CounterpartyCustomer { get; private set; }

        /// <summary>FK to Warehouse the goods are returned from.</summary>
        public int WarehouseId { get; private set; }

        /// <summary>FK to SalesRepresentative (optional — مندوب).</summary>
        public int? SalesRepresentativeId { get; private set; }

        /// <summary>Navigation property to SalesRepresentative.</summary>
        public Sales.SalesRepresentative SalesRepresentative { get; private set; }

        /// <summary>Optional FK to the original purchase invoice being returned against.</summary>
        public int? OriginalInvoiceId { get; private set; }

        /// <summary>Navigation to original purchase invoice.</summary>
        public PurchaseInvoice OriginalInvoice { get; private set; }

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

        /// <summary>FK to auto-generated reversal journal entry (set on posting).</summary>
        public int? JournalEntryId { get; private set; }

        /// <summary>Return line items.</summary>
        public IReadOnlyCollection<PurchaseReturnLine> Lines => _lines.AsReadOnly();

        // ── Domain Methods ──────────────────────────────────────

        /// <summary>Adds a return line. Only allowed in Draft status.</summary>
        public PurchaseReturnLine AddLine(
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
                throw new PurchaseReturnDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitPrice < 0)
                throw new PurchaseReturnDomainException("سعر الوحدة لا يمكن أن يكون سالباً.");
            if (conversionFactor <= 0)
                throw new PurchaseReturnDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");
            if (discountPercent < 0 || discountPercent > 100)
                throw new PurchaseReturnDomainException("نسبة الخصم يجب أن تكون بين 0 و 100.");
            if (vatRate < 0 || vatRate > 100)
                throw new PurchaseReturnDomainException("نسبة الضريبة يجب أن تكون بين 0 و 100.");

            var line = new PurchaseReturnLine(
                productId, unitId, quantity, unitPrice,
                conversionFactor, discountPercent, vatRate);

            _lines.Add(line);
            RecalculateTotals();
            return line;
        }

        /// <summary>Removes a line item. Only allowed in Draft status.</summary>
        public void RemoveLine(PurchaseReturnLine line)
        {
            EnsureDraft("لا يمكن حذف بنود من مرتجع مرحّل أو ملغى.");

            if (!_lines.Remove(line))
                throw new PurchaseReturnDomainException("البند غير موجود في المرتجع.");

            RecalculateTotals();
        }

        /// <summary>Updates the return header. Only allowed in Draft status.</summary>
        public void UpdateHeader(
            DateTime returnDate,
            int? supplierId,
            int warehouseId,
            int? originalInvoiceId,
            string notes,
            int? salesRepresentativeId = null,
            CounterpartyType counterpartyType = CounterpartyType.Supplier,
            int? customerId = null)
        {
            EnsureDraft("لا يمكن تعديل مرتجع مرحّل أو ملغى.");

            if (counterpartyType == CounterpartyType.Supplier && (!supplierId.HasValue || supplierId <= 0))
                throw new PurchaseReturnDomainException("المورد مطلوب.");
            if (warehouseId <= 0)
                throw new PurchaseReturnDomainException("المستودع مطلوب.");
            if (counterpartyType == CounterpartyType.Customer && (!customerId.HasValue || customerId <= 0))
                throw new PurchaseReturnDomainException("العميل مطلوب عند اختيار نوع الطرف (عميل).");

            ReturnDate = returnDate;
            CounterpartyType = counterpartyType;
            SupplierId = counterpartyType == CounterpartyType.Supplier ? supplierId : null;
            CounterpartyCustomerId = counterpartyType == CounterpartyType.Customer ? customerId : null;
            WarehouseId = warehouseId;
            OriginalInvoiceId = originalInvoiceId;
            SalesRepresentativeId = salesRepresentativeId;
            Notes = notes?.Trim();
        }

        /// <summary>Posts the return. Sets JournalEntryId.</summary>
        public void Post(int journalEntryId)
        {
            EnsureDraft("لا يمكن ترحيل مرتجع مرحّل بالفعل أو ملغى.");

            if (!_lines.Any())
                throw new PurchaseReturnDomainException("لا يمكن ترحيل مرتجع بدون بنود.");

            if (journalEntryId <= 0)
                throw new PurchaseReturnDomainException("معرف القيد المحاسبي غير صالح.");

            Status = InvoiceStatus.Posted;
            JournalEntryId = journalEntryId;
        }

        /// <summary>Cancels a posted return.</summary>
        public void Cancel()
        {
            if (Status != InvoiceStatus.Posted)
                throw new PurchaseReturnDomainException("لا يمكن إلغاء إلا المرتجعات المرحّلة.");

            Status = InvoiceStatus.Cancelled;
        }

        /// <summary>Replaces all lines at once.</summary>
        public void ReplaceLines(IEnumerable<PurchaseReturnLine> newLines)
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
                throw new PurchaseReturnDomainException("لا يمكن حذف مرتجع شراء مرحّل أو ملغى — استخدم الإلغاء.");

            base.SoftDelete(deletedBy, deletedAt);
        }

        // ── Private Helpers ─────────────────────────────────────

        private void EnsureDraft(string errorMessage)
        {
            if (Status != InvoiceStatus.Draft)
                throw new PurchaseReturnDomainException(errorMessage);
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
