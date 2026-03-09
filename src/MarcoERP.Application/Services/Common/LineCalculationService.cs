using System;
using System.Collections.Generic;
using System.Linq;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Common;
using MarcoERP.Application.Interfaces;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.Services.Common
{
    /// <summary>
    /// Domain-consistent calculation service for line and invoice totals.
    /// Mirrors rounding behavior used by invoice line entities.
    /// Phase 9B: Extended with profit, cost, and unit conversion calculations.
    /// All business arithmetic is centralized here — ViewModels must not contain math.
    /// </summary>
    [Module(SystemModule.Common)]
    public sealed class LineCalculationService : ILineCalculationService
    {
        private const int Precision = 4;

        private static decimal R(decimal value) => Math.Round(value, Precision, MidpointRounding.ToEven);

        public LineCalculationResult CalculateLine(LineCalculationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var qty = Math.Max(request.Quantity, 0m);
            var unitPrice = Math.Max(request.UnitPrice, 0m);
            var discountPercent = Math.Clamp(request.DiscountPercent, 0m, 100m);
            var vatRate = request.VatRate;
            var conversionFactor = request.ConversionFactor <= 0 ? 1m : request.ConversionFactor;

            var baseQty = R(qty * conversionFactor);
            var subTotal = R(qty * unitPrice);
            var discountAmount = R(subTotal * discountPercent / 100m);

            decimal netTotal;
            decimal vatAmount;
            decimal totalWithVat;

            if (request.IsVatInclusive && vatRate > 0)
            {
                // VAT-inclusive: UnitPrice already contains VAT.
                // Governance formula: LineVAT = LineTotal × (VATRate / (100 + VATRate))
                var inclusiveTotal = subTotal - discountAmount;
                vatAmount = R(inclusiveTotal * vatRate / (100m + vatRate));
                netTotal = inclusiveTotal - vatAmount;
                totalWithVat = inclusiveTotal;
            }
            else
            {
                // VAT-exclusive (default): VAT added on top
                netTotal = subTotal - discountAmount;
                vatAmount = R(netTotal * vatRate / 100m);
                totalWithVat = netTotal + vatAmount;
            }

            // Phase 9B: Profit calculations
            var costPerUnit = R(request.CostPrice * conversionFactor);
            var costTotal = R(baseQty * request.CostPrice);

            var discountFactor = 1m - discountPercent / 100m;
            if (discountFactor < 0m) discountFactor = 0m;
            decimal netUnitPrice;
            if (request.IsVatInclusive && vatRate > 0)
                netUnitPrice = R(unitPrice * discountFactor * 100m / (100m + vatRate));
            else
                netUnitPrice = R(unitPrice * discountFactor);

            var unitProfit = R(netUnitPrice - costPerUnit);
            var totalProfit = R(unitProfit * qty);
            var profitMarginPercent = netTotal != 0
                ? Math.Round(totalProfit / netTotal * 100m, 2, MidpointRounding.ToEven)
                : 0m;

            return new LineCalculationResult
            {
                BaseQuantity = baseQty,
                SubTotal = subTotal,
                DiscountAmount = discountAmount,
                NetTotal = netTotal,
                VatAmount = vatAmount,
                TotalWithVat = totalWithVat,
                CostPerUnit = costPerUnit,
                CostTotal = costTotal,
                NetUnitPrice = netUnitPrice,
                UnitProfit = unitProfit,
                TotalProfit = totalProfit,
                ProfitMarginPercent = profitMarginPercent
            };
        }

        public InvoiceTotalsResult CalculateTotals(IEnumerable<LineCalculationRequest> lines)
        {
            var results = (lines ?? Enumerable.Empty<LineCalculationRequest>())
                .Select(CalculateLine)
                .ToList();

            return new InvoiceTotalsResult
            {
                Subtotal = results.Sum(r => r.SubTotal),
                DiscountTotal = results.Sum(r => r.DiscountAmount),
                VatTotal = results.Sum(r => r.VatAmount),
                NetTotal = results.Sum(r => r.TotalWithVat)
            };
        }

        /// <inheritdoc />
        public decimal ConvertQuantity(decimal quantity, decimal factor)
        {
            if (factor <= 0) return quantity;
            return R(quantity * factor);
        }

        /// <inheritdoc />
        public decimal ConvertPrice(decimal price, decimal factor)
        {
            if (factor <= 0) return price;
            return R(price / factor);
        }

        // ── Phase 9C: Business-logic methods extracted from ViewModels ──

        /// <inheritdoc />
        public HeaderDiscountResult ApplyHeaderDiscount(
            InvoiceTotalsResult lineTotals,
            decimal headerDiscountPercent,
            decimal headerDiscountAmount,
            decimal deliveryFee)
        {
            if (lineTotals == null)
                throw new ArgumentNullException(nameof(lineTotals));

            var subAfterLineDiscount = lineTotals.Subtotal - lineTotals.DiscountTotal;
            var headerPercentValue = R(subAfterLineDiscount * headerDiscountPercent / 100m);
            var totalHeaderDiscount = headerPercentValue + headerDiscountAmount;

            // LC-05: Clamp header discount so it cannot exceed the line subtotal after line discounts.
            totalHeaderDiscount = Math.Min(totalHeaderDiscount, subAfterLineDiscount);

            // ZATCA fix: reduce VatTotal proportionally when header discount is applied.
            // Mirrors domain entity RecalculateTotals logic.
            decimal vatAdjustment = 0m;
            if (subAfterLineDiscount > 0 && totalHeaderDiscount > 0 && lineTotals.VatTotal > 0)
            {
                var effectiveVatRate = lineTotals.VatTotal / subAfterLineDiscount;
                vatAdjustment = R(totalHeaderDiscount * effectiveVatRate);
            }
            var adjustedVatTotal = lineTotals.VatTotal - vatAdjustment;

            return new HeaderDiscountResult
            {
                TotalDiscount = lineTotals.DiscountTotal + totalHeaderDiscount,
                VatTotal = adjustedVatTotal,
                NetTotal = (subAfterLineDiscount - totalHeaderDiscount) + adjustedVatTotal + deliveryFee
            };
        }

        /// <inheritdoc />
        public decimal CalculatePartCount(decimal majorFactor, decimal minorFactor)
        {
            if (majorFactor <= 0 || minorFactor <= 0) return 1m;
            var result = R(majorFactor / minorFactor);
            return result <= 0 ? 1m : result;
        }

        /// <inheritdoc />
        public decimal ConvertBaseToUnitQuantity(decimal baseQuantity, decimal conversionFactor)
        {
            if (conversionFactor <= 0) return baseQuantity;
            return R(baseQuantity / conversionFactor);
        }

        /// <inheritdoc />
        public decimal CalculateCostDifference(decimal differenceQty, decimal conversionFactor, decimal unitCost)
        {
            var baseQty = ConvertQuantity(differenceQty, conversionFactor);
            return R(baseQty * unitCost);
        }

        /// <inheritdoc />
        public decimal CalculateNetCash(decimal cashAmount, decimal netTotal, decimal cardAmount, decimal onAccountAmount)
        {
            return Math.Min(cashAmount, netTotal - cardAmount - onAccountAmount);
        }
    }
}
