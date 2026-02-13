using System;
using System.Threading.Tasks;
using System.Windows;
using MarcoERP.Application.Interfaces.SmartEntry;
using MarcoERP.Domain.Enums;
using MarcoERP.WpfUI.Views.Common;

namespace MarcoERP.WpfUI.Common
{
    public enum PriceHistorySource
    {
        Sales,
        Purchase
    }

    public static class PriceHistoryHelper
    {
        public static async Task<decimal?> ShowAsync(
            ISmartEntryQueryService smartEntry,
            PriceHistorySource source,
            CounterpartyType counterpartyType,
            int? customerId,
            int? supplierId,
            int productId,
            int unitId,
            Window owner)
        {
            if (smartEntry == null)
                throw new ArgumentNullException(nameof(smartEntry));

            if (productId <= 0 || unitId <= 0)
                return null;

            var title = source == PriceHistorySource.Sales ? "سجل أسعار البيع" : "سجل أسعار الشراء";
            var counterpartyLabel = string.Empty;
            decimal? counterpartyPrice = null;

            if (source == PriceHistorySource.Sales)
            {
                var rows = await smartEntry.GetRecentSalesPricesAsync(productId, unitId, 5);

                if (counterpartyType == CounterpartyType.Customer && customerId.HasValue && customerId.Value > 0)
                {
                    counterpartyLabel = "آخر سعر لهذا العميل";
                    counterpartyPrice = await smartEntry.GetLastSalesUnitPriceAsync(customerId.Value, productId, unitId);
                }
                else if (counterpartyType == CounterpartyType.Supplier && supplierId.HasValue && supplierId.Value > 0)
                {
                    counterpartyLabel = "آخر سعر لهذا المورد";
                    counterpartyPrice = await smartEntry.GetLastSalesUnitPriceForSupplierAsync(supplierId.Value, productId, unitId);
                }

                return PriceHistoryDialog.ShowDialog(owner, title, counterpartyLabel, counterpartyPrice, rows);
            }

            var purchaseRows = await smartEntry.GetRecentPurchasePricesAsync(productId, unitId, 5);

            if (counterpartyType == CounterpartyType.Supplier && supplierId.HasValue && supplierId.Value > 0)
            {
                counterpartyLabel = "آخر سعر لهذا المورد";
                counterpartyPrice = await smartEntry.GetLastPurchaseUnitPriceForSupplierAsync(supplierId.Value, productId, unitId);
            }
            else if (counterpartyType == CounterpartyType.Customer && customerId.HasValue && customerId.Value > 0)
            {
                counterpartyLabel = "آخر سعر لهذا العميل";
                counterpartyPrice = await smartEntry.GetLastPurchaseUnitPriceForCustomerAsync(customerId.Value, productId, unitId);
            }

            return PriceHistoryDialog.ShowDialog(owner, title, counterpartyLabel, counterpartyPrice, purchaseRows);
        }
    }
}
