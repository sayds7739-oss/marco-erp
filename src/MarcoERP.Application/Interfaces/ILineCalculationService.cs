using System.Collections.Generic;
using MarcoERP.Application.DTOs.Common;

namespace MarcoERP.Application.Interfaces
{
    /// <summary>
    /// Provides consistent line and invoice total calculations.
    /// Phase 9B: Extended with unit conversion and profit calculations.
    /// Phase 9C: Extended with header discount, part count, cost difference,
    /// base-to-unit conversion, and net cash calculations.
    /// No arithmetic should exist in ViewModels — all math goes through this service.
    /// </summary>
    public interface ILineCalculationService
    {
        /// <summary>
        /// Calculates totals for a single line using domain-consistent rounding.
        /// Includes profit fields when <see cref="LineCalculationRequest.CostPrice"/> is set.
        /// </summary>
        LineCalculationResult CalculateLine(LineCalculationRequest request);

        /// <summary>
        /// Calculates invoice totals from a list of line inputs.
        /// </summary>
        InvoiceTotalsResult CalculateTotals(IEnumerable<LineCalculationRequest> lines);

        /// <summary>
        /// Converts quantity from one unit to another using a conversion factor.
        /// E.g., primary qty → secondary qty: qty × factor.
        /// </summary>
        decimal ConvertQuantity(decimal quantity, decimal factor);

        /// <summary>
        /// Converts price from one unit to another using a conversion factor.
        /// E.g., primary price → secondary price: price / factor.
        /// </summary>
        decimal ConvertPrice(decimal price, decimal factor);

        // ── Phase 9C: Business-logic methods extracted from ViewModels ──

        /// <summary>
        /// Applies header-level discount (percent + fixed amount) and delivery fee
        /// to pre-computed line totals. Returns adjusted TotalDiscount and NetTotal.
        /// </summary>
        HeaderDiscountResult ApplyHeaderDiscount(
            InvoiceTotalsResult lineTotals,
            decimal headerDiscountPercent,
            decimal headerDiscountAmount,
            decimal deliveryFee);

        /// <summary>
        /// Calculates part count: how many minor units fit in one major unit.
        /// Formula: majorFactor / minorFactor, with safe guard for zero/negative.
        /// </summary>
        decimal CalculatePartCount(decimal majorFactor, decimal minorFactor);

        /// <summary>
        /// Converts base quantity to unit quantity (reverse of ConvertQuantity).
        /// Formula: baseQuantity / conversionFactor.
        /// </summary>
        decimal ConvertBaseToUnitQuantity(decimal baseQuantity, decimal conversionFactor);

        /// <summary>
        /// Calculates inventory adjustment cost difference.
        /// Formula: ConvertQuantity(differenceQty, factor) × unitCost.
        /// </summary>
        decimal CalculateCostDifference(decimal differenceQty, decimal conversionFactor, decimal unitCost);

        /// <summary>
        /// Calculates net cash amount (excluding change returned to customer).
        /// Formula: Min(cashAmount, netTotal − cardAmount − onAccountAmount).
        /// </summary>
        decimal CalculateNetCash(decimal cashAmount, decimal netTotal, decimal cardAmount, decimal onAccountAmount);
    }
}
