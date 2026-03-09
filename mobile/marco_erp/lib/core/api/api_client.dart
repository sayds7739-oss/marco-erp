import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../constants/api_constants.dart';

class ApiClient {
  late final Dio _dio;
  final FlutterSecureStorage _storage = const FlutterSecureStorage();
  bool _isRefreshing = false;

  ApiClient() {
    _dio = Dio(BaseOptions(
      baseUrl: ApiConstants.baseUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 15),
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
      },
    ));

    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await _storage.read(key: 'access_token');
        if (token != null) {
          options.headers['Authorization'] = 'Bearer $token';
        }
        return handler.next(options);
      },
      onError: (error, handler) async {
        if (error.response?.statusCode == 401 && !_isRefreshing) {
          // Attempt token refresh before giving up
          final refreshed = await _tryRefreshToken();
          if (refreshed) {
            // Retry the original request with the new token
            final token = await _storage.read(key: 'access_token');
            final opts = error.requestOptions;
            opts.headers['Authorization'] = 'Bearer $token';
            try {
              final response = await _dio.fetch(opts);
              return handler.resolve(response);
            } on DioException catch (retryError) {
              return handler.next(retryError);
            }
          } else {
            // Refresh failed — clear tokens (session expired)
            await _storage.delete(key: 'access_token');
            await _storage.delete(key: 'refresh_token');
          }
        }
        return handler.next(error);
      },
    ));
  }

  /// Attempt to refresh the access token using the stored refresh token.
  /// Returns true if refresh succeeded and new tokens were saved.
  Future<bool> _tryRefreshToken() async {
    _isRefreshing = true;
    try {
      final refreshToken = await _storage.read(key: 'refresh_token');
      if (refreshToken == null) return false;

      final response = await _dio.post(
        '/auth/refresh',
        data: {'refreshToken': refreshToken},
      );

      final data = response.data as Map<String, dynamic>?;
      if (data == null || data['success'] != true) return false;

      final tokenData = data['data'] as Map<String, dynamic>;
      final newAccess = tokenData['accessToken'] as String;
      final newRefresh = tokenData['refreshToken'] as String;

      await _storage.write(key: 'access_token', value: newAccess);
      await _storage.write(key: 'refresh_token', value: newRefresh);
      return true;
    } catch (e) {
      debugPrint('Token refresh failed: $e');
      return false;
    } finally {
      _isRefreshing = false;
    }
  }

  // Generic GET
  Future<ApiResponse<T>> get<T>(
    String path, {
    Map<String, dynamic>? queryParams,
    T Function(dynamic json)? fromJson,
  }) async {
    try {
      final response = await _dio.get(path, queryParameters: queryParams);
      return ApiResponse.fromResponse(response.data, fromJson);
    } on DioException catch (e) {
      return ApiResponse.fromError(e);
    }
  }

  // Generic POST
  Future<ApiResponse<T>> post<T>(
    String path, {
    dynamic data,
    T Function(dynamic json)? fromJson,
  }) async {
    try {
      final response = await _dio.post(path, data: data);
      return ApiResponse.fromResponse(response.data, fromJson);
    } on DioException catch (e) {
      return ApiResponse.fromError(e);
    }
  }

  // Generic PUT
  Future<ApiResponse<T>> put<T>(
    String path, {
    dynamic data,
    T Function(dynamic json)? fromJson,
  }) async {
    try {
      final response = await _dio.put(path, data: data);
      return ApiResponse.fromResponse(response.data, fromJson);
    } on DioException catch (e) {
      return ApiResponse.fromError(e);
    }
  }

  // Generic PATCH
  Future<ApiResponse<T>> patch<T>(
    String path, {
    dynamic data,
    T Function(dynamic json)? fromJson,
  }) async {
    try {
      final response = await _dio.patch(path, data: data);
      return ApiResponse.fromResponse(response.data, fromJson);
    } on DioException catch (e) {
      return ApiResponse.fromError(e);
    }
  }

  // Generic DELETE
  Future<ApiResponse<void>> delete(String path) async {
    try {
      final response = await _dio.delete(path);
      return ApiResponse.fromResponse(response.data, null);
    } on DioException catch (e) {
      return ApiResponse.fromError(e);
    }
  }

  // Token management
  Future<void> saveTokens(String accessToken, String refreshToken) async {
    await _storage.write(key: 'access_token', value: accessToken);
    await _storage.write(key: 'refresh_token', value: refreshToken);
  }

  Future<void> clearTokens() async {
    await _storage.delete(key: 'access_token');
    await _storage.delete(key: 'refresh_token');
  }

  Future<String?> getAccessToken() async {
    return await _storage.read(key: 'access_token');
  }

  void updateBaseUrl(String newUrl) {
    _dio.options.baseUrl = newUrl;
  }
}

/// Standardized API response wrapper that matches the server's JSON envelope
class ApiResponse<T> {
  final bool success;
  final T? data;
  final List<String> errors;

  ApiResponse({required this.success, this.data, this.errors = const []});

  factory ApiResponse.fromResponse(
    dynamic responseData,
    T Function(dynamic json)? fromJson,
  ) {
    final map = responseData as Map<String, dynamic>;
    final success = map['success'] as bool? ?? false;
    final errors = (map['errors'] as List<dynamic>?)
            ?.map((e) => e.toString())
            .toList() ??
        [];

    T? data;
    if (success && map.containsKey('data') && fromJson != null) {
      data = fromJson(map['data']);
    }

    return ApiResponse(success: success, data: data, errors: errors);
  }

  factory ApiResponse.fromError(DioException e) {
    if (e.response?.data is Map<String, dynamic>) {
      final map = e.response!.data as Map<String, dynamic>;
      final errors = (map['errors'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          ['حدث خطأ غير متوقع'];
      return ApiResponse(success: false, errors: errors);
    }

    String message;
    switch (e.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
        message = 'انتهت مهلة الاتصال. تحقق من اتصال الإنترنت.';
        break;
      case DioExceptionType.connectionError:
        message = 'لا يمكن الاتصال بالخادم. تحقق من عنوان الخادم.';
        break;
      default:
        message = 'حدث خطأ غير متوقع.';
    }
    return ApiResponse(success: false, errors: [message]);
  }

  String get errorMessage => errors.join('\n');
}
