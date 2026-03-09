import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/providers/offline_data_provider.dart';

class UnitDetailScreen extends StatefulWidget {
  final Map<String, dynamic>? unit;

  const UnitDetailScreen({super.key, this.unit});

  @override
  State<UnitDetailScreen> createState() => _UnitDetailScreenState();
}

class _UnitDetailScreenState extends State<UnitDetailScreen> {
  final _formKey = GlobalKey<FormState>();
  bool _isSaving = false;

  late TextEditingController _nameArController;
  late TextEditingController _nameEnController;
  bool _isActive = true;

  bool get _isEditing => widget.unit != null;

  @override
  void initState() {
    super.initState();
    final u = widget.unit;
    _nameArController = TextEditingController(text: u?['name_ar'] as String? ?? '');
    _nameEnController = TextEditingController(text: u?['name_en'] as String? ?? '');
    _isActive = (u?['is_active'] as int?) != 0;
  }

  @override
  void dispose() {
    _nameArController.dispose();
    _nameEnController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isSaving = true);
    final offlineData = context.read<OfflineDataProvider>();

    final data = {
      'name_ar': _nameArController.text,
      'name_en': _nameEnController.text.isEmpty ? null : _nameEnController.text,
      'is_active': _isActive ? 1 : 0,
    };

    try {
      if (_isEditing) {
        await offlineData.update(
          entityType: 'Unit',
          table: 'units',
          id: widget.unit!['id'] as int,
          data: data,
        );
      } else {
        await offlineData.create(entityType: 'Unit', table: 'units', data: data);
      }

      setState(() => _isSaving = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(_isEditing ? 'تم تحديث الوحدة بنجاح' : 'تم إضافة الوحدة بنجاح')),
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
        title: Text(_isEditing ? 'تعديل وحدة' : 'إضافة وحدة جديدة'),
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
        content: const Text('هل أنت متأكد من حذف هذه الوحدة؟'),
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
          entityType: 'Unit',
          table: 'units',
          id: widget.unit!['id'] as int,
        );
        setState(() => _isSaving = false);
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تم حذف الوحدة بنجاح')),
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
