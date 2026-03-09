import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';
import '../../../core/models/invoice_model.dart';

class _ReturnLineItem {
  int? productId;
  String? productName;
  double quantity;
  double unitPrice;
  double discount;
  double vat;

  _ReturnLineItem({
    this.productId,
    this.productName,
    this.quantity = 1,
    this.unitPrice = 0,
    this.discount = 0,
    this.vat = 0,
  });

  double get lineTotal => (quantity * unitPrice) - discount + vat;
}

class _ProductLookup {
  final int id;
  final String code;
  final String nameAr;
  final double wholesalePrice;

  _ProductLookup({
    required this.id,
    required this.code,
    required this.nameAr,
    required this.wholesalePrice,
  });

  factory _ProductLookup.fromJson(Map<String, dynamic> json) {
    return _ProductLookup(
      id: json['id'] as int,
      code: json['code'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
      wholesalePrice: _toDouble(json['wholesalePrice']),
    );
  }

  static double _toDouble(dynamic value) {
    if (value == null) return 0.0;
    if (value is double) return value;
    if (value is int) return value.toDouble();
    return double.tryParse(value.toString()) ?? 0.0;
  }
}

class PurchaseReturnCreateScreen extends StatefulWidget {
  const PurchaseReturnCreateScreen({super.key});

  @override
  State<PurchaseReturnCreateScreen> createState() =>
      _PurchaseReturnCreateScreenState();
}

class _PurchaseReturnCreateScreenState
    extends State<PurchaseReturnCreateScreen> {
  final _formKey = GlobalKey<FormState>();
  bool _isLoading = true;
  bool _isSaving = false;
  final _dateFormat = DateFormat('yyyy/MM/dd', 'ar');

  DateTime _returnDate = DateTime.now();
  int? _selectedSupplierId;
  int? _selectedOriginalInvoiceId;
  final _notesController = TextEditingController();
  final List<_ReturnLineItem> _lines = [];

  List<SupplierModel> _suppliers = [];
  List<InvoiceListModel> _purchaseInvoices = [];
  List<_ProductLookup> _products = [];

  @override
  void initState() {
    super.initState();
    _loadLookups();
  }

  @override
  void dispose() {
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _loadLookups() async {
    setState(() => _isLoading = true);
    final api = context.read<ApiClient>();

    final suppResponse = await api.get<List<SupplierModel>>(
      ApiConstants.suppliers,
      fromJson: (json) => (json as List)
          .map((e) => SupplierModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

    final invResponse = await api.get<List<InvoiceListModel>>(
      ApiConstants.purchaseInvoices,
      fromJson: (json) => (json as List)
          .map((e) => InvoiceListModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

    final prodResponse = await api.get<List<_ProductLookup>>(
      ApiConstants.products,
      fromJson: (json) => (json as List)
          .map((e) => _ProductLookup.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

    if (mounted) {
      setState(() {
        _isLoading = false;
        if (suppResponse.success && suppResponse.data != null) {
          _suppliers = suppResponse.data!;
        }
        if (invResponse.success && invResponse.data != null) {
          _purchaseInvoices = invResponse.data!;
        }
        if (prodResponse.success && prodResponse.data != null) {
          _products = prodResponse.data!;
        }
      });
    }
  }

  double get _subtotal =>
      _lines.fold(0, (sum, l) => sum + (l.quantity * l.unitPrice));
  double get _totalDiscount =>
      _lines.fold(0, (sum, l) => sum + l.discount);
  double get _totalVat => _lines.fold(0, (sum, l) => sum + l.vat);
  double get _grandTotal => _subtotal - _totalDiscount + _totalVat;

  Future<void> _selectDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _returnDate,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
      locale: const Locale('ar'),
    );
    if (picked != null) {
      setState(() => _returnDate = picked);
    }
  }

  Future<void> _showProductSelector() async {
    final searchCtrl = TextEditingController();
    final selected = await showModalBottomSheet<_ProductLookup>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => DraggableScrollableSheet(
        initialChildSize: 0.7,
        minChildSize: 0.5,
        maxChildSize: 0.95,
        expand: false,
        builder: (_, controller) => StatefulBuilder(
          builder: (ctx, setSheetState) {
            final query = searchCtrl.text.toLowerCase();
            final filtered = query.isEmpty
                ? _products
                : _products
                    .where((p) =>
                        p.nameAr.toLowerCase().contains(query) ||
                        p.code.toLowerCase().contains(query))
                    .toList();
            return Column(
              children: [
                Padding(
                  padding: const EdgeInsets.all(16),
                  child: TextField(
                    controller: searchCtrl,
                    decoration: InputDecoration(
                      hintText: 'بحث عن صنف...',
                      prefixIcon: const Icon(Icons.search),
                      border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(10)),
                    ),
                    onChanged: (_) => setSheetState(() {}),
                  ),
                ),
                Expanded(
                  child: ListView.builder(
                    controller: controller,
                    itemCount: filtered.length,
                    itemBuilder: (ctx2, i) {
                      final p = filtered[i];
                      return ListTile(
                        title: Text(p.nameAr),
                        subtitle: Text(
                            '${p.code} - ${p.wholesalePrice.toStringAsFixed(2)} ج.م',
                            textDirection: TextDirection.ltr),
                        onTap: () => Navigator.pop(ctx, p),
                      );
                    },
                  ),
                ),
              ],
            );
          },
        ),
      ),
    );

    if (selected != null) {
      setState(() {
        _lines.add(_ReturnLineItem(
          productId: selected.id,
          productName: selected.nameAr,
          unitPrice: selected.wholesalePrice,
          quantity: 1,
        ));
      });
    }
  }

  void _removeLine(int index) {
    setState(() => _lines.removeAt(index));
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    if (_selectedSupplierId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('يرجى اختيار المورد')),
      );
      return;
    }
    if (_lines.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('يرجى إضافة صنف واحد على الأقل')),
      );
      return;
    }

    setState(() => _isSaving = true);

    final api = context.read<ApiClient>();
    final data = {
      'date': _returnDate.toIso8601String(),
      'supplierId': _selectedSupplierId,
      'originalInvoiceId': _selectedOriginalInvoiceId,
      'notes':
          _notesController.text.isEmpty ? null : _notesController.text,
      'lines': _lines
          .map((l) => {
                'productId': l.productId,
                'quantity': l.quantity,
                'unitPrice': l.unitPrice,
                'discountAmount': l.discount,
                'vatAmount': l.vat,
                'lineTotal': l.lineTotal,
              })
          .toList(),
    };

    final response = await api.post(
      ApiConstants.purchaseReturns,
      data: data,
    );

    if (mounted) {
      setState(() => _isSaving = false);
      if (response.success) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تم إنشاء المرتجع بنجاح')),
        );
        Navigator.pop(context, true);
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(response.errorMessage)),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('مرتجع مشتريات جديد'),
        actions: [
          TextButton(
            onPressed: _isSaving ? null : _save,
            child: _isSaving
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(
                        strokeWidth: 2, color: Colors.white),
                  )
                : const Text('حفظ',
                    style: TextStyle(color: Colors.white)),
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : Form(
              key: _formKey,
              child: Column(
                children: [
                  Expanded(
                    child: ListView(
                      padding: const EdgeInsets.all(16),
                      children: [
                        // Date
                        InkWell(
                          onTap: _selectDate,
                          child: InputDecorator(
                            decoration: const InputDecoration(
                              labelText: 'تاريخ المرتجع',
                              border: OutlineInputBorder(),
                              prefixIcon:
                                  Icon(Icons.calendar_today),
                            ),
                            child: Text(
                                _dateFormat.format(_returnDate)),
                          ),
                        ),
                        const SizedBox(height: 16),

                        // Supplier
                        DropdownButtonFormField<int>(
                          value: _selectedSupplierId,
                          decoration: const InputDecoration(
                            labelText: 'المورد *',
                            border: OutlineInputBorder(),
                          ),
                          items: _suppliers.map((s) {
                            return DropdownMenuItem<int>(
                              value: s.id,
                              child: Text(s.nameAr),
                            );
                          }).toList(),
                          onChanged: (v) => setState(
                              () => _selectedSupplierId = v),
                          validator: (v) =>
                              v == null ? 'مطلوب' : null,
                        ),
                        const SizedBox(height: 16),

                        // Original Invoice (optional)
                        DropdownButtonFormField<int>(
                          value: _selectedOriginalInvoiceId,
                          decoration: const InputDecoration(
                            labelText:
                                'فاتورة المشتريات الأصلية (اختياري)',
                            border: OutlineInputBorder(),
                          ),
                          items: [
                            const DropdownMenuItem<int>(
                              value: null,
                              child: Text('بدون'),
                            ),
                            ..._purchaseInvoices.map((inv) {
                              return DropdownMenuItem<int>(
                                value: inv.id,
                                child: Text(
                                    '${inv.invoiceNumber} - ${inv.grandTotal.toStringAsFixed(2)} ج.م'),
                              );
                            }),
                          ],
                          onChanged: (v) => setState(
                              () => _selectedOriginalInvoiceId = v),
                        ),
                        const SizedBox(height: 16),

                        // Notes
                        TextFormField(
                          controller: _notesController,
                          decoration: const InputDecoration(
                            labelText: 'ملاحظات',
                            border: OutlineInputBorder(),
                          ),
                          maxLines: 2,
                        ),
                        const SizedBox(height: 16),

                        // Lines Header
                        Row(
                          children: [
                            const Text('الأصناف',
                                style: TextStyle(
                                    fontWeight: FontWeight.bold,
                                    fontSize: 16)),
                            const Spacer(),
                            TextButton.icon(
                              onPressed: _showProductSelector,
                              icon: const Icon(Icons.add),
                              label: const Text('إضافة صنف'),
                            ),
                          ],
                        ),
                        const Divider(),

                        // Lines
                        if (_lines.isEmpty)
                          const Padding(
                            padding: EdgeInsets.all(32),
                            child: Center(
                              child: Text('لا توجد أصناف',
                                  style: TextStyle(
                                      color: Colors.grey)),
                            ),
                          )
                        else
                          ..._lines.asMap().entries.map((entry) {
                            final i = entry.key;
                            final line = entry.value;
                            return Card(
                              margin:
                                  const EdgeInsets.only(bottom: 8),
                              child: Padding(
                                padding:
                                    const EdgeInsets.all(12),
                                child: Column(
                                  crossAxisAlignment:
                                      CrossAxisAlignment.start,
                                  children: [
                                    Row(
                                      children: [
                                        Expanded(
                                          child: Text(
                                            line.productName ?? '',
                                            style: const TextStyle(
                                                fontWeight:
                                                    FontWeight
                                                        .bold),
                                          ),
                                        ),
                                        IconButton(
                                          icon: const Icon(
                                              Icons.delete,
                                              color: Colors.red,
                                              size: 20),
                                          onPressed: () =>
                                              _removeLine(i),
                                        ),
                                      ],
                                    ),
                                    Row(
                                      children: [
                                        Expanded(
                                          child: TextFormField(
                                            initialValue: line
                                                .quantity
                                                .toString(),
                                            decoration:
                                                const InputDecoration(
                                              labelText: 'الكمية',
                                              border:
                                                  OutlineInputBorder(),
                                              isDense: true,
                                            ),
                                            keyboardType:
                                                TextInputType
                                                    .number,
                                            onChanged: (v) {
                                              setState(() {
                                                line.quantity =
                                                    double.tryParse(
                                                            v) ??
                                                        0;
                                              });
                                            },
                                          ),
                                        ),
                                        const SizedBox(width: 8),
                                        Expanded(
                                          child: TextFormField(
                                            initialValue: line
                                                .unitPrice
                                                .toString(),
                                            decoration:
                                                const InputDecoration(
                                              labelText: 'السعر',
                                              border:
                                                  OutlineInputBorder(),
                                              isDense: true,
                                            ),
                                            keyboardType:
                                                TextInputType
                                                    .number,
                                            onChanged: (v) {
                                              setState(() {
                                                line.unitPrice =
                                                    double.tryParse(
                                                            v) ??
                                                        0;
                                              });
                                            },
                                          ),
                                        ),
                                        const SizedBox(width: 8),
                                        Text(
                                          '${line.lineTotal.toStringAsFixed(2)} ج.م',
                                          style: const TextStyle(
                                              fontWeight:
                                                  FontWeight
                                                      .bold),
                                        ),
                                      ],
                                    ),
                                  ],
                                ),
                              ),
                            );
                          }),
                      ],
                    ),
                  ),

                  // Totals Footer
                  Container(
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(
                      color: Colors.grey.shade100,
                      border: Border(
                          top: BorderSide(
                              color: Colors.grey.shade300)),
                    ),
                    child: Column(
                      children: [
                        _totalRow('المجموع', _subtotal),
                        _totalRow('الخصم', _totalDiscount),
                        _totalRow('الضريبة', _totalVat),
                        const Divider(),
                        _totalRow('الإجمالي', _grandTotal,
                            isBold: true),
                      ],
                    ),
                  ),
                ],
              ),
            ),
    );
  }

  Widget _totalRow(String label, double value,
      {bool isBold = false}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label,
              style: TextStyle(
                  fontWeight:
                      isBold ? FontWeight.bold : FontWeight.normal)),
          Text(
            '${value.toStringAsFixed(2)} ج.م',
            style: TextStyle(
                fontWeight:
                    isBold ? FontWeight.bold : FontWeight.normal),
          ),
        ],
      ),
    );
  }
}
