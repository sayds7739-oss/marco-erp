import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../widgets/sync_status_widget.dart';
import 'category_detail_screen.dart';

class CategoryModel {
  final int id;
  final String code;
  final String nameAr;
  final String? nameEn;
  final int? parentId;
  final String? parentNameAr;
  final bool isActive;

  CategoryModel({
    required this.id,
    required this.code,
    required this.nameAr,
    this.nameEn,
    this.parentId,
    this.parentNameAr,
    required this.isActive,
  });

  factory CategoryModel.fromJson(Map<String, dynamic> json) {
    return CategoryModel(
      id: json['id'] as int,
      code: json['code'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
      nameEn: json['nameEn'] as String?,
      parentId: json['parentId'] as int?,
      parentNameAr: json['parentNameAr'] as String?,
      isActive: json['isActive'] as bool? ?? true,
    );
  }
}

class CategoriesScreen extends StatefulWidget {
  const CategoriesScreen({super.key});

  @override
  State<CategoriesScreen> createState() => _CategoriesScreenState();
}

class _CategoriesScreenState extends State<CategoriesScreen> {
  List<CategoryModel> _categories = [];
  bool _isLoading = true;
  String? _error;
  final _searchController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _loadCategories();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadCategories() async {
    setState(() { _isLoading = true; _error = null; });

    try {
      final offlineData = context.read<OfflineDataProvider>();
      final rows = await offlineData.getAll('categories');
      final categories = rows
          .map((row) => CategoryModel(
                id: row['id'] as int,
                code: (row['code'] as String?) ?? '',
                nameAr: (row['name_ar'] as String?) ?? '',
                nameEn: row['name_en'] as String?,
                parentId: row['parent_id'] as int?,
                isActive: (row['is_active'] as int?) == 1,
              ))
          .toList();

      if (mounted) {
        setState(() {
          _isLoading = false;
          _categories = categories;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _isLoading = false;
          _error = e.toString();
        });
      }
    }
  }

  List<CategoryModel> get _filteredCategories {
    final query = _searchController.text.toLowerCase();
    if (query.isEmpty) return _categories;
    return _categories.where((c) =>
        c.nameAr.toLowerCase().contains(query) ||
        c.code.toLowerCase().contains(query)
    ).toList();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('التصنيفات'),
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(56),
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
            child: TextField(
              controller: _searchController,
              decoration: InputDecoration(
                hintText: 'بحث عن تصنيف...',
                prefixIcon: const Icon(Icons.search),
                filled: true,
                fillColor: Colors.white,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(10),
                  borderSide: BorderSide.none,
                ),
                contentPadding: const EdgeInsets.symmetric(horizontal: 16),
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
                      Text(_error!),
                      const SizedBox(height: 16),
                      ElevatedButton(onPressed: _loadCategories, child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadCategories,
                  child: _filteredCategories.isEmpty
                      ? const Center(child: Text('لا توجد تصنيفات'))
                      : ListView.builder(
                          itemCount: _filteredCategories.length,
                          itemBuilder: (ctx, i) {
                            final c = _filteredCategories[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                              child: ListTile(
                                onTap: () async {
                                  await Navigator.of(context).push(
                                    MaterialPageRoute(
                                      builder: (_) => CategoryDetailScreen(
                                        category: {
                                          'id': c.id,
                                          'code': c.code,
                                          'name_ar': c.nameAr,
                                          'name_en': c.nameEn,
                                          'parent_id': c.parentId,
                                          'is_active': c.isActive ? 1 : 0,
                                        },
                                      ),
                                    ),
                                  );
                                  _loadCategories();
                                },
                                leading: Container(
                                  width: 40,
                                  height: 40,
                                  decoration: BoxDecoration(
                                    color: Colors.teal.shade100,
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: Icon(Icons.category, color: Colors.teal.shade700),
                                ),
                                title: Text(c.nameAr, style: const TextStyle(fontWeight: FontWeight.bold)),
                                subtitle: Row(
                                  children: [
                                    Text(c.code, style: const TextStyle(fontSize: 12)),
                                    if (c.parentNameAr != null) ...[
                                      const Text(' • ', style: TextStyle(fontSize: 12)),
                                      Expanded(
                                        child: Text(
                                          'التصنيف الأب: ${c.parentNameAr}',
                                          style: const TextStyle(fontSize: 12),
                                          overflow: TextOverflow.ellipsis,
                                        ),
                                      ),
                                    ],
                                  ],
                                ),
                                trailing: Icon(
                                  Icons.circle,
                                  size: 12,
                                  color: c.isActive ? Colors.green : Colors.grey,
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
              builder: (_) => const CategoryDetailScreen(),
            ),
          );
          _loadCategories();
        },
        child: const Icon(Icons.add),
      ),
    );
  }
}
