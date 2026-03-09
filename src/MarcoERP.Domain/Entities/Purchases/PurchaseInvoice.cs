using System;
using System.Collections.Generic;
using System.Linq;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Purchases;

namespace MarcoERP.Domain.Entities.Purchases
{
    /// <summary>
    /// Represents a purchase invoice header (فاتورة شراء).
    /// Lifecycle: Draft → Posted → (optionally) Cancelled.
    /// On posting: auto-generates journal entry, updates WAC, creates inventory movements.
    /// </summary>
    public sealed class PurchaseInvoice : CompanyAwareEntity
    {
        private readonly List<PurchaseInvoiceLine> _lines = new();

        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private PurchaseInvoice() { }

        /// <summary>
        /// Creates a new purchase invoice in Draft status.
        /// </summary>
        public PurchaseInvoice(
            string invoiceNumber,
            DateTime invoiceDate,
            int? supplierId,
            int warehouseId,
            string notes,
            int? salesRepresentativeId = null,
            CounterpartyType counterpartyType = CounterpartyType.Supplier,
            int? customerId = null,
            InvoiceType invoiceType = InvoiceType.Cash,
            PaymentMethod paymentMethod = PaymentMethod.Cash,
            DateTime? dueDate = null)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
                throw new PurchaseInvoiceDomainException("رقم فاتورة الشراء مطلوب.");

            if (counterpartyType == CounterpartyType.Supplier && (!supplierId.HasValue || supplierId <= 0))
                throw new PurchaseInvoiceDomainException("المورد مطلوب.");

            if (warehouseId <= 0)
                throw new PurchaseInvoiceDomainException("المستودع مطلوب.");

            if (counterpartyType == CounterpartyType.Customer && (!customerId.HasValue || customerId <= 0))
                throw new PurchaseInvoiceDomainException("العميل مطلوب عند اختيار نوع الطرف (عميل).");

            InvoiceNumber = invoiceNumber.Trim();
            InvoiceDate = invoiceDate;
            CounterpartyType = counterpartyType;
            SupplierId = counterpartyType == CounterpartyType.Supplier ? supplierId : null;
            WarehouseId = warehouseId;
            CounterpartyCustomerId = counterpartyType == CounterpartyType.Customer ? customerId : null;
            SalesRepresentativeId = salesRepresentativeId;
            Status = InvoiceStatus.Draft;
            InvoiceType = invoiceType;
            PaymentMethod = paymentMethod;
            DueDate = invoiceType == InvoiceType.Credit ? dueDate : null;
            Notes = notes?.Trim();

            Subtotal = 0;
            DiscountTotal = 0;
            VatTotal = 0;
            NetTotal = 0;
            PaidAmount = 0;
            HeaderDiscountPercent = 0;
            HeaderDiscountAmount = 0;
            DeliveryFee = 0;
            PaymentStatus = PaymentStatus.Unpaid;
            Supplier = null;
        }

        // ── Properties ──────────────────────────────────────────

        /// <summary>Unique auto-generated invoice number (PI-YYYYMM-####).</summary>
        public string InvoiceNumber { get; private set; }

        /// <summary>Invoice date.</summary>
        public DateTime InvoiceDate { get; private set; }

        /// <summary>FK to Supplier.</summary>
        public int? SupplierId { get; private set; }

        /// <summary>Navigation property.</summary>
        public Supplier Supplier { get; private set; }

        /// <summary>FK to Warehouse receiving the goods.</summary>
        public int WarehouseId { get; private set; }

        /// <summary>Navigation property to Warehouse (read-only for queries).</summary>
        public Warehouse Warehouse { get; private set; }

        /// <summary>FK to SalesRepresentative (optional — مندوب).</summary>
        public int? SalesRepresentativeId { get; private set; }

        /// <summary>Navigation property to SalesRepresentative.</summary>
        public Sales.SalesRepresentative SalesRepresentative { get; private set; }

        /// <summary>Counterparty type — who is the invoice from.</summary>
        public CounterpartyType CounterpartyType { get; private set; }

        /// <summary>FK to Customer (when CounterpartyType = Customer — شراء من عميل).</summary>
        public int? CounterpartyCustomerId { get; private set; }

        /// <summary>Navigation property to Customer.</summary>
        public Sales.Customer CounterpartyCustomer { get; private set; }

        /// <summary>Current lifecycle status.</summary>
        public InvoiceStatus Status { get; private set; }

        /// <summary>نوع الفاتورة (نقدي / آجل).</summary>
        public InvoiceType InvoiceType { get; private set; }

        /// <summary>طريقة الدفع.</summary>
        public PaymentMethod PaymentMethod { get; private set; }

        /// <summary>تاريخ الاستحقاق (للفواتير الآجلة).</summary>
        public DateTime? DueDate { get; private set; }

        /// <summary>Sum of all line subtotals (before discount, before VAT).</summary>
        public decimal Subtotal { get; private set; }

        /// <summary>Total discount across all lines.</summary>
        public decimal DiscountTotal { get; private set; }

        /// <summary>Total VAT across all lines.</summary>
        public decimal VatTotal { get; private set; }

        /// <summary>Net total = Subtotal - DiscountTotal + VatTotal + DeliveryFee.</summary>
        public decimal NetTotal { get; private set; }

        /// <summary>Header-level discount percentage (0–100). Applied after line discounts.</summary>
        public decimal HeaderDiscountPercent { get; private set; }

        /// <summary>Header-level fixed discount amount. Applied after line discounts.</summary>
        public decimal HeaderDiscountAmount { get; private set; }

        /// <summary>Delivery / shipping fee added to the invoice total.</summary>
        public decimal DeliveryFee { get; private set; }

        /// <summary>Total amount paid so far against this invoice.</summary>
        public decimal PaidAmount { get; private set; }

        /// <summary>Remaining balance due = NetTotal - PaidAmount.</summary>
        public decimal BalanceDue => NetTotal - PaidAmount;

        /// <summary>Payment lifecycle status.</summary>
        public PaymentStatus PaymentStatus { get; private set; }

        /// <summary>Optional notes.</summary>
        public string Notes { get; private set; }

        /// <summary>FK to auto-generated journal entry (set on posting).</summary>
        public int? JournalEntryId { get; private set; }

        /// <summary>Invoice line items.</summary>
        public IReadOnlyCollection<PurchaseInvoiceLine> Lines => _lines.AsReadOnly();

        // ── Domain Methods ──────────────────────────────────────

        /// <summary>Adds a line item to the invoice. Only allowed in Draft status.</summary>
        public PurchaseInvoiceLine AddLine(
            int productId,
            int unitId,
            decimal quantity,
            decimal unitPrice,
            decimal conversionFactor,
            decimal discountPercent,
            decimal vatRate)
        {
            EnsureDraft("لا يمكن إضافة بنود لفاتورة مرحّلة أو ملغاة.");

            if (quantity <= 0)
                throw new PurchaseInvoiceDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitPrice < 0)
                throw new PurchaseInvoiceDomainException("سعر الوحدة لا يمكن أن يكون سالباً.");
            if (conversionFactor <= 0)
                throw new PurchaseInvoiceDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");
            if (discountPercent < 0 || discountPercent > 100)
                throw new PurchaseInvoiceDomainException("نسبة الخصم يجب أن تكون بين 0 و 100.");
            if (vatRate < 0 || vatRate > 100)
                throw new PurchaseInvoiceDomainException("نسبة الضريبة يجب أن تكون بين 0 و 100.");

            var line = new PurchaseInvoiceLine(
                productId, unitId, quantity, unitPrice,
                conversionFactor, discountPercent, vatRate);

            _lines.Add(line);
            RecalculateTotals();
            return line;
        }

        /// <summary>Removes a line item. Only allowed in Draft status.</summary>
        public void RemoveLine(PurchaseInvoiceLine line)
        {
            EnsureDraft("لا يمكن حذف بنود من فاتورة مرحّلة أو ملغاة.");

            if (!_lines.Remove(line))
                throw new PurchaseInvoiceDomainException("البند غير موجود في الفاتورة.");

            RecalculateTotals();
        }

        /// <summary>Updates the invoice header. Only allowed in Draft status.</summary>
        public void UpdateHeader(
            DateTime invoiceDate,
            int? supplierId,
            int warehouseId,
            string notes,
            int? salesRepresentativeId = null,
            CounterpartyType counterpartyType = CounterpartyType.Supplier,
            int? customerId = null,
            decimal headerDiscountPercent = 0,
            decimal headerDiscountAmount = 0,
            decimal deliveryFee = 0,
            InvoiceType invoiceType = InvoiceType.Cash,
            PaymentMethod paymentMethod = PaymentMethod.Cash,
            DateTime? dueDate = null)
        {
            EnsureDraft("لا يمكن تعديل فاتورة مرحّلة أو ملغاة.");

            if (counterpartyType == CounterpartyType.Supplier && (!supplierId.HasValue || supplierId <= 0))
                throw new PurchaseInvoiceDomainException("المورد مطلوب.");
            if (warehouseId <= 0)
                throw new PurchaseInvoiceDomainException("المستودع مطلوب.");
            if (counterpartyType == CounterpartyType.Customer && (!customerId.HasValue || customerId <= 0))
                throw new PurchaseInvoiceDomainException("العميل مطلوب عند اختيار نوع الطرف (عميل).");
            if (headerDiscountPercent < 0 || headerDiscountPercent > 100)
                throw new PurchaseInvoiceDomainException("نسبة الخصم الإجمالي يجب أن تكون بين 0 و 100.");
            if (headerDiscountAmount < 0)
                throw new PurchaseInvoiceDomainException("مبلغ الخصم لا يمكن أن يكون سالباً.");
            if (deliveryFee < 0)
                throw new PurchaseInvoiceDomainException("رسوم التوصيل لا يمكن أن تكون سالبة.");

            InvoiceDate = invoiceDate;
            CounterpartyType = counterpartyType;
            SupplierId = counterpartyType == CounterpartyType.Supplier ? supplierId : null;
            WarehouseId = warehouseId;
            CounterpartyCustomerId = counterpartyType == CounterpartyType.Customer ? customerId : null;
            SalesRepresentativeId = salesRepresentativeId;
            Notes = notes?.Trim();
            HeaderDiscountPercent = headerDiscountPercent;
            HeaderDiscountAmount = headerDiscountAmount;
            DeliveryFee = deliveryFee;
            InvoiceType = invoiceType;
            PaymentMethod = paymentMethod;
            DueDate = invoiceType == InvoiceType.Credit ? dueDate : null;
            RecalculateTotals();
        }

        /// <summary>
        /// Posts the invoice. Sets JournalEntryId.
        /// The service layer handles: journal creation, WAC update, stock movement.
        /// </summary>
        public void Post(int journalEntryId)
        {
            EnsureDraft("لا يمكن ترحيل فاتورة مرحّلة بالفعل أو ملغاة.");

            if (!_lines.Any())
                throw new PurchaseInvoiceDomainException("لا يمكن ترحيل فاتورة بدون بنود.");

            if (journalEntryId <= 0)
                throw new PurchaseInvoiceDomainException("معرف القيد المحاسبي غير صالح.");

            Status = InvoiceStatus.Posted;
            JournalEntryId = journalEntryId;
        }

        /// <summary>
        /// Cancels a posted invoice. Blocks if any payment has been recorded.
        /// The service layer generates a reversal journal.
        /// </summary>
        public void Cancel()
        {
            if (Status != InvoiceStatus.Posted)
                throw new PurchaseInvoiceDomainException("لا يمكن إلغاء إلا الفواتير المرحّلة.");

            if (PaidAmount > 0)
                throw new PurchaseInvoiceDomainException("لا يمكن إلغاء فاتورة تم تسديد جزء منها أو كلها. يجب إلغاء المدفوعات أولاً.");

            Status = InvoiceStatus.Cancelled;
        }

        /// <summary>
        /// Replaces all lines at once (used during draft editing from UI).
        /// </summary>
        public void ReplaceLines(IEnumerable<PurchaseInvoiceLine> newLines)
        {
            EnsureDraft("لا يمكن تعديل بنود فاتورة مرحّلة أو ملغاة.");

            var incomingLines = (newLines ?? Enumerable.Empty<PurchaseInvoiceLine>()).ToList();
            var existingById = _lines
                .Where(l => l.Id > 0)
                .ToDictionary(l => l.Id);

            var incomingIds = new HashSet<int>();
            var newIncomingLines = new List<PurchaseInvoiceLine>();

            foreach (var incoming in incomingLines)
            {
                if (incoming.Id > 0)
                {
                    if (!incomingIds.Add(incoming.Id))
                        throw new PurchaseInvoiceDomainException("تكرار معرف بند الفاتورة غير مسموح.");

                    if (!existingById.TryGetValue(incoming.Id, out var existingLine))
                        throw new PurchaseInvoiceDomainException("لا يمكن تحديث بند غير موجود في الفاتورة.");

                    existingLine.UpdateDetails(
                        incoming.ProductId,
                        incoming.UnitId,
                        incoming.Quantity,
                        incoming.UnitPrice,
                        incoming.ConversionFactor,
                        incoming.DiscountPercent,
                        incoming.VatRate);
                }
                else
                {
                    newIncomingLines.Add(incoming);
                }
            }

            // Remove existing persisted lines not in incoming
            var linesToRemove = existingById.Values
                .Where(l => !incomingIds.Contains(l.Id))
                .ToList();

            foreach (var line in linesToRemove)
                _lines.Remove(line);

            // Remove all unsaved (Id=0) lines and replace with new incoming ones
            _lines.RemoveAll(l => l.Id == 0);
            _lines.AddRange(newIncomingLines);

            RecalculateTotals();
        }

        // ── Payment Allocation ────────────────────────────────

        /// <summary>
        /// Records a payment against this invoice (called when a cash payment is posted).
        /// </summary>
        public void ApplyPayment(decimal amount)
        {
            if (Status != InvoiceStatus.Posted)
                throw new PurchaseInvoiceDomainException("لا يمكن تسجيل دفعة على فاتورة غير مرحّلة.");

            if (amount <= 0)
                throw new PurchaseInvoiceDomainException("مبلغ الدفعة يجب أن يكون أكبر من صفر.");

            if (PaidAmount + amount > NetTotal)
                throw new PurchaseInvoiceDomainException(
                    $"مبلغ الدفعة ({amount:N2}) يتجاوز الرصيد المستحق ({BalanceDue:N2}).");

            PaidAmount += amount;
            RecalculatePaymentStatus();
        }

        /// <summary>
        /// Reverses a previously applied payment (called when a cash payment is cancelled).
        /// </summary>
        public void ReversePayment(decimal amount)
        {
            if (Status == InvoiceStatus.Cancelled)
                throw new PurchaseInvoiceDomainException("لا يمكن عكس دفعة على فاتورة ملغاة.");

            if (amount <= 0)
                throw new PurchaseInvoiceDomainException("مبلغ العكس يجب أن يكون أكبر من صفر.");

            if (amount > PaidAmount)
                throw new PurchaseInvoiceDomainException(
                    $"مبلغ العكس ({amount:N2}) يتجاوز المبلغ المدفوع ({PaidAmount:N2}).");

            PaidAmount -= amount;
            RecalculatePaymentStatus();
        }

        private void RecalculatePaymentStatus()
        {
            if (PaidAmount <= 0)
                PaymentStatus = PaymentStatus.Unpaid;
            else if (PaidAmount >= NetTotal)
                PaymentStatus = PaymentStatus.FullyPaid;
            else
                PaymentStatus = PaymentStatus.PartiallyPaid;
        }

        // ── Soft Delete Override ────────────────────────────────

        /// <summary>
        /// Only draft invoices can be soft-deleted.
        /// Posted/Cancelled invoices are immutable.
        /// </summary>
        public override void SoftDelete(string deletedBy, DateTime deletedAt)
        {
            if (Status != InvoiceStatus.Draft)
                throw new PurchaseInvoiceDomainException("لا يمكن حذف فاتورة شراء مرحّلة أو ملغاة — استخدم الإلغاء.");

            base.SoftDelete(deletedBy, deletedAt);
        }

        // ── Private Helpers ─────────────────────────────────────

        private void EnsureDraft(string errorMessage)
        {
            if (Status != InvoiceStatus.Draft)
                throw new PurchaseInvoiceDomainException(errorMessage);
        }

        private void RecalculateTotals()
        {
            Subtotal = _lines.Sum(l => l.SubTotal);
            var lineDiscountTotal = _lines.Sum(l => l.DiscountAmount);
            var lineVatTotal      = _lines.Sum(l => l.VatAmount);

            // Header discount applied on (Subtotal - lineDiscountTotal)
            var subAfterLineDiscount = Subtotal - lineDiscountTotal;
            var headerPercentValue = Math.Round(subAfterLineDiscount * HeaderDiscountPercent / 100m, 4);
            var totalHeaderDiscount = headerPercentValue + HeaderDiscountAmount;

            DiscountTotal = lineDiscountTotal + totalHeaderDiscount;

            // ZATCA fix: reduce VatTotal proportionally when header discount is applied.
            decimal vatAdjustment = 0m;
            if (subAfterLineDiscount > 0 && totalHeaderDiscount > 0 && lineVatTotal > 0)
            {
                var effectiveVatRate = lineVatTotal / subAfterLineDiscount;
                vatAdjustment = Math.Round(totalHeaderDiscount * effectiveVatRate, 4);
            }
            VatTotal = lineVatTotal - vatAdjustment;

            // NetTotal = taxable base (after all discounts) + adjusted VAT + delivery fee
            NetTotal = (subAfterLineDiscount - totalHeaderDiscount) + VatTotal + DeliveryFee;
        }
    }
}
