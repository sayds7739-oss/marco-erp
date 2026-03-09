/// Centralized permission key constants for the mobile app.
/// Mirrors the server-side PermissionKeys (MarcoERP.Application.Common.PermissionKeys).
class PermissionKeys {
  PermissionKeys._(); // prevent instantiation

  // -- Accounting ------------------------------------------------
  static const accountsView = 'accounts.view';
  static const accountsCreate = 'accounts.create';
  static const accountsEdit = 'accounts.edit';
  static const accountsDelete = 'accounts.delete';

  static const journalView = 'journal.view';
  static const journalCreate = 'journal.create';
  static const journalPost = 'journal.post';
  static const journalReverse = 'journal.reverse';

  static const fiscalYearManage = 'fiscalyear.manage';
  static const fiscalPeriodManage = 'fiscalperiod.manage';

  // -- Inventory -------------------------------------------------
  static const inventoryView = 'inventory.view';
  static const inventoryManage = 'inventory.manage';

  // -- Sales -----------------------------------------------------
  static const salesView = 'sales.view';
  static const salesCreate = 'sales.create';
  static const salesPost = 'sales.post';

  // -- Sales Quotations ------------------------------------------
  static const salesQuotationView = 'salesquotation.view';
  static const salesQuotationCreate = 'salesquotation.create';

  // -- Purchases -------------------------------------------------
  static const purchasesView = 'purchases.view';
  static const purchasesCreate = 'purchases.create';
  static const purchasesPost = 'purchases.post';

  // -- Purchase Quotations ---------------------------------------
  static const purchaseQuotationView = 'purchasequotation.view';
  static const purchaseQuotationCreate = 'purchasequotation.create';

  // -- Treasury --------------------------------------------------
  static const treasuryView = 'treasury.view';
  static const treasuryCreate = 'treasury.create';
  static const treasuryPost = 'treasury.post';

  // -- Reports ---------------------------------------------------
  static const reportsView = 'reports.view';

  // -- Settings & Admin ------------------------------------------
  static const settingsManage = 'settings.manage';
  static const usersManage = 'users.manage';
  static const rolesManage = 'roles.manage';
  static const auditLogView = 'auditlog.view';

  // -- POS -------------------------------------------------------
  static const posAccess = 'pos.access';

  // -- Price Lists -----------------------------------------------
  static const priceListView = 'pricelist.view';
  static const priceListManage = 'pricelist.manage';

  // -- Inventory Adjustment --------------------------------------
  static const inventoryAdjustmentView = 'inventoryadjustment.view';
  static const inventoryAdjustmentCreate = 'inventoryadjustment.create';
  static const inventoryAdjustmentPost = 'inventoryadjustment.post';

  // -- Opening Balance -------------------------------------------
  static const openingBalanceView = 'openingbalance.view';
  static const openingBalanceManage = 'openingbalance.manage';

  // -- Governance ------------------------------------------------
  static const governanceAccess = 'governance.access';

  // -- Recycle Bin -----------------------------------------------
  static const recycleBinView = 'recyclebin.view';
  static const recycleBinRestore = 'recyclebin.restore';
}
