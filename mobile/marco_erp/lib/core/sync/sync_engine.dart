import 'dart:async';
import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:uuid/uuid.dart';
import '../api/api_client.dart';
import '../database/local_database.dart';
import '../network/connectivity_service.dart';

/// Maps server entity type names to local SQLite table names.
const Map<String, String> _entityTableMap = {
  'Product': 'products',
  'Category': 'categories',
  'Unit': 'units',
  'Warehouse': 'warehouses',
  'Customer': 'customers',
  'SalesInvoice': 'sales_invoices',
  'SalesInvoiceLine': 'sales_invoice_lines',
  'Supplier': 'suppliers',
  'PurchaseInvoice': 'purchase_invoices',
  'PurchaseInvoiceLine': 'purchase_invoice_lines',
  'Cashbox': 'cashboxes',
  'CashReceipt': 'cash_receipts',
  'CashPayment': 'cash_payments',
  'BankAccount': 'bank_accounts',
};

/// Sync priority tiers — lower number = pulled/pushed first.
/// Tier 1: Master data (referenced by everything else).
/// Tier 2: Transaction headers (reference master data).
/// Tier 3: Transaction lines (reference headers).
const Map<String, int> _syncPriority = {
  'Category': 1,
  'Unit': 1,
  'Warehouse': 1,
  'Product': 1,
  'Customer': 1,
  'Supplier': 1,
  'Cashbox': 1,
  'BankAccount': 1,
  'SalesInvoice': 2,
  'PurchaseInvoice': 2,
  'CashReceipt': 2,
  'CashPayment': 2,
  'SalesInvoiceLine': 3,
  'PurchaseInvoiceLine': 3,
};

/// Returns entity type names sorted by sync priority (master data first).
List<String> _sortedEntityTypes(Iterable<String> types) {
  final list = types.toList();
  list.sort((a, b) =>
      (_syncPriority[a] ?? 99).compareTo(_syncPriority[b] ?? 99));
  return list;
}

/// Maps server JSON camelCase keys to local SQLite snake_case columns per table.
const Map<String, Map<String, String>> _fieldMappings = {
  'products': {
    'id': 'id',
    'code': 'code',
    'nameAr': 'name_ar',
    'nameEn': 'name_en',
    'categoryId': 'category_id',
    'baseUnitId': 'base_unit_id',
    'wholesalePrice': 'wholesale_price',
    'retailPrice': 'retail_price',
    'weightedAverageCost': 'weighted_average_cost',
    'isActive': 'is_active',
    'barcode': 'barcode',
    'description': 'description',
    'syncVersion': 'sync_version',
    'isDeleted': 'is_deleted',
  },
  'categories': {
    'id': 'id',
    'code': 'code',
    'nameAr': 'name_ar',
    'nameEn': 'name_en',
    'parentId': 'parent_id',
    'isActive': 'is_active',
    'syncVersion': 'sync_version',
    'isDeleted': 'is_deleted',
  },
  'units': {
    'id': 'id',
    'code': 'code',
    'nameAr': 'name_ar',
    'nameEn': 'name_en',
    'isActive': 'is_active',
    'syncVersion': 'sync_version',
    'isDeleted': 'is_deleted',
  },
  'warehouses': {
    'id': 'id',
    'code': 'code',
    'nameAr': 'name_ar',
    'nameEn': 'name_en',
    'isActive': 'is_active',
    'syncVersion': 'sync_version',
    'isDeleted': 'is_deleted',
  },
  'customers': {
    'id': 'id',
    'code': 'code',
    'nameAr': 'name_ar',
    'nameEn': 'name_en',
    'phone': 'phone',
    'email': 'email',
    'address': 'address',
    'isActive': 'is_active',
    'syncVersion': 'sync_version',
    'isDeleted': 'is_deleted',
  },
  'sales_invoices': {
    'id': 'id',
    'invoiceNumber': 'invoice_number',
    'invoiceDate': 'invoice_date',
    'customerId': 'customer_id',
    'netTotal': 'net_total',
    'vatTotal': 'vat_total',
    'grandTotal': 'grand_total',
    'status': 'status',
    'notes': 'notes',
    'syncVersion': 'sync_version',
    'isDeleted': 'is_deleted',
  },
  'sales_invoice_lines': {
    'id': 'id',
    'salesInvoiceId': 'sales_invoice_id',
    'productId': 'product_id',
    'quantity': 'quantity',
    'unitPrice': 'unit_price',
    'discountAmount': 'discount_amount',
    'vatAmount': 'vat_amount',
    'lineTotal': 'line_total',
    'syncVersion': 'sync_version',
    'isDeleted': 'is_deleted',
  },
  'suppliers': {
    'id': 'id',
    'code': 'code',
    'nameAr': 'name_ar',
    'nameEn': 'name_en',
    'phone': 'phone',
    'email': 'email',
    'address': 'address',
    'isActive': 'is_active',
    'syncVersion': 'sync_version',
    'isDeleted': 'is_deleted',
  },
  'purchase_invoices': {
    'id': 'id',
    'invoiceNumber': 'invoice_number',
    'invoiceDate': 'invoice_date',
    'supplierId': 'supplier_id',
    'netTotal': 'net_total',
    'vatTotal': 'vat_total',
    'grandTotal': 'grand_total',
    'status': 'status',
    'notes': 'notes',
    'syncVersion': 'sync_version',
    'isDeleted': 'is_deleted',
  },
  'purchase_invoice_lines': {
    'id': 'id',
    'purchaseInvoiceId': 'purchase_invoice_id',
    'productId': 'product_id',
    'quantity': 'quantity',
    'unitPrice': 'unit_price',
    'discountAmount': 'discount_amount',
    'vatAmount': 'vat_amount',
    'lineTotal': 'line_total',
    'syncVersion': 'sync_version',
    'isDeleted': 'is_deleted',
  },
  'cashboxes': {
    'id': 'id',
    'code': 'code',
    'nameAr': 'name_ar',
    'currentBalance': 'current_balance',
    'isDefault': 'is_default',
    'isActive': 'is_active',
    'syncVersion': 'sync_version',
    'isDeleted': 'is_deleted',
  },
  'cash_receipts': {
    'id': 'id',
    'receiptNumber': 'receipt_number',
    'receiptDate': 'receipt_date',
    'amount': 'amount',
    'customerId': 'customer_id',
    'cashboxId': 'cashbox_id',
    'status': 'status',
    'notes': 'notes',
    'syncVersion': 'sync_version',
    'isDeleted': 'is_deleted',
  },
  'cash_payments': {
    'id': 'id',
    'paymentNumber': 'payment_number',
    'paymentDate': 'payment_date',
    'amount': 'amount',
    'supplierId': 'supplier_id',
    'cashboxId': 'cashbox_id',
    'status': 'status',
    'notes': 'notes',
    'syncVersion': 'sync_version',
    'isDeleted': 'is_deleted',
  },
  'bank_accounts': {
    'id': 'id',
    'code': 'code',
    'nameAr': 'name_ar',
    'bankName': 'bank_name',
    'accountNumber': 'account_number',
    'currentBalance': 'current_balance',
    'isActive': 'is_active',
    'syncVersion': 'sync_version',
    'isDeleted': 'is_deleted',
  },
};

/// Orchestrates pull/push sync cycles between local SQLite and the server.
class SyncEngine extends ChangeNotifier {
  final ApiClient _api;
  final LocalDatabase _localDb;
  final ConnectivityService _connectivity;
  final _uuid = const Uuid();

  bool _isSyncing = false;
  bool get isSyncing => _isSyncing;

  bool _isInitialized = false;
  bool get isInitialized => _isInitialized;

  String? _lastError;
  String? get lastError => _lastError;

  DateTime? _lastSyncTime;
  DateTime? get lastSyncTime => _lastSyncTime;

  int _pendingCount = 0;
  int get pendingCount => _pendingCount;

  StreamSubscription<bool>? _connectivitySub;
  Timer? _periodicTimer;

  SyncEngine({
    required ApiClient api,
    required LocalDatabase localDb,
    required ConnectivityService connectivity,
  })  : _api = api,
        _localDb = localDb,
        _connectivity = connectivity;

  /// Initialize sync engine: register device, start listeners.
  /// Guarded to prevent duplicate initialization from widget rebuilds.
  Future<void> initialize() async {
    if (_isInitialized) return;
    _isInitialized = true;

    // Ensure device has a unique ID
    var deviceId = await _localDb.getDeviceId();
    if (deviceId == null) {
      deviceId = _uuid.v4();
      await _localDb.setDeviceId(deviceId);
    }

    await _updatePendingCount();

    // Auto-sync when connectivity returns
    _connectivitySub = _connectivity.onConnectivityChanged.listen((online) {
      if (online) {
        fullSync();
      }
    });

    // Periodic sync every 5 minutes when online
    _periodicTimer = Timer.periodic(const Duration(minutes: 5), (_) {
      if (_connectivity.isOnline && !_isSyncing) {
        fullSync();
      }
    });

    // Initial sync if online
    if (_connectivity.isOnline) {
      await _registerDevice(deviceId);
      await fullSync();
    }
  }

  /// Run a full sync cycle: push local changes first, then pull server changes.
  Future<void> fullSync() async {
    if (_isSyncing) return;
    if (!_connectivity.isOnline) return;

    _isSyncing = true;
    _lastError = null;
    notifyListeners();

    try {
      await _pushChanges();
      await _pullChanges();
      _lastSyncTime = DateTime.now();
      await _localDb.clearSyncedChanges();
    } catch (e) {
      _lastError = e.toString();
      debugPrint('SyncEngine error: $e');
    } finally {
      _isSyncing = false;
      await _updatePendingCount();
      notifyListeners();
    }
  }

  /// Register this device with the server.
  Future<void> _registerDevice(String deviceId) async {
    try {
      await _api.post('/sync/register-device', data: {
        'deviceId': deviceId,
        'deviceName': 'Flutter Mobile',
        'deviceType': 'Mobile',
      });
    } catch (e) {
      debugPrint('Device registration failed: $e');
    }
  }

  // ═══════════════════════════════════════════
  // PULL: Server → Local
  // ═══════════════════════════════════════════

  Future<void> _pullChanges() async {
    final deviceId = await _localDb.getDeviceId();
    if (deviceId == null) return;

    var lastVersion = await _localDb.getLastSyncVersion();
    var hasMore = true;

    while (hasMore) {
      final response = await _api.post<Map<String, dynamic>>(
        '/sync/pull',
        data: {
          'deviceId': deviceId,
          'lastSyncVersion': lastVersion,
          'entityTypes': _sortedEntityTypes(_entityTableMap.keys),
          'pageSize': 500,
        },
        fromJson: (json) => json as Map<String, dynamic>,
      );

      if (!response.success || response.data == null) {
        throw Exception(response.errorMessage);
      }

      final pullData = response.data!;
      final currentVersion = pullData['currentSyncVersion'] as int? ?? 0;
      hasMore = pullData['hasMore'] as bool? ?? false;
      final changes = pullData['changes'] as Map<String, dynamic>? ?? {};

      // Apply each entity type's changes to local DB (priority order)
      final sortedKeys = _sortedEntityTypes(changes.keys);
      for (final entityType in sortedKeys) {
        final table = _entityTableMap[entityType];
        if (table == null) continue;

        final mapping = _fieldMappings[table];
        if (mapping == null) continue;

        final entities = (changes[entityType] as List<dynamic>?) ?? [];
        final rows = <Map<String, dynamic>>[];

        for (final entity in entities) {
          final e = entity as Map<String, dynamic>;
          final entityData = e['data'] as Map<String, dynamic>? ?? {};
          final row = _mapServerToLocal(entityData, mapping);
          row['id'] = e['id'];
          row['sync_version'] = e['syncVersion'] ?? 0;
          row['is_deleted'] = (e['isDeleted'] == true) ? 1 : 0;
          rows.add(row);
        }

        if (rows.isNotEmpty) {
          await _localDb.upsertEntities(table, rows);
        }
      }

      lastVersion = currentVersion;
      await _localDb.setLastSyncVersion(currentVersion);
    }
  }

  // ═══════════════════════════════════════════
  // PUSH: Local → Server (batched, max 50 per request)
  // ═══════════════════════════════════════════

  static const int _pushBatchSize = 50;

  Future<void> _pushChanges() async {
    final deviceId = await _localDb.getDeviceId();
    if (deviceId == null) return;

    final allPending = await _localDb.getPendingChanges();
    if (allPending.isEmpty) return;

    // Process in batches of _pushBatchSize
    for (var batchStart = 0;
        batchStart < allPending.length;
        batchStart += _pushBatchSize) {
      final batchEnd = (batchStart + _pushBatchSize).clamp(0, allPending.length);
      final batch = allPending.sublist(batchStart, batchEnd);

      await _pushBatch(deviceId, batch);
    }
  }

  Future<void> _pushBatch(
      String deviceId, List<Map<String, dynamic>> batch) async {
    // Group changes by entity type
    final grouped = <String, List<Map<String, dynamic>>>{};
    for (final change in batch) {
      final type = change['entity_type'] as String;
      grouped.putIfAbsent(type, () => []).add(change);
    }

    // Build push payload (priority order: master data first)
    final pushChanges = <String, List<Map<String, dynamic>>>{};
    final sortedTypes = _sortedEntityTypes(grouped.keys);
    for (final entityType in sortedTypes) {
      final changes = grouped[entityType]!;
      final list = <Map<String, dynamic>>[];
      for (final change in changes) {
        list.add({
          'id': change['entity_id'] ?? 0,
          'clientTempId': change['client_temp_id'] ?? '',
          'baseSyncVersion': change['base_sync_version'] ?? 0,
          'clientTimestamp': change['created_at'] ?? DateTime.now().toUtc().toIso8601String(),
          'data': jsonDecode(change['data'] as String),
        });
      }
      pushChanges[entityType] = list;
    }

    final idempotencyKey = _uuid.v4();
    final response = await _api.post<Map<String, dynamic>>(
      '/sync/push',
      data: {
        'deviceId': deviceId,
        'idempotencyKey': idempotencyKey,
        'changes': pushChanges,
      },
      fromJson: (json) => json as Map<String, dynamic>,
    );

    if (!response.success || response.data == null) {
      throw Exception(response.errorMessage);
    }

    final pushResult = response.data!;

    // Apply ID mappings for records created offline
    final idMappings =
        (pushResult['idMappings'] as Map<String, dynamic>?) ?? {};
    if (idMappings.isNotEmpty) {
      await _applyIdMappings(idMappings, batch);
    }

    // Mark all pending changes as synced
    final ids = batch.map((c) => c['id'] as int).toList();
    await _localDb.markChangesSynced(ids);
  }

  /// FK relationships: parent table -> (child table, FK column).
  /// Used by _applyIdMappings to cascade ID updates to child records.
  static const Map<String, List<Map<String, String>>> _fkRelationships = {
    'sales_invoices': [
      {'table': 'sales_invoice_lines', 'fk': 'sales_invoice_id'},
    ],
    'purchase_invoices': [
      {'table': 'purchase_invoice_lines', 'fk': 'purchase_invoice_id'},
    ],
  };

  /// Update local DB IDs for records created offline once server assigns real IDs.
  /// Also cascades the ID change to child tables that reference the old temp ID.
  Future<void> _applyIdMappings(
      Map<String, dynamic> mappings, List<Map<String, dynamic>> pending) async {
    final db = await _localDb.database;

    for (final entry in mappings.entries) {
      final clientTempId = entry.key;
      final serverId = entry.value as int;

      // Find the pending change with this clientTempId
      final change = pending.firstWhere(
        (c) => c['client_temp_id'] == clientTempId,
        orElse: () => {},
      );
      if (change.isEmpty) continue;

      final entityType = change['entity_type'] as String;
      final table = _entityTableMap[entityType];
      if (table == null) continue;

      // The entity was inserted locally with a negative temp ID.
      // Now update it to the server-assigned ID.
      final localId = change['entity_id'];
      if (localId != null && localId < 0) {
        // First: update FK references in child tables BEFORE deleting the parent
        final childRefs = _fkRelationships[table] ?? [];
        for (final ref in childRefs) {
          await db.update(
            ref['table']!,
            {ref['fk']!: serverId},
            where: '${ref['fk']!} = ?',
            whereArgs: [localId],
          );
        }

        // Then: replace the parent record's ID
        final existing = await db.query(table,
            where: 'id = ?', whereArgs: [localId], limit: 1);
        if (existing.isNotEmpty) {
          final row = Map<String, dynamic>.from(existing.first);
          row['id'] = serverId;
          await db.delete(table, where: 'id = ?', whereArgs: [localId]);
          await db.insert(table, row);
        }
      }
    }
  }

  /// Convert server camelCase JSON to local snake_case columns.
  Map<String, dynamic> _mapServerToLocal(
      Map<String, dynamic> serverData, Map<String, String> mapping) {
    final row = <String, dynamic>{};
    for (final entry in mapping.entries) {
      final serverKey = entry.key;
      final localCol = entry.value;
      if (serverData.containsKey(serverKey)) {
        var value = serverData[serverKey];
        // Convert booleans to integers for SQLite
        if (value is bool) value = value ? 1 : 0;
        row[localCol] = value;
      }
    }
    return row;
  }

  /// Convert local snake_case columns to server camelCase JSON.
  static Map<String, dynamic> mapLocalToServer(
      String table, Map<String, dynamic> localData) {
    final mapping = _fieldMappings[table];
    if (mapping == null) return localData;

    final serverData = <String, dynamic>{};
    // Reverse the mapping
    final reverseMap = mapping.map((k, v) => MapEntry(v, k));
    for (final entry in localData.entries) {
      final serverKey = reverseMap[entry.key];
      if (serverKey != null) {
        var value = entry.value;
        // Convert SQLite integers back to booleans for known boolean fields
        if ((entry.key == 'is_active' ||
                entry.key == 'is_default' ||
                entry.key == 'is_deleted') &&
            value is int) {
          value = value == 1;
        }
        serverData[serverKey] = value;
      }
    }
    return serverData;
  }

  Future<void> _updatePendingCount() async {
    final pending = await _localDb.getPendingChanges();
    _pendingCount = pending.length;
  }

  void dispose() {
    _connectivitySub?.cancel();
    _periodicTimer?.cancel();
    super.dispose();
  }
}
