import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';
import '../../../core/models/invoice_model.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../widgets/sync_status_widget.dart';
import 'customer_detail_screen.dart';

class CustomersScreen extends StatefulWidget {
  const CustomersScreen({super.key});

  @override
  State<CustomersScreen> createState() => _CustomersScreenState();
}

class _CustomersScreenState extends State<CustomersScreen> {
  List<CustomerModel> _customers = [];
  List<CustomerModel> _filtered = [];
  bool _isLoading = true;
  String? _error;
  final _searchController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _loadCustomers();
    _searchController.addListener(_filterCustomers);
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadCustomers() async {
    setState(() { _isLoading = true; _error = null; });

    try {
      final offlineData = context.read<OfflineDataProvider>();
      final rows = await offlineData.getAll('customers');
      final customers = rows
          .map((row) => CustomerModel(
                id: row['id'] as int,
                code: (row['code'] as String?) ?? '',
                nameAr: (row['name_ar'] as String?) ?? '',
                nameEn: row['name_en'] as String?,
                phone: row['phone'] as String?,
                email: row['email'] as String?,
                address: row['address'] as String?,
                isActive: (row['is_active'] as int?) == 1,
              ))
          .toList();

      if (mounted) {
        setState(() {
          _isLoading = false;
          _customers = customers;
          _filterCustomers();
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

  void _filterCustomers() {
    final query = _searchController.text.toLowerCase();
    setState(() {
      if (query.isEmpty) {
        _filtered = _customers;
      } else {
        _filtered = _customers.where((c) =>
            c.nameAr.toLowerCase().contains(query) ||
            c.code.toLowerCase().contains(query) ||
            (c.phone?.contains(query) ?? false)
        ).toList();
      }
    });
  }

  Future<void> _navigateToDetail(CustomerModel? customer) async {
    final result = await Navigator.of(context).push<bool>(
      MaterialPageRoute(builder: (_) => CustomerDetailScreen(customer: customer)),
    );
    if (result == true) {
      _loadCustomers();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('العملاء'),
        actions: const [SyncStatusWidget()],
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(56),
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
            child: TextField(
              controller: _searchController,
              decoration: InputDecoration(
                hintText: 'بحث عن عميل...',
                prefixIcon: const Icon(Icons.search, color: Colors.white70),
                hintStyle: const TextStyle(color: Colors.white70),
                filled: true,
                fillColor: Colors.white24,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide: BorderSide.none,
                ),
                contentPadding: const EdgeInsets.symmetric(horizontal: 16),
              ),
              style: const TextStyle(color: Colors.white),
            ),
          ),
        ),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => _navigateToDetail(null),
        child: const Icon(Icons.add),
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
                      ElevatedButton(onPressed: _loadCustomers, child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadCustomers,
                  child: _filtered.isEmpty
                      ? const Center(child: Text('لا يوجد عملاء'))
                      : ListView.builder(
                          itemCount: _filtered.length,
                          itemBuilder: (ctx, i) {
                            final c = _filtered[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                              child: ListTile(
                                leading: CircleAvatar(
                                  backgroundColor: Colors.blue.shade100,
                                  child: Text(
                                    c.nameAr.isNotEmpty ? c.nameAr[0] : '?',
                                    style: TextStyle(color: Colors.blue.shade700),
                                  ),
                                ),
                                title: Text(c.nameAr),
                                subtitle: Text('${c.code} • ${c.phone ?? ""}'),
                                trailing: Icon(
                                  Icons.circle,
                                  size: 12,
                                  color: c.isActive ? Colors.green : Colors.grey,
                                ),
                                onTap: () => _navigateToDetail(c),
                              ),
                            );
                          },
                        ),
                ),
    );
  }
}
