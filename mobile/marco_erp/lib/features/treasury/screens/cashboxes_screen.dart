import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../core/models/treasury_model.dart';
import '../../../widgets/sync_status_widget.dart';

class CashboxesScreen extends StatefulWidget {
  const CashboxesScreen({super.key});

  @override
  State<CashboxesScreen> createState() => _CashboxesScreenState();
}

class _CashboxesScreenState extends State<CashboxesScreen> {
  List<CashboxModel> _cashboxes = [];
  bool _isLoading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadCashboxes();
  }

  Future<void> _loadCashboxes() async {
    setState(() { _isLoading = true; _error = null; });
    try {
      final offlineData = context.read<OfflineDataProvider>();
      final rows = await offlineData.getAll('cashboxes');
      if (mounted) {
        setState(() {
          _isLoading = false;
          _cashboxes = rows.map((row) => CashboxModel(
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
      if (mounted) setState(() { _isLoading = false; _error = e.toString(); });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('\u0627\u0644\u062e\u0632\u0646'),
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
                      ElevatedButton(onPressed: _loadCashboxes, child: const Text('\u0625\u0639\u0627\u062f\u0629 \u0627\u0644\u0645\u062d\u0627\u0648\u0644\u0629')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadCashboxes,
                  child: ListView.builder(
                    itemCount: _cashboxes.length,
                    padding: const EdgeInsets.all(12),
                    itemBuilder: (ctx, i) {
                      final cb = _cashboxes[i];
                      return Card(
                        child: ListTile(
                          leading: CircleAvatar(
                            backgroundColor: cb.isDefault
                                ? Colors.green.shade50
                                : Colors.blue.shade50,
                            child: Icon(
                              Icons.account_balance_wallet,
                              color: cb.isDefault ? Colors.green : Colors.blue,
                            ),
                          ),
                          title: Row(
                            children: [
                              Text(cb.nameAr),
                              if (cb.isDefault) ...[
                                const SizedBox(width: 8),
                                Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                                  decoration: BoxDecoration(
                                    color: Colors.green.shade50,
                                    borderRadius: BorderRadius.circular(4),
                                  ),
                                  child: const Text('\u0627\u0641\u062a\u0631\u0627\u0636\u064a', style: TextStyle(fontSize: 10, color: Colors.green)),
                                ),
                              ],
                            ],
                          ),
                          subtitle: Text(cb.code),
                          trailing: Text(
                            '${cb.currentBalance.toStringAsFixed(2)} \u062c.\u0645',
                            style: TextStyle(
                              fontWeight: FontWeight.bold,
                              fontSize: 15,
                              color: cb.currentBalance >= 0 ? Colors.green.shade700 : Colors.red,
                            ),
                          ),
                        ),
                      );
                    },
                  ),
                ),
    );
  }
}
