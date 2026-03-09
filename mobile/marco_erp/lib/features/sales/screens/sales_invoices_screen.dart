import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../core/models/invoice_model.dart';
import '../../../widgets/sync_status_widget.dart';

class SalesInvoicesScreen extends StatefulWidget {
  const SalesInvoicesScreen({super.key});

  @override
  State<SalesInvoicesScreen> createState() => _SalesInvoicesScreenState();
}

class _SalesInvoicesScreenState extends State<SalesInvoicesScreen> {
  List<InvoiceListModel> _invoices = [];
  bool _isLoading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadInvoices();
  }

  Future<void> _loadInvoices() async {
    setState(() { _isLoading = true; _error = null; });
    try {
      final offlineData = context.read<OfflineDataProvider>();
      final rows = await offlineData.getAll('sales_invoices');
      if (mounted) {
        setState(() {
          _isLoading = false;
          _invoices = rows.map((row) => InvoiceListModel(
            id: row['id'] as int,
            invoiceNumber: row['invoice_number'] as String? ?? '',
            invoiceDate: DateTime.parse(row['invoice_date'] as String),
            counterpartyNameAr: null,
            netTotal: (row['net_total'] as num?)?.toDouble() ?? 0,
            vatTotal: (row['vat_total'] as num?)?.toDouble() ?? 0,
            grandTotal: (row['grand_total'] as num?)?.toDouble() ?? 0,
            status: row['status'] as String? ?? '',
          )).toList();
        });
      }
    } catch (e) {
      if (mounted) setState(() { _isLoading = false; _error = e.toString(); });
    }
  }

  Color _statusColor(String status) {
    switch (status.toLowerCase()) {
      case 'draft': return Colors.orange;
      case 'posted': return Colors.green;
      case 'cancelled': return Colors.red;
      default: return Colors.grey;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('\u0641\u0648\u0627\u062a\u064a\u0631 \u0627\u0644\u0628\u064a\u0639'),
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
                      ElevatedButton(onPressed: _loadInvoices, child: const Text('\u0625\u0639\u0627\u062f\u0629 \u0627\u0644\u0645\u062d\u0627\u0648\u0644\u0629')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadInvoices,
                  child: _invoices.isEmpty
                      ? const Center(child: Text('\u0644\u0627 \u062a\u0648\u062c\u062f \u0641\u0648\u0627\u062a\u064a\u0631'))
                      : ListView.builder(
                          itemCount: _invoices.length,
                          itemBuilder: (ctx, i) {
                            final inv = _invoices[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                              child: ListTile(
                                title: Row(
                                  children: [
                                    Text(inv.invoiceNumber, style: const TextStyle(fontWeight: FontWeight.bold)),
                                    const SizedBox(width: 8),
                                    Container(
                                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                                      decoration: BoxDecoration(
                                        color: _statusColor(inv.status).withOpacity(0.1),
                                        borderRadius: BorderRadius.circular(8),
                                      ),
                                      child: Text(
                                        inv.statusAr,
                                        style: TextStyle(
                                          fontSize: 11,
                                          color: _statusColor(inv.status),
                                          fontWeight: FontWeight.bold,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                                subtitle: Text(
                                  '${inv.counterpartyNameAr ?? "\u0639\u0645\u064a\u0644 \u0646\u0642\u062f\u064a"} \u2022 ${inv.invoiceDate.toString().substring(0, 10)}',
                                ),
                                trailing: Text(
                                  '${inv.grandTotal.toStringAsFixed(2)} \u062c.\u0645',
                                  style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15),
                                ),
                              ),
                            );
                          },
                        ),
                ),
    );
  }
}
