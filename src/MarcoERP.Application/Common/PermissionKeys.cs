namespace MarcoERP.Application.Common
{
    /// <summary>
    /// Centralized permission key constants.
    /// Matches the 41 permission keys seeded in SecuritySeed.
    /// AUTHZ-01: Authorization checked at Application layer.
    /// </summary>
    public static class PermissionKeys
    {
        // ── Accounting ────────────────────────────────────
        public const string AccountsView = "accounts.view";
        public const string AccountsCreate = "accounts.create";
        public const string AccountsEdit = "accounts.edit";
        public const string AccountsDelete = "accounts.delete";

        public const string JournalView = "journal.view";
        public const string JournalCreate = "journal.create";
        public const string JournalPost = "journal.post";
        public const string JournalReverse = "journal.reverse";

        public const string FiscalYearManage = "fiscalyear.manage";
        public const string FiscalPeriodManage = "fiscalperiod.manage";

        // ── Inventory ─────────────────────────────────────
        public const string InventoryView = "inventory.view";
        public const string InventoryManage = "inventory.manage";

        // ── Sales ─────────────────────────────────────────
        public const string SalesView = "sales.view";
        public const string SalesCreate = "sales.create";
        public const string SalesPost = "sales.post";

        // ── Sales Quotations ──────────────────────────────
        public const string SalesQuotationView = "salesquotation.view";
        public const string SalesQuotationCreate = "salesquotation.create";

        // ── Purchases ─────────────────────────────────────
        public const string PurchasesView = "purchases.view";
        public const string PurchasesCreate = "purchases.create";
        public const string PurchasesPost = "purchases.post";

        // ── Purchase Quotations ───────────────────────────
        public const string PurchaseQuotationView = "purchasequotation.view";
        public const string PurchaseQuotationCreate = "purchasequotation.create";

        // ── Treasury ──────────────────────────────────────
        public const string TreasuryView = "treasury.view";
        public const string TreasuryCreate = "treasury.create";
        public const string TreasuryPost = "treasury.post";

        // ── Reports ───────────────────────────────────────
        public const string ReportsView = "reports.view";

        // ── Settings & Admin ──────────────────────────────
        public const string SettingsManage = "settings.manage";
        public const string UsersManage = "users.manage";
        public const string RolesManage = "roles.manage";
        public const string AuditLogView = "auditlog.view";

        // ── POS ───────────────────────────────────────────
        public const string PosAccess = "pos.access";

        // ── Price Lists ───────────────────────────────────
        public const string PriceListView = "pricelist.view";
        public const string PriceListManage = "pricelist.manage";

        // ── Inventory Adjustment ──────────────────────────
        public const string InventoryAdjustmentView = "inventoryadjustment.view";
        public const string InventoryAdjustmentCreate = "inventoryadjustment.create";
        public const string InventoryAdjustmentPost = "inventoryadjustment.post";

        // ── Opening Balance ───────────────────────────────────
        public const string OpeningBalanceView = "openingbalance.view";
        public const string OpeningBalanceManage = "openingbalance.manage";

        // ── Governance (Phase 7) ──────────────────────────────
        /// <summary>
        /// Required to access the Governance Console.
        /// NOT assigned to any default role — must be granted explicitly.
        /// </summary>
        public const string GovernanceAccess = "governance.access";

        // ── Recycle Bin ─────────────────────────────────────
        public const string RecycleBinView = "recyclebin.view";
        public const string RecycleBinRestore = "recyclebin.restore";
    }
}
