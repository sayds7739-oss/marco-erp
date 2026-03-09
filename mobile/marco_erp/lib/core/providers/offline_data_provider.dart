import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:uuid/uuid.dart';
import '../api/api_client.dart';
import '../database/local_database.dart';
import '../network/connectivity_service.dart';
import '../sync/sync_engine.dart';

const _uuid = Uuid();

/// Generic offline-first data provider.
/// Reads from local DB, writes go to pending_changes queue + local DB.
/// When online, also hits the API for real-time data and merges.
class OfflineDataProvider extends ChangeNotifier {
  final LocalDatabase localDb;
  final ApiClient api;
  final ConnectivityService connectivity;
  final SyncEngine syncEngine;

  /// Persisted temp ID counter to avoid collisions across app restarts.
  /// Loaded from DB on first create call.
  int? _tempIdCounter;
  bool _counterLoaded = false;

  OfflineDataProvider({
    required this.localDb,
    required this.api,
    required this.connectivity,
    required this.syncEngine,
  });

  /// Get the next temp ID, loading persisted value from DB on first call.
  Future<int> _nextTempId() async {
    if (!_counterLoaded) {
      _tempIdCounter = await localDb.getMinTempId() ?? 0;
      _counterLoaded = true;
    }
    _tempIdCounter = _tempIdCounter! - 1;
    return _tempIdCounter!;
  }

  // ═══════════════════════════════════════════
  // READ operations — always from local DB
  // ═══════════════════════════════════════════

  /// Get all entities from a local table (excludes soft-deleted).
  Future<List<Map<String, dynamic>>> getAll(String table) async {
    return localDb.getAll(table);
  }

  /// Get a single entity by ID.
  Future<Map<String, dynamic>?> getById(String table, int id) async {
    return localDb.getById(table, id);
  }

  /// Search entities by a column value.
  Future<List<Map<String, dynamic>>> search(
      String table, String column, String query) async {
    return localDb.search(table, column, query);
  }

  // ═══════════════════════════════════════════
  // WRITE operations — local DB + pending queue
  // ═══════════════════════════════════════════

  /// Create a new entity offline. Returns the temp local ID.
  Future<int> create({
    required String entityType,
    required String table,
    required Map<String, dynamic> data,
  }) async {
    final tempId = await _nextTempId();
    final clientTempId = _uuid.v4();

    // Store in local DB with negative temp ID
    data['id'] = tempId;
    data['sync_version'] = 0;
    data['is_deleted'] = 0;
    await localDb.upsertEntity(table, tempId, data);

    // Queue for sync
    final serverData = SyncEngine.mapLocalToServer(table, data);
    serverData.remove('id');
    serverData.remove('syncVersion');
    serverData.remove('isDeleted');

    await localDb.addPendingChange(
      entityType: entityType,
      entityId: tempId,
      clientTempId: clientTempId,
      operation: 'create',
      data: jsonEncode(serverData),
      baseSyncVersion: 0,
    );

    notifyListeners();

    // If online, trigger sync immediately
    if (connectivity.isOnline) {
      syncEngine.fullSync();
    }

    return tempId;
  }

  /// Update an existing entity offline.
  Future<void> update({
    required String entityType,
    required String table,
    required int id,
    required Map<String, dynamic> data,
  }) async {
    // Get current sync version
    final existing = await localDb.getById(table, id);
    final baseSyncVersion =
        (existing?['sync_version'] as int?) ?? 0;

    // Update local DB
    data['id'] = id;
    data['sync_version'] = baseSyncVersion;
    data['is_deleted'] = existing?['is_deleted'] ?? 0;
    await localDb.upsertEntity(table, id, data);

    // Queue for sync
    final serverData = SyncEngine.mapLocalToServer(table, data);
    serverData.remove('syncVersion');
    serverData.remove('isDeleted');

    await localDb.addPendingChange(
      entityType: entityType,
      entityId: id,
      operation: 'update',
      data: jsonEncode(serverData),
      baseSyncVersion: baseSyncVersion,
    );

    notifyListeners();

    if (connectivity.isOnline) {
      syncEngine.fullSync();
    }
  }

  /// Soft-delete an entity offline.
  Future<void> softDelete({
    required String entityType,
    required String table,
    required int id,
  }) async {
    final existing = await localDb.getById(table, id);
    if (existing == null) return;

    final baseSyncVersion =
        (existing['sync_version'] as int?) ?? 0;

    // Mark deleted locally
    final db = await localDb.database;
    await db.update(table, {'is_deleted': 1},
        where: 'id = ?', whereArgs: [id]);

    // Queue for sync — send isDeleted flag
    final serverData = SyncEngine.mapLocalToServer(table, existing);
    serverData['isDeleted'] = true;

    await localDb.addPendingChange(
      entityType: entityType,
      entityId: id,
      operation: 'delete',
      data: jsonEncode(serverData),
      baseSyncVersion: baseSyncVersion,
    );

    notifyListeners();

    if (connectivity.isOnline) {
      syncEngine.fullSync();
    }
  }

  /// Force refresh: trigger full sync and notify listeners.
  Future<void> refresh() async {
    if (connectivity.isOnline) {
      await syncEngine.fullSync();
    }
    notifyListeners();
  }
}
