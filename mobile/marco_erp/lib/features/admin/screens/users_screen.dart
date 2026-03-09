import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';

class UserListModel {
  final int id;
  final String username;
  final String fullNameAr;
  final String? fullNameEn;
  final String? email;
  final int? roleId;
  final String? roleNameAr;
  final bool isActive;

  UserListModel({
    required this.id,
    required this.username,
    required this.fullNameAr,
    this.fullNameEn,
    this.email,
    this.roleId,
    this.roleNameAr,
    required this.isActive,
  });

  factory UserListModel.fromJson(Map<String, dynamic> json) {
    return UserListModel(
      id: json['id'] as int,
      username: json['username'] as String? ?? '',
      fullNameAr: json['fullNameAr'] as String? ?? '',
      fullNameEn: json['fullNameEn'] as String?,
      email: json['email'] as String?,
      roleId: json['roleId'] as int?,
      roleNameAr: json['roleNameAr'] as String?,
      isActive: json['isActive'] as bool? ?? true,
    );
  }
}

class RoleLookup {
  final int id;
  final String nameAr;

  RoleLookup({required this.id, required this.nameAr});

  factory RoleLookup.fromJson(Map<String, dynamic> json) {
    return RoleLookup(
      id: json['id'] as int,
      nameAr: json['nameAr'] as String? ?? '',
    );
  }
}

class UsersScreen extends StatefulWidget {
  const UsersScreen({super.key});

  @override
  State<UsersScreen> createState() => _UsersScreenState();
}

class _UsersScreenState extends State<UsersScreen> {
  List<UserListModel> _users = [];
  List<RoleLookup> _roles = [];
  bool _isLoading = true;
  String? _error;
  final _searchController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _loadData();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadData() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    final api = context.read<ApiClient>();

    // Load users and roles in parallel
    final usersResponse = await api.get<List<UserListModel>>(
      ApiConstants.users,
      fromJson: (json) => (json as List)
          .map((e) => UserListModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

    final rolesResponse = await api.get<List<RoleLookup>>(
      ApiConstants.roles,
      fromJson: (json) => (json as List)
          .map((e) => RoleLookup.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

    if (mounted) {
      setState(() {
        _isLoading = false;
        if (usersResponse.success && usersResponse.data != null) {
          _users = usersResponse.data!;
        } else {
          _error = usersResponse.errorMessage;
        }
        if (rolesResponse.success && rolesResponse.data != null) {
          _roles = rolesResponse.data!;
        }
      });
    }
  }

  List<UserListModel> get _filteredUsers {
    final query = _searchController.text.toLowerCase();
    if (query.isEmpty) return _users;
    return _users
        .where((u) =>
            u.fullNameAr.toLowerCase().contains(query) ||
            u.username.toLowerCase().contains(query) ||
            (u.email?.toLowerCase().contains(query) ?? false) ||
            (u.roleNameAr?.toLowerCase().contains(query) ?? false))
        .toList();
  }

  void _showUserDialog({UserListModel? user}) {
    final isEdit = user != null;
    final usernameCtrl = TextEditingController(text: user?.username ?? '');
    final nameArCtrl = TextEditingController(text: user?.fullNameAr ?? '');
    final nameEnCtrl = TextEditingController(text: user?.fullNameEn ?? '');
    final emailCtrl = TextEditingController(text: user?.email ?? '');
    final passwordCtrl = TextEditingController();
    int? selectedRoleId = user?.roleId;
    bool isActive = user?.isActive ?? true;
    final formKey = GlobalKey<FormState>();
    bool isSaving = false;

    showDialog(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          title: Text(isEdit ? 'تعديل المستخدم' : 'إضافة مستخدم جديد'),
          content: SingleChildScrollView(
            child: Form(
              key: formKey,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  TextFormField(
                    controller: usernameCtrl,
                    decoration:
                        const InputDecoration(labelText: 'اسم المستخدم *'),
                    textDirection: TextDirection.ltr,
                    validator: (v) =>
                        (v == null || v.isEmpty) ? 'مطلوب' : null,
                    enabled: !isEdit,
                  ),
                  const SizedBox(height: 8),
                  TextFormField(
                    controller: nameArCtrl,
                    decoration:
                        const InputDecoration(labelText: 'الاسم بالعربية *'),
                    validator: (v) =>
                        (v == null || v.isEmpty) ? 'مطلوب' : null,
                  ),
                  const SizedBox(height: 8),
                  TextFormField(
                    controller: nameEnCtrl,
                    decoration: const InputDecoration(
                        labelText: 'الاسم بالإنجليزية'),
                    textDirection: TextDirection.ltr,
                  ),
                  const SizedBox(height: 8),
                  TextFormField(
                    controller: emailCtrl,
                    decoration:
                        const InputDecoration(labelText: 'البريد الإلكتروني'),
                    keyboardType: TextInputType.emailAddress,
                    textDirection: TextDirection.ltr,
                  ),
                  const SizedBox(height: 8),
                  if (!isEdit) ...[
                    TextFormField(
                      controller: passwordCtrl,
                      decoration:
                          const InputDecoration(labelText: 'كلمة المرور *'),
                      obscureText: true,
                      validator: (v) => !isEdit && (v == null || v.isEmpty)
                          ? 'مطلوب'
                          : null,
                    ),
                    const SizedBox(height: 8),
                  ],
                  DropdownButtonFormField<int>(
                    value: selectedRoleId,
                    decoration: const InputDecoration(labelText: 'الدور *'),
                    items: _roles.map((r) {
                      return DropdownMenuItem<int>(
                        value: r.id,
                        child: Text(r.nameAr),
                      );
                    }).toList(),
                    onChanged: (v) =>
                        setDialogState(() => selectedRoleId = v),
                    validator: (v) => v == null ? 'مطلوب' : null,
                  ),
                  const SizedBox(height: 8),
                  SwitchListTile(
                    title: const Text('نشط'),
                    value: isActive,
                    onChanged: (v) => setDialogState(() => isActive = v),
                    contentPadding: EdgeInsets.zero,
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('إلغاء'),
            ),
            TextButton(
              onPressed: isSaving
                  ? null
                  : () async {
                      if (!formKey.currentState!.validate()) return;
                      setDialogState(() => isSaving = true);

                      final data = <String, dynamic>{
                        'username': usernameCtrl.text,
                        'fullNameAr': nameArCtrl.text,
                        'fullNameEn': nameEnCtrl.text.isEmpty
                            ? null
                            : nameEnCtrl.text,
                        'email':
                            emailCtrl.text.isEmpty ? null : emailCtrl.text,
                        'roleId': selectedRoleId,
                        'isActive': isActive,
                      };
                      if (!isEdit && passwordCtrl.text.isNotEmpty) {
                        data['password'] = passwordCtrl.text;
                      }

                      final api = context.read<ApiClient>();
                      final response = isEdit
                          ? await api.put(
                              '${ApiConstants.users}/${user.id}',
                              data: data)
                          : await api.post(ApiConstants.users,
                              data: data);

                      if (ctx.mounted) {
                        Navigator.pop(ctx);
                        if (response.success) {
                          _loadData();
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                                content: Text(isEdit
                                    ? 'تم تعديل المستخدم بنجاح'
                                    : 'تم إضافة المستخدم بنجاح')),
                          );
                        } else {
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                                content: Text(response.errorMessage)),
                          );
                        }
                      }
                    },
              child: isSaving
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2))
                  : Text(isEdit ? 'تعديل' : 'إضافة'),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('إدارة المستخدمين'),
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(56),
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
            child: TextField(
              controller: _searchController,
              decoration: InputDecoration(
                hintText: 'بحث عن مستخدم...',
                prefixIcon: const Icon(Icons.search),
                filled: true,
                fillColor: Colors.white,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(10),
                  borderSide: BorderSide.none,
                ),
                contentPadding: const EdgeInsets.symmetric(horizontal: 16),
              ),
              onChanged: (_) => setState(() {}),
            ),
          ),
        ),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => _showUserDialog(),
        child: const Icon(Icons.person_add),
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.error_outline,
                          size: 48, color: Colors.grey),
                      const SizedBox(height: 16),
                      Text(_error!,
                          style: const TextStyle(color: Colors.grey)),
                      const SizedBox(height: 16),
                      ElevatedButton(
                          onPressed: _loadData,
                          child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadData,
                  child: _filteredUsers.isEmpty
                      ? const Center(child: Text('لا يوجد مستخدمين'))
                      : ListView.builder(
                          itemCount: _filteredUsers.length,
                          itemBuilder: (ctx, i) {
                            final u = _filteredUsers[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(
                                  horizontal: 12, vertical: 4),
                              child: ListTile(
                                leading: CircleAvatar(
                                  backgroundColor:
                                      u.isActive
                                          ? Colors.blue.shade100
                                          : Colors.grey.shade200,
                                  child: Icon(
                                    Icons.person,
                                    color: u.isActive
                                        ? Colors.blue.shade700
                                        : Colors.grey,
                                  ),
                                ),
                                title: Text(u.fullNameAr,
                                    style: const TextStyle(
                                        fontWeight: FontWeight.bold)),
                                subtitle: Column(
                                  crossAxisAlignment:
                                      CrossAxisAlignment.start,
                                  children: [
                                    Text(u.username,
                                        style: const TextStyle(
                                            fontSize: 12),
                                        textDirection: TextDirection.ltr),
                                    if (u.roleNameAr != null)
                                      Container(
                                        margin:
                                            const EdgeInsets.only(top: 4),
                                        padding: const EdgeInsets.symmetric(
                                            horizontal: 8, vertical: 2),
                                        decoration: BoxDecoration(
                                          color: Colors.purple.shade50,
                                          borderRadius:
                                              BorderRadius.circular(8),
                                        ),
                                        child: Text(
                                          u.roleNameAr!,
                                          style: TextStyle(
                                            fontSize: 11,
                                            color: Colors.purple.shade700,
                                          ),
                                        ),
                                      ),
                                  ],
                                ),
                                trailing: Icon(
                                  Icons.circle,
                                  size: 12,
                                  color: u.isActive
                                      ? Colors.green
                                      : Colors.grey,
                                ),
                                onTap: () => _showUserDialog(user: u),
                              ),
                            );
                          },
                        ),
                ),
    );
  }
}
