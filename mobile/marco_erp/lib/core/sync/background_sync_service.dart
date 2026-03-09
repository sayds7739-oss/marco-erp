import 'package:flutter/foundation.dart';
import 'package:workmanager/workmanager.dart';
import '../api/api_client.dart';
import '../database/local_database.dart';
import '../network/connectivity_service.dart';
import 'sync_engine.dart';

/// Unique task name for the periodic background sync.
const String kBackgroundSyncTask = 'com.marcoerp.backgroundSync';

/// Top-level callback required by WorkManager — runs in an isolate.
@pragma('vm:entry-point')
void callbackDispatcher() {
  Workmanager().executeTask((taskName, inputData) async {
    try {
      if (taskName == kBackgroundSyncTask || taskName == Workmanager.iOSBackgroundTask) {
        final localDb = LocalDatabase();
        await localDb.database;

        final connectivity = ConnectivityService();
        await connectivity.initialize();

        if (!connectivity.isOnline) return true; // nothing to do offline

        final api = ApiClient();
        final engine = SyncEngine(
          api: api,
          localDb: localDb,
          connectivity: connectivity,
        );

        await engine.fullSync();

        connectivity.dispose();
        engine.dispose();
        localDb.close();
      }
      return true;
    } catch (e) {
      debugPrint('Background sync failed: $e');
      return false; // WorkManager will retry
    }
  });
}

/// Registers the periodic background sync task.
/// Call once during app initialization (after first login).
Future<void> initializeBackgroundSync() async {
  await Workmanager().initialize(callbackDispatcher, isInDebugMode: false);
  await Workmanager().registerPeriodicTask(
    'marcoerp-periodic-sync',
    kBackgroundSyncTask,
    frequency: const Duration(minutes: 15), // minimum Android allows
    constraints: Constraints(
      networkType: NetworkType.connected,
    ),
    existingWorkPolicy: ExistingWorkPolicy.keep,
  );
}
