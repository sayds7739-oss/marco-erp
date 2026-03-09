using System;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Exceptions.Purchases;

namespace MarcoERP.Domain.Entities.Purchases
{
    /// <summary>
    /// Represents a single line item on a purchase quotation (بند طلب شراء).
    /// Immutable after creation — editing is done by removing and re-adding lines.
    /// </summary>
    public sealed class PurchaseQuotationLine : BaseEntity
    {
        /// <summary>EF Core only.</summary>
        private PurchaseQuotationLine() { }

        public PurchaseQuotationLine(
            int productId, int unitId, decimal quantity, decimal unitPrice,
            decimal conversionFactor, decimal discountPercent, decimal vatRate,
            int existingId = 0)
        {
            if (productId <= 0)
                throw new PurchaseQuotationDomainException("الصنف مطلوب.");
            if (unitId <= 0)
                throw new PurchaseQuotationDomainException("الوحدة مطلوبة.");
            if (quantity <= 0)
                throw new PurchaseQuotationDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitPrice < 0)
                throw new PurchaseQuotationDomainException("سعر الوحدة لا يمكن أن يكون سالباً.");
            if (conversionFactor <= 0)
                throw new PurchaseQuotationDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");

            ProductId = productId;
            UnitId = unitId;
            Quantity = quantity;
            UnitPrice = unitPrice;
            ConversionFactor = conversionFactor;
            DiscountPercent = discountPercent;
            VatRate = vatRate;
            if (existingId > 0)
                Id = existingId;

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
                throw new PurchaseQuotationDomainException("الصنف مطلوب.");
            if (unitId <= 0)
                throw new PurchaseQuotationDomainException("الوحدة مطلوبة.");
            if (quantity <= 0)
                throw new PurchaseQuotationDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitPrice < 0)
                throw new PurchaseQuotationDomainException("سعر الوحدة لا يمكن أن يكون سالباً.");
            if (conversionFactor <= 0)
                throw new PurchaseQuotationDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");

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

        public int PurchaseQuotationId { get; private set; }
        public int ProductId { get; private set; }
        /// <summary>Navigation property to Product (read-only for queries).</summary>
        public Product Product { get; private set; }
        public int UnitId { get; private set; }
        /// <summary>Navigation property to Unit (read-only for queries).</summary>
        public Unit Unit { get; private set; }
        public decimal Quantity { get; private set; }
        public decimal UnitPrice { get; private set; }
        public decimal ConversionFactor { get; private set; }
        public decimal BaseQuantity { get; private set; }
        public decimal DiscountPercent { get; private set; }
        public decimal DiscountAmount { get; private set; }
        public decimal SubTotal { get; private set; }
        public decimal NetTotal { get; private set; }
        public decimal VatRate { get; private set; }
        public decimal VatAmount { get; private set; }
        public decimal TotalWithVat { get; private set; }
    }
}
