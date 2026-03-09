using System;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Exceptions.Purchases;

namespace MarcoERP.Domain.Entities.Purchases
{
    /// <summary>
    /// Represents a single line item on a purchase return (بند مرتجع شراء).
    /// Shares the same calculation logic as PurchaseInvoiceLine.
    /// Immutable financial record — cannot be deleted (RECORD_PROTECTION_POLICY).
    /// </summary>
    public sealed class PurchaseReturnLine : BaseEntity, IImmutableFinancialRecord
    {
        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private PurchaseReturnLine() { }

        /// <summary>
        /// Creates a new purchase return line with calculated totals.
        /// </summary>
        public PurchaseReturnLine(
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
                throw new PurchaseReturnDomainException("الصنف مطلوب.");
            if (unitId <= 0)
                throw new PurchaseReturnDomainException("الوحدة مطلوبة.");
            if (quantity <= 0)
                throw new PurchaseReturnDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitPrice < 0)
                throw new PurchaseReturnDomainException("سعر الوحدة لا يمكن أن يكون سالباً.");
            if (conversionFactor <= 0)
                throw new PurchaseReturnDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");

            ProductId = productId;
            UnitId = unitId;
            Quantity = quantity;
            UnitPrice = unitPrice;
            ConversionFactor = conversionFactor;
            DiscountPercent = discountPercent;
            VatRate = vatRate;
            PurchaseReturnId = 0;
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
                throw new PurchaseReturnDomainException("الصنف مطلوب.");
            if (unitId <= 0)
                throw new PurchaseReturnDomainException("الوحدة مطلوبة.");
            if (quantity <= 0)
                throw new PurchaseReturnDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitPrice < 0)
                throw new PurchaseReturnDomainException("سعر الوحدة لا يمكن أن يكون سالباً.");
            if (conversionFactor <= 0)
                throw new PurchaseReturnDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");

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

        /// <summary>FK to parent PurchaseReturn.</summary>
        public int PurchaseReturnId { get; private set; }

        /// <summary>FK to Product.</summary>
        public int ProductId { get; private set; }

        /// <summary>Navigation property to Product (read-only for queries).</summary>
        public Product Product { get; private set; }

        /// <summary>FK to Unit of measure.</summary>
        public int UnitId { get; private set; }

        /// <summary>Navigation property to Unit (read-only for queries).</summary>
        public Unit Unit { get; private set; }

        /// <summary>Returned quantity in the selected unit.</summary>
        public decimal Quantity { get; private set; }

        /// <summary>Price per unit (usually same as original purchase price).</summary>
        public decimal UnitPrice { get; private set; }

        /// <summary>Snapshot of conversion factor at time of return.</summary>
        public decimal ConversionFactor { get; private set; }

        /// <summary>Quantity in base units = Quantity × ConversionFactor.</summary>
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
