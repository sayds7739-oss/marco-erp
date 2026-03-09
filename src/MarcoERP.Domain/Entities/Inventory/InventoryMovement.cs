using System;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Inventory;

namespace MarcoERP.Domain.Entities.Inventory
{
    /// <summary>
    /// Records every individual stock movement for auditing and stock card.
    /// Each movement is immutable once created — no edits, no deletes.
    /// Reversals are done by creating opposite movements.
    /// Immutable financial record — cannot be deleted (RECORD_PROTECTION_POLICY).
    /// </summary>
    public sealed class InventoryMovement : AuditableEntity, IImmutableFinancialRecord
    {
        // ── Constructors ─────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private InventoryMovement() { }

        /// <summary>
        /// Creates a new inventory movement record.
        /// </summary>
        public InventoryMovement(
            int productId,
            int warehouseId,
            int unitId,
            MovementType movementType,
            decimal quantity,
            decimal quantityInBaseUnit,
            decimal unitCost,
            decimal totalCost,
            DateTime movementDate,
            string referenceNumber,
            SourceType sourceType,
            int? sourceId = null,
            string notes = null)
        {
            if (productId <= 0)
                throw new InventoryDomainException("الصنف مطلوب.");

            if (warehouseId <= 0)
                throw new InventoryDomainException("المخزن مطلوب.");

            if (unitId <= 0)
                throw new InventoryDomainException("الوحدة مطلوبة.");

            if (quantity <= 0)
                throw new InventoryDomainException("الكمية يجب أن تكون أكبر من صفر.");

            if (quantityInBaseUnit <= 0)
                throw new InventoryDomainException("الكمية بالوحدة الأساسية يجب أن تكون أكبر من صفر.");

            if (unitCost < 0)
                throw new InventoryDomainException("تكلفة الوحدة لا يمكن أن تكون سالبة.");

            if (string.IsNullOrWhiteSpace(referenceNumber))
                throw new InventoryDomainException("رقم المرجع مطلوب.");

            ProductId = productId;
            WarehouseId = warehouseId;
            UnitId = unitId;
            MovementType = movementType;
            Quantity = quantity;
            QuantityInBaseUnit = quantityInBaseUnit;
            UnitCost = unitCost;
            TotalCost = totalCost;

            // Validate TotalCost matches QuantityInBaseUnit * UnitCost
            var expectedTotal = Math.Round(quantityInBaseUnit * unitCost, 4);
            if (Math.Abs(totalCost - expectedTotal) > 0.01m)
                throw new InventoryDomainException($"إجمالي التكلفة ({totalCost}) لا يتطابق مع الكمية × تكلفة الوحدة ({expectedTotal}).");

            MovementDate = movementDate;
            ReferenceNumber = referenceNumber.Trim();
            SourceType = sourceType;
            SourceId = sourceId;
            Notes = notes?.Trim();
            BalanceAfter = 0; // Must be set by the service after updating WarehouseProduct
        }

        // ── Properties ───────────────────────────────────────────

        /// <summary>Product FK.</summary>
        public int ProductId { get; private set; }

        /// <summary>Warehouse FK.</summary>
        public int WarehouseId { get; private set; }

        /// <summary>Unit used in this movement.</summary>
        public int UnitId { get; private set; }

        /// <summary>Type of stock movement.</summary>
        public MovementType MovementType { get; private set; }

        /// <summary>Quantity in the specified unit.</summary>
        public decimal Quantity { get; private set; }

        /// <summary>Quantity converted to base units (for stock tracking).</summary>
        public decimal QuantityInBaseUnit { get; private set; }

        /// <summary>Cost per base unit at the time of movement.</summary>
        public decimal UnitCost { get; private set; }

        /// <summary>Total cost of this movement (QuantityInBaseUnit × UnitCost).</summary>
        public decimal TotalCost { get; private set; }

        /// <summary>Date the movement occurred.</summary>
        public DateTime MovementDate { get; private set; }

        /// <summary>Reference document number (invoice number, adjustment number, etc.).</summary>
        public string ReferenceNumber { get; private set; }

        /// <summary>Source document type (purchase, sale, adjustment, etc.).</summary>
        public SourceType SourceType { get; private set; }

        /// <summary>Source document ID (invoice ID, etc.).</summary>
        public int? SourceId { get; private set; }

        /// <summary>Stock balance after this movement (in base units, this warehouse).</summary>
        public decimal BalanceAfter { get; private set; }

        /// <summary>Optional notes.</summary>
        public string Notes { get; private set; }

        // ── Navigation Properties ────────────────────────────────

        /// <summary>Related product.</summary>
        public Product Product { get; private set; }

        /// <summary>Related warehouse.</summary>
        public Warehouse Warehouse { get; private set; }

        /// <summary>Unit used in this movement.</summary>
        public Unit Unit { get; private set; }

        // ── Domain Methods ───────────────────────────────────────

        /// <summary>
        /// Sets the balance-after value. Called by the service after
        /// updating WarehouseProduct stock.
        /// </summary>
        public void SetBalanceAfter(decimal balanceAfter)
        {
            if (balanceAfter < 0)
                throw new InventoryDomainException("الرصيد بعد الحركة لا يمكن أن يكون سالباً.");

            BalanceAfter = balanceAfter;
        }

        /// <summary>
        /// Sets the balance-after value allowing negative (used when AllowNegativeStock = true).
        /// </summary>
        public void SetBalanceAfterAllowNegative(decimal balanceAfter)
        {
            BalanceAfter = balanceAfter;
        }

        /// <summary>Returns true if this movement increases stock.</summary>
        public bool IsIncoming() => MovementType switch
        {
            Enums.MovementType.PurchaseIn => true,
            Enums.MovementType.SalesReturn => true,
            Enums.MovementType.AdjustmentIn => true,
            Enums.MovementType.TransferIn => true,
            Enums.MovementType.OpeningBalance => true,
            _ => false
        };

        /// <summary>Returns true if this movement decreases stock.</summary>
        public bool IsOutgoing() => !IsIncoming();
    }
}
