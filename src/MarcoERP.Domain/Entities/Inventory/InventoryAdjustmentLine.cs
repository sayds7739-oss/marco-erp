using System;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Exceptions.Inventory;

namespace MarcoERP.Domain.Entities.Inventory
{
    /// <summary>
    /// Represents a single line item on an inventory adjustment (بند تسوية مخزنية).
    /// Compares system quantity vs actual (physical count) quantity.
    /// Difference = Actual - System (positive = surplus, negative = shortage).
    /// Immutable financial record — cannot be deleted (RECORD_PROTECTION_POLICY).
    /// </summary>
    public sealed class InventoryAdjustmentLine : BaseEntity, IImmutableFinancialRecord
    {
        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private InventoryAdjustmentLine() { }

        /// <summary>
        /// Creates a new adjustment line with calculated difference.
        /// </summary>
        public InventoryAdjustmentLine(
            int productId,
            int unitId,
            decimal systemQuantity,
            decimal actualQuantity,
            decimal conversionFactor,
            decimal unitCost,
            int existingId = 0)
        {
            if (productId <= 0)
                throw new InventoryDomainException("الصنف مطلوب.");
            if (unitId <= 0)
                throw new InventoryDomainException("الوحدة مطلوبة.");
            if (conversionFactor <= 0)
                throw new InventoryDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");

            ProductId = productId;
            UnitId = unitId;
            SystemQuantity = systemQuantity;
            ActualQuantity = actualQuantity;
            ConversionFactor = conversionFactor;
            UnitCost = unitCost;
            if (existingId > 0)
                Id = existingId;

            // ── Calculated fields ───────────────────────────────
            DifferenceQuantity = actualQuantity - systemQuantity;
            DifferenceInBaseUnit = Math.Round(DifferenceQuantity * conversionFactor, 4);
            CostDifference = Math.Round(DifferenceInBaseUnit * unitCost, 4);
        }

        public void UpdateDetails(
            int productId,
            int unitId,
            decimal systemQuantity,
            decimal actualQuantity,
            decimal conversionFactor,
            decimal unitCost)
        {
            if (productId <= 0)
                throw new InventoryDomainException("الصنف مطلوب.");
            if (unitId <= 0)
                throw new InventoryDomainException("الوحدة مطلوبة.");
            if (conversionFactor <= 0)
                throw new InventoryDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");

            ProductId = productId;
            UnitId = unitId;
            SystemQuantity = systemQuantity;
            ActualQuantity = actualQuantity;
            ConversionFactor = conversionFactor;
            UnitCost = unitCost;

            DifferenceQuantity = actualQuantity - systemQuantity;
            DifferenceInBaseUnit = Math.Round(DifferenceQuantity * conversionFactor, 4);
            CostDifference = Math.Round(DifferenceInBaseUnit * unitCost, 4);
        }

        // ── Properties ──────────────────────────────────────────

        /// <summary>FK to parent InventoryAdjustment.</summary>
        public int InventoryAdjustmentId { get; private set; }

        /// <summary>FK to Product.</summary>
        public int ProductId { get; private set; }

        /// <summary>FK to Unit of measure.</summary>
        public int UnitId { get; private set; }

        /// <summary>System quantity (current stock per warehouse records).</summary>
        public decimal SystemQuantity { get; private set; }

        /// <summary>Actual quantity from physical count.</summary>
        public decimal ActualQuantity { get; private set; }

        /// <summary>Difference = Actual - System. Positive = surplus, Negative = shortage.</summary>
        public decimal DifferenceQuantity { get; private set; }

        /// <summary>Conversion factor to base unit.</summary>
        public decimal ConversionFactor { get; private set; }

        /// <summary>Difference converted to base units.</summary>
        public decimal DifferenceInBaseUnit { get; private set; }

        /// <summary>Cost per base unit (WAC at time of adjustment).</summary>
        public decimal UnitCost { get; private set; }

        /// <summary>Cost difference = DifferenceInBaseUnit × UnitCost.</summary>
        public decimal CostDifference { get; private set; }

        // ── Navigation Properties ───────────────────────────────

        /// <summary>Related product.</summary>
        public Product Product { get; private set; }

        /// <summary>Unit used.</summary>
        public Unit Unit { get; private set; }
    }
}
