import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../core/network/connectivity_service.dart';
import '../core/sync/sync_engine.dart';

/// A compact widget showing sync status — put it in any AppBar or bottom bar.
class SyncStatusWidget extends StatelessWidget {
  const SyncStatusWidget({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<SyncEngine>(
      builder: (ctx, sync, _) {
        final connectivity = ctx.read<ConnectivityService>();

        if (sync.isSyncing) {
          return const Padding(
            padding: EdgeInsets.symmetric(horizontal: 8),
            child: SizedBox(
              width: 16,
              height: 16,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: Colors.white,
              ),
            ),
          );
        }

        final isOnline = connectivity.isOnline;
        final pendingCount = sync.pendingCount;

        return InkWell(
          onTap: () => sync.fullSync(),
          borderRadius: BorderRadius.circular(20),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(
                  isOnline ? Icons.cloud_done : Icons.cloud_off,
                  size: 18,
                  color: isOnline ? Colors.green.shade300 : Colors.orange.shade300,
                ),
                if (pendingCount > 0) ...[
                  const SizedBox(width: 4),
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                    decoration: BoxDecoration(
                      color: Colors.orange.shade700,
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Text(
                      '$pendingCount',
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 10,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ),
        );
      },
    );
  }
}

/// A banner shown at the top of screens when offline.
class OfflineBanner extends StatelessWidget {
  const OfflineBanner({super.key});

  @override
  Widget build(BuildContext context) {
    final connectivity = context.read<ConnectivityService>();
    if (connectivity.isOnline) return const SizedBox.shrink();

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(vertical: 6, horizontal: 16),
      color: Colors.orange.shade800,
      child: const Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(Icons.wifi_off, color: Colors.white, size: 16),
          SizedBox(width: 8),
          Text(
            'وضع بدون اتصال — البيانات محفوظة محلياً',
            style: TextStyle(color: Colors.white, fontSize: 12),
          ),
        ],
      ),
    );
  }
}
