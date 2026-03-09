import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';
import 'cash_transfer_create_screen.dart';

class CashTransferListModel {
  final int id;
  final String transferNumber;
  final DateTime transferDate;
  final double amount;
  final String? fromCashboxNameAr;
  final String? toCashboxNameAr;
  final String status;

  CashTransferListModel({
    required this.id,
    required this.transferNumber,
    required this.transferDate,
    required this.amount,
    this.fromCashboxNameAr,
    this.toCashboxNameAr,
    required this.status,
  });

  factory CashTransferListModel.fromJson(Map<String, dynamic> json) {
    return CashTransferListModel(
      id: json['id'] as int,
      transferNumber: json['transferNumber'] as String? ?? '',
      transferDate: DateTime.parse(json['transferDate'] as String),
      amount: _d(json['amount']),
      fromCashboxNameAr: json['fromCashboxNameAr'] as String?,
      toCashboxNameAr: json['toCashboxNameAr'] as String?,
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

class CashTransfersScreen extends StatefulWidget {
  const CashTransfersScreen({super.key});

  @override
  State<CashTransfersScreen> createState() => _CashTransfersScreenState();
}

class _CashTransfersScreenState extends State<CashTransfersScreen> {
  List<CashTransferListModel> _transfers = [];
  bool _isLoading = true;
  String? _error;
  final _dateFormat = DateFormat('yyyy/MM/dd', 'ar');

  @override
  void initState() {
    super.initState();
    _loadTransfers();
  }

  Future<void> _loadTransfers() async {
    setState(() { _isLoading = true; _error = null; });
    final api = context.read<ApiClient>();

    final response = await api.get<List<CashTransferListModel>>(
      ApiConstants.cashTransfers,
      fromJson: (json) => (json as List)
          .map((e) => CashTransferListModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

    if (mounted) {
      setState(() {
        _isLoading = false;
        if (response.success && response.data != null) {
          _transfers = response.data!;
        } else {
          _error = response.errorMessage;
        }
      });
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
      appBar: AppBar(title: const Text('تحويلات الخزنة')),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(_error!),
                      const SizedBox(height: 16),
                      ElevatedButton(onPressed: _loadTransfers, child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadTransfers,
                  child: _transfers.isEmpty
                      ? const Center(child: Text('لا توجد تحويلات'))
                      : ListView.builder(
                          itemCount: _transfers.length,
                          itemBuilder: (ctx, i) {
                            final t = _transfers[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                              child: ListTile(
                                leading: Container(
                                  width: 40,
                                  height: 40,
                                  decoration: BoxDecoration(
                                    color: Colors.blue.shade100,
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: Icon(Icons.swap_horiz, color: Colors.blue.shade700),
                                ),
                                title: Row(
                                  children: [
                                    Text(t.transferNumber, style: const TextStyle(fontWeight: FontWeight.bold)),
                                    const Spacer(),
                                    Container(
                                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                                      decoration: BoxDecoration(
                                        color: _statusColor(t.status).withOpacity(0.1),
                                        borderRadius: BorderRadius.circular(12),
                                      ),
                                      child: Text(
                                        _statusAr(t.status),
                                        style: TextStyle(
                                          color: _statusColor(t.status),
                                          fontSize: 12,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                                subtitle: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Row(
                                      children: [
                                        if (t.fromCashboxNameAr != null)
                                          Expanded(child: Text('من: ${t.fromCashboxNameAr}', overflow: TextOverflow.ellipsis)),
                                        const Icon(Icons.arrow_forward, size: 14),
                                        if (t.toCashboxNameAr != null)
                                          Expanded(child: Text('إلى: ${t.toCashboxNameAr}', overflow: TextOverflow.ellipsis)),
                                      ],
                                    ),
                                    Row(
                                      children: [
                                        Text(_dateFormat.format(t.transferDate), style: const TextStyle(fontSize: 12)),
                                        const Spacer(),
                                        Text(
                                          '${t.amount.toStringAsFixed(2)} ج.م',
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
              builder: (_) => const CashTransferCreateScreen(),
            ),
          );
          _loadTransfers();
        },
        child: const Icon(Icons.add),
      ),
    );
  }
}
