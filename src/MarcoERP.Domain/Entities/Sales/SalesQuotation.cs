using System;
using System.Collections.Generic;
using System.Linq;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Sales;

namespace MarcoERP.Domain.Entities.Sales
{
    /// <summary>
    /// Represents a sales quotation header (عرض سعر بيع).
    /// Lifecycle: Draft → Sent → Accepted / Rejected → Converted (to SalesInvoice) or Expired / Cancelled.
    /// </summary>
    public sealed class SalesQuotation : CompanyAwareEntity
    {
        private readonly List<SalesQuotationLine> _lines = new();

        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private SalesQuotation() { }

        /// <summary>
        /// Creates a new sales quotation in Draft status.
        /// </summary>
        public SalesQuotation(
            string quotationNumber,
            DateTime quotationDate,
            DateTime validUntil,
            int customerId,
            int warehouseId,
            string notes,
            int? salesRepresentativeId = null)
        {
            if (string.IsNullOrWhiteSpace(quotationNumber))
                throw new SalesQuotationDomainException("رقم عرض السعر مطلوب.");

            if (customerId <= 0)
                throw new SalesQuotationDomainException("العميل مطلوب.");

            if (warehouseId <= 0)
                throw new SalesQuotationDomainException("المستودع مطلوب.");

            if (validUntil <= quotationDate)
                throw new SalesQuotationDomainException("تاريخ الصلاحية يجب أن يكون بعد تاريخ العرض.");

            QuotationNumber = quotationNumber.Trim();
            QuotationDate = quotationDate;
            ValidUntil = validUntil;
            CustomerId = customerId;
            WarehouseId = warehouseId;
            SalesRepresentativeId = salesRepresentativeId;
            Status = QuotationStatus.Draft;
            Notes = notes?.Trim();

            Subtotal = 0;
            DiscountTotal = 0;
            VatTotal = 0;
            NetTotal = 0;
        }

        // ── Properties ──────────────────────────────────────────

        /// <summary>Unique auto-generated quotation number (SQ-YYYYMM-####).</summary>
        public string QuotationNumber { get; private set; }

        /// <summary>Quotation date.</summary>
        public DateTime QuotationDate { get; private set; }

        /// <summary>Quotation validity end date.</summary>
        public DateTime ValidUntil { get; private set; }

        /// <summary>FK to Customer.</summary>
        public int CustomerId { get; private set; }

        /// <summary>Navigation property.</summary>
        public Customer Customer { get; private set; }

        /// <summary>FK to Warehouse.</summary>
        public int WarehouseId { get; private set; }

        /// <summary>FK to SalesRepresentative (optional).</summary>
        public int? SalesRepresentativeId { get; private set; }

        /// <summary>Navigation property to SalesRepresentative.</summary>
        public SalesRepresentative SalesRepresentative { get; private set; }

        /// <summary>Current lifecycle status.</summary>
        public QuotationStatus Status { get; private set; }

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

        /// <summary>FK to the SalesInvoice created from this quotation (set on conversion).</summary>
        public int? ConvertedToInvoiceId { get; private set; }

        /// <summary>Date when this quotation was converted to an invoice.</summary>
        public DateTime? ConvertedDate { get; private set; }

        /// <summary>Quotation line items.</summary>
        public IReadOnlyCollection<SalesQuotationLine> Lines => _lines.AsReadOnly();

        // ── Domain Methods ──────────────────────────────────────

        /// <summary>Adds a line item to the quotation. Only allowed in Draft status.</summary>
        public SalesQuotationLine AddLine(
            int productId,
            int unitId,
            decimal quantity,
            decimal unitPrice,
            decimal conversionFactor,
            decimal discountPercent,
            decimal vatRate)
        {
            EnsureDraft("لا يمكن إضافة بنود لعرض سعر غير مسودة.");

            if (quantity <= 0)
                throw new SalesQuotationDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitPrice < 0)
                throw new SalesQuotationDomainException("سعر الوحدة لا يمكن أن يكون سالباً.");
            if (conversionFactor <= 0)
                throw new SalesQuotationDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");
            if (discountPercent < 0 || discountPercent > 100)
                throw new SalesQuotationDomainException("نسبة الخصم يجب أن تكون بين 0 و 100.");
            if (vatRate < 0 || vatRate > 100)
                throw new SalesQuotationDomainException("نسبة الضريبة يجب أن تكون بين 0 و 100.");

            var line = new SalesQuotationLine(
                productId, unitId, quantity, unitPrice,
                conversionFactor, discountPercent, vatRate);

            _lines.Add(line);
            RecalculateTotals();
            return line;
        }

        /// <summary>Removes a line item. Only allowed in Draft status.</summary>
        public void RemoveLine(SalesQuotationLine line)
        {
            EnsureDraft("لا يمكن حذف بنود من عرض سعر غير مسودة.");

            if (!_lines.Remove(line))
                throw new SalesQuotationDomainException("البند غير موجود في عرض السعر.");

            RecalculateTotals();
        }

        /// <summary>Updates the quotation header. Only allowed in Draft status.</summary>
        public void UpdateHeader(
            DateTime quotationDate,
            DateTime validUntil,
            int customerId,
            int warehouseId,
            string notes,
            int? salesRepresentativeId = null)
        {
            EnsureDraft("لا يمكن تعديل عرض سعر غير مسودة.");

            if (customerId <= 0)
                throw new SalesQuotationDomainException("العميل مطلوب.");
            if (warehouseId <= 0)
                throw new SalesQuotationDomainException("المستودع مطلوب.");
            if (validUntil <= quotationDate)
                throw new SalesQuotationDomainException("تاريخ الصلاحية يجب أن يكون بعد تاريخ العرض.");

            QuotationDate = quotationDate;
            ValidUntil = validUntil;
            CustomerId = customerId;
            WarehouseId = warehouseId;
            SalesRepresentativeId = salesRepresentativeId;
            Notes = notes?.Trim();
        }

        /// <summary>Sends the quotation to the customer.</summary>
        public void Send()
        {
            EnsureDraft("لا يمكن إرسال عرض سعر غير مسودة.");

            if (!_lines.Any())
                throw new SalesQuotationDomainException("لا يمكن إرسال عرض سعر بدون بنود.");

            Status = QuotationStatus.Sent;
        }

        /// <summary>Marks the quotation as accepted.</summary>
        public void Accept(DateTime utcNow)
        {
            if (Status != QuotationStatus.Sent)
                throw new SalesQuotationDomainException("لا يمكن قبول عرض سعر غير مرسل.");

            if (IsExpired(utcNow))
                throw new SalesQuotationDomainException("عرض السعر منتهي الصلاحية.");

            Status = QuotationStatus.Accepted;
        }

        /// <summary>Marks the quotation as rejected.</summary>
        public void Reject(string reason = null)
        {
            if (Status != QuotationStatus.Sent && Status != QuotationStatus.Accepted)
                throw new SalesQuotationDomainException("لا يمكن رفض عرض السعر في هذه الحالة.");

            Status = QuotationStatus.Rejected;
            if (!string.IsNullOrWhiteSpace(reason))
                Notes = reason.Trim();
        }

        /// <summary>Marks the quotation as converted to a sales invoice.</summary>
        public void MarkAsConverted(int invoiceId, DateTime convertedDate)
        {
            if (Status != QuotationStatus.Accepted)
                throw new SalesQuotationDomainException("يجب قبول عرض السعر قبل التحويل لفاتورة.");

            if (invoiceId <= 0)
                throw new SalesQuotationDomainException("معرف الفاتورة غير صالح.");

            Status = QuotationStatus.Converted;
            ConvertedToInvoiceId = invoiceId;
            ConvertedDate = convertedDate;
        }

        /// <summary>Cancels the quotation.</summary>
        public void Cancel()
        {
            if (Status == QuotationStatus.Converted)
                throw new SalesQuotationDomainException("لا يمكن إلغاء عرض سعر تم تحويله لفاتورة.");

            if (Status == QuotationStatus.Cancelled)
                throw new SalesQuotationDomainException("عرض السعر ملغى بالفعل.");

            Status = QuotationStatus.Cancelled;
        }

        /// <summary>Replaces all lines at once (used during draft editing from UI).</summary>
        public void ReplaceLines(IEnumerable<SalesQuotationLine> newLines)
        {
            EnsureDraft("لا يمكن تعديل بنود عرض سعر غير مسودة.");

            var incomingLines = (newLines ?? Enumerable.Empty<SalesQuotationLine>()).ToList();
            var existingById = _lines
                .Where(l => l.Id > 0)
                .ToDictionary(l => l.Id);

            var incomingIds = new HashSet<int>();
            var newIncomingLines = new List<SalesQuotationLine>();

            foreach (var incoming in incomingLines)
            {
                if (incoming.Id > 0)
                {
                    if (!incomingIds.Add(incoming.Id))
                        throw new SalesQuotationDomainException("تكرار معرف بند عرض السعر غير مسموح.");

                    if (!existingById.TryGetValue(incoming.Id, out var existingLine))
                        throw new SalesQuotationDomainException("لا يمكن تحديث بند غير موجود في عرض السعر.");

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

            var linesToRemove = existingById.Values
                .Where(l => !incomingIds.Contains(l.Id))
                .ToList();

            foreach (var line in linesToRemove)
                _lines.Remove(line);

            _lines.RemoveAll(l => l.Id == 0);
            _lines.AddRange(newIncomingLines);

            RecalculateTotals();
        }

        /// <summary>Checks if the quotation has expired.</summary>
        public bool IsExpired(DateTime utcNow) => ValidUntil < utcNow;

        /// <summary>Only draft quotations can be soft-deleted.</summary>
        public override void SoftDelete(string deletedBy, DateTime deletedAt)
        {
            if (Status != QuotationStatus.Draft)
                throw new SalesQuotationDomainException("لا يمكن حذف عرض سعر غير مسودة — استخدم الإلغاء.");

            base.SoftDelete(deletedBy, deletedAt);
        }

        // ── Private Helpers ─────────────────────────────────────

        private void EnsureDraft(string errorMessage)
        {
            if (Status != QuotationStatus.Draft)
                throw new SalesQuotationDomainException(errorMessage);
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
