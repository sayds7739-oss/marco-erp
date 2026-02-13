using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Inventory
{
    // ════════════════════════════════════════════════════════════
    //  Product DTOs
    // ════════════════════════════════════════════════════════════

    public sealed class ProductDto
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string NameAr { get; set; }
        public string NameEn { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int BaseUnitId { get; set; }
        public string BaseUnitName { get; set; }
        public decimal CostPrice { get; set; }
        public decimal DefaultSalePrice { get; set; }
        public decimal WeightedAverageCost { get; set; }
        public decimal MinimumStock { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal VatRate { get; set; }
        public string Barcode { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public int? DefaultSupplierId { get; set; }
        public string DefaultSupplierName { get; set; }
        public List<ProductUnitDto> Units { get; set; } = new();
    }

    public sealed class ProductUnitDto
    {
        public int Id { get; set; }
        public int UnitId { get; set; }
        public string UnitNameAr { get; set; }
        public string AbbreviationAr { get; set; }
        public decimal ConversionFactor { get; set; }
        public decimal SalePrice { get; set; }
        public decimal PurchasePrice { get; set; }
        public string Barcode { get; set; }
        public bool IsDefault { get; set; }
    }

    public sealed class CreateProductDto
    {
        public string Code { get; set; }
        public string NameAr { get; set; }
        public string NameEn { get; set; }
        public int CategoryId { get; set; }
        public int BaseUnitId { get; set; }
        public decimal CostPrice { get; set; }
        public decimal DefaultSalePrice { get; set; }
        public decimal MinimumStock { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal VatRate { get; set; }
        public string Barcode { get; set; }
        public string Description { get; set; }
        public int? DefaultSupplierId { get; set; }
        public List<CreateProductUnitDto> Units { get; set; } = new();
    }

    public sealed class CreateProductUnitDto
    {
        public int UnitId { get; set; }
        public decimal ConversionFactor { get; set; }
        public decimal SalePrice { get; set; }
        public decimal PurchasePrice { get; set; }
        public string Barcode { get; set; }
        public bool IsDefault { get; set; }
    }

    public sealed class UpdateProductDto
    {
        public int Id { get; set; }
        public string NameAr { get; set; }
        public string NameEn { get; set; }
        public int CategoryId { get; set; }
        public decimal CostPrice { get; set; }
        public decimal DefaultSalePrice { get; set; }
        public decimal MinimumStock { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal VatRate { get; set; }
        public string Barcode { get; set; }
        public string Description { get; set; }
        public int? DefaultSupplierId { get; set; }
        public List<CreateProductUnitDto> Units { get; set; } = new();
    }

    /// <summary>Lightweight DTO for product popup search results.</summary>
    public sealed class ProductSearchResultDto
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string NameAr { get; set; }
        public string BaseUnitName { get; set; }
        public decimal DefaultSalePrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal TotalStock { get; set; }
        public List<ProductUnitDto> Units { get; set; } = new();
    }
}
