using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Common
{
    /// <summary>
    /// Input data for line total calculations.
    /// Phase 9B: Extended with CostPrice for profit calculation.
    /// </summary>
    public sealed class LineCalculationRequest
    {
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal VatRate { get; set; }
        public decimal ConversionFactor { get; set; }
        /// <summary>Weighted average cost per BASE unit (Phase 9B).</summary>
        public decimal CostPrice { get; set; }

        /// <summary>
        /// When true, UnitPrice is treated as VAT-inclusive.
        /// VAT is extracted using: VAT = Total × (VatRate / (100 + VatRate)).
        /// Governance: ACCOUNTING_PRINCIPLES VAT-03.
        /// </summary>
        public bool IsVatInclusive { get; set; }
    }

    /// <summary>
    /// Output values for a calculated line.
    /// Phase 9B: Extended with profit and cost fields.
    /// </summary>
    public sealed class LineCalculationResult
    {
        public decimal BaseQuantity { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal NetTotal { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalWithVat { get; set; }

        // Phase 9B: Profit fields
        /// <summary>Cost for the selected unit = CostPrice × ConversionFactor.</summary>
        public decimal CostPerUnit { get; set; }
        /// <summary>Total cost = CostPerUnit × Quantity.</summary>
        public decimal CostTotal { get; set; }
        /// <summary>Net unit price after discount.</summary>
        public decimal NetUnitPrice { get; set; }
        /// <summary>Unit profit = NetUnitPrice − CostPerUnit.</summary>
        public decimal UnitProfit { get; set; }
        /// <summary>Total profit = UnitProfit × Quantity.</summary>
        public decimal TotalProfit { get; set; }
        /// <summary>Profit margin percent = (TotalProfit / NetTotal) × 100.</summary>
        public decimal ProfitMarginPercent { get; set; }
    }

    /// <summary>
    /// Output values for invoice totals.
    /// </summary>
    public sealed class InvoiceTotalsResult
    {
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal NetTotal { get; set; }
    }

    /// <summary>
    /// Output values for header-level discount application.
    /// Phase 9C: Extracted from ViewModel inline calculation.
    /// </summary>
    public sealed class HeaderDiscountResult
    {
        /// <summary>Total discount (line-level + header-level combined).</summary>
        public decimal TotalDiscount { get; set; }
        /// <summary>VAT total after proportional adjustment for header discount.</summary>
        public decimal VatTotal { get; set; }
        /// <summary>Net total after header discount and delivery fee.</summary>
        public decimal NetTotal { get; set; }
    }
}
