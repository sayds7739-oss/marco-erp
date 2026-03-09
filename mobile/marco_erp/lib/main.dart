import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:provider/provider.dart';
import 'core/constants/app_theme.dart';
import 'core/auth/auth_provider.dart';
import 'core/api/api_client.dart';
import 'core/database/local_database.dart';
import 'core/network/connectivity_service.dart';
import 'core/sync/sync_engine.dart';
import 'core/sync/background_sync_service.dart';
import 'core/providers/offline_data_provider.dart';
import 'features/auth/screens/login_screen.dart';
import 'features/dashboard/screens/dashboard_screen.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Initialize core services
  final localDb = LocalDatabase();
  await localDb.database; // ensure DB is created

  final connectivity = ConnectivityService();
  await connectivity.initialize();

  final apiClient = ApiClient();

  final syncEngine = SyncEngine(
    api: apiClient,
    localDb: localDb,
    connectivity: connectivity,
  );

  final offlineData = OfflineDataProvider(
    localDb: localDb,
    api: apiClient,
    connectivity: connectivity,
    syncEngine: syncEngine,
  );

  runApp(MarcoERPApp(
    apiClient: apiClient,
    localDb: localDb,
    connectivity: connectivity,
    syncEngine: syncEngine,
    offlineData: offlineData,
  ));
}

class MarcoERPApp extends StatefulWidget {
  final ApiClient apiClient;
  final LocalDatabase localDb;
  final ConnectivityService connectivity;
  final SyncEngine syncEngine;
  final OfflineDataProvider offlineData;

  const MarcoERPApp({
    super.key,
    required this.apiClient,
    required this.localDb,
    required this.connectivity,
    required this.syncEngine,
    required this.offlineData,
  });

  @override
  State<MarcoERPApp> createState() => _MarcoERPAppState();
}

class _MarcoERPAppState extends State<MarcoERPApp> {
  bool _backgroundSyncInitialized = false;

  @override
  void dispose() {
    widget.connectivity.dispose();
    widget.syncEngine.dispose();
    widget.localDb.close();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        Provider<ApiClient>.value(value: widget.apiClient),
        Provider<LocalDatabase>.value(value: widget.localDb),
        Provider<ConnectivityService>.value(value: widget.connectivity),
        ChangeNotifierProvider<SyncEngine>.value(value: widget.syncEngine),
        ChangeNotifierProvider<OfflineDataProvider>.value(
            value: widget.offlineData),
        ChangeNotifierProvider<AuthProvider>(
          create: (ctx) => AuthProvider(ctx.read<ApiClient>()),
        ),
      ],
      child: Consumer<AuthProvider>(
        builder: (ctx, auth, _) {
          // Initialize sync after login (guards prevent duplicate calls)
          if (auth.isAuthenticated) {
            widget.syncEngine.initialize();
            if (!_backgroundSyncInitialized) {
              _backgroundSyncInitialized = true;
              initializeBackgroundSync();
            }
          }

          Widget home;
          if (!auth.isAuthenticated) {
            home = const LoginScreen();
          } else if (auth.mustChangePassword) {
            // Force password change before allowing access to the app
            home = const _ForceChangePasswordScreen();
          } else {
            home = const DashboardScreen();
          }

          return MaterialApp(
            title: 'MarcoERP',
            debugShowCheckedModeBanner: false,
            theme: AppTheme.lightTheme,
            darkTheme: AppTheme.darkTheme,
            themeMode: ThemeMode.light,
            locale: const Locale('ar'),
            supportedLocales: const [
              Locale('ar'),
              Locale('en'),
            ],
            localizationsDelegates: const [
              GlobalMaterialLocalizations.delegate,
              GlobalWidgetsLocalizations.delegate,
              GlobalCupertinoLocalizations.delegate,
            ],
            home: home,
          );
        },
      ),
    );
  }
}

/// Screen shown when the server requires the user to change their password.
class _ForceChangePasswordScreen extends StatefulWidget {
  const _ForceChangePasswordScreen();

  @override
  State<_ForceChangePasswordScreen> createState() => _ForceChangePasswordScreenState();
}

class _ForceChangePasswordScreenState extends State<_ForceChangePasswordScreen> {
  final _currentPw = TextEditingController();
  final _newPw = TextEditingController();
  final _confirmPw = TextEditingController();
  bool _isLoading = false;
  String? _error;

  @override
  void dispose() {
    _currentPw.dispose();
    _newPw.dispose();
    _confirmPw.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('تغيير كلمة المرور مطلوب')),
      body: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.lock_outline, size: 64, color: Colors.orange),
            const SizedBox(height: 16),
            const Text(
              'يجب تغيير كلمة المرور قبل المتابعة',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 24),
            TextField(
              controller: _currentPw,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'كلمة المرور الحالية'),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _newPw,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'كلمة المرور الجديدة'),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _confirmPw,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'تأكيد كلمة المرور'),
            ),
            if (_error != null) ...[
              const SizedBox(height: 12),
              Text(_error!, style: const TextStyle(color: Colors.red)),
            ],
            const SizedBox(height: 24),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton(
                onPressed: _isLoading ? null : _changePassword,
                child: _isLoading
                    ? const CircularProgressIndicator()
                    : const Text('تغيير كلمة المرور'),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _changePassword() async {
    if (_newPw.text != _confirmPw.text) {
      setState(() => _error = 'كلمة المرور الجديدة وتأكيدها غير متطابقين');
      return;
    }
    setState(() { _isLoading = true; _error = null; });
    final result = await context.read<AuthProvider>().changePassword(
      _currentPw.text,
      _newPw.text,
      _confirmPw.text,
    );
    if (mounted) {
      setState(() {
        _isLoading = false;
        _error = result; // null on success
      });
    }
  }
}
