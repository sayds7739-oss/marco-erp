using System;

namespace MarcoERP.Application.Common
{
    /// <summary>
    /// System setting keys stored in the SystemSettings table.
    /// NOTE: AllowNegativeStock, AllowNegativeCashboxBalance, and EnableReceiptPrinting
    /// are duplicated in the Feature Governance Engine (FeatureKeys).
    /// New code should prefer FeatureKeys for these toggles.
    /// </summary>
    public static class SystemSettingKeys
    {
        // DEPRECATED: Use FeatureKeys.AllowNegativeStock instead (Feature Governance Engine)
        [Obsolete("Use FeatureKeys.AllowNegativeStock instead")]
        public const string AllowNegativeStock = "AllowNegativeStock";

        // DEPRECATED: Use FeatureKeys.AllowNegativeCash instead (Feature Governance Engine)
        [Obsolete("Use FeatureKeys.AllowNegativeCash instead")]
        public const string AllowNegativeCashboxBalance = "AllowNegativeCashboxBalance";

        // DEPRECATED: Use FeatureKeys.ReceiptPrinting instead (Feature Governance Engine)
        [Obsolete("Use FeatureKeys.ReceiptPrinting instead")]
        public const string EnableReceiptPrinting = "EnableReceiptPrinting";
    }
}
