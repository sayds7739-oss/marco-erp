import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';

class InventoryAdjustmentModel {
  final int id;
  final String adjustmentNumber;
  final DateTime date;
  final int? warehouseId;
  final String? warehouseNameAr;
  final String? notes;
  final String status;
  final int lineCount;

  InventoryAdjustmentModel({
    required this.id,
    required this.adjustmentNumber,
    required this.date,
    this.warehouseId,
    this.warehouseNameAr,
    this.notes,
    required this.status,
    this.lineCount = 0,
  });

  factory InventoryAdjustmentModel.fromJson(Map<String, dynamic> json) {
    return InventoryAdjustmentModel(
      id: json['id'] as int,
      adjustmentNumber: json['adjustmentNumber'] as String? ?? '',
      date: DateTime.parse(json['date'] as String),
      warehouseId: json['warehouseId'] as int?,
      warehouseNameAr: json['warehouseNameAr'] as String?,
      notes: json['notes'] as String?,
      status: json['status'] as String? ?? '',
      lineCount: json['lineCount'] as int? ?? 0,
    );
  }

  String get statusAr {
    switch (status.toLowerCase()) {
      case 'draft':
        return 'مسودة';
      case 'posted':
        return 'مرحّلة';
      case 'cancelled':
        return 'ملغاة';
      default:
        return status;
    }
  }
}

class _AdjustmentLineItem {
  int? productId;
  String? productName;
  double adjustmentQty;
  String reason;

  _AdjustmentLineItem({
    this.productId,
    this.productName,
    this.adjustmentQty = 0,
    this.reason = '',
  });
}

class _ProductLookup {
  final int id;
  final String code;
  final String nameAr;

  _ProductLookup(
      {required this.id, required this.code, required this.nameAr});

  factory _ProductLookup.fromJson(Map<String, dynamic> json) {
    return _ProductLookup(
      id: json['id'] as int,
      code: json['code'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
    );
  }
}

class _WarehouseLookup {
  final int id;
  final String nameAr;

  _WarehouseLookup({required this.id, required this.nameAr});

  factory _WarehouseLookup.fromJson(Map<String, dynamic> json) {
    return _WarehouseLookup(
      id: json['id'] as int,
      nameAr: json['nameAr'] as String? ?? '',
    );
  }
}

class InventoryAdjustmentsScreen extends StatefulWidget {
  const InventoryAdjustmentsScreen({super.key});

  @override
  State<InventoryAdjustmentsScreen> createState() =>
      _InventoryAdjustmentsScreenState();
}

class _InventoryAdjustmentsScreenState
    extends State<InventoryAdjustmentsScreen> {
  List<InventoryAdjustmentModel> _adjustments = [];
  bool _isLoading = true;
  String? _error;
  final _dateFormat = DateFormat('yyyy/MM/dd', 'ar');

  @override
  void initState() {
    super.initState();
    _loadAdjustments();
  }

  Future<void> _loadAdjustments() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    final api = context.read<ApiClient>();
    final response = await api.get<List<InventoryAdjustmentModel>>(
      ApiConstants.inventoryAdjustments,
      fromJson: (json) => (json as List)
          .map((e) => InventoryAdjustmentModel.fromJson(
              e as Map<String, dynamic>))
          .toList(),
    );

    if (mounted) {
      setState(() {
        _isLoading = false;
        if (response.success && response.data != null) {
          _adjustments = response.data!;
        } else {
          _error = response.errorMessage;
        }
      });
    }
  }

  Color _statusColor(String status) {
    switch (status.toLowerCase()) {
      case 'posted':
        return Colors.green;
      case 'draft':
        return Colors.orange;
      case 'cancelled':
        return Colors.red;
      default:
        return Colors.grey;
    }
  }

  void _navigateToCreate() {
    Navigator.of(context).push(
      MaterialPageRoute(
          builder: (_) => const _InventoryAdjustmentCreateScreen()),
    ).then((result) {
      if (result == true) _loadAdjustments();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('تسويات المخزون')),
      floatingActionButton: FloatingActionButton(
        onPressed: _navigateToCreate,
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
                          onPressed: _loadAdjustments,
                          child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadAdjustments,
                  child: _adjustments.isEmpty
                      ? const Center(child: Text('لا توجد تسويات'))
                      : ListView.builder(
                          itemCount: _adjustments.length,
                          itemBuilder: (ctx, i) {
                            final adj = _adjustments[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(
                                  horizontal: 12, vertical: 4),
                              child: ListTile(
                                leading: Container(
                                  width: 40,
                                  height: 40,
                                  decoration: BoxDecoration(
                                    color: Colors.amber.shade100,
                                    borderRadius:
                                        BorderRadius.circular(8),
                                  ),
                                  child: Icon(Icons.tune,
                                      color: Colors.amber.shade700),
                                ),
                                title: Row(
                                  children: [
                                    Text(adj.adjustmentNumber,
                                        style: const TextStyle(
                                            fontWeight:
                                                FontWeight.bold)),
                                    const Spacer(),
                                    Container(
                                      padding:
                                          const EdgeInsets.symmetric(
                                              horizontal: 8,
                                              vertical: 2),
                                      decoration: BoxDecoration(
                                        color:
                                            _statusColor(adj.status)
                                                .withOpacity(0.1),
                                        borderRadius:
                                            BorderRadius.circular(12),
                                      ),
                                      child: Text(
                                        adj.statusAr,
                                        style: TextStyle(
                                          color: _statusColor(
                                              adj.status),
                                          fontSize: 12,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                                subtitle: Column(
                                  crossAxisAlignment:
                                      CrossAxisAlignment.start,
                                  children: [
                                    if (adj.warehouseNameAr != null)
                                      Text(adj.warehouseNameAr!,
                                          style: const TextStyle(
                                              fontSize: 12)),
                                    Row(
                                      children: [
                                        Text(
                                          _dateFormat.format(adj.date),
                                          style: const TextStyle(
                                              fontSize: 12),
                                        ),
                                        const Spacer(),
                                        Text(
                                          '${adj.lineCount} صنف',
                                          style: TextStyle(
                                            fontSize: 12,
                                            color: Colors.grey.shade600,
                                          ),
                                        ),
                                      ],
                                    ),
                                    if (adj.notes != null &&
                                        adj.notes!.isNotEmpty)
                                      Text(
                                        adj.notes!,
                                        style: TextStyle(
                                            fontSize: 11,
                                            color:
                                                Colors.grey.shade500),
                                        maxLines: 1,
                                        overflow:
                                            TextOverflow.ellipsis,
                                      ),
                                  ],
                                ),
                              ),
                            );
                          },
                        ),
                ),
    );
  }
}

/// Create screen for inventory adjustments
class _InventoryAdjustmentCreateScreen extends StatefulWidget {
  const _InventoryAdjustmentCreateScreen();

  @override
  State<_InventoryAdjustmentCreateScreen> createState() =>
      _InventoryAdjustmentCreateScreenState();
}

class _InventoryAdjustmentCreateScreenState
    extends State<_InventoryAdjustmentCreateScreen> {
  final _formKey = GlobalKey<FormState>();
  bool _isLoading = true;
  bool _isSaving = false;
  final _dateFormat = DateFormat('yyyy/MM/dd', 'ar');

  DateTime _adjustmentDate = DateTime.now();
  int? _selectedWarehouseId;
  final _notesController = TextEditingController();
  final List<_AdjustmentLineItem> _lines = [];

  List<_WarehouseLookup> _warehouses = [];
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

    final whResponse = await api.get<List<_WarehouseLookup>>(
      ApiConstants.warehouses,
      fromJson: (json) => (json as List)
          .map((e) => _WarehouseLookup.fromJson(e as Map<String, dynamic>))
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
        if (whResponse.success && whResponse.data != null) {
          _warehouses = whResponse.data!;
          if (_warehouses.isNotEmpty) {
            _selectedWarehouseId = _warehouses.first.id;
          }
        }
        if (prodResponse.success && prodResponse.data != null) {
          _products = prodResponse.data!;
        }
      });
    }
  }

  Future<void> _selectDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _adjustmentDate,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
      locale: const Locale('ar'),
    );
    if (picked != null) {
      setState(() => _adjustmentDate = picked);
    }
  }

  void _addLine() {
    _showProductSelector();
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
                        subtitle: Text(p.code,
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
        _lines.add(_AdjustmentLineItem(
          productId: selected.id,
          productName: selected.nameAr,
        ));
      });
    }
  }

  void _removeLine(int index) {
    setState(() => _lines.removeAt(index));
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    if (_selectedWarehouseId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('يرجى اختيار المخزن')),
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
      'date': _adjustmentDate.toIso8601String(),
      'warehouseId': _selectedWarehouseId,
      'notes':
          _notesController.text.isEmpty ? null : _notesController.text,
      'lines': _lines
          .map((l) => {
                'productId': l.productId,
                'adjustmentQty': l.adjustmentQty,
                'reason': l.reason.isEmpty ? null : l.reason,
              })
          .toList(),
    };

    final response = await api.post(
      ApiConstants.inventoryAdjustments,
      data: data,
    );

    if (mounted) {
      setState(() => _isSaving = false);
      if (response.success) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تم إنشاء التسوية بنجاح')),
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
        title: const Text('تسوية مخزون جديدة'),
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
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  // Date
                  InkWell(
                    onTap: _selectDate,
                    child: InputDecorator(
                      decoration: const InputDecoration(
                        labelText: 'تاريخ التسوية',
                        border: OutlineInputBorder(),
                        prefixIcon: Icon(Icons.calendar_today),
                      ),
                      child: Text(
                          _dateFormat.format(_adjustmentDate)),
                    ),
                  ),
                  const SizedBox(height: 16),

                  // Warehouse
                  DropdownButtonFormField<int>(
                    value: _selectedWarehouseId,
                    decoration: const InputDecoration(
                      labelText: 'المخزن *',
                      border: OutlineInputBorder(),
                    ),
                    items: _warehouses.map((w) {
                      return DropdownMenuItem<int>(
                        value: w.id,
                        child: Text(w.nameAr),
                      );
                    }).toList(),
                    onChanged: (v) =>
                        setState(() => _selectedWarehouseId = v),
                    validator: (v) => v == null ? 'مطلوب' : null,
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
                        child: Text('لا توجد أصناف',
                            style: TextStyle(color: Colors.grey)),
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
                                              FontWeight.bold),
                                    ),
                                  ),
                                  IconButton(
                                    icon: const Icon(Icons.delete,
                                        color: Colors.red, size: 20),
                                    onPressed: () =>
                                        _removeLine(i),
                                  ),
                                ],
                              ),
                              Row(
                                children: [
                                  Expanded(
                                    child: TextFormField(
                                      initialValue:
                                          line.adjustmentQty
                                              .toString(),
                                      decoration:
                                          const InputDecoration(
                                        labelText:
                                            'الكمية (+ أو -)',
                                        border:
                                            OutlineInputBorder(),
                                        isDense: true,
                                      ),
                                      keyboardType:
                                          const TextInputType
                                              .numberWithOptions(
                                              signed: true,
                                              decimal: true),
                                      textDirection:
                                          TextDirection.ltr,
                                      onChanged: (v) {
                                        line.adjustmentQty =
                                            double.tryParse(v) ??
                                                0;
                                      },
                                    ),
                                  ),
                                  const SizedBox(width: 8),
                                  Expanded(
                                    flex: 2,
                                    child: TextFormField(
                                      initialValue: line.reason,
                                      decoration:
                                          const InputDecoration(
                                        labelText: 'السبب',
                                        border:
                                            OutlineInputBorder(),
                                        isDense: true,
                                      ),
                                      onChanged: (v) =>
                                          line.reason = v,
                                    ),
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
    );
  }
}
