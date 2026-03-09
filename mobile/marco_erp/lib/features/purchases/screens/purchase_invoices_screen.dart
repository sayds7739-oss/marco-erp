import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../core/models/invoice_model.dart';
import '../../../widgets/sync_status_widget.dart';
import 'purchase_invoice_create_screen.dart';

class PurchaseInvoicesScreen extends StatefulWidget {
  const PurchaseInvoicesScreen({super.key});

  @override
  State<PurchaseInvoicesScreen> createState() => _PurchaseInvoicesScreenState();
}

class _PurchaseInvoicesScreenState extends State<PurchaseInvoicesScreen> {
  List<InvoiceListModel> _invoices = [];
  bool _isLoading = true;
  String? _error;
  final _dateFormat = DateFormat('yyyy/MM/dd', 'ar');

  @override
  void initState() {
    super.initState();
    _loadInvoices();
  }

  Future<void> _loadInvoices() async {
    setState(() { _isLoading = true; _error = null; });
    try {
      final offlineData = context.read<OfflineDataProvider>();
      final rows = await offlineData.getAll('purchase_invoices');
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
      case 'posted': return Colors.green;
      case 'draft': return Colors.orange;
      case 'cancelled': return Colors.red;
      default: return Colors.grey;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('فواتير المشتريات')),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(_error!),
                      const SizedBox(height: 16),
                      ElevatedButton(onPressed: _loadInvoices, child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadInvoices,
                  child: _invoices.isEmpty
                      ? const Center(child: Text('لا توجد فواتير'))
                      : ListView.builder(
                          itemCount: _invoices.length,
                          itemBuilder: (ctx, i) {
                            final inv = _invoices[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                              child: ListTile(
                                leading: Container(
                                  width: 40,
                                  height: 40,
                                  decoration: BoxDecoration(
                                    color: Colors.orange.shade100,
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: Icon(Icons.shopping_cart, color: Colors.orange.shade700),
                                ),
                                title: Row(
                                  children: [
                                    Text(inv.invoiceNumber, style: const TextStyle(fontWeight: FontWeight.bold)),
                                    const Spacer(),
                                    Container(
                                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                                      decoration: BoxDecoration(
                                        color: _statusColor(inv.status).withOpacity(0.1),
                                        borderRadius: BorderRadius.circular(12),
                                      ),
                                      child: Text(
                                        inv.statusAr,
                                        style: TextStyle(
                                          color: _statusColor(inv.status),
                                          fontSize: 12,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                                subtitle: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    if (inv.counterpartyNameAr != null)
                                      Text(inv.counterpartyNameAr!),
                                    Row(
                                      children: [
                                        Text(
                                          _dateFormat.format(inv.invoiceDate),
                                          style: const TextStyle(fontSize: 12),
                                        ),
                                        const Spacer(),
                                        Text(
                                          '${inv.grandTotal.toStringAsFixed(2)} ج.م',
                                          style: const TextStyle(fontWeight: FontWeight.bold),
                                        ),
                                      ],
                                    ),
                                  ],
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
              builder: (_) => const PurchaseInvoiceCreateScreen(),
            ),
          );
          _loadInvoices();
        },
        child: const Icon(Icons.add),
      ),
    );
  }
}
