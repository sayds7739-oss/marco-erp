import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';

class ReportsScreen extends StatefulWidget {
  const ReportsScreen({super.key});

  @override
  State<ReportsScreen> createState() => _ReportsScreenState();
}

class _ReportsScreenState extends State<ReportsScreen> {
  DateTime _fromDate = DateTime.now().subtract(const Duration(days: 30));
  DateTime _toDate = DateTime.now();
  final _dateFormat = DateFormat('yyyy/MM/dd', 'ar');

  Map<String, dynamic>? _reportData;
  bool _isLoading = false;
  String? _error;
  String _selectedReport = 'sales';

  final List<Map<String, dynamic>> _reports = [
    {'key': 'sales', 'name': 'تقرير المبيعات', 'icon': Icons.trending_up, 'color': Colors.green},
    {'key': 'purchases', 'name': 'تقرير المشتريات', 'icon': Icons.shopping_cart, 'color': Colors.orange},
    {'key': 'inventory', 'name': 'تقرير المخزون', 'icon': Icons.inventory, 'color': Colors.blue},
    {'key': 'cashflow', 'name': 'تقرير التدفق النقدي', 'icon': Icons.account_balance_wallet, 'color': Colors.purple},
    {'key': 'profit', 'name': 'تقرير الأرباح', 'icon': Icons.bar_chart, 'color': Colors.teal},
  ];

  Future<void> _loadReport() async {
    setState(() { _isLoading = true; _error = null; });
    final api = context.read<ApiClient>();

    final queryParams = {
      'reportType': _selectedReport,
      'fromDate': _fromDate.toIso8601String(),
      'toDate': _toDate.toIso8601String(),
    };

    final response = await api.get<Map<String, dynamic>>(
      '${ApiConstants.reports}?${Uri(queryParameters: queryParams).query}',
      fromJson: (json) => json as Map<String, dynamic>,
    );

    if (mounted) {
      setState(() {
        _isLoading = false;
        if (response.success && response.data != null) {
          _reportData = response.data;
        } else {
          _error = response.errorMessage;
        }
      });
    }
  }

  Future<void> _selectDate(bool isFrom) async {
    final picked = await showDatePicker(
      context: context,
      initialDate: isFrom ? _fromDate : _toDate,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
      locale: const Locale('ar'),
    );
    if (picked != null) {
      setState(() {
        if (isFrom) {
          _fromDate = picked;
        } else {
          _toDate = picked;
        }
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('التقارير')),
      body: Column(
        children: [
          // Report Type Selector
          Container(
            height: 100,
            padding: const EdgeInsets.symmetric(vertical: 8),
            child: ListView.builder(
              scrollDirection: Axis.horizontal,
              itemCount: _reports.length,
              padding: const EdgeInsets.symmetric(horizontal: 8),
              itemBuilder: (ctx, i) {
                final r = _reports[i];
                final isSelected = _selectedReport == r['key'];
                return GestureDetector(
                  onTap: () => setState(() => _selectedReport = r['key']),
                  child: Container(
                    width: 90,
                    margin: const EdgeInsets.symmetric(horizontal: 4),
                    decoration: BoxDecoration(
                      color: isSelected ? (r['color'] as Color).withOpacity(0.2) : Colors.grey.shade100,
                      borderRadius: BorderRadius.circular(12),
                      border: isSelected ? Border.all(color: r['color'] as Color, width: 2) : null,
                    ),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(r['icon'] as IconData, color: r['color'] as Color, size: 28),
                        const SizedBox(height: 4),
                        Text(
                          r['name'] as String,
                          style: TextStyle(
                            fontSize: 10,
                            fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                            color: isSelected ? r['color'] as Color : Colors.grey.shade700,
                          ),
                          textAlign: TextAlign.center,
                          maxLines: 2,
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
          ),

          // Date Range Selector
          Padding(
            padding: const EdgeInsets.all(12),
            child: Row(
              children: [
                Expanded(
                  child: InkWell(
                    onTap: () => _selectDate(true),
                    child: InputDecorator(
                      decoration: const InputDecoration(
                        labelText: 'من تاريخ',
                        border: OutlineInputBorder(),
                        contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                      ),
                      child: Text(_dateFormat.format(_fromDate)),
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: InkWell(
                    onTap: () => _selectDate(false),
                    child: InputDecorator(
                      decoration: const InputDecoration(
                        labelText: 'إلى تاريخ',
                        border: OutlineInputBorder(),
                        contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                      ),
                      child: Text(_dateFormat.format(_toDate)),
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                ElevatedButton(
                  onPressed: _isLoading ? null : _loadReport,
                  style: ElevatedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
                  ),
                  child: _isLoading
                      ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                      : const Icon(Icons.search),
                ),
              ],
            ),
          ),

          const Divider(),

          // Report Content
          Expanded(
            child: _isLoading
                ? const Center(child: CircularProgressIndicator())
                : _error != null
                    ? Center(
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            const Icon(Icons.error_outline, size: 48, color: Colors.grey),
                            const SizedBox(height: 16),
                            Text(_error!, style: const TextStyle(color: Colors.grey)),
                            const SizedBox(height: 16),
                            ElevatedButton(onPressed: _loadReport, child: const Text('إعادة المحاولة')),
                          ],
                        ),
                      )
                    : _reportData == null
                        ? const Center(
                            child: Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                Icon(Icons.analytics_outlined, size: 64, color: Colors.grey),
                                SizedBox(height: 16),
                                Text('اختر نوع التقرير واضغط بحث', style: TextStyle(color: Colors.grey)),
                              ],
                            ),
                          )
                        : _buildReportContent(),
          ),
        ],
      ),
    );
  }

  Widget _buildReportContent() {
    final data = _reportData!;
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        // Summary Cards
        if (data['summary'] != null) ...[
          Text('ملخص التقرير', style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold)),
          const SizedBox(height: 12),
          GridView.count(
            crossAxisCount: 2,
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            mainAxisSpacing: 8,
            crossAxisSpacing: 8,
            childAspectRatio: 1.5,
            children: (data['summary'] as Map<String, dynamic>).entries.map((e) {
              return Card(
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(
                        _translateKey(e.key),
                        style: TextStyle(fontSize: 12, color: Colors.grey.shade600),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        _formatValue(e.value),
                        style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                      ),
                    ],
                  ),
                ),
              );
            }).toList(),
          ),
          const SizedBox(height: 24),
        ],

        // Details
        if (data['details'] != null && (data['details'] as List).isNotEmpty) ...[
          Text('التفاصيل', style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold)),
          const SizedBox(height: 12),
          ...(data['details'] as List).map((item) {
            final m = item as Map<String, dynamic>;
            return Card(
              margin: const EdgeInsets.only(bottom: 8),
              child: ListTile(
                title: Text(m['name']?.toString() ?? m['description']?.toString() ?? ''),
                subtitle: Text(m['date']?.toString() ?? ''),
                trailing: Text(
                  _formatValue(m['amount'] ?? m['total'] ?? m['value']),
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
            );
          }),
        ],
      ],
    );
  }

  String _translateKey(String key) {
    final translations = {
      'totalSales': 'إجمالي المبيعات',
      'totalPurchases': 'إجمالي المشتريات',
      'totalReceipts': 'إجمالي المقبوضات',
      'totalPayments': 'إجمالي المدفوعات',
      'netProfit': 'صافي الربح',
      'invoiceCount': 'عدد الفواتير',
      'productCount': 'عدد الأصناف',
      'totalStock': 'إجمالي المخزون',
      'stockValue': 'قيمة المخزون',
    };
    return translations[key] ?? key;
  }

  String _formatValue(dynamic value) {
    if (value == null) return '-';
    if (value is num) {
      return '${value.toStringAsFixed(2)} ج.م';
    }
    return value.toString();
  }
}
