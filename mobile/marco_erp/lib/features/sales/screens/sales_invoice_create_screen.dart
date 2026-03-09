import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../core/models/invoice_model.dart';
import '../../../core/models/product_model.dart';

class SalesInvoiceLineItem {
  int? productId;
  String? productName;
  double quantity;
  double unitPrice;
  double discount;
  double vat;

  SalesInvoiceLineItem({
    this.productId,
    this.productName,
    this.quantity = 1,
    this.unitPrice = 0,
    this.discount = 0,
    this.vat = 0,
  });

  double get lineTotal => (quantity * unitPrice) - discount + vat;
}

class SalesInvoiceCreateScreen extends StatefulWidget {
  const SalesInvoiceCreateScreen({super.key});

  @override
  State<SalesInvoiceCreateScreen> createState() => _SalesInvoiceCreateScreenState();
}

class _SalesInvoiceCreateScreenState extends State<SalesInvoiceCreateScreen> {
  final _formKey = GlobalKey<FormState>();
  bool _isLoading = true;
  bool _isSaving = false;
  final _dateFormat = DateFormat('yyyy/MM/dd', 'ar');

  DateTime _invoiceDate = DateTime.now();
  int? _selectedCustomerId;
  int? _selectedWarehouseId;
  final List<SalesInvoiceLineItem> _lines = [];
  final _notesController = TextEditingController();

  List<CustomerModel> _customers = [];
  List<Map<String, dynamic>> _warehouses = [];
  List<ProductModel> _products = [];

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
    final offlineData = context.read<OfflineDataProvider>();

    try {
      final customerRows = await offlineData.getAll('customers');
      final warehouseRows = await offlineData.getAll('warehouses');
      final productRows = await offlineData.getAll('products');

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
          _warehouses = warehouseRows.map((row) => {
            'id': row['id'],
            'nameAr': row['name_ar'] ?? '',
            'isDefault': (row['is_active'] as int?) == 1,
          }).toList();
          if (_warehouses.isNotEmpty) {
            final defaultWh = _warehouses.firstWhere(
              (w) => w['isDefault'] == true,
              orElse: () => _warehouses.first,
            );
            _selectedWarehouseId = defaultWh['id'] as int?;
          }
          _products = productRows.map((row) => ProductModel(
            id: row['id'] as int,
            code: row['code'] as String? ?? '',
            nameAr: row['name_ar'] as String? ?? '',
            nameEn: row['name_en'] as String?,
            categoryId: row['category_id'] as int? ?? 0,
            baseUnitId: row['base_unit_id'] as int? ?? 0,
            wholesalePrice: (row['wholesale_price'] as num?)?.toDouble() ?? 0,
            retailPrice: (row['retail_price'] as num?)?.toDouble() ?? 0,
            weightedAverageCost: (row['weighted_average_cost'] as num?)?.toDouble() ?? 0,
            isActive: (row['is_active'] as int?) == 1,
            barcode: row['barcode'] as String?,
            description: row['description'] as String?,
          )).toList();
        });
      }
    } catch (e) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  double get _subtotal => _lines.fold(0, (sum, line) => sum + (line.quantity * line.unitPrice));
  double get _totalDiscount => _lines.fold(0, (sum, line) => sum + line.discount);
  double get _totalVat => _lines.fold(0, (sum, line) => sum + line.vat);
  double get _grandTotal => _subtotal - _totalDiscount + _totalVat;

  Future<void> _selectDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _invoiceDate,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
      locale: const Locale('ar'),
    );
    if (picked != null) {
      setState(() => _invoiceDate = picked);
    }
  }

  void _addLine() {
    _showProductSelector();
  }

  Future<void> _showProductSelector() async {
    final selected = await showModalBottomSheet<ProductModel>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => DraggableScrollableSheet(
        initialChildSize: 0.7,
        minChildSize: 0.5,
        maxChildSize: 0.95,
        expand: false,
        builder: (_, controller) => _ProductSelectorSheet(
          products: _products,
          scrollController: controller,
        ),
      ),
    );

    if (selected != null) {
      setState(() {
        _lines.add(SalesInvoiceLineItem(
          productId: selected.id,
          productName: selected.nameAr,
          unitPrice: selected.retailPrice,
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
    if (_selectedCustomerId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('يرجى اختيار العميل')),
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
    final offlineData = context.read<OfflineDataProvider>();

    final data = {
      'invoice_date': _invoiceDate.toIso8601String(),
      'customer_id': _selectedCustomerId,
      'net_total': _subtotal,
      'vat_total': _totalVat,
      'grand_total': _grandTotal,
      'status': 'Draft',
      'notes': _notesController.text.isEmpty ? null : _notesController.text,
    };

    try {
      final invoiceId = await offlineData.create(
        entityType: 'SalesInvoice',
        table: 'sales_invoices',
        data: data,
      );

      // Save invoice lines to local DB
      for (final line in _lines) {
        await offlineData.create(
          entityType: 'SalesInvoiceLine',
          table: 'sales_invoice_lines',
          data: {
            'sales_invoice_id': invoiceId,
            'product_id': line.productId ?? 0,
            'quantity': line.quantity,
            'unit_price': line.unitPrice,
            'discount_amount': line.discount,
            'vat_amount': line.vat,
            'line_total': line.lineTotal,
          },
        );
      }
      setState(() => _isSaving = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تم إنشاء الفاتورة بنجاح')),
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
        title: const Text('فاتورة بيع جديدة'),
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
                              labelText: 'تاريخ الفاتورة',
                              border: OutlineInputBorder(),
                              prefixIcon: Icon(Icons.calendar_today),
                            ),
                            child: Text(_dateFormat.format(_invoiceDate)),
                          ),
                        ),
                        const SizedBox(height: 16),

                        // Customer
                        DropdownButtonFormField<int>(
                          value: _selectedCustomerId,
                          decoration: const InputDecoration(
                            labelText: 'العميل *',
                            border: OutlineInputBorder(),
                          ),
                          items: _customers.map((c) {
                            return DropdownMenuItem<int>(
                              value: c.id,
                              child: Text(c.nameAr),
                            );
                          }).toList(),
                          onChanged: (v) => setState(() => _selectedCustomerId = v),
                          validator: (v) => v == null ? 'مطلوب' : null,
                        ),
                        const SizedBox(height: 16),

                        // Warehouse
                        DropdownButtonFormField<int>(
                          value: _selectedWarehouseId,
                          decoration: const InputDecoration(
                            labelText: 'المخزن',
                            border: OutlineInputBorder(),
                          ),
                          items: _warehouses.map((w) {
                            return DropdownMenuItem<int>(
                              value: w['id'] as int,
                              child: Text(w['nameAr'] as String? ?? ''),
                            );
                          }).toList(),
                          onChanged: (v) => setState(() => _selectedWarehouseId = v),
                        ),
                        const SizedBox(height: 16),

                        // Lines Header
                        Row(
                          children: [
                            const Text('الأصناف', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
                            const Spacer(),
                            TextButton.icon(
                              onPressed: _addLine,
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
                              child: Text('لا توجد أصناف', style: TextStyle(color: Colors.grey)),
                            ),
                          )
                        else
                          ..._lines.asMap().entries.map((entry) {
                            final i = entry.key;
                            final line = entry.value;
                            return Card(
                              margin: const EdgeInsets.only(bottom: 8),
                              child: Padding(
                                padding: const EdgeInsets.all(12),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Row(
                                      children: [
                                        Expanded(
                                          child: Text(
                                            line.productName ?? '',
                                            style: const TextStyle(fontWeight: FontWeight.bold),
                                          ),
                                        ),
                                        IconButton(
                                          icon: const Icon(Icons.delete, color: Colors.red, size: 20),
                                          onPressed: () => _removeLine(i),
                                        ),
                                      ],
                                    ),
                                    Row(
                                      children: [
                                        Expanded(
                                          child: TextFormField(
                                            initialValue: line.quantity.toString(),
                                            decoration: const InputDecoration(
                                              labelText: 'الكمية',
                                              border: OutlineInputBorder(),
                                              isDense: true,
                                            ),
                                            keyboardType: TextInputType.number,
                                            onChanged: (v) {
                                              setState(() {
                                                line.quantity = double.tryParse(v) ?? 0;
                                              });
                                            },
                                          ),
                                        ),
                                        const SizedBox(width: 8),
                                        Expanded(
                                          child: TextFormField(
                                            initialValue: line.unitPrice.toString(),
                                            decoration: const InputDecoration(
                                              labelText: 'السعر',
                                              border: OutlineInputBorder(),
                                              isDense: true,
                                            ),
                                            keyboardType: TextInputType.number,
                                            onChanged: (v) {
                                              setState(() {
                                                line.unitPrice = double.tryParse(v) ?? 0;
                                              });
                                            },
                                          ),
                                        ),
                                        const SizedBox(width: 8),
                                        Text(
                                          '${line.lineTotal.toStringAsFixed(2)} ج.م',
                                          style: const TextStyle(fontWeight: FontWeight.bold),
                                        ),
                                      ],
                                    ),
                                  ],
                                ),
                              ),
                            );
                          }),

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
                      ],
                    ),
                  ),

                  // Totals Footer
                  Container(
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(
                      color: Colors.grey.shade100,
                      border: Border(top: BorderSide(color: Colors.grey.shade300)),
                    ),
                    child: Column(
                      children: [
                        _totalRow('المجموع', _subtotal),
                        _totalRow('الخصم', _totalDiscount),
                        _totalRow('الضريبة', _totalVat),
                        const Divider(),
                        _totalRow('الإجمالي', _grandTotal, isBold: true),
                      ],
                    ),
                  ),
                ],
              ),
            ),
    );
  }

  Widget _totalRow(String label, double value, {bool isBold = false}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: TextStyle(fontWeight: isBold ? FontWeight.bold : FontWeight.normal)),
          Text(
            '${value.toStringAsFixed(2)} ج.م',
            style: TextStyle(fontWeight: isBold ? FontWeight.bold : FontWeight.normal),
          ),
        ],
      ),
    );
  }
}

class _ProductSelectorSheet extends StatefulWidget {
  final List<ProductModel> products;
  final ScrollController scrollController;

  const _ProductSelectorSheet({
    required this.products,
    required this.scrollController,
  });

  @override
  State<_ProductSelectorSheet> createState() => _ProductSelectorSheetState();
}

class _ProductSelectorSheetState extends State<_ProductSelectorSheet> {
  final _searchController = TextEditingController();
  List<ProductModel> _filtered = [];

  @override
  void initState() {
    super.initState();
    _filtered = widget.products;
  }

  void _search(String query) {
    setState(() {
      if (query.isEmpty) {
        _filtered = widget.products;
      } else {
        _filtered = widget.products.where((p) =>
            p.nameAr.toLowerCase().contains(query.toLowerCase()) ||
            p.code.toLowerCase().contains(query.toLowerCase()) ||
            (p.barcode?.toLowerCase().contains(query.toLowerCase()) ?? false)
        ).toList();
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Container(
          padding: const EdgeInsets.all(16),
          child: TextField(
            controller: _searchController,
            decoration: InputDecoration(
              hintText: 'بحث عن صنف...',
              prefixIcon: const Icon(Icons.search),
              border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
            ),
            onChanged: _search,
          ),
        ),
        Expanded(
          child: ListView.builder(
            controller: widget.scrollController,
            itemCount: _filtered.length,
            itemBuilder: (ctx, i) {
              final p = _filtered[i];
              return ListTile(
                title: Text(p.nameAr),
                subtitle: Text('${p.code} • ${p.retailPrice.toStringAsFixed(2)} ج.م'),
                onTap: () => Navigator.pop(context, p),
              );
            },
          ),
        ),
      ],
    );
  }
}
