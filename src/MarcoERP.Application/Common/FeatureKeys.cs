namespace MarcoERP.Application.Common
{
    /// <summary>
    /// Centralized constants for feature governance keys.
    /// Must match the keys seeded in FeatureSeed.
    /// </summary>
    public static class FeatureKeys
    {
        // ── Module Features ─────────────────────────────────────
        public const string Accounting = "Accounting";
        public const string Inventory = "Inventory";
        public const string Sales = "Sales";
        public const string Purchases = "Purchases";
        public const string Treasury = "Treasury";
        public const string POS = "POS";
        public const string Reporting = "Reporting";
        public const string UserManagement = "UserManagement";

        // ── Toggle Features ─────────────────────────────────────
        public const string AllowNegativeStock = "FEATURE_ALLOW_NEGATIVE_STOCK";
        public const string AllowNegativeCash = "FEATURE_ALLOW_NEGATIVE_CASH";
        public const string ReceiptPrinting = "FEATURE_RECEIPT_PRINTING";
    }
}
