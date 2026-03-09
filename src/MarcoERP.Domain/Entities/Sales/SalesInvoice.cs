using System;
using System.Collections.Generic;
using System.Linq;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Sales;

namespace MarcoERP.Domain.Entities.Sales
{
    /// <summary>
    /// Represents a sales invoice header (فاتورة بيع).
    /// Lifecycle: Draft → Posted → (optionally) Cancelled.
    /// On posting: auto revenue journal, COGS journal, stock deduction.
    /// </summary>
    public sealed class SalesInvoice : CompanyAwareEntity
    {
        private readonly List<SalesInvoiceLine> _lines = new();

        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private SalesInvoice() { }

        /// <summary>
        /// Creates a new sales invoice in Draft status.
        /// </summary>
        public SalesInvoice(
            string invoiceNumber,
            DateTime invoiceDate,
            int? customerId,
            int warehouseId,
            string notes,
            int? salesRepresentativeId = null,
            CounterpartyType counterpartyType = CounterpartyType.Customer,
            int? supplierId = null,
            InvoiceType invoiceType = InvoiceType.Cash,
            PaymentMethod paymentMethod = PaymentMethod.Cash,
            DateTime? dueDate = null)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
                throw new SalesInvoiceDomainException("رقم فاتورة البيع مطلوب.");

            if (counterpartyType == CounterpartyType.Customer && (!customerId.HasValue || customerId <= 0))
                throw new SalesInvoiceDomainException("العميل مطلوب.");

            if (warehouseId <= 0)
                throw new SalesInvoiceDomainException("المستودع مطلوب.");

            if (counterpartyType == CounterpartyType.Supplier && (!supplierId.HasValue || supplierId <= 0))
                throw new SalesInvoiceDomainException("المورد مطلوب عند اختيار نوع الطرف (مورد).");

            InvoiceNumber = invoiceNumber.Trim();
            InvoiceDate = invoiceDate;
            CustomerId = counterpartyType == CounterpartyType.Customer ? customerId : null;
            WarehouseId = warehouseId;
            SalesRepresentativeId = salesRepresentativeId;
            CounterpartyType = counterpartyType;
            SupplierId = counterpartyType == CounterpartyType.Supplier ? supplierId : null;
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
            Customer = null;
        }

        // ── Properties ──────────────────────────────────────────

        /// <summary>Unique auto-generated invoice number (SI-YYYYMM-####).</summary>
        public string InvoiceNumber { get; private set; }

        /// <summary>Invoice date.</summary>
        public DateTime InvoiceDate { get; private set; }

        /// <summary>FK to Customer (null when CounterpartyType is Supplier).</summary>
        public int? CustomerId { get; private set; }

        /// <summary>Navigation property.</summary>
        public Customer Customer { get; private set; }

        /// <summary>FK to Warehouse delivering the goods.</summary>
        public int WarehouseId { get; private set; }

        /// <summary>Navigation property to Warehouse (read-only for queries).</summary>
        public Warehouse Warehouse { get; private set; }

        /// <summary>FK to SalesRepresentative (optional — مندوب المبيعات).</summary>
        public int? SalesRepresentativeId { get; private set; }

        /// <summary>Navigation property to SalesRepresentative.</summary>
        public SalesRepresentative SalesRepresentative { get; private set; }

        /// <summary>Counterparty type — who is the invoice addressed to.</summary>
        public CounterpartyType CounterpartyType { get; private set; }

        /// <summary>FK to Supplier (when CounterpartyType = Supplier — بيع لمورد).</summary>
        public int? SupplierId { get; private set; }

        /// <summary>Navigation property to Supplier.</summary>
        public Purchases.Supplier CounterpartySupplier { get; private set; }

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

        /// <summary>FK to auto-generated revenue journal entry (set on posting).</summary>
        public int? JournalEntryId { get; private set; }

        /// <summary>FK to auto-generated COGS journal entry (set on posting).</summary>
        public int? CogsJournalEntryId { get; private set; }

        /// <summary>FK to auto-generated commission journal entry (set on posting when sales rep has commission).</summary>
        public int? CommissionJournalEntryId { get; private set; }

        /// <summary>Invoice line items.</summary>
        public IReadOnlyCollection<SalesInvoiceLine> Lines => _lines.AsReadOnly();

        // ── Domain Methods ──────────────────────────────────────

        /// <summary>Adds a line item to the invoice. Only allowed in Draft status.</summary>
        public SalesInvoiceLine AddLine(
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
                throw new SalesInvoiceDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitPrice < 0)
                throw new SalesInvoiceDomainException("سعر الوحدة لا يمكن أن يكون سالباً.");
            if (conversionFactor <= 0)
                throw new SalesInvoiceDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");
            if (discountPercent < 0 || discountPercent > 100)
                throw new SalesInvoiceDomainException("نسبة الخصم يجب أن تكون بين 0 و 100.");
            if (vatRate < 0 || vatRate > 100)
                throw new SalesInvoiceDomainException("نسبة الضريبة يجب أن تكون بين 0 و 100.");

            var line = new SalesInvoiceLine(
                productId, unitId, quantity, unitPrice,
                conversionFactor, discountPercent, vatRate);

            _lines.Add(line);
            RecalculateTotals();
            return line;
        }

        /// <summary>Removes a line item. Only allowed in Draft status.</summary>
        public void RemoveLine(SalesInvoiceLine line)
        {
            EnsureDraft("لا يمكن حذف بنود من فاتورة مرحّلة أو ملغاة.");

            if (!_lines.Remove(line))
                throw new SalesInvoiceDomainException("البند غير موجود في الفاتورة.");

            RecalculateTotals();
        }

        /// <summary>Updates the invoice header. Only allowed in Draft status.</summary>
        public void UpdateHeader(
            DateTime invoiceDate,
            int? customerId,
            int warehouseId,
            string notes,
            int? salesRepresentativeId = null,
            CounterpartyType counterpartyType = CounterpartyType.Customer,
            int? supplierId = null,
            decimal headerDiscountPercent = 0,
            decimal headerDiscountAmount = 0,
            decimal deliveryFee = 0,
            InvoiceType invoiceType = InvoiceType.Cash,
            PaymentMethod paymentMethod = PaymentMethod.Cash,
            DateTime? dueDate = null)
        {
            EnsureDraft("لا يمكن تعديل فاتورة مرحّلة أو ملغاة.");

            if (counterpartyType == CounterpartyType.Customer && (!customerId.HasValue || customerId <= 0))
                throw new SalesInvoiceDomainException("العميل مطلوب.");
            if (warehouseId <= 0)
                throw new SalesInvoiceDomainException("المستودع مطلوب.");
            if (counterpartyType == CounterpartyType.Supplier && (!supplierId.HasValue || supplierId <= 0))
                throw new SalesInvoiceDomainException("المورد مطلوب عند اختيار نوع الطرف (مورد).");
            if (headerDiscountPercent < 0 || headerDiscountPercent > 100)
                throw new SalesInvoiceDomainException("نسبة الخصم الإجمالي يجب أن تكون بين 0 و 100.");
            if (headerDiscountAmount < 0)
                throw new SalesInvoiceDomainException("مبلغ الخصم لا يمكن أن يكون سالباً.");
            if (deliveryFee < 0)
                throw new SalesInvoiceDomainException("رسوم التوصيل لا يمكن أن تكون سالبة.");

            InvoiceDate = invoiceDate;
            CustomerId = counterpartyType == CounterpartyType.Customer ? customerId : null;
            WarehouseId = warehouseId;
            SalesRepresentativeId = salesRepresentativeId;
            CounterpartyType = counterpartyType;
            SupplierId = counterpartyType == CounterpartyType.Supplier ? supplierId : null;
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
        /// Posts the invoice. Sets revenue journal entry ID and optional COGS journal entry ID.
        /// COGS journal is null when all products have zero weighted-average cost.
        /// Commission journal is null when no sales representative or zero commission rate.
        /// The service layer handles: journal creation, COGS calculation, stock deduction.
        /// </summary>
        public void Post(int journalEntryId, int? cogsJournalEntryId, int? commissionJournalEntryId = null)
        {
            EnsureDraft("لا يمكن ترحيل فاتورة مرحّلة بالفعل أو ملغاة.");

            if (!_lines.Any())
                throw new SalesInvoiceDomainException("لا يمكن ترحيل فاتورة بدون بنود.");

            if (journalEntryId <= 0)
                throw new SalesInvoiceDomainException("معرف القيد المحاسبي (الإيرادات) غير صالح.");

            if (cogsJournalEntryId.HasValue && cogsJournalEntryId.Value <= 0)
                throw new SalesInvoiceDomainException("معرف قيد تكلفة البضاعة المباعة غير صالح.");

            if (commissionJournalEntryId.HasValue && commissionJournalEntryId.Value <= 0)
                throw new SalesInvoiceDomainException("معرف قيد العمولة غير صالح.");

            Status = InvoiceStatus.Posted;
            JournalEntryId = journalEntryId;
            CogsJournalEntryId = cogsJournalEntryId;
            CommissionJournalEntryId = commissionJournalEntryId;
        }

        /// <summary>Cancels a posted invoice. Blocks if any payment has been recorded.</summary>
        public void Cancel()
        {
            if (Status != InvoiceStatus.Posted)
                throw new SalesInvoiceDomainException("لا يمكن إلغاء إلا الفواتير المرحّلة.");

            if (PaidAmount > 0)
                throw new SalesInvoiceDomainException("لا يمكن إلغاء فاتورة تم تسديد جزء منها أو كلها. يجب إلغاء المدفوعات أولاً.");

            Status = InvoiceStatus.Cancelled;
        }

        /// <summary>Replaces all lines at once (used during draft editing from UI).</summary>
        public void ReplaceLines(IEnumerable<SalesInvoiceLine> newLines)
        {
            EnsureDraft("لا يمكن تعديل بنود فاتورة مرحّلة أو ملغاة.");

            var incomingLines = (newLines ?? Enumerable.Empty<SalesInvoiceLine>()).ToList();
            var existingById = _lines
                .Where(l => l.Id > 0)
                .ToDictionary(l => l.Id);

            var incomingIds = new HashSet<int>();
            var newIncomingLines = new List<SalesInvoiceLine>();

            foreach (var incoming in incomingLines)
            {
                if (incoming.Id > 0)
                {
                    if (!incomingIds.Add(incoming.Id))
                        throw new SalesInvoiceDomainException("تكرار معرف بند الفاتورة غير مسموح.");

                    if (!existingById.TryGetValue(incoming.Id, out var existingLine))
                        throw new SalesInvoiceDomainException("لا يمكن تحديث بند غير موجود في الفاتورة.");

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
        /// Records a payment against this invoice (called when a cash receipt is posted).
        /// </summary>
        public void ApplyPayment(decimal amount)
        {
            if (Status != InvoiceStatus.Posted)
                throw new SalesInvoiceDomainException("لا يمكن تسجيل دفعة على فاتورة غير مرحّلة.");

            if (amount <= 0)
                throw new SalesInvoiceDomainException("مبلغ الدفعة يجب أن يكون أكبر من صفر.");

            if (PaidAmount + amount > NetTotal)
                throw new SalesInvoiceDomainException(
                    $"مبلغ الدفعة ({amount:N2}) يتجاوز الرصيد المستحق ({BalanceDue:N2}).");

            PaidAmount += amount;
            RecalculatePaymentStatus();
        }

        /// <summary>
        /// Reverses a previously applied payment (called when a cash receipt is cancelled).
        /// </summary>
        public void ReversePayment(decimal amount)
        {
            if (Status == InvoiceStatus.Cancelled)
                throw new SalesInvoiceDomainException("لا يمكن عكس دفعة على فاتورة ملغاة.");

            if (amount <= 0)
                throw new SalesInvoiceDomainException("مبلغ العكس يجب أن يكون أكبر من صفر.");

            if (amount > PaidAmount)
                throw new SalesInvoiceDomainException(
                    $"مبلغ العكس ({amount:N2}) يتجاوز المبلغ المدفوع ({PaidAmount:N2}).");

            PaidAmount -= amount;
            RecalculatePaymentStatus();
        }

        /// <summary>
        /// Applies a sales return credit against this invoice (reduces the net total effectively).
        /// Called when a sales return is posted against this invoice.
        /// </summary>
        public void ApplyReturnCredit(decimal amount)
        {
            if (Status != InvoiceStatus.Posted)
                throw new SalesInvoiceDomainException("لا يمكن تطبيق مرتجع على فاتورة غير مرحّلة.");

            if (amount <= 0)
                throw new SalesInvoiceDomainException("مبلغ المرتجع يجب أن يكون أكبر من صفر.");

            if (amount > BalanceDue)
                throw new SalesInvoiceDomainException(
                    $"مبلغ المرتجع ({amount:N2}) يتجاوز الرصيد المستحق ({BalanceDue:N2}).");

            PaidAmount += amount;
            RecalculatePaymentStatus();
        }

        /// <summary>
        /// Reverses a previously applied return credit (called when a sales return is cancelled).
        /// </summary>
        public void ReverseReturnCredit(decimal amount)
        {
            if (Status == InvoiceStatus.Cancelled)
                throw new SalesInvoiceDomainException("لا يمكن عكس مرتجع على فاتورة ملغاة.");

            if (amount <= 0)
                throw new SalesInvoiceDomainException("مبلغ عكس المرتجع يجب أن يكون أكبر من صفر.");

            if (amount > PaidAmount)
                throw new SalesInvoiceDomainException(
                    $"مبلغ عكس المرتجع ({amount:N2}) يتجاوز المبلغ المدفوع ({PaidAmount:N2}).");

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
                throw new SalesInvoiceDomainException("لا يمكن حذف فاتورة بيع مرحّلة أو ملغاة — استخدم الإلغاء.");

            base.SoftDelete(deletedBy, deletedAt);
        }

        // ── Private Helpers ─────────────────────────────────────

        private void EnsureDraft(string errorMessage)
        {
            if (Status != InvoiceStatus.Draft)
                throw new SalesInvoiceDomainException(errorMessage);
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
            // The header discount lowers the taxable base, so VAT must be recalculated.
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
