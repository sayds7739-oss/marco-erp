import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/providers/offline_data_provider.dart';

class CategoryDetailScreen extends StatefulWidget {
  final Map<String, dynamic>? category;

  const CategoryDetailScreen({super.key, this.category});

  @override
  State<CategoryDetailScreen> createState() => _CategoryDetailScreenState();
}

class _CategoryDetailScreenState extends State<CategoryDetailScreen> {
  final _formKey = GlobalKey<FormState>();
  bool _isLoading = false;
  bool _isSaving = false;

  late TextEditingController _nameArController;
  late TextEditingController _nameEnController;
  int? _parentCategoryId;
  bool _isActive = true;

  List<Map<String, dynamic>> _parentCategories = [];

  bool get _isEditing => widget.category != null;

  @override
  void initState() {
    super.initState();
    final c = widget.category;
    _nameArController = TextEditingController(text: c?['name_ar'] as String? ?? '');
    _nameEnController = TextEditingController(text: c?['name_en'] as String? ?? '');
    _parentCategoryId = c?['parent_category_id'] as int?;
    _isActive = (c?['is_active'] as int?) != 0;
    _loadLookups();
  }

  @override
  void dispose() {
    _nameArController.dispose();
    _nameEnController.dispose();
    super.dispose();
  }

  Future<void> _loadLookups() async {
    setState(() => _isLoading = true);
    final offlineData = context.read<OfflineDataProvider>();

    try {
      final categoriesRows = await offlineData.getAll('categories');

      if (mounted) {
        setState(() {
          _isLoading = false;
          // Exclude the current category from parent list to prevent self-reference
          _parentCategories = categoriesRows
              .where((row) => !_isEditing || row['id'] != widget.category!['id'])
              .map((row) => {
                    'id': row['id'],
                    'nameAr': row['name_ar'] ?? '',
                  })
              .toList();
        });
      }
    } catch (e) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isSaving = true);
    final offlineData = context.read<OfflineDataProvider>();

    final data = {
      'name_ar': _nameArController.text,
      'name_en': _nameEnController.text.isEmpty ? null : _nameEnController.text,
      'parent_category_id': _parentCategoryId,
      'is_active': _isActive ? 1 : 0,
    };

    try {
      if (_isEditing) {
        await offlineData.update(
          entityType: 'Category',
          table: 'categories',
          id: widget.category!['id'] as int,
          data: data,
        );
      } else {
        await offlineData.create(entityType: 'Category', table: 'categories', data: data);
      }

      setState(() => _isSaving = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(_isEditing ? 'تم تحديث التصنيف بنجاح' : 'تم إضافة التصنيف بنجاح')),
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
        title: Text(_isEditing ? 'تعديل تصنيف' : 'إضافة تصنيف جديد'),
        actions: [
          if (_isEditing)
            IconButton(
              icon: const Icon(Icons.delete),
              onPressed: _confirmDelete,
            ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : Form(
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

                  // Parent Category (optional)
                  DropdownButtonFormField<int?>(
                    value: _parentCategoryId,
                    decoration: const InputDecoration(
                      labelText: 'التصنيف الأب (اختياري)',
                      border: OutlineInputBorder(),
                    ),
                    items: [
                      const DropdownMenuItem<int?>(
                        value: null,
                        child: Text('-- بدون تصنيف أب --'),
                      ),
                      ..._parentCategories.map((c) {
                        return DropdownMenuItem<int?>(
                          value: c['id'] as int,
                          child: Text(c['nameAr'] as String? ?? ''),
                        );
                      }),
                    ],
                    onChanged: (v) => setState(() => _parentCategoryId = v),
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
        content: const Text('هل أنت متأكد من حذف هذا التصنيف؟'),
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
          entityType: 'Category',
          table: 'categories',
          id: widget.category!['id'] as int,
        );
        setState(() => _isSaving = false);
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تم حذف التصنيف بنجاح')),
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
