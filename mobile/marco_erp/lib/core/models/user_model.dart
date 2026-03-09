class UserModel {
  final int userId;
  final String username;
  final String fullNameAr;
  final int roleId;
  final String roleNameAr;
  final bool mustChangePassword;
  final List<String> permissions;

  UserModel({
    required this.userId,
    required this.username,
    required this.fullNameAr,
    required this.roleId,
    required this.roleNameAr,
    this.mustChangePassword = false,
    this.permissions = const [],
  });

  factory UserModel.fromJson(Map<String, dynamic> json) {
    return UserModel(
      userId: json['userId'] as int,
      username: json['username'] as String,
      fullNameAr: json['fullNameAr'] as String,
      roleId: json['roleId'] as int,
      roleNameAr: json['roleNameAr'] as String,
      mustChangePassword: json['mustChangePassword'] as bool? ?? false,
      permissions: (json['permissions'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          [],
    );
  }

  Map<String, dynamic> toJson() => {
        'userId': userId,
        'username': username,
        'fullNameAr': fullNameAr,
        'roleId': roleId,
        'roleNameAr': roleNameAr,
        'mustChangePassword': mustChangePassword,
        'permissions': permissions,
      };
}
