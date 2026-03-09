import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';
import '../../../core/models/invoice_model.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../widgets/sync_status_widget.dart';
import 'supplier_detail_screen.dart';

class SuppliersScreen extends StatefulWidget {
  const SuppliersScreen({super.key});

  @override
  State<SuppliersScreen> createState() => _SuppliersScreenState();
}

class _SuppliersScreenState extends State<SuppliersScreen> {
  List<SupplierModel> _suppliers = [];
  bool _isLoading = true;
  String? _error;
  final _searchController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _loadSuppliers();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadSuppliers() async {
    setState(() { _isLoading = true; _error = null; });

    try {
      final offlineData = context.read<OfflineDataProvider>();
      final rows = await offlineData.getAll('suppliers');
      final suppliers = rows
          .map((row) => SupplierModel(
                id: row['id'] as int,
                code: (row['code'] as String?) ?? '',
                nameAr: (row['name_ar'] as String?) ?? '',
                phone: row['phone'] as String?,
                email: row['email'] as String?,
                isActive: (row['is_active'] as int?) == 1,
              ))
          .toList();

      if (mounted) {
        setState(() {
          _isLoading = false;
          _suppliers = suppliers;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _isLoading = false;
          _error = e.toString();
        });
      }
    }
  }

  List<SupplierModel> get _filteredSuppliers {
    final query = _searchController.text.toLowerCase();
    if (query.isEmpty) return _suppliers;
    return _suppliers.where((s) =>
        s.nameAr.toLowerCase().contains(query) ||
        s.code.toLowerCase().contains(query) ||
        (s.phone?.contains(query) ?? false)
    ).toList();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('الموردين'),
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(56),
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
            child: TextField(
              controller: _searchController,
              decoration: InputDecoration(
                hintText: 'بحث عن مورد...',
                prefixIcon: const Icon(Icons.search),
                filled: true,
                fillColor: Colors.white,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(10),
                  borderSide: BorderSide.none,
                ),
                contentPadding: const EdgeInsets.symmetric(horizontal: 16),
              ),
              onChanged: (_) => setState(() {}),
            ),
          ),
        ),
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
                      ElevatedButton(onPressed: _loadSuppliers, child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadSuppliers,
                  child: _filteredSuppliers.isEmpty
                      ? const Center(child: Text('لا توجد نتائج'))
                      : ListView.builder(
                          itemCount: _filteredSuppliers.length,
                          itemBuilder: (ctx, i) {
                            final s = _filteredSuppliers[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                              child: ListTile(
                                onTap: () async {
                                  await Navigator.of(context).push(
                                    MaterialPageRoute(
                                      builder: (_) => SupplierDetailScreen(supplier: s),
                                    ),
                                  );
                                  _loadSuppliers();
                                },
                                leading: CircleAvatar(
                                  backgroundColor: Colors.orange.shade100,
                                  child: Text(
                                    s.nameAr.isNotEmpty ? s.nameAr[0] : '?',
                                    style: TextStyle(color: Colors.orange.shade700),
                                  ),
                                ),
                                title: Text(s.nameAr),
                                subtitle: Text('${s.code} • ${s.phone ?? ""}'),
                                trailing: Icon(
                                  Icons.circle,
                                  size: 12,
                                  color: s.isActive ? Colors.green : Colors.grey,
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
              builder: (_) => const SupplierDetailScreen(),
            ),
          );
          _loadSuppliers();
        },
        child: const Icon(Icons.add),
      ),
    );
  }
}
