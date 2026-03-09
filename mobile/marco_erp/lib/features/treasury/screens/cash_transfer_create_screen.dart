import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../core/models/treasury_model.dart';

class CashTransferCreateScreen extends StatefulWidget {
  const CashTransferCreateScreen({super.key});

  @override
  State<CashTransferCreateScreen> createState() => _CashTransferCreateScreenState();
}

class _CashTransferCreateScreenState extends State<CashTransferCreateScreen> {
  final _formKey = GlobalKey<FormState>();
  bool _isLoading = true;
  bool _isSaving = false;
  final _dateFormat = DateFormat('yyyy/MM/dd', 'ar');

  DateTime _transferDate = DateTime.now();
  int? _fromCashboxId;
  int? _toCashboxId;
  final _amountController = TextEditingController();
  final _notesController = TextEditingController();

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
      final cashboxRows = await offlineData.getAll('cashboxes');

      if (mounted) {
        setState(() {
          _isLoading = false;
          _cashboxes = cashboxRows.map((row) => CashboxModel(
            id: row['id'] as int,
            code: row['code'] as String? ?? '',
            nameAr: row['name_ar'] as String? ?? '',
            currentBalance: (row['current_balance'] as num?)?.toDouble() ?? 0,
            isDefault: (row['is_default'] as int?) == 1,
            isActive: (row['is_active'] as int?) == 1,
          )).toList();
        });
      }
    } catch (e) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _selectDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _transferDate,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
      locale: const Locale('ar'),
    );
    if (picked != null) {
      setState(() => _transferDate = picked);
    }
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    if (_fromCashboxId == null || _toCashboxId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('يرجى اختيار خزنة المصدر وخزنة الوجهة')),
      );
      return;
    }
    if (_fromCashboxId == _toCashboxId) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('لا يمكن التحويل من وإلى نفس الخزنة')),
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
      'transfer_date': _transferDate.toIso8601String(),
      'from_cashbox_id': _fromCashboxId,
      'to_cashbox_id': _toCashboxId,
      'amount': amount,
      'status': 'Draft',
      'notes': _notesController.text.isEmpty ? null : _notesController.text,
    };

    try {
      await offlineData.create(entityType: 'CashTransfer', table: 'cash_transfers', data: data);
      setState(() => _isSaving = false);

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تم إنشاء التحويل بنجاح')),
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
        title: const Text('تحويل نقدي جديد'),
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
                        labelText: 'تاريخ التحويل',
                        border: OutlineInputBorder(),
                        prefixIcon: Icon(Icons.calendar_today),
                      ),
                      child: Text(_dateFormat.format(_transferDate)),
                    ),
                  ),
                  const SizedBox(height: 16),

                  // From Cashbox
                  DropdownButtonFormField<int>(
                    value: _fromCashboxId,
                    decoration: const InputDecoration(
                      labelText: 'من خزنة *',
                      border: OutlineInputBorder(),
                    ),
                    items: _cashboxes.map((c) {
                      return DropdownMenuItem<int>(
                        value: c.id,
                        child: Text('${c.nameAr} (${c.currentBalance.toStringAsFixed(2)} ج.م)'),
                      );
                    }).toList(),
                    onChanged: (v) => setState(() => _fromCashboxId = v),
                    validator: (v) => v == null ? 'مطلوب' : null,
                  ),
                  const SizedBox(height: 16),

                  // To Cashbox
                  DropdownButtonFormField<int>(
                    value: _toCashboxId,
                    decoration: const InputDecoration(
                      labelText: 'إلى خزنة *',
                      border: OutlineInputBorder(),
                    ),
                    items: _cashboxes.map((c) {
                      return DropdownMenuItem<int>(
                        value: c.id,
                        child: Text('${c.nameAr} (${c.currentBalance.toStringAsFixed(2)} ج.م)'),
                      );
                    }).toList(),
                    onChanged: (v) => setState(() => _toCashboxId = v),
                    validator: (v) => v == null ? 'مطلوب' : null,
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
                        : const Text('حفظ التحويل'),
                  ),
                ],
              ),
            ),
    );
  }
}
