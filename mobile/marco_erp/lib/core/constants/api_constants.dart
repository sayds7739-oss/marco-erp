class ApiConstants {
  // Configurable base URL — set from the login screen's server config or app settings.
  // Defaults to empty; the user must provide the server address before first use.
  static String baseUrl = '';

  /// Update the base URL at runtime (e.g. from settings or login screen).
  static void updateBaseUrl(String url) {
    baseUrl = url.trimRight().endsWith('/')
        ? url.trimRight().substring(0, url.trimRight().length - 1)
        : url.trim();
  }

  // Auth
  static const String login = '/auth/login';
  static const String changePassword = '/auth/change-password';

  // Products
  static const String products = '/products';
  static const String productSearch = '/products/search';

  // Categories
  static const String categories = '/categories';

  // Customers
  static const String customers = '/customers';
  static const String customerSearch = '/customers/search';

  // Suppliers
  static const String suppliers = '/suppliers';

  // Sales
  static const String salesInvoices = '/sales-invoices';
  static const String salesReturns = '/sales-returns';
  static const String salesQuotations = '/sales-quotations';
  static const String salesRepresentatives = '/sales-representatives';

  // Purchases
  static const String purchaseInvoices = '/purchase-invoices';
  static const String purchaseReturns = '/purchase-returns';
  static const String purchaseQuotations = '/purchase-quotations';

  // Inventory
  static const String inventoryAdjustments = '/inventory-adjustments';

  // Treasury
  static const String cashboxes = '/cashboxes';
  static const String cashReceipts = '/cash-receipts';
  static const String cashPayments = '/cash-payments';
  static const String cashTransfers = '/cash-transfers';
  static const String bankAccounts = '/bank-accounts';

  // Accounting
  static const String accounts = '/accounts';
  static const String journalEntries = '/journal-entries';
  static const String fiscalYears = '/fiscal-years';

  // Reports
  static const String reports = '/reports';
  static const String dashboard = '/dashboard';

  // Settings
  static const String systemSettings = '/system-settings';
  static const String features = '/features';
  static const String users = '/users';
  static const String roles = '/roles';
  static const String auditLogs = '/audit-logs';

  // Warehouses & Units
  static const String warehouses = '/warehouses';
  static const String units = '/units';

  // Sync
  static const String syncPull = '/sync/pull';
  static const String syncPush = '/sync/push';
  static const String syncRegisterDevice = '/sync/register-device';
  static const String syncStatus = '/sync/status';
}
