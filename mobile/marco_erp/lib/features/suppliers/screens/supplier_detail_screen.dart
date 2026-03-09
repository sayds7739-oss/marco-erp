import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../core/models/invoice_model.dart';

class SupplierDetailScreen extends StatefulWidget {
  final SupplierModel? supplier;

  const SupplierDetailScreen({super.key, this.supplier});

  @override
  State<SupplierDetailScreen> createState() => _SupplierDetailScreenState();
}

class _SupplierDetailScreenState extends State<SupplierDetailScreen> {
  final _formKey = GlobalKey<FormState>();
  bool _isSaving = false;

  late TextEditingController _codeController;
  late TextEditingController _nameArController;
  late TextEditingController _nameEnController;
  late TextEditingController _phoneController;
  late TextEditingController _emailController;
  late TextEditingController _addressController;
  late TextEditingController _taxNumberController;
  bool _isActive = true;

  bool get _isEditing => widget.supplier != null;

  @override
  void initState() {
    super.initState();
    final s = widget.supplier;
    _codeController = TextEditingController(text: s?.code ?? '');
    _nameArController = TextEditingController(text: s?.nameAr ?? '');
    _nameEnController = TextEditingController(text: '');
    _phoneController = TextEditingController(text: s?.phone ?? '');
    _emailController = TextEditingController(text: s?.email ?? '');
    _addressController = TextEditingController(text: '');
    _taxNumberController = TextEditingController(text: '');
    _isActive = s?.isActive ?? true;

    // If editing, load full details from local DB
    if (_isEditing) {
      _loadFullDetails();
    }
  }

  Future<void> _loadFullDetails() async {
    final offlineData = context.read<OfflineDataProvider>();
    final row = await offlineData.getById('suppliers', widget.supplier!.id);
    if (row != null && mounted) {
      setState(() {
        _nameEnController.text = row['name_en'] as String? ?? '';
        _addressController.text = row['address'] as String? ?? '';
        _taxNumberController.text = row['tax_number'] as String? ?? '';
      });
    }
  }

  @override
  void dispose() {
    _codeController.dispose();
    _nameArController.dispose();
    _nameEnController.dispose();
    _phoneController.dispose();
    _emailController.dispose();
    _addressController.dispose();
    _taxNumberController.dispose();
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
      'phone': _phoneController.text.isEmpty ? null : _phoneController.text,
      'email': _emailController.text.isEmpty ? null : _emailController.text,
      'address': _addressController.text.isEmpty ? null : _addressController.text,
      'tax_number': _taxNumberController.text.isEmpty ? null : _taxNumberController.text,
      'is_active': _isActive ? 1 : 0,
    };

    try {
      if (_isEditing) {
        await offlineData.update(entityType: 'Supplier', table: 'suppliers', id: widget.supplier!.id, data: data);
      } else {
        await offlineData.create(entityType: 'Supplier', table: 'suppliers', data: data);
      }

      setState(() => _isSaving = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(_isEditing ? 'تم تحديث المورد بنجاح' : 'تم إضافة المورد بنجاح')),
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
        title: Text(_isEditing ? 'تعديل مورد' : 'إضافة مورد جديد'),
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

            // Phone
            TextFormField(
              controller: _phoneController,
              decoration: const InputDecoration(
                labelText: 'رقم الهاتف',
                border: OutlineInputBorder(),
                prefixIcon: Icon(Icons.phone),
              ),
              keyboardType: TextInputType.phone,
            ),
            const SizedBox(height: 16),

            // Email
            TextFormField(
              controller: _emailController,
              decoration: const InputDecoration(
                labelText: 'البريد الإلكتروني',
                border: OutlineInputBorder(),
                prefixIcon: Icon(Icons.email),
              ),
              keyboardType: TextInputType.emailAddress,
            ),
            const SizedBox(height: 16),

            // Address
            TextFormField(
              controller: _addressController,
              decoration: const InputDecoration(
                labelText: 'العنوان',
                border: OutlineInputBorder(),
                prefixIcon: Icon(Icons.location_on),
              ),
              maxLines: 2,
            ),
            const SizedBox(height: 16),

            // Tax Number
            TextFormField(
              controller: _taxNumberController,
              decoration: const InputDecoration(
                labelText: 'الرقم الضريبي',
                border: OutlineInputBorder(),
                prefixIcon: Icon(Icons.receipt_long),
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
        content: const Text('هل أنت متأكد من حذف هذا المورد؟'),
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
        await offlineData.softDelete(entityType: 'Supplier', table: 'suppliers', id: widget.supplier!.id);
        setState(() => _isSaving = false);
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تم حذف المورد بنجاح')),
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
