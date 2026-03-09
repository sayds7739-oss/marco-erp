using System;
using System.Collections.Generic;
using System.Linq;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Inventory;

namespace MarcoERP.Domain.Entities.Inventory
{
    /// <summary>
    /// Represents a product (item/SKU) in the inventory system.
    /// Supports multiple units of measure via ProductUnit relationship.
    /// Stock is always tracked in the base unit; conversion factors handle multi-unit operations.
    /// </summary>
    public sealed class Product : CompanyAwareEntity
    {
        // ── Private collections ──────────────────────────────────
        private readonly List<ProductUnit> _productUnits = new();

        // ── Constructors ─────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private Product() { }

        /// <summary>
        /// Creates a new Product with full invariant validation.
        /// </summary>
        public Product(
            string code,
            string nameAr,
            string nameEn,
            int categoryId,
            int baseUnitId,
            decimal initialCostPrice,
            decimal defaultSalePrice,
            decimal minimumStock,
            decimal reorderLevel,
            decimal vatRate,
            string barcode = null,
            string description = null,
            decimal wholesalePrice = 0,
            decimal retailPrice = 0,
            string imagePath = null,
            decimal maximumStock = 0)
        {
            // ── PRD-INV-01: Code is required ──
            if (string.IsNullOrWhiteSpace(code))
                throw new InventoryDomainException("كود الصنف مطلوب.");

            if (string.IsNullOrWhiteSpace(nameAr))
                throw new InventoryDomainException("اسم الصنف بالعربي مطلوب.");

            if (categoryId <= 0)
                throw new InventoryDomainException("تصنيف الصنف مطلوب.");

            if (baseUnitId <= 0)
                throw new InventoryDomainException("الوحدة الأساسية مطلوبة.");

            if (initialCostPrice < 0)
                throw new InventoryDomainException("سعر التكلفة لا يمكن أن يكون سالباً.");

            if (defaultSalePrice < 0)
                throw new InventoryDomainException("سعر البيع لا يمكن أن يكون سالباً.");

            if (minimumStock < 0)
                throw new InventoryDomainException("الحد الأدنى للمخزون لا يمكن أن يكون سالباً.");

            if (vatRate < 0 || vatRate > 100)
                throw new InventoryDomainException("نسبة الضريبة يجب أن تكون بين 0 و 100.");

            Code = code.Trim();
            NameAr = nameAr.Trim();
            NameEn = nameEn?.Trim();
            CategoryId = categoryId;
            BaseUnitId = baseUnitId;
            CostPrice = initialCostPrice;
            DefaultSalePrice = defaultSalePrice;
            WholesalePrice = wholesalePrice;
            RetailPrice = retailPrice;
            ImagePath = imagePath?.Trim();
            MaximumStock = maximumStock;
            MinimumStock = minimumStock;
            ReorderLevel = reorderLevel;
            VatRate = vatRate;
            Barcode = barcode?.Trim();
            Description = description?.Trim();
            Status = ProductStatus.Active;
            WeightedAverageCost = initialCostPrice; // WAC starts at initial cost
        }

        // ── Properties ───────────────────────────────────────────

        /// <summary>Unique product code (auto-generated or manual).</summary>
        public string Code { get; private set; }

        /// <summary>Arabic product name (required).</summary>
        public string NameAr { get; private set; }

        /// <summary>English product name (optional).</summary>
        public string NameEn { get; private set; }

        /// <summary>Category FK.</summary>
        public int CategoryId { get; private set; }

        /// <summary>Base unit of measure FK (all stock is tracked in this unit).</summary>
        public int BaseUnitId { get; private set; }

        /// <summary>Last known purchase cost price (per base unit).</summary>
        public decimal CostPrice { get; private set; }

        /// <summary>Default selling price (per base unit).</summary>
        public decimal DefaultSalePrice { get; private set; }

        /// <summary>سعر الجملة (للوحدة الأساسية).</summary>
        public decimal WholesalePrice { get; private set; }

        /// <summary>سعر القطاعي (للوحدة الأساسية).</summary>
        public decimal RetailPrice { get; private set; }

        /// <summary>مسار صورة الصنف.</summary>
        public string ImagePath { get; private set; }

        /// <summary>الحد الأقصى للمخزون.</summary>
        public decimal MaximumStock { get; private set; }

        /// <summary>Weighted Average Cost — updated on each purchase receipt.</summary>
        public decimal WeightedAverageCost { get; private set; }

        /// <summary>Minimum stock level (base unit) — triggers alert.</summary>
        public decimal MinimumStock { get; private set; }

        /// <summary>Reorder level (base unit) — triggers purchase suggestion.</summary>
        public decimal ReorderLevel { get; private set; }

        /// <summary>VAT percentage (e.g., 14 for 14%).</summary>
        public decimal VatRate { get; private set; }

        /// <summary>Product barcode (optional, on base unit).</summary>
        public string Barcode { get; private set; }

        /// <summary>Optional description / notes.</summary>
        public string Description { get; private set; }

        /// <summary>Product lifecycle status.</summary>
        public ProductStatus Status { get; private set; }

        /// <summary>Default supplier FK (optional — المورّد الأساسي).</summary>
        public int? DefaultSupplierId { get; private set; }

        /// <summary>Sets or clears the default supplier.</summary>
        public void SetDefaultSupplier(int? supplierId)
        {
            DefaultSupplierId = supplierId;
        }

        // ── Navigation Properties ────────────────────────────────

        /// <summary>Product category.</summary>
        public Category Category { get; private set; }

        /// <summary>Base unit of measure.</summary>
        public Unit BaseUnit { get; private set; }

        /// <summary>Default supplier (optional).</summary>
        public Supplier DefaultSupplier { get; private set; }

        /// <summary>Available units for this product with conversion factors.</summary>
        public IReadOnlyCollection<ProductUnit> ProductUnits => _productUnits.AsReadOnly();

        // ── Domain Methods ───────────────────────────────────────

        /// <summary>Updates basic product information.</summary>
        public void Update(
            string nameAr,
            string nameEn,
            int categoryId,
            decimal defaultSalePrice,
            decimal minimumStock,
            decimal reorderLevel,
            decimal vatRate,
            string barcode,
            string description,
            int? defaultSupplierId = null,
            decimal wholesalePrice = 0,
            decimal retailPrice = 0,
            string imagePath = null,
            decimal maximumStock = 0)
        {
            if (string.IsNullOrWhiteSpace(nameAr))
                throw new InventoryDomainException("اسم الصنف بالعربي مطلوب.");

            if (defaultSalePrice < 0)
                throw new InventoryDomainException("سعر البيع لا يمكن أن يكون سالباً.");

            if (vatRate < 0 || vatRate > 100)
                throw new InventoryDomainException("نسبة الضريبة يجب أن تكون بين 0 و 100.");

            if (wholesalePrice < 0)
                throw new InventoryDomainException("سعر الجملة لا يمكن أن يكون سالباً.");

            if (retailPrice < 0)
                throw new InventoryDomainException("سعر التجزئة لا يمكن أن يكون سالباً.");

            NameAr = nameAr.Trim();
            NameEn = nameEn?.Trim();
            CategoryId = categoryId;
            DefaultSalePrice = defaultSalePrice;
            WholesalePrice = wholesalePrice;
            RetailPrice = retailPrice;
            ImagePath = imagePath?.Trim();
            MaximumStock = maximumStock;
            MinimumStock = minimumStock;
            ReorderLevel = reorderLevel;
            VatRate = vatRate;
            Barcode = barcode?.Trim();
            Description = description?.Trim();
            DefaultSupplierId = defaultSupplierId;
        }

        /// <summary>
        /// Updates the Weighted Average Cost after a purchase receipt.
        /// WAC = ((existing qty × old WAC) + (new qty × purchase unit cost)) / (existing qty + new qty)
        /// </summary>
        /// <param name="existingQuantity">Current stock in base units across all warehouses.</param>
        /// <param name="receivedQuantity">Quantity received in base units.</param>
        /// <param name="unitCost">Cost per base unit from this purchase.</param>
        public void UpdateWeightedAverageCost(decimal existingQuantity, decimal receivedQuantity, decimal unitCost)
        {
            if (receivedQuantity <= 0)
                throw new InventoryDomainException("الكمية المستلمة يجب أن تكون أكبر من صفر.");

            if (unitCost < 0)
                throw new InventoryDomainException("تكلفة الوحدة لا يمكن أن تكون سالبة.");

            // C-07 fix: When existing stock is zero or negative (possible after
            // AllowNegativeStock cancel/adjustment flows), the standard WAC blending
            // formula produces distorted or negative results.  Standard accounting
            // practice: reset WAC to the new purchase cost — treat as fresh start.
            if (existingQuantity <= 0)
            {
                WeightedAverageCost = Math.Round(unitCost, 4);
            }
            else
            {
                decimal totalExistingValue = existingQuantity * WeightedAverageCost;
                decimal totalNewValue = receivedQuantity * unitCost;
                decimal totalQuantity = existingQuantity + receivedQuantity;

                WeightedAverageCost = Math.Round((totalExistingValue + totalNewValue) / totalQuantity, 4);
            }

            // Also update the last cost price
            CostPrice = unitCost;
        }

        /// <summary>Updates the cost price directly (for bulk price updates / manual override).</summary>
        /// <param name="newCostPrice">The new cost price per base unit.</param>
        public void UpdateCostPrice(decimal newCostPrice)
        {
            if (newCostPrice < 0)
                throw new InventoryDomainException("سعر التكلفة لا يمكن أن يكون سالباً.");
            CostPrice = newCostPrice;
        }

        /// <summary>
        /// Sets WAC directly. Used when reversing a purchase (cancel/return)
        /// where the standard additive formula doesn't apply.
        /// </summary>
        public void SetWeightedAverageCost(decimal newWac)
        {
            if (newWac < 0)
                throw new InventoryDomainException("متوسط التكلفة المرجح لا يمكن أن يكون سالباً.");
            WeightedAverageCost = Math.Round(newWac, 4);
        }

        /// <summary>Adds a unit option for this product.</summary>
        public void AddUnit(ProductUnit productUnit)
        {
            if (productUnit == null)
                throw new InventoryDomainException("بيانات الوحدة مطلوبة.");

            // PRD-INV-10: No duplicate units per product
            if (_productUnits.Any(pu => pu.UnitId == productUnit.UnitId))
                throw new InventoryDomainException($"الوحدة مضافة مسبقاً لهذا الصنف.");

            _productUnits.Add(productUnit);
        }

        /// <summary>Removes a unit option. Cannot remove the base unit.</summary>
        public void RemoveUnit(int unitId)
        {
            if (unitId == BaseUnitId)
                throw new InventoryDomainException("لا يمكن حذف الوحدة الأساسية من الصنف.");

            var existing = _productUnits.FirstOrDefault(pu => pu.UnitId == unitId);
            if (existing == null)
                throw new InventoryDomainException("الوحدة غير موجودة في هذا الصنف.");

            _productUnits.Remove(existing);
        }

        /// <summary>Sets product status to Active.</summary>
        public void Activate()
        {
            Status = ProductStatus.Active;
        }

        /// <summary>Sets product status to Inactive.</summary>
        public void Deactivate()
        {
            Status = ProductStatus.Inactive;
        }

        /// <summary>Marks product as discontinued. Cannot be reactivated easily.</summary>
        public void Discontinue()
        {
            Status = ProductStatus.Discontinued;
        }

        /// <summary>تحديث صورة الصنف.</summary>
        public void SetImagePath(string imagePath)
        {
            ImagePath = imagePath?.Trim();
        }

        /// <summary>تحديث أسعار البيع المتعددة.</summary>
        public void UpdatePrices(decimal defaultSalePrice, decimal wholesalePrice, decimal retailPrice)
        {
            if (defaultSalePrice < 0)
                throw new InventoryDomainException("سعر البيع لا يمكن أن يكون سالباً.");
            if (wholesalePrice < 0)
                throw new InventoryDomainException("سعر الجملة لا يمكن أن يكون سالباً.");
            if (retailPrice < 0)
                throw new InventoryDomainException("سعر التجزئة لا يمكن أن يكون سالباً.");

            DefaultSalePrice = defaultSalePrice;
            WholesalePrice = wholesalePrice;
            RetailPrice = retailPrice;
        }
    }
}
