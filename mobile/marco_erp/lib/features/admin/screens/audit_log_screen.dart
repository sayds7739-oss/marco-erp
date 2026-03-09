import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';

class AuditLogModel {
  final int id;
  final String action;
  final String entityType;
  final int? entityId;
  final String? userName;
  final String? details;
  final DateTime timestamp;

  AuditLogModel({
    required this.id,
    required this.action,
    required this.entityType,
    this.entityId,
    this.userName,
    this.details,
    required this.timestamp,
  });

  factory AuditLogModel.fromJson(Map<String, dynamic> json) {
    return AuditLogModel(
      id: json['id'] as int,
      action: json['action'] as String? ?? '',
      entityType: json['entityType'] as String? ?? '',
      entityId: json['entityId'] as int?,
      userName: json['userName'] as String?,
      details: json['details'] as String?,
      timestamp: DateTime.parse(json['timestamp'] as String),
    );
  }

  String get actionAr {
    switch (action.toLowerCase()) {
      case 'create':
        return 'إنشاء';
      case 'update':
        return 'تعديل';
      case 'delete':
        return 'حذف';
      case 'post':
        return 'ترحيل';
      case 'cancel':
        return 'إلغاء';
      case 'login':
        return 'تسجيل دخول';
      case 'logout':
        return 'تسجيل خروج';
      default:
        return action;
    }
  }

  Color get actionColor {
    switch (action.toLowerCase()) {
      case 'create':
        return Colors.green;
      case 'update':
        return Colors.blue;
      case 'delete':
        return Colors.red;
      case 'post':
        return Colors.teal;
      case 'cancel':
        return Colors.orange;
      case 'login':
        return Colors.indigo;
      case 'logout':
        return Colors.grey;
      default:
        return Colors.grey;
    }
  }

  IconData get actionIcon {
    switch (action.toLowerCase()) {
      case 'create':
        return Icons.add_circle_outline;
      case 'update':
        return Icons.edit;
      case 'delete':
        return Icons.delete_outline;
      case 'post':
        return Icons.check_circle_outline;
      case 'cancel':
        return Icons.cancel_outlined;
      case 'login':
        return Icons.login;
      case 'logout':
        return Icons.logout;
      default:
        return Icons.info_outline;
    }
  }
}

class AuditLogScreen extends StatefulWidget {
  const AuditLogScreen({super.key});

  @override
  State<AuditLogScreen> createState() => _AuditLogScreenState();
}

class _AuditLogScreenState extends State<AuditLogScreen> {
  List<AuditLogModel> _logs = [];
  bool _isLoading = false;
  String? _error;
  final _dateFormat = DateFormat('yyyy/MM/dd', 'ar');
  final _timeFormat = DateFormat('HH:mm:ss', 'ar');

  DateTime _fromDate = DateTime.now().subtract(const Duration(days: 7));
  DateTime _toDate = DateTime.now();
  final _searchController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _loadLogs();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadLogs() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    final api = context.read<ApiClient>();
    final queryParams = {
      'fromDate': _fromDate.toIso8601String(),
      'toDate': _toDate.add(const Duration(days: 1)).toIso8601String(),
    };

    final response = await api.get<List<AuditLogModel>>(
      '${ApiConstants.auditLogs}?${Uri(queryParameters: queryParams).query}',
      fromJson: (json) => (json as List)
          .map((e) => AuditLogModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

    if (mounted) {
      setState(() {
        _isLoading = false;
        if (response.success && response.data != null) {
          _logs = response.data!;
        } else {
          _error = response.errorMessage;
        }
      });
    }
  }

  List<AuditLogModel> get _filteredLogs {
    final query = _searchController.text.toLowerCase();
    if (query.isEmpty) return _logs;
    return _logs
        .where((l) =>
            l.entityType.toLowerCase().contains(query) ||
            l.actionAr.contains(query) ||
            (l.userName?.toLowerCase().contains(query) ?? false) ||
            (l.details?.toLowerCase().contains(query) ?? false))
        .toList();
  }

  Future<void> _selectDate(bool isFrom) async {
    final picked = await showDatePicker(
      context: context,
      initialDate: isFrom ? _fromDate : _toDate,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 1)),
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

  void _showLogDetails(AuditLogModel log) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Row(
          children: [
            Icon(log.actionIcon, color: log.actionColor, size: 24),
            const SizedBox(width: 8),
            Expanded(child: Text('${log.actionAr} - ${log.entityType}')),
          ],
        ),
        content: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              _detailRow('المستخدم', log.userName ?? '-'),
              _detailRow('العملية', log.actionAr),
              _detailRow('نوع السجل', log.entityType),
              if (log.entityId != null)
                _detailRow('رقم السجل', log.entityId.toString()),
              _detailRow(
                  'التاريخ', _dateFormat.format(log.timestamp)),
              _detailRow(
                  'الوقت', _timeFormat.format(log.timestamp)),
              if (log.details != null && log.details!.isNotEmpty) ...[
                const SizedBox(height: 12),
                const Text('التفاصيل:',
                    style: TextStyle(fontWeight: FontWeight.bold)),
                const SizedBox(height: 4),
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(8),
                  decoration: BoxDecoration(
                    color: Colors.grey.shade100,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    log.details!,
                    style: const TextStyle(fontSize: 12),
                    textDirection: TextDirection.ltr,
                  ),
                ),
              ],
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('إغلاق'),
          ),
        ],
      ),
    );
  }

  Widget _detailRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 80,
            child: Text(label,
                style: TextStyle(
                    color: Colors.grey.shade600, fontSize: 13)),
          ),
          Expanded(
              child: Text(value,
                  style: const TextStyle(fontWeight: FontWeight.w500))),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('سجل المراجعة'),
      ),
      body: Column(
        children: [
          // Date filter row
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
                        contentPadding:
                            EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                      ),
                      child: Text(_dateFormat.format(_fromDate)),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: InkWell(
                    onTap: () => _selectDate(false),
                    child: InputDecorator(
                      decoration: const InputDecoration(
                        labelText: 'إلى تاريخ',
                        border: OutlineInputBorder(),
                        contentPadding:
                            EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                      ),
                      child: Text(_dateFormat.format(_toDate)),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                ElevatedButton(
                  onPressed: _isLoading ? null : _loadLogs,
                  style: ElevatedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(
                        horizontal: 16, vertical: 16),
                  ),
                  child: _isLoading
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child:
                              CircularProgressIndicator(strokeWidth: 2))
                      : const Icon(Icons.search),
                ),
              ],
            ),
          ),

          // Search bar
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: TextField(
              controller: _searchController,
              decoration: InputDecoration(
                hintText: 'بحث في السجلات...',
                prefixIcon: const Icon(Icons.filter_list),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(10),
                ),
                contentPadding:
                    const EdgeInsets.symmetric(horizontal: 16),
              ),
              onChanged: (_) => setState(() {}),
            ),
          ),
          const SizedBox(height: 8),

          // Log list
          Expanded(
            child: _isLoading
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
                                onPressed: _loadLogs,
                                child: const Text('إعادة المحاولة')),
                          ],
                        ),
                      )
                    : _filteredLogs.isEmpty
                        ? const Center(
                            child: Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                Icon(Icons.history,
                                    size: 64, color: Colors.grey),
                                SizedBox(height: 16),
                                Text('لا توجد سجلات',
                                    style:
                                        TextStyle(color: Colors.grey)),
                              ],
                            ),
                          )
                        : RefreshIndicator(
                            onRefresh: _loadLogs,
                            child: ListView.builder(
                              itemCount: _filteredLogs.length,
                              itemBuilder: (ctx, i) {
                                final log = _filteredLogs[i];
                                return Card(
                                  margin: const EdgeInsets.symmetric(
                                      horizontal: 12, vertical: 3),
                                  child: ListTile(
                                    leading: Container(
                                      width: 40,
                                      height: 40,
                                      decoration: BoxDecoration(
                                        color: log.actionColor
                                            .withOpacity(0.1),
                                        borderRadius:
                                            BorderRadius.circular(8),
                                      ),
                                      child: Icon(log.actionIcon,
                                          color: log.actionColor,
                                          size: 20),
                                    ),
                                    title: Row(
                                      children: [
                                        Container(
                                          padding:
                                              const EdgeInsets.symmetric(
                                                  horizontal: 8,
                                                  vertical: 2),
                                          decoration: BoxDecoration(
                                            color: log.actionColor
                                                .withOpacity(0.1),
                                            borderRadius:
                                                BorderRadius.circular(12),
                                          ),
                                          child: Text(
                                            log.actionAr,
                                            style: TextStyle(
                                              color: log.actionColor,
                                              fontSize: 12,
                                              fontWeight: FontWeight.bold,
                                            ),
                                          ),
                                        ),
                                        const SizedBox(width: 8),
                                        Expanded(
                                          child: Text(
                                            log.entityType,
                                            style: const TextStyle(
                                                fontWeight:
                                                    FontWeight.bold,
                                                fontSize: 14),
                                            overflow:
                                                TextOverflow.ellipsis,
                                          ),
                                        ),
                                      ],
                                    ),
                                    subtitle: Row(
                                      children: [
                                        if (log.userName != null)
                                          Text(
                                            log.userName!,
                                            style: const TextStyle(
                                                fontSize: 12),
                                          ),
                                        const Spacer(),
                                        Text(
                                          '${_dateFormat.format(log.timestamp)} ${_timeFormat.format(log.timestamp)}',
                                          style: TextStyle(
                                            fontSize: 11,
                                            color: Colors.grey.shade600,
                                          ),
                                        ),
                                      ],
                                    ),
                                    onTap: () => _showLogDetails(log),
                                  ),
                                );
                              },
                            ),
                          ),
          ),
        ],
      ),
    );
  }
}
