import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';
import '../../../core/models/invoice_model.dart';

class SalesQuotationsScreen extends StatefulWidget {
  const SalesQuotationsScreen({super.key});

  @override
  State<SalesQuotationsScreen> createState() => _SalesQuotationsScreenState();
}

class _SalesQuotationsScreenState extends State<SalesQuotationsScreen> {
  List<InvoiceListModel> _quotations = [];
  bool _isLoading = true;
  String? _error;
  final _dateFormat = DateFormat('yyyy/MM/dd', 'ar');

  @override
  void initState() {
    super.initState();
    _loadQuotations();
  }

  Future<void> _loadQuotations() async {
    setState(() { _isLoading = true; _error = null; });
    final api = context.read<ApiClient>();

    final response = await api.get<List<InvoiceListModel>>(
      ApiConstants.salesQuotations,
      fromJson: (json) => (json as List)
          .map((e) => InvoiceListModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

    if (mounted) {
      setState(() {
        _isLoading = false;
        if (response.success && response.data != null) {
          _quotations = response.data!;
        } else {
          _error = response.errorMessage;
        }
      });
    }
  }

  Color _statusColor(String status) {
    switch (status.toLowerCase()) {
      case 'approved': return Colors.green;
      case 'pending': return Colors.orange;
      case 'rejected': case 'cancelled': return Colors.red;
      case 'converted': return Colors.blue;
      default: return Colors.grey;
    }
  }

  String _statusAr(String status) {
    switch (status.toLowerCase()) {
      case 'pending': return 'قيد الانتظار';
      case 'approved': return 'موافق عليه';
      case 'rejected': return 'مرفوض';
      case 'converted': return 'تم التحويل';
      case 'cancelled': return 'ملغي';
      default: return status;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('عروض الأسعار')),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(_error!),
                      const SizedBox(height: 16),
                      ElevatedButton(onPressed: _loadQuotations, child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadQuotations,
                  child: _quotations.isEmpty
                      ? const Center(child: Text('لا توجد عروض أسعار'))
                      : ListView.builder(
                          itemCount: _quotations.length,
                          itemBuilder: (ctx, i) {
                            final q = _quotations[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                              child: ListTile(
                                leading: Container(
                                  width: 40,
                                  height: 40,
                                  decoration: BoxDecoration(
                                    color: Colors.purple.shade100,
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: Icon(Icons.request_quote, color: Colors.purple.shade700),
                                ),
                                title: Row(
                                  children: [
                                    Text(q.invoiceNumber, style: const TextStyle(fontWeight: FontWeight.bold)),
                                    const Spacer(),
                                    Container(
                                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                                      decoration: BoxDecoration(
                                        color: _statusColor(q.status).withOpacity(0.1),
                                        borderRadius: BorderRadius.circular(12),
                                      ),
                                      child: Text(
                                        _statusAr(q.status),
                                        style: TextStyle(
                                          color: _statusColor(q.status),
                                          fontSize: 12,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                                subtitle: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    if (q.counterpartyNameAr != null)
                                      Text(q.counterpartyNameAr!),
                                    Row(
                                      children: [
                                        Text(
                                          _dateFormat.format(q.invoiceDate),
                                          style: const TextStyle(fontSize: 12),
                                        ),
                                        const Spacer(),
                                        Text(
                                          '${q.grandTotal.toStringAsFixed(2)} ج.م',
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
    );
  }
}
