using System.Collections.Generic;
using System.Linq;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Domain.Entities.Inventory;

namespace MarcoERP.Application.Mappers.Inventory
{
    /// <summary>Manual mapper for Product entity ↔ DTOs.</summary>
    public static class ProductMapper
    {
        public static ProductDto ToDto(Product entity)
        {
            if (entity == null) return null;

            return new ProductDto
            {
                Id = entity.Id,
                Code = entity.Code,
                NameAr = entity.NameAr,
                NameEn = entity.NameEn,
                CategoryId = entity.CategoryId,
                CategoryName = entity.Category?.NameAr,
                BaseUnitId = entity.BaseUnitId,
                BaseUnitName = entity.BaseUnit?.NameAr,
                CostPrice = entity.CostPrice,
                DefaultSalePrice = entity.DefaultSalePrice,
                WholesalePrice = entity.WholesalePrice,
                RetailPrice = entity.RetailPrice,
                ImagePath = entity.ImagePath,
                MaximumStock = entity.MaximumStock,
                WeightedAverageCost = entity.WeightedAverageCost,
                MinimumStock = entity.MinimumStock,
                ReorderLevel = entity.ReorderLevel,
                VatRate = entity.VatRate,
                Barcode = entity.Barcode,
                Description = entity.Description,
                Status = entity.Status.ToString(),
                DefaultSupplierId = entity.DefaultSupplierId,
                DefaultSupplierName = entity.DefaultSupplier?.NameAr,
                Units = entity.ProductUnits?.Select(ToProductUnitDto).ToList() ?? new()
            };
        }

        public static ProductUnitDto ToProductUnitDto(ProductUnit pu)
        {
            if (pu == null) return null;

            return new ProductUnitDto
            {
                Id = pu.Id,
                UnitId = pu.UnitId,
                UnitNameAr = pu.Unit?.NameAr,
                AbbreviationAr = pu.Unit?.AbbreviationAr,
                ConversionFactor = pu.ConversionFactor,
                SalePrice = pu.SalePrice,
                PurchasePrice = pu.PurchasePrice,
                Barcode = pu.Barcode,
                IsDefault = pu.IsDefault
            };
        }

        public static ProductSearchResultDto ToSearchResult(Product entity, decimal totalStock)
        {
            if (entity == null) return null;

            return new ProductSearchResultDto
            {
                Id = entity.Id,
                Code = entity.Code,
                NameAr = entity.NameAr,
                BaseUnitName = entity.BaseUnit?.NameAr,
                DefaultSalePrice = entity.DefaultSalePrice,
                CostPrice = entity.CostPrice,
                TotalStock = totalStock,
                Units = entity.ProductUnits?.Select(ToProductUnitDto).ToList() ?? new()
            };
        }
    }
}
