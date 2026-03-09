import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';

class RoleModel {
  final int id;
  final String nameAr;
  final String? nameEn;
  final List<String> permissions;

  RoleModel({
    required this.id,
    required this.nameAr,
    this.nameEn,
    required this.permissions,
  });

  factory RoleModel.fromJson(Map<String, dynamic> json) {
    return RoleModel(
      id: json['id'] as int,
      nameAr: json['nameAr'] as String? ?? '',
      nameEn: json['nameEn'] as String?,
      permissions: (json['permissions'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          [],
    );
  }
}

/// Known permission keys for the multi-select list
const List<Map<String, String>> _allPermissions = [
  {'key': 'sales.view', 'label': 'عرض المبيعات'},
  {'key': 'sales.create', 'label': 'إنشاء مبيعات'},
  {'key': 'sales.edit', 'label': 'تعديل المبيعات'},
  {'key': 'sales.delete', 'label': 'حذف المبيعات'},
  {'key': 'sales.post', 'label': 'ترحيل المبيعات'},
  {'key': 'purchases.view', 'label': 'عرض المشتريات'},
  {'key': 'purchases.create', 'label': 'إنشاء مشتريات'},
  {'key': 'purchases.edit', 'label': 'تعديل المشتريات'},
  {'key': 'purchases.delete', 'label': 'حذف المشتريات'},
  {'key': 'purchases.post', 'label': 'ترحيل المشتريات'},
  {'key': 'inventory.view', 'label': 'عرض المخزون'},
  {'key': 'inventory.create', 'label': 'إنشاء مخزون'},
  {'key': 'inventory.edit', 'label': 'تعديل المخزون'},
  {'key': 'inventory.adjust', 'label': 'تسوية المخزون'},
  {'key': 'treasury.view', 'label': 'عرض الخزينة'},
  {'key': 'treasury.create', 'label': 'إنشاء سندات'},
  {'key': 'treasury.edit', 'label': 'تعديل سندات'},
  {'key': 'treasury.transfer', 'label': 'التحويلات'},
  {'key': 'accounting.view', 'label': 'عرض المحاسبة'},
  {'key': 'accounting.create', 'label': 'إنشاء قيود'},
  {'key': 'reports.view', 'label': 'عرض التقارير'},
  {'key': 'settings.view', 'label': 'عرض الإعدادات'},
  {'key': 'settings.edit', 'label': 'تعديل الإعدادات'},
  {'key': 'users.view', 'label': 'عرض المستخدمين'},
  {'key': 'users.manage', 'label': 'إدارة المستخدمين'},
  {'key': 'roles.manage', 'label': 'إدارة الأدوار'},
];

class RolesScreen extends StatefulWidget {
  const RolesScreen({super.key});

  @override
  State<RolesScreen> createState() => _RolesScreenState();
}

class _RolesScreenState extends State<RolesScreen> {
  List<RoleModel> _roles = [];
  bool _isLoading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadRoles();
  }

  Future<void> _loadRoles() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    final api = context.read<ApiClient>();
    final response = await api.get<List<RoleModel>>(
      ApiConstants.roles,
      fromJson: (json) => (json as List)
          .map((e) => RoleModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

    if (mounted) {
      setState(() {
        _isLoading = false;
        if (response.success && response.data != null) {
          _roles = response.data!;
        } else {
          _error = response.errorMessage;
        }
      });
    }
  }

  void _showRoleDialog({RoleModel? role}) {
    final isEdit = role != null;
    final nameArCtrl = TextEditingController(text: role?.nameAr ?? '');
    final nameEnCtrl = TextEditingController(text: role?.nameEn ?? '');
    final selectedPermissions = <String>{...?role?.permissions};
    final formKey = GlobalKey<FormState>();
    bool isSaving = false;

    showDialog(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          title: Text(isEdit ? 'تعديل الدور' : 'إضافة دور جديد'),
          content: SizedBox(
            width: double.maxFinite,
            child: Form(
              key: formKey,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  TextFormField(
                    controller: nameArCtrl,
                    decoration: const InputDecoration(
                        labelText: 'اسم الدور بالعربية *'),
                    validator: (v) =>
                        (v == null || v.isEmpty) ? 'مطلوب' : null,
                  ),
                  const SizedBox(height: 8),
                  TextFormField(
                    controller: nameEnCtrl,
                    decoration: const InputDecoration(
                        labelText: 'اسم الدور بالإنجليزية'),
                    textDirection: TextDirection.ltr,
                  ),
                  const SizedBox(height: 12),
                  const Align(
                    alignment: Alignment.centerRight,
                    child: Text('الصلاحيات:',
                        style: TextStyle(fontWeight: FontWeight.bold)),
                  ),
                  const SizedBox(height: 4),
                  SizedBox(
                    height: 300,
                    child: ListView.builder(
                      shrinkWrap: true,
                      itemCount: _allPermissions.length,
                      itemBuilder: (_, i) {
                        final perm = _allPermissions[i];
                        final key = perm['key']!;
                        final label = perm['label']!;
                        return CheckboxListTile(
                          title: Text(label,
                              style: const TextStyle(fontSize: 13)),
                          subtitle: Text(key,
                              style: const TextStyle(
                                  fontSize: 10, color: Colors.grey),
                              textDirection: TextDirection.ltr),
                          dense: true,
                          value: selectedPermissions.contains(key),
                          onChanged: (v) {
                            setDialogState(() {
                              if (v == true) {
                                selectedPermissions.add(key);
                              } else {
                                selectedPermissions.remove(key);
                              }
                            });
                          },
                          controlAffinity: ListTileControlAffinity.leading,
                        );
                      },
                    ),
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

                      final data = {
                        'nameAr': nameArCtrl.text,
                        'nameEn': nameEnCtrl.text.isEmpty
                            ? null
                            : nameEnCtrl.text,
                        'permissions': selectedPermissions.toList(),
                      };

                      final api = context.read<ApiClient>();
                      final response = isEdit
                          ? await api.put(
                              '${ApiConstants.roles}/${role.id}',
                              data: data)
                          : await api.post(ApiConstants.roles,
                              data: data);

                      if (ctx.mounted) {
                        Navigator.pop(ctx);
                        if (response.success) {
                          _loadRoles();
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                                content: Text(isEdit
                                    ? 'تم تعديل الدور بنجاح'
                                    : 'تم إضافة الدور بنجاح')),
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
      appBar: AppBar(title: const Text('إدارة الأدوار')),
      floatingActionButton: FloatingActionButton(
        onPressed: () => _showRoleDialog(),
        child: const Icon(Icons.add),
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
                          onPressed: _loadRoles,
                          child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadRoles,
                  child: _roles.isEmpty
                      ? const Center(child: Text('لا توجد أدوار'))
                      : ListView.builder(
                          itemCount: _roles.length,
                          itemBuilder: (ctx, i) {
                            final role = _roles[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(
                                  horizontal: 12, vertical: 4),
                              child: ListTile(
                                leading: Container(
                                  width: 40,
                                  height: 40,
                                  decoration: BoxDecoration(
                                    color: Colors.deepPurple.shade100,
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: Icon(Icons.admin_panel_settings,
                                      color: Colors.deepPurple.shade700),
                                ),
                                title: Text(role.nameAr,
                                    style: const TextStyle(
                                        fontWeight: FontWeight.bold)),
                                subtitle: Column(
                                  crossAxisAlignment:
                                      CrossAxisAlignment.start,
                                  children: [
                                    if (role.nameEn != null &&
                                        role.nameEn!.isNotEmpty)
                                      Text(role.nameEn!,
                                          style: const TextStyle(
                                              fontSize: 12),
                                          textDirection: TextDirection.ltr),
                                    Text(
                                      '${role.permissions.length} صلاحية',
                                      style: TextStyle(
                                          fontSize: 12,
                                          color: Colors.grey.shade600),
                                    ),
                                  ],
                                ),
                                trailing: const Icon(Icons.chevron_left),
                                onTap: () => _showRoleDialog(role: role),
                              ),
                            );
                          },
                        ),
                ),
    );
  }
}
