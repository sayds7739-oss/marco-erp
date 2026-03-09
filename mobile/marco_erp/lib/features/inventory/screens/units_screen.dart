import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';
import '../../../core/providers/offline_data_provider.dart';
import 'unit_detail_screen.dart';

class UnitModel {
  final int id;
  final String code;
  final String nameAr;
  final String? nameEn;
  final bool isActive;

  UnitModel({
    required this.id,
    required this.code,
    required this.nameAr,
    this.nameEn,
    required this.isActive,
  });

  factory UnitModel.fromJson(Map<String, dynamic> json) {
    return UnitModel(
      id: json['id'] as int,
      code: json['code'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
      nameEn: json['nameEn'] as String?,
      isActive: json['isActive'] as bool? ?? true,
    );
  }
}

class UnitsScreen extends StatefulWidget {
  const UnitsScreen({super.key});

  @override
  State<UnitsScreen> createState() => _UnitsScreenState();
}

class _UnitsScreenState extends State<UnitsScreen> {
  List<UnitModel> _units = [];
  bool _isLoading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadUnits();
  }

  Future<void> _loadUnits() async {
    setState(() { _isLoading = true; _error = null; });

    try {
      final offlineData = context.read<OfflineDataProvider>();
      final rows = await offlineData.getAll('units');
      final units = rows
          .map((row) => UnitModel(
                id: row['id'] as int,
                code: (row['code'] as String?) ?? '',
                nameAr: (row['name_ar'] as String?) ?? '',
                nameEn: row['name_en'] as String?,
                isActive: (row['is_active'] as int?) == 1,
              ))
          .toList();

      if (mounted) {
        setState(() {
          _isLoading = false;
          _units = units;
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

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('الوحدات')),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(_error!),
                      const SizedBox(height: 16),
                      ElevatedButton(onPressed: _loadUnits, child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadUnits,
                  child: _units.isEmpty
                      ? const Center(child: Text('لا توجد وحدات'))
                      : ListView.builder(
                          itemCount: _units.length,
                          itemBuilder: (ctx, i) {
                            final u = _units[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                              child: ListTile(
                                onTap: () async {
                                  await Navigator.of(context).push(
                                    MaterialPageRoute(
                                      builder: (_) => UnitDetailScreen(
                                        unit: {
                                          'id': u.id,
                                          'code': u.code,
                                          'name_ar': u.nameAr,
                                          'name_en': u.nameEn,
                                          'is_active': u.isActive ? 1 : 0,
                                        },
                                      ),
                                    ),
                                  );
                                  _loadUnits();
                                },
                                leading: Container(
                                  width: 40,
                                  height: 40,
                                  decoration: BoxDecoration(
                                    color: Colors.deepPurple.shade100,
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: Icon(Icons.straighten, color: Colors.deepPurple.shade700),
                                ),
                                title: Text(u.nameAr, style: const TextStyle(fontWeight: FontWeight.bold)),
                                subtitle: Row(
                                  children: [
                                    Text(u.code, style: const TextStyle(fontSize: 12)),
                                    if (u.nameEn != null) ...[
                                      const Text(' • ', style: TextStyle(fontSize: 12)),
                                      Text(u.nameEn!, style: const TextStyle(fontSize: 12)),
                                    ],
                                  ],
                                ),
                                trailing: Icon(
                                  Icons.circle,
                                  size: 12,
                                  color: u.isActive ? Colors.green : Colors.grey,
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
              builder: (_) => const UnitDetailScreen(),
            ),
          );
          _loadUnits();
        },
        child: const Icon(Icons.add),
      ),
    );
  }
}
