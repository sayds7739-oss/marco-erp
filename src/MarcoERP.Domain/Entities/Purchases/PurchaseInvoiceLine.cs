using System;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Exceptions.Purchases;

namespace MarcoERP.Domain.Entities.Purchases
{
    /// <summary>
    /// Represents a single line item on a purchase invoice (بند فاتورة شراء).
    /// Immutable after creation — editing is done by removing and re-adding lines.
    /// All monetary calculations happen in the constructor for data integrity.
    /// Immutable financial record — cannot be deleted (RECORD_PROTECTION_POLICY).
    /// </summary>
    public sealed class PurchaseInvoiceLine : BaseEntity, IImmutableFinancialRecord
    {
        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private PurchaseInvoiceLine() { }

        /// <summary>
        /// Creates a new purchase invoice line with calculated totals.
        /// </summary>
        public PurchaseInvoiceLine(
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
                throw new PurchaseInvoiceDomainException("الصنف مطلوب.");
            if (unitId <= 0)
                throw new PurchaseInvoiceDomainException("الوحدة مطلوبة.");
            if (quantity <= 0)
                throw new PurchaseInvoiceDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitPrice < 0)
                throw new PurchaseInvoiceDomainException("سعر الوحدة لا يمكن أن يكون سالباً.");
            if (conversionFactor <= 0)
                throw new PurchaseInvoiceDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");

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
                throw new PurchaseInvoiceDomainException("الصنف مطلوب.");
            if (unitId <= 0)
                throw new PurchaseInvoiceDomainException("الوحدة مطلوبة.");
            if (quantity <= 0)
                throw new PurchaseInvoiceDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitPrice < 0)
                throw new PurchaseInvoiceDomainException("سعر الوحدة لا يمكن أن يكون سالباً.");
            if (conversionFactor <= 0)
                throw new PurchaseInvoiceDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");

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

        /// <summary>FK to parent PurchaseInvoice.</summary>
        public int PurchaseInvoiceId { get; private set; }

        /// <summary>FK to Product.</summary>
        public int ProductId { get; private set; }

        /// <summary>Navigation property to Product (read-only for queries).</summary>
        public Product Product { get; private set; }

        /// <summary>FK to Unit of measure used in this line.</summary>
        public int UnitId { get; private set; }

        /// <summary>Navigation property to Unit (read-only for queries).</summary>
        public Unit Unit { get; private set; }

        /// <summary>Purchased quantity in the selected unit.</summary>
        public decimal Quantity { get; private set; }

        /// <summary>Price per unit as purchased.</summary>
        public decimal UnitPrice { get; private set; }

        /// <summary>
        /// Snapshot of ProductUnit.ConversionFactor at time of invoice.
        /// Converts from the selected unit to the base (smallest) unit.
        /// </summary>
        public decimal ConversionFactor { get; private set; }

        /// <summary>
        /// Quantity converted to base units = Quantity × ConversionFactor.
        /// Used for WAC calculation and stock movement.
        /// </summary>
        public decimal BaseQuantity { get; private set; }

        /// <summary>Discount percentage (0–100).</summary>
        public decimal DiscountPercent { get; private set; }

        /// <summary>Discount amount = SubTotal × DiscountPercent / 100.</summary>
        public decimal DiscountAmount { get; private set; }

        /// <summary>Subtotal before discount = Quantity × UnitPrice.</summary>
        public decimal SubTotal { get; private set; }

        /// <summary>Net after discount = SubTotal - DiscountAmount.</summary>
        public decimal NetTotal { get; private set; }

        /// <summary>VAT rate percentage (snapshot from Product.VatRate).</summary>
        public decimal VatRate { get; private set; }

        /// <summary>VAT amount = NetTotal × VatRate / 100.</summary>
        public decimal VatAmount { get; private set; }

        /// <summary>Total including VAT = NetTotal + VatAmount.</summary>
        public decimal TotalWithVat { get; private set; }
    }
}
