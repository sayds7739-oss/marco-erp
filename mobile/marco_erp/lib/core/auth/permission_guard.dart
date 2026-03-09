import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'auth_provider.dart';

/// Widget that checks a permission and shows [child] only if the current
/// user is authorized.  When not authorized it renders [fallback] (defaults
/// to an invisible zero-size box).
class PermissionGuard extends StatelessWidget {
  final String permissionKey;
  final Widget child;
  final Widget? fallback;

  const PermissionGuard({
    super.key,
    required this.permissionKey,
    required this.child,
    this.fallback,
  });

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    if (auth.hasPermission(permissionKey)) {
      return child;
    }
    return fallback ?? const SizedBox.shrink();
  }
}

/// Convenience helpers so callers can write `auth.canView(key)` instead of
/// `auth.hasPermission(key)`.
extension PermissionCheck on AuthProvider {
  /// Check if the user can view a section (used for drawer items).
  bool canView(String key) => hasPermission(key);

  /// Check if the user can create in a section.
  bool canCreate(String key) => hasPermission(key);
}
