import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/auth/auth_provider.dart';
import '../../../core/auth/permission_keys.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';
import '../../../core/models/dashboard_model.dart';
import '../../../widgets/sync_status_widget.dart';
// Products
import '../../products/screens/products_screen.dart';
import '../../products/screens/product_detail_screen.dart';
// Customers
import '../../customers/screens/customers_screen.dart';
import '../../customers/screens/customer_detail_screen.dart';
// Sales
import '../../sales/screens/sales_invoices_screen.dart';
import '../../sales/screens/sales_invoice_create_screen.dart';
import '../../sales/screens/sales_returns_screen.dart';
import '../../sales/screens/sales_quotations_screen.dart';
import '../../sales/screens/sales_representatives_screen.dart';
// Suppliers
import '../../suppliers/screens/suppliers_screen.dart';
// Purchases
import '../../purchases/screens/purchase_invoices_screen.dart';
import '../../purchases/screens/purchase_returns_screen.dart';
import '../../purchases/screens/purchase_quotations_screen.dart';
// Treasury
import '../../treasury/screens/cashboxes_screen.dart';
import '../../treasury/screens/cash_receipts_screen.dart';
import '../../treasury/screens/cash_receipt_create_screen.dart';
import '../../treasury/screens/cash_payments_screen.dart';
import '../../treasury/screens/cash_payment_create_screen.dart';
import '../../treasury/screens/cash_transfers_screen.dart';
import '../../treasury/screens/bank_accounts_screen.dart';
// Inventory
import '../../inventory/screens/categories_screen.dart';
import '../../inventory/screens/warehouses_screen.dart';
import '../../inventory/screens/units_screen.dart';
import '../../inventory/screens/inventory_adjustments_screen.dart';
// Reports
import '../../reports/screens/reports_screen.dart';
// Settings
import '../../settings/screens/settings_screen.dart';
// Admin
import '../../admin/screens/users_screen.dart';
import '../../admin/screens/roles_screen.dart';
import '../../admin/screens/audit_log_screen.dart';
// Accounting
import '../../accounting/screens/chart_of_accounts_screen.dart';
import '../../accounting/screens/journal_entries_screen.dart';

class DashboardScreen extends StatefulWidget {
  const DashboardScreen({super.key});

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  DashboardModel? _dashboard;
  bool _isLoading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadDashboard();
  }

  Future<void> _loadDashboard() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    final api = context.read<ApiClient>();
    final response = await api.get<DashboardModel>(
      ApiConstants.dashboard,
      fromJson: (json) => DashboardModel.fromJson(json as Map<String, dynamic>),
    );

    if (mounted) {
      setState(() {
        _isLoading = false;
        if (response.success && response.data != null) {
          _dashboard = response.data;
        } else {
          _error = response.errorMessage;
        }
      });
    }
  }

  void _navigateTo(Widget screen) {
    Navigator.of(context).push(MaterialPageRoute(builder: (_) => screen));
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();

    return Scaffold(
      appBar: AppBar(
        title: const Text('ماركو ERP'),
        actions: [
          const SyncStatusWidget(),
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadDashboard,
          ),
        ],
      ),
      drawer: _buildDrawer(auth),
      floatingActionButton: _buildFab(),
      body: RefreshIndicator(
        onRefresh: _loadDashboard,
        child: _isLoading
            ? const Center(child: CircularProgressIndicator())
            : _error != null
                ? _buildError()
                : _buildContent(),
      ),
    );
  }

  Widget _buildFab() {
    return FloatingActionButton(
      onPressed: () => _showQuickActions(),
      child: const Icon(Icons.add),
    );
  }

  void _showQuickActions() {
    final auth = context.read<AuthProvider>();

    // Build the list of quick-action tiles, filtered by permission.
    final actions = <Widget>[
      if (auth.hasPermission(PermissionKeys.salesCreate))
        ListTile(
          leading: Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: Colors.green.shade100,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Icon(Icons.receipt_long, color: Colors.green.shade700),
          ),
          title: const Text('فاتورة بيع'),
          onTap: () {
            Navigator.pop(context);
            _navigateTo(const SalesInvoiceCreateScreen());
          },
        ),
      if (auth.hasPermission(PermissionKeys.treasuryCreate))
        ListTile(
          leading: Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: Colors.blue.shade100,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Icon(Icons.payments, color: Colors.blue.shade700),
          ),
          title: const Text('سند قبض'),
          onTap: () {
            Navigator.pop(context);
            _navigateTo(const CashReceiptCreateScreen());
          },
        ),
      if (auth.hasPermission(PermissionKeys.treasuryCreate))
        ListTile(
          leading: Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: Colors.red.shade100,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Icon(Icons.payments_outlined, color: Colors.red.shade700),
          ),
          title: const Text('سند صرف'),
          onTap: () {
            Navigator.pop(context);
            _navigateTo(const CashPaymentCreateScreen());
          },
        ),
      if (auth.hasPermission(PermissionKeys.inventoryManage))
        ListTile(
          leading: Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: Colors.amber.shade100,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Icon(Icons.inventory_2, color: Colors.amber.shade700),
          ),
          title: const Text('صنف جديد'),
          onTap: () {
            Navigator.pop(context);
            _navigateTo(const ProductDetailScreen());
          },
        ),
      if (auth.hasPermission(PermissionKeys.salesCreate))
        ListTile(
          leading: Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: Colors.purple.shade100,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Icon(Icons.person_add, color: Colors.purple.shade700),
          ),
          title: const Text('عميل جديد'),
          onTap: () {
            Navigator.pop(context);
            _navigateTo(const CustomerDetailScreen());
          },
        ),
    ];

    // If the user has no create permissions at all, don't show the sheet.
    if (actions.isEmpty) return;

    showModalBottomSheet(
      context: context,
      builder: (_) => Container(
        padding: const EdgeInsets.all(16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text(
              'إنشاء جديد',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 16),
            ...actions,
          ],
        ),
      ),
    );
  }

  Widget _buildContent() {
    final d = _dashboard;
    final auth = context.read<AuthProvider>();

    // Build permission-filtered quick-action buttons for the grid.
    final quickActions = <Widget>[
      if (auth.hasPermission(PermissionKeys.salesCreate))
        _quickAction('فاتورة بيع', Icons.receipt_long, Colors.green.shade400,
            () => _navigateTo(const SalesInvoiceCreateScreen())),
      if (auth.hasPermission(PermissionKeys.treasuryCreate))
        _quickAction('سند قبض', Icons.payments, Colors.blue.shade400,
            () => _navigateTo(const CashReceiptCreateScreen())),
      if (auth.hasPermission(PermissionKeys.inventoryView))
        _quickAction('الأصناف', Icons.inventory_2, Colors.amber.shade600,
            () => _navigateTo(const ProductsScreen())),
      if (auth.hasPermission(PermissionKeys.salesView))
        _quickAction('العملاء', Icons.people, Colors.purple.shade400,
            () => _navigateTo(const CustomersScreen())),
    ];

    // Build permission-filtered summary cards.
    final summaryCards = <Widget>[
      if (auth.hasPermission(PermissionKeys.salesView))
        _summaryCard('المبيعات اليوم', d?.totalSalesToday ?? 0, Icons.trending_up, Colors.green),
      if (auth.hasPermission(PermissionKeys.purchasesView))
        _summaryCard('المشتريات اليوم', d?.totalPurchasesToday ?? 0, Icons.shopping_cart, Colors.orange),
      if (auth.hasPermission(PermissionKeys.treasuryView))
        _summaryCard('رصيد الخزنة', d?.totalCashBalance ?? 0, Icons.account_balance_wallet, Colors.blue),
      if (auth.hasPermission(PermissionKeys.reportsView))
        _summaryCard('صافي الربح', d?.dailyNetProfit ?? 0, Icons.bar_chart, Colors.purple),
    ];

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        // Welcome
        Text(
          'مرحباً ${auth.userName}',
          style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                fontWeight: FontWeight.bold,
              ),
        ),
        const SizedBox(height: 4),
        Text(
          auth.roleName,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                color: Colors.grey,
              ),
        ),
        const SizedBox(height: 20),

        // Summary Cards (only those the user is allowed to see)
        if (summaryCards.isNotEmpty) ...[
          GridView.count(
            crossAxisCount: 2,
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            mainAxisSpacing: 12,
            crossAxisSpacing: 12,
            childAspectRatio: 1.4,
            children: summaryCards,
          ),
          const SizedBox(height: 24),
        ],

        // Quick Actions (only those the user is allowed to perform)
        if (quickActions.isNotEmpty) ...[
          Text(
            'إجراءات سريعة',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
          ),
          const SizedBox(height: 12),
          GridView.count(
            crossAxisCount: 4,
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            mainAxisSpacing: 8,
            crossAxisSpacing: 8,
            children: quickActions,
          ),
          const SizedBox(height: 24),
        ],

        // Stats row
        if (d != null) ...[
          Text(
            'إحصائيات',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
          ),
          const SizedBox(height: 12),
          if (auth.hasPermission(PermissionKeys.inventoryView))
            _statTile('إجمالي الأصناف', '${d.totalProducts}', Icons.inventory_2_outlined),
          if (auth.hasPermission(PermissionKeys.salesView))
            _statTile('إجمالي العملاء', '${d.totalCustomers}', Icons.people_outlined),
          if (auth.hasPermission(PermissionKeys.purchasesView))
            _statTile('إجمالي الموردين', '${d.totalSuppliers}', Icons.local_shipping_outlined),
          if (auth.hasPermission(PermissionKeys.inventoryView))
            _statTile('أصناف أقل من الحد', '${d.lowStockProducts}', Icons.warning_amber,
                isWarning: d.lowStockProducts > 0),
        ],
      ],
    );
  }

  Widget _summaryCard(String title, double value, IconData icon, Color color) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Row(
              children: [
                Icon(icon, color: color, size: 20),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(
                    title,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: Colors.grey.shade600,
                        ),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ],
            ),
            Text(
              _formatCurrency(value),
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: color,
                  ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _quickAction(String label, IconData icon, Color color, VoidCallback onTap) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: color.withOpacity(0.1),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Icon(icon, color: color, size: 28),
          ),
          const SizedBox(height: 6),
          Text(
            label,
            style: Theme.of(context).textTheme.bodySmall,
            textAlign: TextAlign.center,
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ),
    );
  }

  Widget _statTile(String label, String value, IconData icon, {bool isWarning = false}) {
    return ListTile(
      leading: Icon(icon, color: isWarning ? Colors.orange : Colors.grey),
      title: Text(label),
      trailing: Text(
        value,
        style: TextStyle(
          fontWeight: FontWeight.bold,
          color: isWarning ? Colors.orange : null,
        ),
      ),
    );
  }

  Widget _buildError() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(Icons.error_outline, size: 64, color: Colors.grey),
          const SizedBox(height: 16),
          Text(_error ?? 'حدث خطأ', style: const TextStyle(color: Colors.grey)),
          const SizedBox(height: 16),
          ElevatedButton.icon(
            onPressed: _loadDashboard,
            icon: const Icon(Icons.refresh),
            label: const Text('إعادة المحاولة'),
          ),
        ],
      ),
    );
  }

  Widget _buildDrawer(AuthProvider auth) {
    return Drawer(
      child: ListView(
        padding: EdgeInsets.zero,
        children: [
          DrawerHeader(
            decoration: BoxDecoration(
              color: Theme.of(context).colorScheme.primary,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                const CircleAvatar(
                  radius: 30,
                  backgroundColor: Colors.white24,
                  child: Icon(Icons.person, size: 36, color: Colors.white),
                ),
                const SizedBox(height: 8),
                Text(
                  auth.userName,
                  style: const TextStyle(color: Colors.white, fontSize: 16, fontWeight: FontWeight.bold),
                ),
                Text(
                  auth.roleName,
                  style: const TextStyle(color: Colors.white70, fontSize: 13),
                ),
              ],
            ),
          ),
          // Dashboard is always visible
          _drawerItem('لوحة التحكم', Icons.dashboard, () => Navigator.pop(context)),
          const Divider(),

          // ── Sales Section ───────────────────────────────────
          if (_hasAny(auth, [PermissionKeys.salesView, PermissionKeys.salesQuotationView])) ...[
            _drawerSection('المبيعات'),
            if (auth.hasPermission(PermissionKeys.salesView))
              _drawerItem('فواتير البيع', Icons.receipt_long, () => _navigateFromDrawer(const SalesInvoicesScreen())),
            if (auth.hasPermission(PermissionKeys.salesView))
              _drawerItem('مرتجعات المبيعات', Icons.assignment_return, () => _navigateFromDrawer(const SalesReturnsScreen())),
            if (auth.hasPermission(PermissionKeys.salesQuotationView))
              _drawerItem('عروض الأسعار', Icons.request_quote, () => _navigateFromDrawer(const SalesQuotationsScreen())),
            if (auth.hasPermission(PermissionKeys.salesView))
              _drawerItem('العملاء', Icons.people, () => _navigateFromDrawer(const CustomersScreen())),
            if (auth.hasPermission(PermissionKeys.salesView))
              _drawerItem('مندوبي المبيعات', Icons.badge, () => _navigateFromDrawer(const SalesRepresentativesScreen())),
            const Divider(),
          ],

          // ── Purchases Section ───────────────────────────────
          if (_hasAny(auth, [PermissionKeys.purchasesView, PermissionKeys.purchaseQuotationView])) ...[
            _drawerSection('المشتريات'),
            if (auth.hasPermission(PermissionKeys.purchasesView))
              _drawerItem('فواتير المشتريات', Icons.shopping_cart, () => _navigateFromDrawer(const PurchaseInvoicesScreen())),
            if (auth.hasPermission(PermissionKeys.purchasesView))
              _drawerItem('مرتجعات المشتريات', Icons.assignment_return_outlined, () => _navigateFromDrawer(const PurchaseReturnsScreen())),
            if (auth.hasPermission(PermissionKeys.purchaseQuotationView))
              _drawerItem('عروض أسعار المشتريات', Icons.request_quote_outlined, () => _navigateFromDrawer(const PurchaseQuotationsScreen())),
            if (auth.hasPermission(PermissionKeys.purchasesView))
              _drawerItem('الموردين', Icons.local_shipping, () => _navigateFromDrawer(const SuppliersScreen())),
            const Divider(),
          ],

          // ── Inventory Section ───────────────────────────────
          if (_hasAny(auth, [PermissionKeys.inventoryView, PermissionKeys.inventoryAdjustmentView])) ...[
            _drawerSection('المخزون'),
            if (auth.hasPermission(PermissionKeys.inventoryView))
              _drawerItem('الأصناف', Icons.inventory_2, () => _navigateFromDrawer(const ProductsScreen())),
            if (auth.hasPermission(PermissionKeys.inventoryView))
              _drawerItem('التصنيفات', Icons.category, () => _navigateFromDrawer(const CategoriesScreen())),
            if (auth.hasPermission(PermissionKeys.inventoryView))
              _drawerItem('المخازن', Icons.warehouse, () => _navigateFromDrawer(const WarehousesScreen())),
            if (auth.hasPermission(PermissionKeys.inventoryView))
              _drawerItem('الوحدات', Icons.straighten, () => _navigateFromDrawer(const UnitsScreen())),
            if (auth.hasPermission(PermissionKeys.inventoryAdjustmentView))
              _drawerItem('تسويات المخزون', Icons.tune, () => _navigateFromDrawer(const InventoryAdjustmentsScreen())),
            const Divider(),
          ],

          // ── Treasury Section ────────────────────────────────
          if (auth.hasPermission(PermissionKeys.treasuryView)) ...[
            _drawerSection('الخزينة'),
            _drawerItem('الخزن', Icons.account_balance_wallet, () => _navigateFromDrawer(const CashboxesScreen())),
            _drawerItem('سندات القبض', Icons.payments, () => _navigateFromDrawer(const CashReceiptsScreen())),
            _drawerItem('سندات الصرف', Icons.payments_outlined, () => _navigateFromDrawer(const CashPaymentsScreen())),
            _drawerItem('التحويلات', Icons.swap_horiz, () => _navigateFromDrawer(const CashTransfersScreen())),
            _drawerItem('الحسابات البنكية', Icons.account_balance, () => _navigateFromDrawer(const BankAccountsScreen())),
            const Divider(),
          ],

          // ── Reports Section ─────────────────────────────────
          if (auth.hasPermission(PermissionKeys.reportsView)) ...[
            _drawerItem('التقارير', Icons.analytics, () => _navigateFromDrawer(const ReportsScreen())),
            const Divider(),
          ],

          // ── Accounting Section ──────────────────────────────
          if (_hasAny(auth, [PermissionKeys.accountsView, PermissionKeys.journalView])) ...[
            _drawerSection('المحاسبة'),
            if (auth.hasPermission(PermissionKeys.accountsView))
              _drawerItem('دليل الحسابات', Icons.account_tree, () => _navigateFromDrawer(const ChartOfAccountsScreen())),
            if (auth.hasPermission(PermissionKeys.journalView))
              _drawerItem('القيود اليومية', Icons.description, () => _navigateFromDrawer(const JournalEntriesScreen())),
            const Divider(),
          ],

          // ── Admin Section ───────────────────────────────────
          if (_hasAny(auth, [PermissionKeys.usersManage, PermissionKeys.rolesManage, PermissionKeys.auditLogView])) ...[
            _drawerSection('الإدارة'),
            if (auth.hasPermission(PermissionKeys.usersManage))
              _drawerItem('إدارة المستخدمين', Icons.people_outline, () => _navigateFromDrawer(const UsersScreen())),
            if (auth.hasPermission(PermissionKeys.rolesManage))
              _drawerItem('إدارة الأدوار', Icons.admin_panel_settings, () => _navigateFromDrawer(const RolesScreen())),
            if (auth.hasPermission(PermissionKeys.auditLogView))
              _drawerItem('سجل المراجعة', Icons.history, () => _navigateFromDrawer(const AuditLogScreen())),
            const Divider(),
          ],

          // ── Settings (always-visible) & Logout ──────────────
          if (auth.hasPermission(PermissionKeys.settingsManage))
            _drawerItem('الإعدادات', Icons.settings, () => _navigateFromDrawer(const SettingsScreen())),
          _drawerItem(
            'تسجيل الخروج',
            Icons.logout,
            () async {
              await auth.logout();
              if (mounted) Navigator.pop(context);
            },
            color: Colors.red,
          ),
        ],
      ),
    );
  }

  /// Returns true when the user has at least one of the given permission keys.
  bool _hasAny(AuthProvider auth, List<String> keys) {
    return keys.any((k) => auth.hasPermission(k));
  }

  Widget _drawerSection(String title) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
      child: Text(
        title,
        style: TextStyle(
          color: Colors.grey.shade600,
          fontWeight: FontWeight.bold,
          fontSize: 12,
        ),
      ),
    );
  }

  Widget _drawerItem(String title, IconData icon, VoidCallback onTap, {Color? color}) {
    return ListTile(
      leading: Icon(icon, color: color),
      title: Text(title, style: TextStyle(color: color)),
      onTap: onTap,
    );
  }

  void _navigateFromDrawer(Widget screen) {
    Navigator.pop(context); // close drawer
    _navigateTo(screen);
  }

  String _formatCurrency(double amount) {
    final formatted = amount.toStringAsFixed(2);
    return '$formatted ج.م';
  }
}
