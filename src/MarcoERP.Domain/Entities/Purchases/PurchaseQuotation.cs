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
    /// Represents a purchase quotation header (عرض سعر شراء / طلب شراء).
    /// Lifecycle: Draft → Sent → Accepted / Rejected → Converted (to PurchaseInvoice) or Expired / Cancelled.
    /// </summary>
    public sealed class PurchaseQuotation : CompanyAwareEntity
    {
        private readonly List<PurchaseQuotationLine> _lines = new();

        /// <summary>EF Core only.</summary>
        private PurchaseQuotation() { }

        /// <summary>Creates a new purchase quotation in Draft status.</summary>
        public PurchaseQuotation(
            string quotationNumber,
            DateTime quotationDate,
            DateTime validUntil,
            int supplierId,
            int warehouseId,
            string notes)
        {
            if (string.IsNullOrWhiteSpace(quotationNumber))
                throw new PurchaseQuotationDomainException("رقم طلب الشراء مطلوب.");
            if (supplierId <= 0)
                throw new PurchaseQuotationDomainException("المورد مطلوب.");
            if (warehouseId <= 0)
                throw new PurchaseQuotationDomainException("المستودع مطلوب.");
            if (validUntil <= quotationDate)
                throw new PurchaseQuotationDomainException("تاريخ الصلاحية يجب أن يكون بعد تاريخ الطلب.");

            QuotationNumber = quotationNumber.Trim();
            QuotationDate = quotationDate;
            ValidUntil = validUntil;
            SupplierId = supplierId;
            WarehouseId = warehouseId;
            Status = QuotationStatus.Draft;
            Notes = notes?.Trim();

            Subtotal = 0;
            DiscountTotal = 0;
            VatTotal = 0;
            NetTotal = 0;
        }

        // ── Properties ──────────────────────────────────────────

        public string QuotationNumber { get; private set; }
        public DateTime QuotationDate { get; private set; }
        public DateTime ValidUntil { get; private set; }
        public int SupplierId { get; private set; }
        public Supplier Supplier { get; private set; }
        public int WarehouseId { get; private set; }
        /// <summary>Navigation property to Warehouse (read-only for queries).</summary>
        public Warehouse Warehouse { get; private set; }
        public QuotationStatus Status { get; private set; }
        public decimal Subtotal { get; private set; }
        public decimal DiscountTotal { get; private set; }
        public decimal VatTotal { get; private set; }
        public decimal NetTotal { get; private set; }
        public string Notes { get; private set; }
        public int? ConvertedToInvoiceId { get; private set; }
        public DateTime? ConvertedDate { get; private set; }
        public IReadOnlyCollection<PurchaseQuotationLine> Lines => _lines.AsReadOnly();

        // ── Domain Methods ──────────────────────────────────────

        public PurchaseQuotationLine AddLine(
            int productId, int unitId, decimal quantity, decimal unitPrice,
            decimal conversionFactor, decimal discountPercent, decimal vatRate)
        {
            EnsureDraft("لا يمكن إضافة بنود لطلب شراء غير مسودة.");

            if (quantity <= 0)
                throw new PurchaseQuotationDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitPrice < 0)
                throw new PurchaseQuotationDomainException("سعر الوحدة لا يمكن أن يكون سالباً.");
            if (conversionFactor <= 0)
                throw new PurchaseQuotationDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");
            if (discountPercent < 0 || discountPercent > 100)
                throw new PurchaseQuotationDomainException("نسبة الخصم يجب أن تكون بين 0 و 100.");
            if (vatRate < 0 || vatRate > 100)
                throw new PurchaseQuotationDomainException("نسبة الضريبة يجب أن تكون بين 0 و 100.");

            var line = new PurchaseQuotationLine(
                productId, unitId, quantity, unitPrice,
                conversionFactor, discountPercent, vatRate);

            _lines.Add(line);
            RecalculateTotals();
            return line;
        }

        public void RemoveLine(PurchaseQuotationLine line)
        {
            EnsureDraft("لا يمكن حذف بنود من طلب شراء غير مسودة.");
            if (!_lines.Remove(line))
                throw new PurchaseQuotationDomainException("البند غير موجود في طلب الشراء.");
            RecalculateTotals();
        }

        public void UpdateHeader(DateTime quotationDate, DateTime validUntil, int supplierId, int warehouseId, string notes)
        {
            EnsureDraft("لا يمكن تعديل طلب شراء غير مسودة.");
            if (supplierId <= 0)
                throw new PurchaseQuotationDomainException("المورد مطلوب.");
            if (warehouseId <= 0)
                throw new PurchaseQuotationDomainException("المستودع مطلوب.");
            if (validUntil <= quotationDate)
                throw new PurchaseQuotationDomainException("تاريخ الصلاحية يجب أن يكون بعد تاريخ الطلب.");

            QuotationDate = quotationDate;
            ValidUntil = validUntil;
            SupplierId = supplierId;
            WarehouseId = warehouseId;
            Notes = notes?.Trim();
        }

        public void Send()
        {
            EnsureDraft("لا يمكن إرسال طلب شراء غير مسودة.");
            if (!_lines.Any())
                throw new PurchaseQuotationDomainException("لا يمكن إرسال طلب شراء بدون بنود.");
            Status = QuotationStatus.Sent;
        }

        public void Accept(DateTime utcNow)
        {
            if (Status != QuotationStatus.Sent)
                throw new PurchaseQuotationDomainException("لا يمكن قبول طلب شراء غير مرسل.");
            if (IsExpired(utcNow))
                throw new PurchaseQuotationDomainException("طلب الشراء منتهي الصلاحية.");
            Status = QuotationStatus.Accepted;
        }

        public void Reject(string reason = null)
        {
            if (Status != QuotationStatus.Sent && Status != QuotationStatus.Accepted)
                throw new PurchaseQuotationDomainException("لا يمكن رفض طلب الشراء في هذه الحالة.");
            Status = QuotationStatus.Rejected;
            if (!string.IsNullOrWhiteSpace(reason))
                Notes = reason.Trim();
        }

        public void MarkAsConverted(int invoiceId, DateTime convertedDate)
        {
            if (Status != QuotationStatus.Accepted)
                throw new PurchaseQuotationDomainException("يجب قبول طلب الشراء قبل التحويل لفاتورة.");
            if (invoiceId <= 0)
                throw new PurchaseQuotationDomainException("معرف الفاتورة غير صالح.");
            Status = QuotationStatus.Converted;
            ConvertedToInvoiceId = invoiceId;
            ConvertedDate = convertedDate;
        }

        public void Cancel()
        {
            if (Status == QuotationStatus.Converted)
                throw new PurchaseQuotationDomainException("لا يمكن إلغاء طلب شراء تم تحويله لفاتورة.");
            if (Status == QuotationStatus.Cancelled)
                throw new PurchaseQuotationDomainException("طلب الشراء ملغى بالفعل.");
            Status = QuotationStatus.Cancelled;
        }

        public void ReplaceLines(IEnumerable<PurchaseQuotationLine> newLines)
        {
            EnsureDraft("لا يمكن تعديل بنود طلب شراء غير مسودة.");

            var incomingLines = (newLines ?? Enumerable.Empty<PurchaseQuotationLine>()).ToList();
            var existingById = _lines
                .Where(l => l.Id > 0)
                .ToDictionary(l => l.Id);

            var incomingIds = new HashSet<int>();
            var newIncomingLines = new List<PurchaseQuotationLine>();

            foreach (var incoming in incomingLines)
            {
                if (incoming.Id > 0)
                {
                    if (!incomingIds.Add(incoming.Id))
                        throw new PurchaseQuotationDomainException("تكرار معرف بند طلب الشراء غير مسموح.");

                    if (!existingById.TryGetValue(incoming.Id, out var existingLine))
                        throw new PurchaseQuotationDomainException("لا يمكن تحديث بند غير موجود في طلب الشراء.");

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

        public bool IsExpired(DateTime utcNow) => ValidUntil < utcNow;

        public override void SoftDelete(string deletedBy, DateTime deletedAt)
        {
            if (Status != QuotationStatus.Draft)
                throw new PurchaseQuotationDomainException("لا يمكن حذف طلب شراء غير مسودة — استخدم الإلغاء.");
            base.SoftDelete(deletedBy, deletedAt);
        }

        private void EnsureDraft(string errorMessage)
        {
            if (Status != QuotationStatus.Draft)
                throw new PurchaseQuotationDomainException(errorMessage);
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
