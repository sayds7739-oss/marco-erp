import 'package:sqflite/sqflite.dart';
import 'package:path/path.dart' as p;

/// Central SQLite database for offline-first storage.
/// Mirrors server entities needed for mobile use.
class LocalDatabase {
  static const _dbName = 'marco_erp.db';
  static const _dbVersion = 1;

  Database? _db;

  Future<Database> get database async {
    _db ??= await _initDb();
    return _db!;
  }

  Future<Database> _initDb() async {
    final dbPath = await getDatabasesPath();
    final path = p.join(dbPath, _dbName);

    return openDatabase(
      path,
      version: _dbVersion,
      onCreate: _onCreate,
      onUpgrade: _onUpgrade,
    );
  }

  Future<void> _onCreate(Database db, int version) async {
    final batch = db.batch();

    // ═══════════════════════════════════════════
    // Sync metadata
    // ═══════════════════════════════════════════
    batch.execute('''
      CREATE TABLE sync_metadata (
        key TEXT PRIMARY KEY,
        value TEXT NOT NULL
      )
    ''');

    // ═══════════════════════════════════════════
    // Pending changes queue (offline writes)
    // ═══════════════════════════════════════════
    batch.execute('''
      CREATE TABLE pending_changes (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        entity_type TEXT NOT NULL,
        entity_id INTEGER,
        client_temp_id TEXT,
        operation TEXT NOT NULL,
        data TEXT NOT NULL,
        base_sync_version INTEGER NOT NULL DEFAULT 0,
        created_at TEXT NOT NULL,
        synced INTEGER NOT NULL DEFAULT 0
      )
    ''');

    batch.execute(
        'CREATE INDEX idx_pending_changes_synced ON pending_changes(synced)');

    // ═══════════════════════════════════════════
    // Inventory
    // ═══════════════════════════════════════════
    batch.execute('''
      CREATE TABLE products (
        id INTEGER PRIMARY KEY,
        code TEXT NOT NULL,
        name_ar TEXT NOT NULL,
        name_en TEXT,
        category_id INTEGER,
        base_unit_id INTEGER,
        wholesale_price REAL NOT NULL DEFAULT 0,
        retail_price REAL NOT NULL DEFAULT 0,
        weighted_average_cost REAL NOT NULL DEFAULT 0,
        is_active INTEGER NOT NULL DEFAULT 1,
        barcode TEXT,
        description TEXT,
        sync_version INTEGER NOT NULL DEFAULT 0,
        is_deleted INTEGER NOT NULL DEFAULT 0
      )
    ''');

    batch.execute('''
      CREATE TABLE categories (
        id INTEGER PRIMARY KEY,
        code TEXT NOT NULL,
        name_ar TEXT NOT NULL,
        name_en TEXT,
        parent_id INTEGER,
        is_active INTEGER NOT NULL DEFAULT 1,
        sync_version INTEGER NOT NULL DEFAULT 0,
        is_deleted INTEGER NOT NULL DEFAULT 0
      )
    ''');

    batch.execute('''
      CREATE TABLE units (
        id INTEGER PRIMARY KEY,
        code TEXT NOT NULL,
        name_ar TEXT NOT NULL,
        name_en TEXT,
        is_active INTEGER NOT NULL DEFAULT 1,
        sync_version INTEGER NOT NULL DEFAULT 0,
        is_deleted INTEGER NOT NULL DEFAULT 0
      )
    ''');

    batch.execute('''
      CREATE TABLE warehouses (
        id INTEGER PRIMARY KEY,
        code TEXT NOT NULL,
        name_ar TEXT NOT NULL,
        name_en TEXT,
        is_active INTEGER NOT NULL DEFAULT 1,
        sync_version INTEGER NOT NULL DEFAULT 0,
        is_deleted INTEGER NOT NULL DEFAULT 0
      )
    ''');

    // ═══════════════════════════════════════════
    // Sales
    // ═══════════════════════════════════════════
    batch.execute('''
      CREATE TABLE customers (
        id INTEGER PRIMARY KEY,
        code TEXT NOT NULL,
        name_ar TEXT NOT NULL,
        name_en TEXT,
        phone TEXT,
        email TEXT,
        address TEXT,
        is_active INTEGER NOT NULL DEFAULT 1,
        sync_version INTEGER NOT NULL DEFAULT 0,
        is_deleted INTEGER NOT NULL DEFAULT 0
      )
    ''');

    batch.execute('''
      CREATE TABLE sales_invoices (
        id INTEGER PRIMARY KEY,
        invoice_number TEXT,
        invoice_date TEXT,
        customer_id INTEGER,
        net_total REAL NOT NULL DEFAULT 0,
        vat_total REAL NOT NULL DEFAULT 0,
        grand_total REAL NOT NULL DEFAULT 0,
        status TEXT NOT NULL DEFAULT 'Draft',
        notes TEXT,
        sync_version INTEGER NOT NULL DEFAULT 0,
        is_deleted INTEGER NOT NULL DEFAULT 0
      )
    ''');

    batch.execute('''
      CREATE TABLE sales_invoice_lines (
        id INTEGER PRIMARY KEY,
        sales_invoice_id INTEGER NOT NULL,
        product_id INTEGER NOT NULL,
        quantity REAL NOT NULL DEFAULT 0,
        unit_price REAL NOT NULL DEFAULT 0,
        discount_amount REAL NOT NULL DEFAULT 0,
        vat_amount REAL NOT NULL DEFAULT 0,
        line_total REAL NOT NULL DEFAULT 0,
        sync_version INTEGER NOT NULL DEFAULT 0,
        is_deleted INTEGER NOT NULL DEFAULT 0
      )
    ''');

    // ═══════════════════════════════════════════
    // Purchases
    // ═══════════════════════════════════════════
    batch.execute('''
      CREATE TABLE suppliers (
        id INTEGER PRIMARY KEY,
        code TEXT NOT NULL,
        name_ar TEXT NOT NULL,
        name_en TEXT,
        phone TEXT,
        email TEXT,
        address TEXT,
        is_active INTEGER NOT NULL DEFAULT 1,
        sync_version INTEGER NOT NULL DEFAULT 0,
        is_deleted INTEGER NOT NULL DEFAULT 0
      )
    ''');

    batch.execute('''
      CREATE TABLE purchase_invoices (
        id INTEGER PRIMARY KEY,
        invoice_number TEXT,
        invoice_date TEXT,
        supplier_id INTEGER,
        net_total REAL NOT NULL DEFAULT 0,
        vat_total REAL NOT NULL DEFAULT 0,
        grand_total REAL NOT NULL DEFAULT 0,
        status TEXT NOT NULL DEFAULT 'Draft',
        notes TEXT,
        sync_version INTEGER NOT NULL DEFAULT 0,
        is_deleted INTEGER NOT NULL DEFAULT 0
      )
    ''');

    batch.execute('''
      CREATE TABLE purchase_invoice_lines (
        id INTEGER PRIMARY KEY,
        purchase_invoice_id INTEGER NOT NULL,
        product_id INTEGER NOT NULL,
        quantity REAL NOT NULL DEFAULT 0,
        unit_price REAL NOT NULL DEFAULT 0,
        discount_amount REAL NOT NULL DEFAULT 0,
        vat_amount REAL NOT NULL DEFAULT 0,
        line_total REAL NOT NULL DEFAULT 0,
        sync_version INTEGER NOT NULL DEFAULT 0,
        is_deleted INTEGER NOT NULL DEFAULT 0
      )
    ''');

    // ═══════════════════════════════════════════
    // Treasury
    // ═══════════════════════════════════════════
    batch.execute('''
      CREATE TABLE cashboxes (
        id INTEGER PRIMARY KEY,
        code TEXT NOT NULL,
        name_ar TEXT NOT NULL,
        current_balance REAL NOT NULL DEFAULT 0,
        is_default INTEGER NOT NULL DEFAULT 0,
        is_active INTEGER NOT NULL DEFAULT 1,
        sync_version INTEGER NOT NULL DEFAULT 0,
        is_deleted INTEGER NOT NULL DEFAULT 0
      )
    ''');

    batch.execute('''
      CREATE TABLE cash_receipts (
        id INTEGER PRIMARY KEY,
        receipt_number TEXT,
        receipt_date TEXT,
        amount REAL NOT NULL DEFAULT 0,
        customer_id INTEGER,
        cashbox_id INTEGER,
        status TEXT NOT NULL DEFAULT 'Draft',
        notes TEXT,
        sync_version INTEGER NOT NULL DEFAULT 0,
        is_deleted INTEGER NOT NULL DEFAULT 0
      )
    ''');

    batch.execute('''
      CREATE TABLE cash_payments (
        id INTEGER PRIMARY KEY,
        payment_number TEXT,
        payment_date TEXT,
        amount REAL NOT NULL DEFAULT 0,
        supplier_id INTEGER,
        cashbox_id INTEGER,
        status TEXT NOT NULL DEFAULT 'Draft',
        notes TEXT,
        sync_version INTEGER NOT NULL DEFAULT 0,
        is_deleted INTEGER NOT NULL DEFAULT 0
      )
    ''');

    batch.execute('''
      CREATE TABLE bank_accounts (
        id INTEGER PRIMARY KEY,
        code TEXT NOT NULL,
        name_ar TEXT NOT NULL,
        bank_name TEXT,
        account_number TEXT,
        current_balance REAL NOT NULL DEFAULT 0,
        is_active INTEGER NOT NULL DEFAULT 1,
        sync_version INTEGER NOT NULL DEFAULT 0,
        is_deleted INTEGER NOT NULL DEFAULT 0
      )
    ''');

    // Create indexes for sync_version queries
    for (final table in [
      'products',
      'categories',
      'units',
      'warehouses',
      'customers',
      'sales_invoices',
      'sales_invoice_lines',
      'suppliers',
      'purchase_invoices',
      'purchase_invoice_lines',
      'cashboxes',
      'cash_receipts',
      'cash_payments',
      'bank_accounts',
    ]) {
      batch.execute(
          'CREATE INDEX idx_${table}_sync ON $table(sync_version)');
    }

    await batch.commit(noResult: true);
  }

  Future<void> _onUpgrade(Database db, int oldVersion, int newVersion) async {
    // Future migrations go here
  }

  // ═══════════════════════════════════════════
  // Sync metadata helpers
  // ═══════════════════════════════════════════

  Future<String?> getMeta(String key) async {
    final db = await database;
    final rows = await db.query('sync_metadata',
        where: 'key = ?', whereArgs: [key], limit: 1);
    if (rows.isEmpty) return null;
    return rows.first['value'] as String?;
  }

  Future<void> setMeta(String key, String value) async {
    final db = await database;
    await db.insert('sync_metadata', {'key': key, 'value': value},
        conflictAlgorithm: ConflictAlgorithm.replace);
  }

  Future<int> getLastSyncVersion() async {
    final v = await getMeta('last_sync_version');
    return v != null ? int.parse(v) : 0;
  }

  Future<void> setLastSyncVersion(int version) async {
    await setMeta('last_sync_version', version.toString());
  }

  Future<String?> getDeviceId() async => getMeta('device_id');

  Future<void> setDeviceId(String id) async => setMeta('device_id', id);

  // ═══════════════════════════════════════════
  // Pending changes queue
  // ═══════════════════════════════════════════

  Future<int> addPendingChange({
    required String entityType,
    int? entityId,
    String? clientTempId,
    required String operation,
    required String data,
    int baseSyncVersion = 0,
  }) async {
    final db = await database;
    return db.insert('pending_changes', {
      'entity_type': entityType,
      'entity_id': entityId,
      'client_temp_id': clientTempId,
      'operation': operation,
      'data': data,
      'base_sync_version': baseSyncVersion,
      'created_at': DateTime.now().toIso8601String(),
      'synced': 0,
    });
  }

  Future<List<Map<String, dynamic>>> getPendingChanges() async {
    final db = await database;
    return db.query('pending_changes',
        where: 'synced = ?', whereArgs: [0], orderBy: 'id ASC');
  }

  Future<void> markChangesSynced(List<int> ids) async {
    if (ids.isEmpty) return;
    final db = await database;
    final placeholders = ids.map((_) => '?').join(',');
    await db.rawUpdate(
        'UPDATE pending_changes SET synced = 1 WHERE id IN ($placeholders)',
        ids);
  }

  Future<void> clearSyncedChanges() async {
    final db = await database;
    await db.delete('pending_changes', where: 'synced = ?', whereArgs: [1]);
  }

  // ═══════════════════════════════════════════
  // Generic CRUD for synced entities
  // ═══════════════════════════════════════════

  Future<void> upsertEntity(
      String table, int id, Map<String, dynamic> data) async {
    final db = await database;
    data['id'] = id;
    await db.insert(table, data, conflictAlgorithm: ConflictAlgorithm.replace);
  }

  Future<void> upsertEntities(
      String table, List<Map<String, dynamic>> rows) async {
    final db = await database;
    final batch = db.batch();
    for (final row in rows) {
      batch.insert(table, row, conflictAlgorithm: ConflictAlgorithm.replace);
    }
    await batch.commit(noResult: true);
  }

  Future<List<Map<String, dynamic>>> getAll(String table,
      {bool includeDeleted = false}) async {
    final db = await database;
    if (includeDeleted) {
      return db.query(table, orderBy: 'id ASC');
    }
    return db.query(table,
        where: 'is_deleted = ?', whereArgs: [0], orderBy: 'id ASC');
  }

  Future<Map<String, dynamic>?> getById(String table, int id) async {
    final db = await database;
    final rows =
        await db.query(table, where: 'id = ?', whereArgs: [id], limit: 1);
    return rows.isEmpty ? null : rows.first;
  }

  /// Allowed search columns per table — prevents SQL injection via column interpolation.
  static const _validSearchColumns = {
    'products': ['name_ar', 'name_en', 'code', 'barcode', 'description'],
    'categories': ['name_ar', 'name_en', 'code'],
    'units': ['name_ar', 'name_en', 'code'],
    'warehouses': ['name_ar', 'name_en', 'code'],
    'customers': ['name_ar', 'name_en', 'code', 'phone', 'email'],
    'suppliers': ['name_ar', 'name_en', 'code', 'phone', 'email'],
    'sales_invoices': ['invoice_number', 'notes'],
    'purchase_invoices': ['invoice_number', 'notes'],
    'cashboxes': ['name_ar', 'name_en', 'code'],
    'cash_receipts': ['receipt_number', 'notes'],
    'cash_payments': ['payment_number', 'notes'],
    'bank_accounts': ['name_ar', 'name_en', 'code'],
  };

  Future<List<Map<String, dynamic>>> search(
      String table, String column, String query) async {
    // Validate column name against whitelist to prevent SQL injection
    final allowed = _validSearchColumns[table];
    if (allowed == null || !allowed.contains(column)) {
      throw ArgumentError('Invalid search column "$column" for table "$table"');
    }

    final db = await database;
    return db.query(table,
        where: 'is_deleted = 0 AND $column LIKE ?',
        whereArgs: ['%$query%'],
        orderBy: 'id ASC');
  }

  Future<void> close() async {
    final db = _db;
    if (db != null && db.isOpen) {
      await db.close();
      _db = null;
    }
  }

  /// Get the minimum (most negative) entity_id from pending_changes table.
  /// Used to resume the temp ID counter after app restart.
  Future<int?> getMinTempId() async {
    final db = await database;
    final result = await db.rawQuery(
        'SELECT MIN(entity_id) as min_id FROM pending_changes WHERE entity_id < 0');
    if (result.isEmpty || result.first['min_id'] == null) return null;
    return result.first['min_id'] as int;
  }

  /// Purge tombstones (is_deleted=1) that have been synced (sync_version > 0).
  /// Prevents SQLite bloat from accumulating soft-deleted records.
  Future<int> purgeTombstones({int days = 30}) async {
    final db = await database;
    int total = 0;
    // Only purge from entity tables that have sync_version and is_deleted columns
    // We purge records that are deleted AND have been synced to the server
    for (final table in [
      'products', 'categories', 'units', 'warehouses',
      'customers', 'sales_invoices', 'sales_invoice_lines',
      'suppliers', 'purchase_invoices', 'purchase_invoice_lines',
      'cashboxes', 'cash_receipts', 'cash_payments', 'bank_accounts',
    ]) {
      final count = await db.delete(
        table,
        where: 'is_deleted = 1 AND sync_version > 0',
      );
      total += count;
    }
    return total;
  }
}
