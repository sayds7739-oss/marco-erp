using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Inventory
{
    /// <summary>
    /// Request DTO for bulk price update operation.
    /// </summary>
    public sealed class BulkPriceUpdateRequestDto
    {
        /// <summary>Product IDs to update.</summary>
        public List<int> ProductIds { get; set; } = new();

        /// <summary>
        /// Update mode: "Percentage" for percentage change, "Direct" for direct price.
        /// </summary>
        public string Mode { get; set; }

        /// <summary>
        /// Percentage change (positive = increase, negative = decrease).
        /// Used when Mode = "Percentage".
        /// </summary>
        public decimal PercentageChange { get; set; }

        /// <summary>
        /// Direct new price. Used when Mode = "Direct".
        /// </summary>
        public decimal DirectPrice { get; set; }

        /// <summary>
        /// Which price to update: "SalePrice" or "CostPrice".
        /// </summary>
        public string PriceTarget { get; set; } = "SalePrice";

        /// <summary>
        /// Which unit level to update: "MajorUnit" (base) or "MinorUnit".
        /// </summary>
        public string UnitLevel { get; set; } = "MajorUnit";

        /// <summary>
        /// When true, update all unit prices using conversion factors to keep values synchronized.
        /// </summary>
        public bool SyncByConversion { get; set; } = true;
    }

    /// <summary>
    /// Preview item showing what will change before confirming.
    /// </summary>
    public sealed class BulkPricePreviewItemDto
    {
        public int ProductId { get; set; }
        public string Code { get; set; }
        public string NameAr { get; set; }
        public string UnitLevel { get; set; }
        public string UnitNameAr { get; set; }
        public decimal ConversionFactor { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal NewPrice { get; set; }
        public decimal Difference { get; set; }
        public decimal PercentageChange { get; set; }
        public decimal CurrentMajorPrice { get; set; }
        public decimal NewMajorPrice { get; set; }
        public decimal CurrentMinorPrice { get; set; }
        public decimal NewMinorPrice { get; set; }
    }

    /// <summary>
    /// Result of a bulk price update operation.
    /// </summary>
    public sealed class BulkPriceUpdateResultDto
    {
        public int UpdatedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
