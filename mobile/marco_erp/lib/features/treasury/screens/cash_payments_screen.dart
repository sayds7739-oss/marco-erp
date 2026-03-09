import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../widgets/sync_status_widget.dart';

class CashPaymentListModel {
  final int id;
  final String paymentNumber;
  final DateTime paymentDate;
  final double amount;
  final String? supplierNameAr;
  final String? cashboxNameAr;
  final String status;

  CashPaymentListModel({
    required this.id,
    required this.paymentNumber,
    required this.paymentDate,
    required this.amount,
    this.supplierNameAr,
    this.cashboxNameAr,
    required this.status,
  });

  factory CashPaymentListModel.fromJson(Map<String, dynamic> json) {
    return CashPaymentListModel(
      id: json['id'] as int,
      paymentNumber: json['paymentNumber'] as String? ?? '',
      paymentDate: DateTime.parse(json['paymentDate'] as String),
      amount: _d(json['amount']),
      supplierNameAr: json['supplierNameAr'] as String?,
      cashboxNameAr: json['cashboxNameAr'] as String?,
      status: json['status'] as String? ?? '',
    );
  }

  static double _d(dynamic v) {
    if (v == null) return 0;
    if (v is double) return v;
    if (v is int) return v.toDouble();
    return double.tryParse(v.toString()) ?? 0;
  }
}

class CashPaymentsScreen extends StatefulWidget {
  const CashPaymentsScreen({super.key});

  @override
  State<CashPaymentsScreen> createState() => _CashPaymentsScreenState();
}

class _CashPaymentsScreenState extends State<CashPaymentsScreen> {
  List<CashPaymentListModel> _payments = [];
  bool _isLoading = true;
  String? _error;
  final _dateFormat = DateFormat('yyyy/MM/dd', 'ar');

  @override
  void initState() {
    super.initState();
    _loadPayments();
  }

  Future<void> _loadPayments() async {
    setState(() { _isLoading = true; _error = null; });
    try {
      final offlineData = context.read<OfflineDataProvider>();
      final rows = await offlineData.getAll('cash_payments');
      if (mounted) {
        setState(() {
          _isLoading = false;
          _payments = rows.map((row) => CashPaymentListModel(
            id: row['id'] as int,
            paymentNumber: row['payment_number'] as String? ?? '',
            paymentDate: DateTime.parse(row['payment_date'] as String),
            amount: (row['amount'] as num?)?.toDouble() ?? 0,
            supplierNameAr: null,
            cashboxNameAr: null,
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

  String _statusAr(String status) {
    switch (status.toLowerCase()) {
      case 'posted': return 'مرحّل';
      case 'draft': return 'مسودة';
      case 'cancelled': return 'ملغي';
      default: return status;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('سندات الصرف')),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(_error!),
                      const SizedBox(height: 16),
                      ElevatedButton(onPressed: _loadPayments, child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadPayments,
                  child: _payments.isEmpty
                      ? const Center(child: Text('لا توجد سندات صرف'))
                      : ListView.builder(
                          itemCount: _payments.length,
                          itemBuilder: (ctx, i) {
                            final p = _payments[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                              child: ListTile(
                                leading: Container(
                                  width: 40,
                                  height: 40,
                                  decoration: BoxDecoration(
                                    color: Colors.red.shade100,
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: Icon(Icons.payments_outlined, color: Colors.red.shade700),
                                ),
                                title: Row(
                                  children: [
                                    Text(p.paymentNumber, style: const TextStyle(fontWeight: FontWeight.bold)),
                                    const Spacer(),
                                    Container(
                                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                                      decoration: BoxDecoration(
                                        color: _statusColor(p.status).withOpacity(0.1),
                                        borderRadius: BorderRadius.circular(12),
                                      ),
                                      child: Text(
                                        _statusAr(p.status),
                                        style: TextStyle(
                                          color: _statusColor(p.status),
                                          fontSize: 12,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                                subtitle: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    if (p.supplierNameAr != null)
                                      Text(p.supplierNameAr!),
                                    Row(
                                      children: [
                                        Text(_dateFormat.format(p.paymentDate), style: const TextStyle(fontSize: 12)),
                                        if (p.cashboxNameAr != null) ...[
                                          const Text(' • ', style: TextStyle(fontSize: 12)),
                                          Text(p.cashboxNameAr!, style: const TextStyle(fontSize: 12)),
                                        ],
                                        const Spacer(),
                                        Text(
                                          '${p.amount.toStringAsFixed(2)} ج.م',
                                          style: const TextStyle(fontWeight: FontWeight.bold, color: Colors.red),
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
