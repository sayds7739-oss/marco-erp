import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/providers/offline_data_provider.dart';

class WarehouseDetailScreen extends StatefulWidget {
  final Map<String, dynamic>? warehouse;

  const WarehouseDetailScreen({super.key, this.warehouse});

  @override
  State<WarehouseDetailScreen> createState() => _WarehouseDetailScreenState();
}

class _WarehouseDetailScreenState extends State<WarehouseDetailScreen> {
  final _formKey = GlobalKey<FormState>();
  bool _isSaving = false;

  late TextEditingController _codeController;
  late TextEditingController _nameArController;
  late TextEditingController _nameEnController;
  bool _isDefault = false;
  bool _isActive = true;

  bool get _isEditing => widget.warehouse != null;

  @override
  void initState() {
    super.initState();
    final w = widget.warehouse;
    _codeController = TextEditingController(text: w?['code'] as String? ?? '');
    _nameArController = TextEditingController(text: w?['name_ar'] as String? ?? '');
    _nameEnController = TextEditingController(text: w?['name_en'] as String? ?? '');
    _isDefault = (w?['is_default'] as int?) == 1;
    _isActive = (w?['is_active'] as int?) != 0;
  }

  @override
  void dispose() {
    _codeController.dispose();
    _nameArController.dispose();
    _nameEnController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isSaving = true);
    final offlineData = context.read<OfflineDataProvider>();

    final data = {
      'code': _codeController.text,
      'name_ar': _nameArController.text,
      'name_en': _nameEnController.text.isEmpty ? null : _nameEnController.text,
      'is_default': _isDefault ? 1 : 0,
      'is_active': _isActive ? 1 : 0,
    };

    try {
      if (_isEditing) {
        await offlineData.update(
          entityType: 'Warehouse',
          table: 'warehouses',
          id: widget.warehouse!['id'] as int,
          data: data,
        );
      } else {
        await offlineData.create(entityType: 'Warehouse', table: 'warehouses', data: data);
      }

      setState(() => _isSaving = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(_isEditing ? 'تم تحديث المخزن بنجاح' : 'تم إضافة المخزن بنجاح')),
        );
        Navigator.pop(context, true);
      }
    } catch (e) {
      setState(() => _isSaving = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e.toString())),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(_isEditing ? 'تعديل مخزن' : 'إضافة مخزن جديد'),
        actions: [
          if (_isEditing)
            IconButton(
              icon: const Icon(Icons.delete),
              onPressed: _confirmDelete,
            ),
        ],
      ),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            // Code
            TextFormField(
              controller: _codeController,
              decoration: const InputDecoration(
                labelText: 'الكود *',
                border: OutlineInputBorder(),
              ),
              validator: (v) => v == null || v.isEmpty ? 'مطلوب' : null,
            ),
            const SizedBox(height: 16),

            // Name Arabic
            TextFormField(
              controller: _nameArController,
              decoration: const InputDecoration(
                labelText: 'الاسم بالعربي *',
                border: OutlineInputBorder(),
              ),
              validator: (v) => v == null || v.isEmpty ? 'مطلوب' : null,
            ),
            const SizedBox(height: 16),

            // Name English
            TextFormField(
              controller: _nameEnController,
              decoration: const InputDecoration(
                labelText: 'الاسم بالإنجليزي',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 16),

            // Is Default
            SwitchListTile(
              title: const Text('المخزن الافتراضي'),
              subtitle: const Text('سيتم استخدام هذا المخزن تلقائياً'),
              value: _isDefault,
              onChanged: (v) => setState(() => _isDefault = v),
            ),

            // Is Active
            SwitchListTile(
              title: const Text('نشط'),
              value: _isActive,
              onChanged: (v) => setState(() => _isActive = v),
            ),
            const SizedBox(height: 24),

            // Save Button
            ElevatedButton(
              onPressed: _isSaving ? null : _save,
              style: ElevatedButton.styleFrom(
                padding: const EdgeInsets.symmetric(vertical: 16),
              ),
              child: _isSaving
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : Text(_isEditing ? 'تحديث' : 'حفظ'),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _confirmDelete() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تأكيد الحذف'),
        content: const Text('هل أنت متأكد من حذف هذا المخزن؟'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('إلغاء'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: TextButton.styleFrom(foregroundColor: Colors.red),
            child: const Text('حذف'),
          ),
        ],
      ),
    );

    if (confirmed == true) {
      setState(() => _isSaving = true);
      try {
        final offlineData = context.read<OfflineDataProvider>();
        await offlineData.softDelete(
          entityType: 'Warehouse',
          table: 'warehouses',
          id: widget.warehouse!['id'] as int,
        );
        setState(() => _isSaving = false);
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تم حذف المخزن بنجاح')),
          );
          Navigator.pop(context, true);
        }
      } catch (e) {
        setState(() => _isSaving = false);
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(e.toString())),
          );
        }
      }
    }
  }
}
