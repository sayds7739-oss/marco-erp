using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Inventory
{
    /// <summary>
    /// Represents a single row from an Excel import file, pre-validation.
    /// </summary>
    public sealed class ProductImportRowDto
    {
        /// <summary>Excel row number (1-based, excluding header).</summary>
        public int RowNumber { get; set; }

        // ── Required Fields ──
        public string Code { get; set; }
        public string NameAr { get; set; }
        public string CategoryName { get; set; }
        public string BaseUnitName { get; set; }
        public string MinorUnitName { get; set; }
        public decimal MinorUnitConversionFactor { get; set; }

        // ── Optional Fields ──
        public string NameEn { get; set; }
        public decimal CostPrice { get; set; }
        public decimal DefaultSalePrice { get; set; }
        public decimal MinimumStock { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal VatRate { get; set; }
        public string Barcode { get; set; }
        public string Description { get; set; }
        public string SupplierName { get; set; }

        // ── Validation State ──
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();

        // ── Resolved IDs (after lookup) ──
        public int? ResolvedCategoryId { get; set; }
        public int? ResolvedBaseUnitId { get; set; }
        public int? ResolvedMinorUnitId { get; set; }
        public int? ResolvedSupplierId { get; set; }
    }

    /// <summary>
    /// Result of a batch import operation.
    /// </summary>
    public sealed class ProductImportResultDto
    {
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<ProductImportRowDto> FailedRows { get; set; } = new();
        public List<string> GeneralErrors { get; set; } = new();
    }
}
