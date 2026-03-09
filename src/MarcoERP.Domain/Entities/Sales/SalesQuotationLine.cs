using System;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Exceptions.Sales;

namespace MarcoERP.Domain.Entities.Sales
{
    /// <summary>
    /// Represents a single line item on a sales quotation (بند عرض سعر بيع).
    /// Immutable after creation — editing is done by removing and re-adding lines.
    /// All monetary calculations happen in the constructor for data integrity.
    /// </summary>
    public sealed class SalesQuotationLine : BaseEntity
    {
        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private SalesQuotationLine() { }

        /// <summary>
        /// Creates a new sales quotation line with calculated totals.
        /// </summary>
        public SalesQuotationLine(
            int productId,
            int unitId,
            decimal quantity,
            decimal unitPrice,
            decimal conversionFactor,
            decimal discountPercent,
            decimal vatRate,
            int existingId = 0)
        {
            if (productId <= 0)
                throw new SalesQuotationDomainException("الصنف مطلوب.");
            if (unitId <= 0)
                throw new SalesQuotationDomainException("الوحدة مطلوبة.");
            if (quantity <= 0)
                throw new SalesQuotationDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitPrice < 0)
                throw new SalesQuotationDomainException("سعر الوحدة لا يمكن أن يكون سالباً.");
            if (conversionFactor <= 0)
                throw new SalesQuotationDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");

            ProductId = productId;
            UnitId = unitId;
            Quantity = quantity;
            UnitPrice = unitPrice;
            ConversionFactor = conversionFactor;
            DiscountPercent = discountPercent;
            VatRate = vatRate;
            if (existingId > 0)
                Id = existingId;

            // ── Calculated fields ───────────────────────────────
            BaseQuantity = Math.Round(quantity * conversionFactor, 4);
            SubTotal = Math.Round(quantity * unitPrice, 4);
            DiscountAmount = Math.Round(SubTotal * discountPercent / 100m, 4);
            NetTotal = SubTotal - DiscountAmount;
            VatAmount = Math.Round(NetTotal * vatRate / 100m, 4);
            TotalWithVat = NetTotal + VatAmount;
        }

        public void UpdateDetails(
            int productId,
            int unitId,
            decimal quantity,
            decimal unitPrice,
            decimal conversionFactor,
            decimal discountPercent,
            decimal vatRate)
        {
            if (productId <= 0)
                throw new SalesQuotationDomainException("الصنف مطلوب.");
            if (unitId <= 0)
                throw new SalesQuotationDomainException("الوحدة مطلوبة.");
            if (quantity <= 0)
                throw new SalesQuotationDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitPrice < 0)
                throw new SalesQuotationDomainException("سعر الوحدة لا يمكن أن يكون سالباً.");
            if (conversionFactor <= 0)
                throw new SalesQuotationDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");

            ProductId = productId;
            UnitId = unitId;
            Quantity = quantity;
            UnitPrice = unitPrice;
            ConversionFactor = conversionFactor;
            DiscountPercent = discountPercent;
            VatRate = vatRate;

            BaseQuantity = Math.Round(quantity * conversionFactor, 4);
            SubTotal = Math.Round(quantity * unitPrice, 4);
            DiscountAmount = Math.Round(SubTotal * discountPercent / 100m, 4);
            NetTotal = SubTotal - DiscountAmount;
            VatAmount = Math.Round(NetTotal * vatRate / 100m, 4);
            TotalWithVat = NetTotal + VatAmount;
        }

        // ── Properties ──────────────────────────────────────────

        /// <summary>FK to parent SalesQuotation.</summary>
        public int SalesQuotationId { get; private set; }

        /// <summary>FK to Product.</summary>
        public int ProductId { get; private set; }

        /// <summary>Navigation property to Product (read-only for queries).</summary>
        public Product Product { get; private set; }

        /// <summary>FK to Unit of measure used in this line.</summary>
        public int UnitId { get; private set; }

        /// <summary>Navigation property to Unit (read-only for queries).</summary>
        public Unit Unit { get; private set; }

        /// <summary>Quantity in the selected unit.</summary>
        public decimal Quantity { get; private set; }

        /// <summary>Price per unit.</summary>
        public decimal UnitPrice { get; private set; }

        /// <summary>Snapshot of ProductUnit.ConversionFactor at time of quotation.</summary>
        public decimal ConversionFactor { get; private set; }

        /// <summary>Quantity converted to base units = Quantity × ConversionFactor.</summary>
        public decimal BaseQuantity { get; private set; }

        /// <summary>Discount percentage (0–100).</summary>
        public decimal DiscountPercent { get; private set; }

        /// <summary>Discount amount = SubTotal × DiscountPercent / 100.</summary>
        public decimal DiscountAmount { get; private set; }

        /// <summary>Subtotal before discount = Quantity × UnitPrice.</summary>
        public decimal SubTotal { get; private set; }

        /// <summary>Net after discount = SubTotal - DiscountAmount.</summary>
        public decimal NetTotal { get; private set; }

        /// <summary>VAT rate percentage.</summary>
        public decimal VatRate { get; private set; }

        /// <summary>VAT amount = NetTotal × VatRate / 100.</summary>
        public decimal VatAmount { get; private set; }

        /// <summary>Total including VAT = NetTotal + VatAmount.</summary>
        public decimal TotalWithVat { get; private set; }
    }
}
