using System;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Exceptions.Inventory;

namespace MarcoERP.Domain.Entities.Inventory
{
    /// <summary>
    /// Tracks the stock balance of a specific product in a specific warehouse.
    /// Quantity is always in the product's base unit.
    /// Updated by InventoryMovement operations.
    /// Immutable financial record — cannot be hard-deleted (RECORD_PROTECTION_POLICY).
    /// </summary>
    public sealed class WarehouseProduct : BaseEntity, IImmutableFinancialRecord
    {
        // ── Constructors ─────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private WarehouseProduct() { }

        /// <summary>
        /// Creates a new WarehouseProduct balance record.
        /// </summary>
        public WarehouseProduct(int warehouseId, int productId, decimal initialQuantity = 0)
        {
            if (warehouseId <= 0)
                throw new InventoryDomainException("المخزن مطلوب.");

            if (productId <= 0)
                throw new InventoryDomainException("الصنف مطلوب.");

            if (initialQuantity < 0)
                throw new InventoryDomainException("الكمية الابتدائية لا يمكن أن تكون سالبة.");

            WarehouseId = warehouseId;
            ProductId = productId;
            Quantity = initialQuantity;
            Warehouse = null;
            Product = null;
        }

        // ── Properties ───────────────────────────────────────────

        /// <summary>Warehouse FK.</summary>
        public int WarehouseId { get; private set; }

        /// <summary>Product FK.</summary>
        public int ProductId { get; private set; }

        /// <summary>Current stock quantity in base units.</summary>
        public decimal Quantity { get; private set; }

        // ── Navigation Properties ────────────────────────────────

        /// <summary>Related warehouse.</summary>
        public Warehouse Warehouse { get; private set; }

        /// <summary>Related product.</summary>
        public Product Product { get; private set; }

        // ── Domain Methods ───────────────────────────────────────

        /// <summary>
        /// Increases stock by the given quantity (in base units).
        /// Used for: Purchase receipt, Sales return, Transfer in, Adjustment in.
        /// </summary>
        public void IncreaseStock(decimal quantity)
        {
            if (quantity <= 0)
                throw new InventoryDomainException("كمية الإضافة يجب أن تكون أكبر من صفر.");

            Quantity += quantity;
        }

        /// <summary>
        /// Decreases stock by the given quantity (in base units).
        /// Used for: Sales delivery, Purchase return, Transfer out, Adjustment out.
        /// STK-INV-01: Cannot decrease below zero.
        /// </summary>
        public void DecreaseStock(decimal quantity)
        {
            if (quantity <= 0)
                throw new InventoryDomainException("كمية الخصم يجب أن تكون أكبر من صفر.");

            if (Quantity < quantity)
                throw new InventoryDomainException(
                    $"الرصيد غير كافي. المتاح: {Quantity:N2}، المطلوب: {quantity:N2}");

            Quantity -= quantity;
        }

        /// <summary>
        /// Decreases stock without enforcing non-negative balance.
        /// Used only when negative stock is explicitly allowed by system settings.
        /// </summary>
        public void DecreaseStockAllowNegative(decimal quantity)
        {
            if (quantity <= 0)
                throw new InventoryDomainException("كمية الخصم يجب أن تكون أكبر من صفر.");

            Quantity -= quantity;
        }

        /// <summary>
        /// Sets the stock to an exact quantity. Used only for opening balance.
        /// </summary>
        public void SetOpeningBalance(decimal quantity)
        {
            if (quantity < 0)
                throw new InventoryDomainException("الرصيد الافتتاحي لا يمكن أن يكون سالباً.");

            Quantity = quantity;
        }
    }
}
