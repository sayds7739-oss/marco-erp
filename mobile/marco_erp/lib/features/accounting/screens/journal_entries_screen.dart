import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';

class JournalEntryModel {
  final int id;
  final String journalNumber;
  final DateTime date;
  final String? description;
  final String status;
  final double totalDebit;
  final double totalCredit;
  final List<JournalEntryLineModel> lines;

  JournalEntryModel({
    required this.id,
    required this.journalNumber,
    required this.date,
    this.description,
    required this.status,
    required this.totalDebit,
    required this.totalCredit,
    this.lines = const [],
  });

  factory JournalEntryModel.fromJson(Map<String, dynamic> json) {
    return JournalEntryModel(
      id: json['id'] as int,
      journalNumber: json['journalNumber'] as String? ?? '',
      date: DateTime.parse(json['date'] as String),
      description: json['description'] as String?,
      status: json['status'] as String? ?? '',
      totalDebit: _toDouble(json['totalDebit']),
      totalCredit: _toDouble(json['totalCredit']),
      lines: (json['lines'] as List<dynamic>?)
              ?.map((l) => JournalEntryLineModel.fromJson(
                  l as Map<String, dynamic>))
              .toList() ??
          [],
    );
  }

  static double _toDouble(dynamic value) {
    if (value == null) return 0.0;
    if (value is double) return value;
    if (value is int) return value.toDouble();
    return double.tryParse(value.toString()) ?? 0.0;
  }

  String get statusAr {
    switch (status.toLowerCase()) {
      case 'draft':
        return 'مسودة';
      case 'posted':
        return 'مرحّل';
      case 'cancelled':
        return 'ملغي';
      default:
        return status;
    }
  }

  bool get isBalanced =>
      (totalDebit - totalCredit).abs() < 0.01;
}

class JournalEntryLineModel {
  final int id;
  final String? accountCode;
  final String? accountNameAr;
  final double debit;
  final double credit;
  final String? description;

  JournalEntryLineModel({
    required this.id,
    this.accountCode,
    this.accountNameAr,
    required this.debit,
    required this.credit,
    this.description,
  });

  factory JournalEntryLineModel.fromJson(Map<String, dynamic> json) {
    return JournalEntryLineModel(
      id: json['id'] as int,
      accountCode: json['accountCode'] as String?,
      accountNameAr: json['accountNameAr'] as String?,
      debit: JournalEntryModel._toDouble(json['debit']),
      credit: JournalEntryModel._toDouble(json['credit']),
      description: json['description'] as String?,
    );
  }
}

class JournalEntriesScreen extends StatefulWidget {
  const JournalEntriesScreen({super.key});

  @override
  State<JournalEntriesScreen> createState() =>
      _JournalEntriesScreenState();
}

class _JournalEntriesScreenState extends State<JournalEntriesScreen> {
  List<JournalEntryModel> _entries = [];
  bool _isLoading = true;
  String? _error;
  final _dateFormat = DateFormat('yyyy/MM/dd', 'ar');
  final _searchController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _loadEntries();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadEntries() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    final api = context.read<ApiClient>();
    final response = await api.get<List<JournalEntryModel>>(
      ApiConstants.journalEntries,
      fromJson: (json) => (json as List)
          .map((e) =>
              JournalEntryModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

    if (mounted) {
      setState(() {
        _isLoading = false;
        if (response.success && response.data != null) {
          _entries = response.data!;
        } else {
          _error = response.errorMessage;
        }
      });
    }
  }

  List<JournalEntryModel> get _filteredEntries {
    final query = _searchController.text.toLowerCase();
    if (query.isEmpty) return _entries;
    return _entries
        .where((e) =>
            e.journalNumber.toLowerCase().contains(query) ||
            (e.description?.toLowerCase().contains(query) ?? false) ||
            e.statusAr.contains(query))
        .toList();
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

  void _showEntryDetails(JournalEntryModel entry) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => DraggableScrollableSheet(
        initialChildSize: 0.7,
        minChildSize: 0.4,
        maxChildSize: 0.95,
        expand: false,
        builder: (_, controller) => Column(
          children: [
            // Header
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: Theme.of(context).colorScheme.primary,
                borderRadius: const BorderRadius.only(
                  topLeft: Radius.circular(16),
                  topRight: Radius.circular(16),
                ),
              ),
              child: Column(
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          'قيد ${entry.journalNumber}',
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 18,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: Colors.white24,
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Text(
                          entry.statusAr,
                          style: const TextStyle(
                              color: Colors.white, fontSize: 12),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Text(
                        _dateFormat.format(entry.date),
                        style: const TextStyle(
                            color: Colors.white70, fontSize: 13),
                      ),
                      const Spacer(),
                      if (!entry.isBalanced)
                        Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 8, vertical: 2),
                          decoration: BoxDecoration(
                            color: Colors.red.shade300,
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: const Text(
                            'غير متوازن',
                            style: TextStyle(
                                color: Colors.white, fontSize: 11),
                          ),
                        ),
                    ],
                  ),
                  if (entry.description != null &&
                      entry.description!.isNotEmpty)
                    Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Align(
                        alignment: Alignment.centerRight,
                        child: Text(
                          entry.description!,
                          style: const TextStyle(
                              color: Colors.white70, fontSize: 13),
                        ),
                      ),
                    ),
                ],
              ),
            ),

            // Debit/Credit summary
            Container(
              padding: const EdgeInsets.symmetric(
                  horizontal: 16, vertical: 12),
              color: Colors.grey.shade100,
              child: Row(
                children: [
                  Expanded(
                    child: Column(
                      children: [
                        const Text('إجمالي المدين',
                            style: TextStyle(fontSize: 12)),
                        Text(
                          '${entry.totalDebit.toStringAsFixed(2)} ج.م',
                          style: const TextStyle(
                              fontWeight: FontWeight.bold,
                              color: Colors.green),
                        ),
                      ],
                    ),
                  ),
                  Container(
                      width: 1, height: 30, color: Colors.grey),
                  Expanded(
                    child: Column(
                      children: [
                        const Text('إجمالي الدائن',
                            style: TextStyle(fontSize: 12)),
                        Text(
                          '${entry.totalCredit.toStringAsFixed(2)} ج.م',
                          style: const TextStyle(
                              fontWeight: FontWeight.bold,
                              color: Colors.red),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),

            // Lines
            Expanded(
              child: entry.lines.isEmpty
                  ? const Center(
                      child: Text('لا توجد بنود',
                          style: TextStyle(color: Colors.grey)))
                  : ListView.builder(
                      controller: controller,
                      itemCount: entry.lines.length,
                      itemBuilder: (ctx2, i) {
                        final line = entry.lines[i];
                        final isDebit = line.debit > 0;
                        return ListTile(
                          leading: Container(
                            width: 36,
                            height: 36,
                            decoration: BoxDecoration(
                              color: isDebit
                                  ? Colors.green.shade50
                                  : Colors.red.shade50,
                              borderRadius:
                                  BorderRadius.circular(8),
                            ),
                            child: Center(
                              child: Text(
                                isDebit ? 'مد' : 'دا',
                                style: TextStyle(
                                  color: isDebit
                                      ? Colors.green
                                      : Colors.red,
                                  fontWeight: FontWeight.bold,
                                  fontSize: 12,
                                ),
                              ),
                            ),
                          ),
                          title: Text(
                            line.accountNameAr ?? '-',
                            style: const TextStyle(fontSize: 14),
                          ),
                          subtitle: Column(
                            crossAxisAlignment:
                                CrossAxisAlignment.start,
                            children: [
                              if (line.accountCode != null)
                                Text(line.accountCode!,
                                    style: const TextStyle(
                                        fontSize: 11),
                                    textDirection:
                                        TextDirection.ltr),
                              if (line.description != null &&
                                  line.description!.isNotEmpty)
                                Text(line.description!,
                                    style: TextStyle(
                                        fontSize: 11,
                                        color: Colors
                                            .grey.shade600)),
                            ],
                          ),
                          trailing: Text(
                            '${(isDebit ? line.debit : line.credit).toStringAsFixed(2)} ج.م',
                            style: TextStyle(
                              fontWeight: FontWeight.bold,
                              color: isDebit
                                  ? Colors.green
                                  : Colors.red,
                            ),
                          ),
                        );
                      },
                    ),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('القيود اليومية'),
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(56),
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
            child: TextField(
              controller: _searchController,
              decoration: InputDecoration(
                hintText: 'بحث عن قيد...',
                prefixIcon: const Icon(Icons.search),
                filled: true,
                fillColor: Colors.white,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(10),
                  borderSide: BorderSide.none,
                ),
                contentPadding:
                    const EdgeInsets.symmetric(horizontal: 16),
              ),
              onChanged: (_) => setState(() {}),
            ),
          ),
        ),
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
                          style:
                              const TextStyle(color: Colors.grey)),
                      const SizedBox(height: 16),
                      ElevatedButton(
                          onPressed: _loadEntries,
                          child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadEntries,
                  child: _filteredEntries.isEmpty
                      ? const Center(
                          child: Text('لا توجد قيود'))
                      : ListView.builder(
                          itemCount: _filteredEntries.length,
                          itemBuilder: (ctx, i) {
                            final entry = _filteredEntries[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(
                                  horizontal: 12, vertical: 4),
                              child: ListTile(
                                leading: Container(
                                  width: 40,
                                  height: 40,
                                  decoration: BoxDecoration(
                                    color: Colors.cyan.shade100,
                                    borderRadius:
                                        BorderRadius.circular(8),
                                  ),
                                  child: Icon(
                                      Icons.description,
                                      color:
                                          Colors.cyan.shade700),
                                ),
                                title: Row(
                                  children: [
                                    Text(entry.journalNumber,
                                        style: const TextStyle(
                                            fontWeight:
                                                FontWeight.bold)),
                                    const Spacer(),
                                    Container(
                                      padding:
                                          const EdgeInsets
                                              .symmetric(
                                              horizontal: 8,
                                              vertical: 2),
                                      decoration: BoxDecoration(
                                        color: _statusColor(
                                                entry.status)
                                            .withOpacity(0.1),
                                        borderRadius:
                                            BorderRadius.circular(
                                                12),
                                      ),
                                      child: Text(
                                        entry.statusAr,
                                        style: TextStyle(
                                          color: _statusColor(
                                              entry.status),
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
                                    if (entry.description !=
                                            null &&
                                        entry.description!
                                            .isNotEmpty)
                                      Text(
                                        entry.description!,
                                        maxLines: 1,
                                        overflow: TextOverflow
                                            .ellipsis,
                                        style: const TextStyle(
                                            fontSize: 12),
                                      ),
                                    Row(
                                      children: [
                                        Text(
                                          _dateFormat.format(
                                              entry.date),
                                          style: const TextStyle(
                                              fontSize: 12),
                                        ),
                                        const Spacer(),
                                        Text(
                                          '${entry.totalDebit.toStringAsFixed(2)} ج.م',
                                          style: const TextStyle(
                                            fontWeight:
                                                FontWeight.bold,
                                            fontSize: 13,
                                          ),
                                        ),
                                        if (!entry.isBalanced)
                                          const Padding(
                                            padding:
                                                EdgeInsets.only(
                                                    right: 4),
                                            child: Icon(
                                                Icons.warning,
                                                size: 14,
                                                color: Colors
                                                    .orange),
                                          ),
                                      ],
                                    ),
                                  ],
                                ),
                                onTap: () =>
                                    _showEntryDetails(entry),
                              ),
                            );
                          },
                        ),
                ),
    );
  }
}
