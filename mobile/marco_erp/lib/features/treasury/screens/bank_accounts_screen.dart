import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../widgets/sync_status_widget.dart';

class BankAccountModel {
  final int id;
  final String code;
  final String nameAr;
  final String? bankName;
  final String? accountNumber;
  final double currentBalance;
  final bool isActive;

  BankAccountModel({
    required this.id,
    required this.code,
    required this.nameAr,
    this.bankName,
    this.accountNumber,
    required this.currentBalance,
    required this.isActive,
  });

  factory BankAccountModel.fromJson(Map<String, dynamic> json) {
    return BankAccountModel(
      id: json['id'] as int,
      code: json['code'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
      bankName: json['bankName'] as String?,
      accountNumber: json['accountNumber'] as String?,
      currentBalance: _d(json['currentBalance']),
      isActive: json['isActive'] as bool? ?? true,
    );
  }

  static double _d(dynamic v) {
    if (v == null) return 0;
    if (v is double) return v;
    if (v is int) return v.toDouble();
    return double.tryParse(v.toString()) ?? 0;
  }
}

class BankAccountsScreen extends StatefulWidget {
  const BankAccountsScreen({super.key});

  @override
  State<BankAccountsScreen> createState() => _BankAccountsScreenState();
}

class _BankAccountsScreenState extends State<BankAccountsScreen> {
  List<BankAccountModel> _accounts = [];
  bool _isLoading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadAccounts();
  }

  Future<void> _loadAccounts() async {
    setState(() { _isLoading = true; _error = null; });
    try {
      final offlineData = context.read<OfflineDataProvider>();
      final rows = await offlineData.getAll('bank_accounts');
      if (mounted) {
        setState(() {
          _isLoading = false;
          _accounts = rows.map((row) => BankAccountModel(
            id: row['id'] as int,
            code: row['code'] as String? ?? '',
            nameAr: row['name_ar'] as String? ?? '',
            bankName: row['bank_name'] as String?,
            accountNumber: row['account_number'] as String?,
            currentBalance: (row['current_balance'] as num?)?.toDouble() ?? 0,
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
      appBar: AppBar(title: const Text('الحسابات البنكية')),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(_error!),
                      const SizedBox(height: 16),
                      ElevatedButton(onPressed: _loadAccounts, child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadAccounts,
                  child: _accounts.isEmpty
                      ? const Center(child: Text('لا توجد حسابات بنكية'))
                      : ListView.builder(
                          itemCount: _accounts.length,
                          itemBuilder: (ctx, i) {
                            final a = _accounts[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                              child: ListTile(
                                leading: Container(
                                  width: 40,
                                  height: 40,
                                  decoration: BoxDecoration(
                                    color: Colors.indigo.shade100,
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: Icon(Icons.account_balance, color: Colors.indigo.shade700),
                                ),
                                title: Text(a.nameAr, style: const TextStyle(fontWeight: FontWeight.bold)),
                                subtitle: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    if (a.bankName != null)
                                      Text(a.bankName!),
                                    Row(
                                      children: [
                                        Text(a.code, style: const TextStyle(fontSize: 12)),
                                        if (a.accountNumber != null) ...[
                                          const Text(' • ', style: TextStyle(fontSize: 12)),
                                          Text(a.accountNumber!, style: const TextStyle(fontSize: 12)),
                                        ],
                                        const Spacer(),
                                        Text(
                                          '${a.currentBalance.toStringAsFixed(2)} ج.م',
                                          style: TextStyle(
                                            fontWeight: FontWeight.bold,
                                            color: a.currentBalance >= 0 ? Colors.green : Colors.red,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ],
                                ),
                                trailing: Icon(
                                  Icons.circle,
                                  size: 12,
                                  color: a.isActive ? Colors.green : Colors.grey,
                                ),
                              ),
                            );
                          },
                        ),
                ),
    );
  }
}
