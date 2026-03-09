import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../core/models/invoice_model.dart';
import '../../../core/models/treasury_model.dart';

class CashReceiptCreateScreen extends StatefulWidget {
  const CashReceiptCreateScreen({super.key});

  @override
  State<CashReceiptCreateScreen> createState() => _CashReceiptCreateScreenState();
}

class _CashReceiptCreateScreenState extends State<CashReceiptCreateScreen> {
  final _formKey = GlobalKey<FormState>();
  bool _isLoading = true;
  bool _isSaving = false;
  final _dateFormat = DateFormat('yyyy/MM/dd', 'ar');

  DateTime _receiptDate = DateTime.now();
  int? _selectedCustomerId;
  int? _selectedCashboxId;
  final _amountController = TextEditingController();
  final _notesController = TextEditingController();

  List<CustomerModel> _customers = [];
  List<CashboxModel> _cashboxes = [];

  @override
  void initState() {
    super.initState();
    _loadLookups();
  }

  @override
  void dispose() {
    _amountController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _loadLookups() async {
    setState(() => _isLoading = true);
    final offlineData = context.read<OfflineDataProvider>();

    try {
      final customerRows = await offlineData.getAll('customers');
      final cashboxRows = await offlineData.getAll('cashboxes');

      if (mounted) {
        setState(() {
          _isLoading = false;
          _customers = customerRows.map((row) => CustomerModel(
            id: row['id'] as int,
            code: row['code'] as String? ?? '',
            nameAr: row['name_ar'] as String? ?? '',
            nameEn: row['name_en'] as String?,
            phone: row['phone'] as String?,
            email: row['email'] as String?,
            address: null,
            isActive: (row['is_active'] as int?) == 1,
          )).toList();
          _cashboxes = cashboxRows.map((row) => CashboxModel(
            id: row['id'] as int,
            code: row['code'] as String? ?? '',
            nameAr: row['name_ar'] as String? ?? '',
            currentBalance: (row['current_balance'] as num?)?.toDouble() ?? 0,
            isDefault: (row['is_default'] as int?) == 1,
            isActive: (row['is_active'] as int?) == 1,
          )).toList();
          if (_cashboxes.isNotEmpty) {
            final defaultBox = _cashboxes.firstWhere(
              (c) => c.isDefault,
              orElse: () => _cashboxes.first,
            );
            _selectedCashboxId = defaultBox.id;
          }
        });
      }
    } catch (e) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _selectDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _receiptDate,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
      locale: const Locale('ar'),
    );
    if (picked != null) {
      setState(() => _receiptDate = picked);
    }
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    if (_selectedCashboxId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('يرجى اختيار الخزنة')),
      );
      return;
    }

    final amount = double.tryParse(_amountController.text);
    if (amount == null || amount <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('يرجى إدخال مبلغ صحيح')),
      );
      return;
    }

    setState(() => _isSaving = true);
    final offlineData = context.read<OfflineDataProvider>();

    final data = {
      'receipt_date': _receiptDate.toIso8601String(),
      'customer_id': _selectedCustomerId,
      'cashbox_id': _selectedCashboxId,
      'amount': amount,
      'status': 'Draft',
      'notes': _notesController.text.isEmpty ? null : _notesController.text,
    };

    try {
      await offlineData.create(entityType: 'CashReceipt', table: 'cash_receipts', data: data);
      setState(() => _isSaving = false);

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تم إنشاء سند القبض بنجاح')),
        );
        Navigator.pop(context, true);
      }
    } catch (e) {
      setState(() => _isSaving = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('حدث خطأ: $e')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('سند قبض جديد'),
        actions: [
          TextButton(
            onPressed: _isSaving ? null : _save,
            child: _isSaving
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                  )
                : const Text('حفظ', style: TextStyle(color: Colors.white)),
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
                  // Date
                  InkWell(
                    onTap: _selectDate,
                    child: InputDecorator(
                      decoration: const InputDecoration(
                        labelText: 'تاريخ السند',
                        border: OutlineInputBorder(),
                        prefixIcon: Icon(Icons.calendar_today),
                      ),
                      child: Text(_dateFormat.format(_receiptDate)),
                    ),
                  ),
                  const SizedBox(height: 16),

                  // Cashbox
                  DropdownButtonFormField<int>(
                    value: _selectedCashboxId,
                    decoration: const InputDecoration(
                      labelText: 'الخزنة *',
                      border: OutlineInputBorder(),
                    ),
                    items: _cashboxes.map((c) {
                      return DropdownMenuItem<int>(
                        value: c.id,
                        child: Text('${c.nameAr} (${c.currentBalance.toStringAsFixed(2)} ج.م)'),
                      );
                    }).toList(),
                    onChanged: (v) => setState(() => _selectedCashboxId = v),
                    validator: (v) => v == null ? 'مطلوب' : null,
                  ),
                  const SizedBox(height: 16),

                  // Customer (optional)
                  DropdownButtonFormField<int?>(
                    value: _selectedCustomerId,
                    decoration: const InputDecoration(
                      labelText: 'العميل (اختياري)',
                      border: OutlineInputBorder(),
                    ),
                    items: [
                      const DropdownMenuItem<int?>(
                        value: null,
                        child: Text('-- بدون عميل --'),
                      ),
                      ..._customers.map((c) {
                        return DropdownMenuItem<int?>(
                          value: c.id,
                          child: Text(c.nameAr),
                        );
                      }),
                    ],
                    onChanged: (v) => setState(() => _selectedCustomerId = v),
                  ),
                  const SizedBox(height: 16),

                  // Amount
                  TextFormField(
                    controller: _amountController,
                    decoration: const InputDecoration(
                      labelText: 'المبلغ *',
                      border: OutlineInputBorder(),
                      prefixIcon: Icon(Icons.attach_money),
                      suffixText: 'ج.م',
                    ),
                    keyboardType: TextInputType.number,
                    validator: (v) {
                      if (v == null || v.isEmpty) return 'مطلوب';
                      final amount = double.tryParse(v);
                      if (amount == null || amount <= 0) return 'أدخل مبلغ صحيح';
                      return null;
                    },
                  ),
                  const SizedBox(height: 16),

                  // Notes
                  TextFormField(
                    controller: _notesController,
                    decoration: const InputDecoration(
                      labelText: 'ملاحظات',
                      border: OutlineInputBorder(),
                    ),
                    maxLines: 3,
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
                        : const Text('حفظ السند'),
                  ),
                ],
              ),
            ),
    );
  }
}
