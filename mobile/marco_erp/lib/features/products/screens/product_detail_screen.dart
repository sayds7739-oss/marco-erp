import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../core/models/product_model.dart';

class ProductDetailScreen extends StatefulWidget {
  final ProductModel? product;

  const ProductDetailScreen({super.key, this.product});

  @override
  State<ProductDetailScreen> createState() => _ProductDetailScreenState();
}

class _ProductDetailScreenState extends State<ProductDetailScreen> {
  final _formKey = GlobalKey<FormState>();
  bool _isLoading = false;
  bool _isSaving = false;

  late TextEditingController _codeController;
  late TextEditingController _nameArController;
  late TextEditingController _nameEnController;
  late TextEditingController _barcodeController;
  late TextEditingController _descriptionController;
  late TextEditingController _wholesalePriceController;
  late TextEditingController _retailPriceController;

  int? _selectedCategoryId;
  int? _selectedUnitId;
  bool _isActive = true;

  List<Map<String, dynamic>> _categories = [];
  List<Map<String, dynamic>> _units = [];

  bool get _isEditing => widget.product != null;

  @override
  void initState() {
    super.initState();
    final p = widget.product;
    _codeController = TextEditingController(text: p?.code ?? '');
    _nameArController = TextEditingController(text: p?.nameAr ?? '');
    _nameEnController = TextEditingController(text: p?.nameEn ?? '');
    _barcodeController = TextEditingController(text: p?.barcode ?? '');
    _descriptionController = TextEditingController(text: p?.description ?? '');
    _wholesalePriceController = TextEditingController(text: p?.wholesalePrice.toString() ?? '0');
    _retailPriceController = TextEditingController(text: p?.retailPrice.toString() ?? '0');
    _selectedCategoryId = p?.categoryId;
    _selectedUnitId = p?.baseUnitId;
    _isActive = p?.isActive ?? true;
    _loadLookups();
  }

  @override
  void dispose() {
    _codeController.dispose();
    _nameArController.dispose();
    _nameEnController.dispose();
    _barcodeController.dispose();
    _descriptionController.dispose();
    _wholesalePriceController.dispose();
    _retailPriceController.dispose();
    super.dispose();
  }

  Future<void> _loadLookups() async {
    setState(() => _isLoading = true);
    final offlineData = context.read<OfflineDataProvider>();

    try {
      final categoriesRows = await offlineData.getAll('categories');
      final unitsRows = await offlineData.getAll('units');

      if (mounted) {
        setState(() {
          _isLoading = false;
          _categories = categoriesRows.map((row) => {
            'id': row['id'],
            'nameAr': row['name_ar'] ?? '',
          }).toList();
          _units = unitsRows.map((row) => {
            'id': row['id'],
            'nameAr': row['name_ar'] ?? '',
          }).toList();
        });
      }
    } catch (e) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    if (_selectedCategoryId == null || _selectedUnitId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('يرجى اختيار التصنيف والوحدة')),
      );
      return;
    }

    setState(() => _isSaving = true);
    final offlineData = context.read<OfflineDataProvider>();

    final data = {
      'code': _codeController.text,
      'name_ar': _nameArController.text,
      'name_en': _nameEnController.text.isEmpty ? null : _nameEnController.text,
      'barcode': _barcodeController.text.isEmpty ? null : _barcodeController.text,
      'description': _descriptionController.text.isEmpty ? null : _descriptionController.text,
      'category_id': _selectedCategoryId,
      'base_unit_id': _selectedUnitId,
      'wholesale_price': double.tryParse(_wholesalePriceController.text) ?? 0,
      'retail_price': double.tryParse(_retailPriceController.text) ?? 0,
      'is_active': _isActive ? 1 : 0,
    };

    try {
      if (_isEditing) {
        await offlineData.update(entityType: 'Product', table: 'products', id: widget.product!.id, data: data);
      } else {
        await offlineData.create(entityType: 'Product', table: 'products', data: data);
      }

      setState(() => _isSaving = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(_isEditing ? 'تم تحديث الصنف بنجاح' : 'تم إضافة الصنف بنجاح')),
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
        title: Text(_isEditing ? 'تعديل صنف' : 'إضافة صنف جديد'),
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

                  // Barcode
                  TextFormField(
                    controller: _barcodeController,
                    decoration: const InputDecoration(
                      labelText: 'الباركود',
                      border: OutlineInputBorder(),
                      prefixIcon: Icon(Icons.qr_code),
                    ),
                  ),
                  const SizedBox(height: 16),

                  // Category
                  DropdownButtonFormField<int>(
                    value: _selectedCategoryId,
                    decoration: const InputDecoration(
                      labelText: 'التصنيف *',
                      border: OutlineInputBorder(),
                    ),
                    items: _categories.map((c) {
                      return DropdownMenuItem<int>(
                        value: c['id'] as int,
                        child: Text(c['nameAr'] as String? ?? ''),
                      );
                    }).toList(),
                    onChanged: (v) => setState(() => _selectedCategoryId = v),
                    validator: (v) => v == null ? 'مطلوب' : null,
                  ),
                  const SizedBox(height: 16),

                  // Unit
                  DropdownButtonFormField<int>(
                    value: _selectedUnitId,
                    decoration: const InputDecoration(
                      labelText: 'الوحدة الأساسية *',
                      border: OutlineInputBorder(),
                    ),
                    items: _units.map((u) {
                      return DropdownMenuItem<int>(
                        value: u['id'] as int,
                        child: Text(u['nameAr'] as String? ?? ''),
                      );
                    }).toList(),
                    onChanged: (v) => setState(() => _selectedUnitId = v),
                    validator: (v) => v == null ? 'مطلوب' : null,
                  ),
                  const SizedBox(height: 16),

                  // Prices
                  Row(
                    children: [
                      Expanded(
                        child: TextFormField(
                          controller: _wholesalePriceController,
                          decoration: const InputDecoration(
                            labelText: 'سعر الجملة',
                            border: OutlineInputBorder(),
                          ),
                          keyboardType: TextInputType.number,
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: TextFormField(
                          controller: _retailPriceController,
                          decoration: const InputDecoration(
                            labelText: 'سعر التجزئة',
                            border: OutlineInputBorder(),
                          ),
                          keyboardType: TextInputType.number,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),

                  // Description
                  TextFormField(
                    controller: _descriptionController,
                    decoration: const InputDecoration(
                      labelText: 'الوصف',
                      border: OutlineInputBorder(),
                    ),
                    maxLines: 3,
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
        content: const Text('هل أنت متأكد من حذف هذا الصنف؟'),
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
        await offlineData.softDelete(entityType: 'Product', table: 'products', id: widget.product!.id);
        setState(() => _isSaving = false);
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تم حذف الصنف بنجاح')),
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
