import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../widgets/sync_status_widget.dart';
import 'warehouse_detail_screen.dart';

class WarehouseModel {
  final int id;
  final String code;
  final String nameAr;
  final String? address;
  final bool isDefault;
  final bool isActive;

  WarehouseModel({
    required this.id,
    required this.code,
    required this.nameAr,
    this.address,
    required this.isDefault,
    required this.isActive,
  });

  factory WarehouseModel.fromJson(Map<String, dynamic> json) {
    return WarehouseModel(
      id: json['id'] as int,
      code: json['code'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
      address: json['address'] as String?,
      isDefault: json['isDefault'] as bool? ?? false,
      isActive: json['isActive'] as bool? ?? true,
    );
  }
}

class WarehousesScreen extends StatefulWidget {
  const WarehousesScreen({super.key});

  @override
  State<WarehousesScreen> createState() => _WarehousesScreenState();
}

class _WarehousesScreenState extends State<WarehousesScreen> {
  List<WarehouseModel> _warehouses = [];
  bool _isLoading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadWarehouses();
  }

  Future<void> _loadWarehouses() async {
    setState(() { _isLoading = true; _error = null; });
    try {
      final offlineData = context.read<OfflineDataProvider>();
      final rows = await offlineData.getAll('warehouses');
      if (mounted) {
        setState(() {
          _isLoading = false;
          _warehouses = rows.map((row) => WarehouseModel(
            id: row['id'] as int,
            code: row['code'] as String? ?? '',
            nameAr: row['name_ar'] as String? ?? '',
            address: null,
            isDefault: false,
            isActive: (row['is_active'] as int?) == 1,
          )).toList();
        });
      }
    } catch (e) {
      if (mounted) setState(() { _isLoading = false; _error = e.toString(); });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('المخازن'),
        actions: const [SyncStatusWidget()],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(_error!),
                      const SizedBox(height: 16),
                      ElevatedButton(onPressed: _loadWarehouses, child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadWarehouses,
                  child: _warehouses.isEmpty
                      ? const Center(child: Text('لا توجد مخازن'))
                      : ListView.builder(
                          itemCount: _warehouses.length,
                          itemBuilder: (ctx, i) {
                            final w = _warehouses[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                              child: ListTile(
                                onTap: () async {
                                  await Navigator.of(context).push(
                                    MaterialPageRoute(
                                      builder: (_) => WarehouseDetailScreen(
                                        warehouse: {
                                          'id': w.id,
                                          'code': w.code,
                                          'name_ar': w.nameAr,
                                          'is_default': w.isDefault ? 1 : 0,
                                          'is_active': w.isActive ? 1 : 0,
                                        },
                                      ),
                                    ),
                                  );
                                  _loadWarehouses();
                                },
                                leading: Container(
                                  width: 40,
                                  height: 40,
                                  decoration: BoxDecoration(
                                    color: Colors.brown.shade100,
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: Icon(Icons.warehouse, color: Colors.brown.shade700),
                                ),
                                title: Row(
                                  children: [
                                    Text(w.nameAr, style: const TextStyle(fontWeight: FontWeight.bold)),
                                    if (w.isDefault) ...[
                                      const SizedBox(width: 8),
                                      Container(
                                        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                                        decoration: BoxDecoration(
                                          color: Colors.blue.shade100,
                                          borderRadius: BorderRadius.circular(8),
                                        ),
                                        child: Text(
                                          'افتراضي',
                                          style: TextStyle(fontSize: 10, color: Colors.blue.shade700),
                                        ),
                                      ),
                                    ],
                                  ],
                                ),
                                subtitle: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(w.code, style: const TextStyle(fontSize: 12)),
                                    if (w.address != null)
                                      Text(w.address!, style: const TextStyle(fontSize: 12)),
                                  ],
                                ),
                                trailing: Icon(
                                  Icons.circle,
                                  size: 12,
                                  color: w.isActive ? Colors.green : Colors.grey,
                                ),
                              ),
                            );
                          },
                        ),
                ),
      floatingActionButton: FloatingActionButton(
        onPressed: () async {
          await Navigator.of(context).push(
            MaterialPageRoute(
              builder: (_) => const WarehouseDetailScreen(),
            ),
          );
          _loadWarehouses();
        },
        child: const Icon(Icons.add),
      ),
    );
  }
}
