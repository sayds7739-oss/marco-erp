import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../core/models/treasury_model.dart';
import '../../../widgets/sync_status_widget.dart';

class CashReceiptsScreen extends StatefulWidget {
  const CashReceiptsScreen({super.key});

  @override
  State<CashReceiptsScreen> createState() => _CashReceiptsScreenState();
}

class _CashReceiptsScreenState extends State<CashReceiptsScreen> {
  List<CashReceiptListModel> _receipts = [];
  bool _isLoading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadReceipts();
  }

  Future<void> _loadReceipts() async {
    setState(() { _isLoading = true; _error = null; });
    try {
      final offlineData = context.read<OfflineDataProvider>();
      final rows = await offlineData.getAll('cash_receipts');
      if (mounted) {
        setState(() {
          _isLoading = false;
          _receipts = rows.map((row) => CashReceiptListModel(
            id: row['id'] as int,
            receiptNumber: row['receipt_number'] as String? ?? '',
            receiptDate: DateTime.parse(row['receipt_date'] as String),
            amount: (row['amount'] as num?)?.toDouble() ?? 0,
            customerNameAr: null,
            cashboxNameAr: null,
            status: row['status'] as String? ?? '',
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
        title: const Text('\u0633\u0646\u062f\u0627\u062a \u0627\u0644\u0642\u0628\u0636'),
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
                      ElevatedButton(onPressed: _loadReceipts, child: const Text('\u0625\u0639\u0627\u062f\u0629 \u0627\u0644\u0645\u062d\u0627\u0648\u0644\u0629')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadReceipts,
                  child: ListView.builder(
                    itemCount: _receipts.length,
                    itemBuilder: (ctx, i) {
                      final r = _receipts[i];
                      return Card(
                        margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                        child: ListTile(
                          leading: CircleAvatar(
                            backgroundColor: Colors.green.shade50,
                            child: const Icon(Icons.payments, color: Colors.green),
                          ),
                          title: Text(r.receiptNumber),
                          subtitle: Text(
                            '${r.customerNameAr ?? "-"} \u2022 ${r.receiptDate.toString().substring(0, 10)}',
                          ),
                          trailing: Text(
                            '${r.amount.toStringAsFixed(2)} \u062c.\u0645',
                            style: const TextStyle(fontWeight: FontWeight.bold),
                          ),
                        ),
                      );
                    },
                  ),
                ),
    );
  }
}
