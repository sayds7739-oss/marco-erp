import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';
import '../../../core/models/invoice_model.dart';
import 'purchase_return_create_screen.dart';

class PurchaseReturnsScreen extends StatefulWidget {
  const PurchaseReturnsScreen({super.key});

  @override
  State<PurchaseReturnsScreen> createState() => _PurchaseReturnsScreenState();
}

class _PurchaseReturnsScreenState extends State<PurchaseReturnsScreen> {
  List<InvoiceListModel> _returns = [];
  bool _isLoading = true;
  String? _error;
  final _dateFormat = DateFormat('yyyy/MM/dd', 'ar');

  @override
  void initState() {
    super.initState();
    _loadReturns();
  }

  Future<void> _loadReturns() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    final api = context.read<ApiClient>();
    final response = await api.get<List<InvoiceListModel>>(
      ApiConstants.purchaseReturns,
      fromJson: (json) => (json as List)
          .map((e) => InvoiceListModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

    if (mounted) {
      setState(() {
        _isLoading = false;
        if (response.success && response.data != null) {
          _returns = response.data!;
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
    Navigator.of(context)
        .push(MaterialPageRoute(
            builder: (_) => const PurchaseReturnCreateScreen()))
        .then((result) {
      if (result == true) _loadReturns();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('مرتجعات المشتريات')),
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
                          onPressed: _loadReturns,
                          child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadReturns,
                  child: _returns.isEmpty
                      ? const Center(child: Text('لا توجد مرتجعات'))
                      : ListView.builder(
                          itemCount: _returns.length,
                          itemBuilder: (ctx, i) {
                            final ret = _returns[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(
                                  horizontal: 12, vertical: 4),
                              child: ListTile(
                                leading: Container(
                                  width: 40,
                                  height: 40,
                                  decoration: BoxDecoration(
                                    color: Colors.deepOrange.shade100,
                                    borderRadius:
                                        BorderRadius.circular(8),
                                  ),
                                  child: Icon(
                                      Icons.assignment_return,
                                      color:
                                          Colors.deepOrange.shade700),
                                ),
                                title: Row(
                                  children: [
                                    Text(ret.invoiceNumber,
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
                                            _statusColor(ret.status)
                                                .withOpacity(0.1),
                                        borderRadius:
                                            BorderRadius.circular(12),
                                      ),
                                      child: Text(
                                        ret.statusAr,
                                        style: TextStyle(
                                          color: _statusColor(
                                              ret.status),
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
                                    if (ret.counterpartyNameAr !=
                                        null)
                                      Text(
                                          ret.counterpartyNameAr!),
                                    Row(
                                      children: [
                                        Text(
                                          _dateFormat.format(
                                              ret.invoiceDate),
                                          style: const TextStyle(
                                              fontSize: 12),
                                        ),
                                        const Spacer(),
                                        Text(
                                          '${ret.grandTotal.toStringAsFixed(2)} ج.م',
                                          style: const TextStyle(
                                              fontWeight:
                                                  FontWeight.bold,
                                              color: Colors
                                                  .deepOrange),
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
    );
  }
}
