import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';
import '../../../core/models/product_model.dart';
import '../../../core/providers/offline_data_provider.dart';
import '../../../widgets/sync_status_widget.dart';
import 'product_detail_screen.dart';

class ProductsScreen extends StatefulWidget {
  const ProductsScreen({super.key});

  @override
  State<ProductsScreen> createState() => _ProductsScreenState();
}

class _ProductsScreenState extends State<ProductsScreen> {
  List<ProductModel> _products = [];
  List<ProductModel> _filtered = [];
  bool _isLoading = true;
  String? _error;
  final _searchController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _loadProducts();
    _searchController.addListener(_filterProducts);
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadProducts() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    try {
      final offlineData = context.read<OfflineDataProvider>();
      final rows = await offlineData.getAll('products');
      final products = rows
          .map((row) => ProductModel(
                id: row['id'] as int,
                code: (row['code'] as String?) ?? '',
                nameAr: (row['name_ar'] as String?) ?? '',
                nameEn: row['name_en'] as String?,
                categoryId: (row['category_id'] as int?) ?? 0,
                baseUnitId: (row['base_unit_id'] as int?) ?? 0,
                wholesalePrice:
                    (row['wholesale_price'] as num?)?.toDouble() ?? 0,
                retailPrice: (row['retail_price'] as num?)?.toDouble() ?? 0,
                weightedAverageCost:
                    (row['weighted_average_cost'] as num?)?.toDouble() ?? 0,
                isActive: (row['is_active'] as int?) == 1,
                barcode: row['barcode'] as String?,
                description: row['description'] as String?,
              ))
          .toList();

      if (mounted) {
        setState(() {
          _isLoading = false;
          _products = products;
          _filterProducts();
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

  void _filterProducts() {
    final query = _searchController.text.toLowerCase();
    setState(() {
      if (query.isEmpty) {
        _filtered = _products;
      } else {
        _filtered = _products
            .where((p) =>
                p.nameAr.toLowerCase().contains(query) ||
                p.code.toLowerCase().contains(query) ||
                (p.barcode?.toLowerCase().contains(query) ?? false))
            .toList();
      }
    });
  }

  Future<void> _navigateToDetail(ProductModel? product) async {
    final result = await Navigator.of(context).push<bool>(
      MaterialPageRoute(builder: (_) => ProductDetailScreen(product: product)),
    );
    if (result == true) {
      _loadProducts();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('الأصناف'),
        actions: const [SyncStatusWidget()],
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(56),
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
            child: TextField(
              controller: _searchController,
              decoration: InputDecoration(
                hintText: 'بحث بالاسم أو الكود أو الباركود...',
                prefixIcon: const Icon(Icons.search, color: Colors.white70),
                hintStyle: const TextStyle(color: Colors.white70),
                filled: true,
                fillColor: Colors.white24,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide: BorderSide.none,
                ),
                contentPadding: const EdgeInsets.symmetric(horizontal: 16),
              ),
              style: const TextStyle(color: Colors.white),
            ),
          ),
        ),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => _navigateToDetail(null),
        child: const Icon(Icons.add),
      ),
      body: Column(
        children: [
          const OfflineBanner(),
          Expanded(
            child: _isLoading
                ? const Center(child: CircularProgressIndicator())
                : _error != null
                    ? _buildError()
                    : RefreshIndicator(
                        onRefresh: _loadProducts,
                        child: _filtered.isEmpty
                            ? const Center(child: Text('لا توجد أصناف'))
                            : ListView.builder(
                                itemCount: _filtered.length,
                                itemBuilder: (ctx, i) =>
                                    _productTile(_filtered[i]),
                              ),
                      ),
          ),
        ],
      ),
    );
  }

  Widget _productTile(ProductModel product) {
    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
      child: ListTile(
        leading: CircleAvatar(
          backgroundColor: product.isActive
              ? Theme.of(context).colorScheme.primary.withOpacity(0.1)
              : Colors.grey.shade200,
          child: Icon(
            Icons.inventory_2_outlined,
            color: product.isActive ? Theme.of(context).colorScheme.primary : Colors.grey,
          ),
        ),
        title: Text(product.nameAr),
        subtitle: Text(
          '${product.code} • ${product.categoryNameAr ?? ""}',
          style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
        ),
        trailing: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Text(
              '${product.retailPrice.toStringAsFixed(2)} ج.م',
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
            Text(
              'جملة: ${product.wholesalePrice.toStringAsFixed(2)}',
              style: TextStyle(fontSize: 11, color: Colors.grey.shade600),
            ),
          ],
        ),
        onTap: () => _navigateToDetail(product),
      ),
    );
  }

  Widget _buildError() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(Icons.error_outline, size: 64, color: Colors.grey),
          const SizedBox(height: 16),
          Text(_error ?? 'حدث خطأ'),
          const SizedBox(height: 16),
          ElevatedButton.icon(
            onPressed: _loadProducts,
            icon: const Icon(Icons.refresh),
            label: const Text('إعادة المحاولة'),
          ),
        ],
      ),
    );
  }
}
