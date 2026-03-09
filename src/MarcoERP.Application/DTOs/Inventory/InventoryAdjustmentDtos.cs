using System;
using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Inventory
{
    // ── InventoryAdjustment DTOs ─────────────────────────────

    /// <summary>DTO for InventoryAdjustment listing.</summary>
    public sealed class InventoryAdjustmentListDto
    {
        public int Id { get; set; }
        public string AdjustmentNumber { get; set; }
        public DateTime AdjustmentDate { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; }
        public string Reason { get; set; }
        public string Status { get; set; }
        public decimal TotalCostDifference { get; set; }
        public int LineCount { get; set; }
    }

    /// <summary>Full InventoryAdjustment DTO with lines.</summary>
    public sealed class InventoryAdjustmentDto
    {
        public int Id { get; set; }
        public string AdjustmentNumber { get; set; }
        public DateTime AdjustmentDate { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; }
        public string Reason { get; set; }
        public string Notes { get; set; }
        public string Status { get; set; }
        public decimal TotalCostDifference { get; set; }
        public int? JournalEntryId { get; set; }
        public List<InventoryAdjustmentLineDto> Lines { get; set; } = new();
    }

    /// <summary>DTO for a single adjustment line.</summary>
    public sealed class InventoryAdjustmentLineDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public int UnitId { get; set; }
        public string UnitName { get; set; }
        public decimal SystemQuantity { get; set; }
        public decimal ActualQuantity { get; set; }
        public decimal DifferenceQuantity { get; set; }
        public decimal ConversionFactor { get; set; }
        public decimal DifferenceInBaseUnit { get; set; }
        public decimal UnitCost { get; set; }
        public decimal CostDifference { get; set; }
    }

    /// <summary>DTO for creating an InventoryAdjustment.</summary>
    public sealed class CreateInventoryAdjustmentDto
    {
        public DateTime AdjustmentDate { get; set; }
        public int WarehouseId { get; set; }
        public string Reason { get; set; }
        public string Notes { get; set; }
        public List<CreateInventoryAdjustmentLineDto> Lines { get; set; } = new();
    }

    /// <summary>DTO for creating an adjustment line.</summary>
    public sealed class CreateInventoryAdjustmentLineDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int UnitId { get; set; }
        public decimal ActualQuantity { get; set; }
    }

    /// <summary>DTO for updating an InventoryAdjustment.</summary>
    public sealed class UpdateInventoryAdjustmentDto
    {
        public int Id { get; set; }
        public DateTime AdjustmentDate { get; set; }
        public int WarehouseId { get; set; }
        public string Reason { get; set; }
        public string Notes { get; set; }
        public List<CreateInventoryAdjustmentLineDto> Lines { get; set; } = new();
    }
}
