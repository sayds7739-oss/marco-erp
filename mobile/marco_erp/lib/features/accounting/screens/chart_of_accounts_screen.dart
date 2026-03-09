import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';

class AccountTreeNode {
  final int id;
  final String code;
  final String nameAr;
  final String? nameEn;
  final String accountType;
  final String? nature;
  final bool isActive;
  final int? parentId;
  final List<AccountTreeNode> children;
  bool isExpanded;

  AccountTreeNode({
    required this.id,
    required this.code,
    required this.nameAr,
    this.nameEn,
    required this.accountType,
    this.nature,
    required this.isActive,
    this.parentId,
    this.children = const [],
    this.isExpanded = false,
  });

  factory AccountTreeNode.fromJson(Map<String, dynamic> json) {
    return AccountTreeNode(
      id: json['id'] as int,
      code: json['code'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
      nameEn: json['nameEn'] as String?,
      accountType: json['accountType'] as String? ?? '',
      nature: json['nature'] as String?,
      isActive: json['isActive'] as bool? ?? true,
      parentId: json['parentId'] as int?,
      children: (json['children'] as List<dynamic>?)
              ?.map((c) =>
                  AccountTreeNode.fromJson(c as Map<String, dynamic>))
              .toList() ??
          [],
    );
  }

  bool get hasChildren => children.isNotEmpty;

  String get accountTypeAr {
    switch (accountType.toLowerCase()) {
      case 'asset':
        return 'أصول';
      case 'liability':
        return 'خصوم';
      case 'equity':
        return 'حقوق ملكية';
      case 'revenue':
        return 'إيرادات';
      case 'expense':
        return 'مصروفات';
      default:
        return accountType;
    }
  }

  Color get typeColor {
    switch (accountType.toLowerCase()) {
      case 'asset':
        return Colors.blue;
      case 'liability':
        return Colors.red;
      case 'equity':
        return Colors.purple;
      case 'revenue':
        return Colors.green;
      case 'expense':
        return Colors.orange;
      default:
        return Colors.grey;
    }
  }
}

class ChartOfAccountsScreen extends StatefulWidget {
  const ChartOfAccountsScreen({super.key});

  @override
  State<ChartOfAccountsScreen> createState() =>
      _ChartOfAccountsScreenState();
}

class _ChartOfAccountsScreenState extends State<ChartOfAccountsScreen> {
  List<AccountTreeNode> _accounts = [];
  bool _isLoading = true;
  String? _error;
  final _searchController = TextEditingController();
  bool _flatView = false;

  @override
  void initState() {
    super.initState();
    _loadAccounts();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadAccounts() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    final api = context.read<ApiClient>();
    final response = await api.get<List<AccountTreeNode>>(
      '${ApiConstants.accounts}/tree',
      fromJson: (json) => (json as List)
          .map((e) =>
              AccountTreeNode.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

    if (mounted) {
      setState(() {
        _isLoading = false;
        if (response.success && response.data != null) {
          _accounts = response.data!;
        } else {
          _error = response.errorMessage;
        }
      });
    }
  }

  /// Flatten the tree into a list for search
  List<AccountTreeNode> _flattenTree(List<AccountTreeNode> nodes) {
    final result = <AccountTreeNode>[];
    for (final node in nodes) {
      result.add(node);
      if (node.children.isNotEmpty) {
        result.addAll(_flattenTree(node.children));
      }
    }
    return result;
  }

  List<AccountTreeNode> get _filteredFlatAccounts {
    final flat = _flattenTree(_accounts);
    final query = _searchController.text.toLowerCase();
    if (query.isEmpty) return flat;
    return flat
        .where((a) =>
            a.nameAr.toLowerCase().contains(query) ||
            a.code.toLowerCase().contains(query) ||
            a.accountTypeAr.contains(query))
        .toList();
  }

  void _showAccountDetails(AccountTreeNode account) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(account.nameAr),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _detailRow('الكود', account.code),
            _detailRow('الاسم بالعربية', account.nameAr),
            if (account.nameEn != null && account.nameEn!.isNotEmpty)
              _detailRow('الاسم بالإنجليزية', account.nameEn!),
            _detailRow('نوع الحساب', account.accountTypeAr),
            if (account.nature != null)
              _detailRow('الطبيعة', account.nature!),
            _detailRow('الحالة', account.isActive ? 'نشط' : 'غير نشط'),
          ],
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
            width: 120,
            child: Text(label,
                style: TextStyle(
                    color: Colors.grey.shade600, fontSize: 13)),
          ),
          Expanded(
              child: Text(value,
                  style:
                      const TextStyle(fontWeight: FontWeight.w500))),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('دليل الحسابات'),
        actions: [
          IconButton(
            icon: Icon(
                _flatView ? Icons.account_tree : Icons.list),
            tooltip: _flatView ? 'عرض شجري' : 'عرض مسطح',
            onPressed: () =>
                setState(() => _flatView = !_flatView),
          ),
        ],
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(56),
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
            child: TextField(
              controller: _searchController,
              decoration: InputDecoration(
                hintText: 'بحث عن حساب...',
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
              onChanged: (_) =>
                  setState(() => _flatView = true),
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
                          onPressed: _loadAccounts,
                          child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadAccounts,
                  child: _flatView || _searchController.text.isNotEmpty
                      ? _buildFlatList()
                      : _buildTreeView(),
                ),
    );
  }

  Widget _buildFlatList() {
    final accounts = _filteredFlatAccounts;
    if (accounts.isEmpty) {
      return const Center(child: Text('لا توجد حسابات'));
    }
    return ListView.builder(
      itemCount: accounts.length,
      itemBuilder: (ctx, i) {
        final a = accounts[i];
        return Card(
          margin:
              const EdgeInsets.symmetric(horizontal: 12, vertical: 3),
          child: ListTile(
            leading: Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: a.typeColor.withOpacity(0.1),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Center(
                child: Text(
                  a.code.length > 3
                      ? a.code.substring(0, 3)
                      : a.code,
                  style: TextStyle(
                    color: a.typeColor,
                    fontWeight: FontWeight.bold,
                    fontSize: 11,
                  ),
                  textDirection: TextDirection.ltr,
                ),
              ),
            ),
            title: Text(a.nameAr,
                style: const TextStyle(
                    fontWeight: FontWeight.bold, fontSize: 14)),
            subtitle: Row(
              children: [
                Text(a.code,
                    style: const TextStyle(fontSize: 12),
                    textDirection: TextDirection.ltr),
                const SizedBox(width: 8),
                Container(
                  padding: const EdgeInsets.symmetric(
                      horizontal: 6, vertical: 1),
                  decoration: BoxDecoration(
                    color: a.typeColor.withOpacity(0.1),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    a.accountTypeAr,
                    style: TextStyle(
                        fontSize: 10, color: a.typeColor),
                  ),
                ),
              ],
            ),
            trailing: Icon(
              Icons.circle,
              size: 10,
              color: a.isActive ? Colors.green : Colors.grey,
            ),
            onTap: () => _showAccountDetails(a),
          ),
        );
      },
    );
  }

  Widget _buildTreeView() {
    if (_accounts.isEmpty) {
      return const Center(child: Text('لا توجد حسابات'));
    }
    return ListView(
      children: _accounts
          .map((node) => _buildTreeNode(node, 0))
          .toList(),
    );
  }

  Widget _buildTreeNode(AccountTreeNode node, int depth) {
    return Column(
      children: [
        InkWell(
          onTap: () {
            if (node.hasChildren) {
              setState(() => node.isExpanded = !node.isExpanded);
            } else {
              _showAccountDetails(node);
            }
          },
          child: Container(
            padding: EdgeInsets.only(
              right: 16.0 + (depth * 24.0),
              left: 12,
              top: 8,
              bottom: 8,
            ),
            child: Row(
              children: [
                if (node.hasChildren)
                  Icon(
                    node.isExpanded
                        ? Icons.expand_more
                        : Icons.chevron_left,
                    size: 20,
                    color: Colors.grey,
                  )
                else
                  const SizedBox(width: 20),
                const SizedBox(width: 4),
                Container(
                  width: 32,
                  height: 32,
                  decoration: BoxDecoration(
                    color: node.typeColor.withOpacity(0.1),
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: Icon(
                    node.hasChildren
                        ? Icons.folder_outlined
                        : Icons.description_outlined,
                    size: 16,
                    color: node.typeColor,
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        node.nameAr,
                        style: TextStyle(
                          fontWeight: node.hasChildren
                              ? FontWeight.bold
                              : FontWeight.normal,
                          fontSize: 14,
                        ),
                      ),
                      Text(
                        node.code,
                        style: TextStyle(
                            fontSize: 11,
                            color: Colors.grey.shade600),
                        textDirection: TextDirection.ltr,
                      ),
                    ],
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(
                      horizontal: 6, vertical: 1),
                  decoration: BoxDecoration(
                    color: node.typeColor.withOpacity(0.1),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    node.accountTypeAr,
                    style: TextStyle(
                        fontSize: 10, color: node.typeColor),
                  ),
                ),
              ],
            ),
          ),
        ),
        const Divider(height: 1),
        if (node.isExpanded && node.hasChildren)
          ...node.children
              .map((child) => _buildTreeNode(child, depth + 1)),
      ],
    );
  }
}
