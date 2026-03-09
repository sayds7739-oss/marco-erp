import 'dart:async';
import 'package:connectivity_plus/connectivity_plus.dart';

/// Monitors network connectivity and notifies listeners.
class ConnectivityService {
  final Connectivity _connectivity = Connectivity();
  StreamSubscription<List<ConnectivityResult>>? _subscription;
  final _controller = StreamController<bool>.broadcast();

  bool _isOnline = true;
  bool get isOnline => _isOnline;

  /// Stream that fires true/false when connectivity changes.
  Stream<bool> get onConnectivityChanged => _controller.stream;

  Future<void> initialize() async {
    final results = await _connectivity.checkConnectivity();
    _isOnline = _hasConnection(results);

    _subscription = _connectivity.onConnectivityChanged.listen((results) {
      final online = _hasConnection(results);
      if (online != _isOnline) {
        _isOnline = online;
        _controller.add(_isOnline);
      }
    });
  }

  bool _hasConnection(List<ConnectivityResult> results) {
    return results.any((r) =>
        r == ConnectivityResult.wifi ||
        r == ConnectivityResult.mobile ||
        r == ConnectivityResult.ethernet);
  }

  void dispose() {
    _subscription?.cancel();
    _controller.close();
  }
}
