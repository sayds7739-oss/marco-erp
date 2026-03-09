import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../api/api_client.dart';
import '../constants/api_constants.dart';
import '../models/user_model.dart';

class AuthProvider with ChangeNotifier {
  final ApiClient _apiClient;
  final FlutterSecureStorage _secureStorage = const FlutterSecureStorage();
  UserModel? _user;
  bool _isLoading = false;
  bool _mustChangePassword = false;

  AuthProvider(this._apiClient) {
    _loadSavedUser();
  }

  UserModel? get user => _user;
  bool get isAuthenticated => _user != null;
  bool get isLoading => _isLoading;
  bool get mustChangePassword => _mustChangePassword;
  String get userName => _user?.fullNameAr ?? '';
  String get roleName => _user?.roleNameAr ?? '';
  List<String> get permissions => _user?.permissions ?? [];

  bool hasPermission(String key) {
    if (_user == null) return false;
    return _user!.permissions.contains('*') || _user!.permissions.contains(key);
  }

  Future<void> _loadSavedUser() async {
    final userJson = await _secureStorage.read(key: 'user_data');
    final token = await _apiClient.getAccessToken();

    if (userJson != null && token != null) {
      _user = UserModel.fromJson(jsonDecode(userJson));
      notifyListeners();
    }
  }

  Future<String?> login(String username, String password) async {
    _isLoading = true;
    notifyListeners();

    try {
      final response = await _apiClient.post<Map<String, dynamic>>(
        ApiConstants.login,
        data: {'username': username, 'password': password},
        fromJson: (json) => json as Map<String, dynamic>,
      );

      if (!response.success || response.data == null) {
        return response.errorMessage;
      }

      final data = response.data!;
      final accessToken = data['accessToken'] as String;
      final refreshToken = data['refreshToken'] as String;
      final userData = data['user'] as Map<String, dynamic>;

      await _apiClient.saveTokens(accessToken, refreshToken);

      _user = UserModel.fromJson(userData);
      _mustChangePassword = userData['mustChangePassword'] == true;

      // Save user data in secure storage (not plaintext SharedPreferences)
      await _secureStorage.write(key: 'user_data', value: jsonEncode(userData));

      notifyListeners();
      return null; // Success, no error
    } catch (e) {
      return 'حدث خطأ أثناء تسجيل الدخول.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  /// Call after password change to clear the mustChangePassword flag.
  void clearMustChangePassword() {
    _mustChangePassword = false;
    notifyListeners();
  }

  Future<void> logout() async {
    await _apiClient.clearTokens();
    await _secureStorage.delete(key: 'user_data');
    _user = null;
    _mustChangePassword = false;
    notifyListeners();
  }

  Future<String?> changePassword(String currentPassword, String newPassword, String confirmPassword) async {
    final response = await _apiClient.post(
      ApiConstants.changePassword,
      data: {
        'currentPassword': currentPassword,
        'newPassword': newPassword,
        'confirmNewPassword': confirmPassword,
      },
    );

    if (response.success) {
      clearMustChangePassword();
      return null;
    }
    return response.errorMessage;
  }
}
