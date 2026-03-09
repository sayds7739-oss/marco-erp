import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/api/api_client.dart';
import '../../../core/constants/api_constants.dart';

class SalesRepresentativeModel {
  final int id;
  final String code;
  final String nameAr;
  final String? nameEn;
  final String? phone;
  final String? email;
  final double commissionRate;
  final bool isActive;

  SalesRepresentativeModel({
    required this.id,
    required this.code,
    required this.nameAr,
    this.nameEn,
    this.phone,
    this.email,
    required this.commissionRate,
    required this.isActive,
  });

  factory SalesRepresentativeModel.fromJson(Map<String, dynamic> json) {
    return SalesRepresentativeModel(
      id: json['id'] as int,
      code: json['code'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
      nameEn: json['nameEn'] as String?,
      phone: json['phone'] as String?,
      email: json['email'] as String?,
      commissionRate: _toDouble(json['commissionRate']),
      isActive: json['isActive'] as bool? ?? true,
    );
  }

  static double _toDouble(dynamic value) {
    if (value == null) return 0.0;
    if (value is double) return value;
    if (value is int) return value.toDouble();
    return double.tryParse(value.toString()) ?? 0.0;
  }

  Map<String, dynamic> toJson() => {
        'code': code,
        'nameAr': nameAr,
        'nameEn': nameEn,
        'phone': phone,
        'email': email,
        'commissionRate': commissionRate,
        'isActive': isActive,
      };
}

class SalesRepresentativesScreen extends StatefulWidget {
  const SalesRepresentativesScreen({super.key});

  @override
  State<SalesRepresentativesScreen> createState() =>
      _SalesRepresentativesScreenState();
}

class _SalesRepresentativesScreenState
    extends State<SalesRepresentativesScreen> {
  List<SalesRepresentativeModel> _reps = [];
  bool _isLoading = true;
  String? _error;
  final _searchController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _loadReps();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadReps() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    final api = context.read<ApiClient>();
    final response = await api.get<List<SalesRepresentativeModel>>(
      ApiConstants.salesRepresentatives,
      fromJson: (json) => (json as List)
          .map((e) =>
              SalesRepresentativeModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

    if (mounted) {
      setState(() {
        _isLoading = false;
        if (response.success && response.data != null) {
          _reps = response.data!;
        } else {
          _error = response.errorMessage;
        }
      });
    }
  }

  List<SalesRepresentativeModel> get _filteredReps {
    final query = _searchController.text.toLowerCase();
    if (query.isEmpty) return _reps;
    return _reps
        .where((r) =>
            r.nameAr.toLowerCase().contains(query) ||
            r.code.toLowerCase().contains(query) ||
            (r.phone?.contains(query) ?? false) ||
            (r.email?.toLowerCase().contains(query) ?? false))
        .toList();
  }

  void _showRepDialog({SalesRepresentativeModel? rep}) {
    final isEdit = rep != null;
    final codeCtrl = TextEditingController(text: rep?.code ?? '');
    final nameArCtrl = TextEditingController(text: rep?.nameAr ?? '');
    final nameEnCtrl = TextEditingController(text: rep?.nameEn ?? '');
    final phoneCtrl = TextEditingController(text: rep?.phone ?? '');
    final emailCtrl = TextEditingController(text: rep?.email ?? '');
    final commissionCtrl =
        TextEditingController(text: rep?.commissionRate.toString() ?? '0');
    bool isActive = rep?.isActive ?? true;
    final formKey = GlobalKey<FormState>();
    bool isSaving = false;

    showDialog(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          title: Text(isEdit ? 'تعديل مندوب المبيعات' : 'إضافة مندوب مبيعات'),
          content: SingleChildScrollView(
            child: Form(
              key: formKey,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  TextFormField(
                    controller: codeCtrl,
                    decoration: const InputDecoration(labelText: 'الكود *'),
                    validator: (v) =>
                        (v == null || v.isEmpty) ? 'مطلوب' : null,
                  ),
                  const SizedBox(height: 8),
                  TextFormField(
                    controller: nameArCtrl,
                    decoration:
                        const InputDecoration(labelText: 'الاسم بالعربية *'),
                    validator: (v) =>
                        (v == null || v.isEmpty) ? 'مطلوب' : null,
                  ),
                  const SizedBox(height: 8),
                  TextFormField(
                    controller: nameEnCtrl,
                    decoration: const InputDecoration(
                        labelText: 'الاسم بالإنجليزية'),
                    textDirection: TextDirection.ltr,
                  ),
                  const SizedBox(height: 8),
                  TextFormField(
                    controller: phoneCtrl,
                    decoration: const InputDecoration(labelText: 'الهاتف'),
                    keyboardType: TextInputType.phone,
                    textDirection: TextDirection.ltr,
                  ),
                  const SizedBox(height: 8),
                  TextFormField(
                    controller: emailCtrl,
                    decoration:
                        const InputDecoration(labelText: 'البريد الإلكتروني'),
                    keyboardType: TextInputType.emailAddress,
                    textDirection: TextDirection.ltr,
                  ),
                  const SizedBox(height: 8),
                  TextFormField(
                    controller: commissionCtrl,
                    decoration:
                        const InputDecoration(labelText: 'نسبة العمولة %'),
                    keyboardType:
                        const TextInputType.numberWithOptions(decimal: true),
                    textDirection: TextDirection.ltr,
                  ),
                  const SizedBox(height: 8),
                  SwitchListTile(
                    title: const Text('نشط'),
                    value: isActive,
                    onChanged: (v) => setDialogState(() => isActive = v),
                    contentPadding: EdgeInsets.zero,
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('إلغاء'),
            ),
            TextButton(
              onPressed: isSaving
                  ? null
                  : () async {
                      if (!formKey.currentState!.validate()) return;
                      setDialogState(() => isSaving = true);

                      final data = {
                        'code': codeCtrl.text,
                        'nameAr': nameArCtrl.text,
                        'nameEn': nameEnCtrl.text.isEmpty
                            ? null
                            : nameEnCtrl.text,
                        'phone':
                            phoneCtrl.text.isEmpty ? null : phoneCtrl.text,
                        'email':
                            emailCtrl.text.isEmpty ? null : emailCtrl.text,
                        'commissionRate':
                            double.tryParse(commissionCtrl.text) ?? 0,
                        'isActive': isActive,
                      };

                      final api = context.read<ApiClient>();
                      final response = isEdit
                          ? await api.put(
                              '${ApiConstants.salesRepresentatives}/${rep.id}',
                              data: data)
                          : await api.post(
                              ApiConstants.salesRepresentatives,
                              data: data);

                      if (ctx.mounted) {
                        Navigator.pop(ctx);
                        if (response.success) {
                          _loadReps();
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                                content: Text(isEdit
                                    ? 'تم تعديل المندوب بنجاح'
                                    : 'تم إضافة المندوب بنجاح')),
                          );
                        } else {
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                                content:
                                    Text(response.errorMessage)),
                          );
                        }
                      }
                    },
              child: isSaving
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2))
                  : Text(isEdit ? 'تعديل' : 'إضافة'),
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
        title: const Text('مندوبي المبيعات'),
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(56),
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
            child: TextField(
              controller: _searchController,
              decoration: InputDecoration(
                hintText: 'بحث عن مندوب...',
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
      floatingActionButton: FloatingActionButton(
        onPressed: () => _showRepDialog(),
        child: const Icon(Icons.add),
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
                          style: const TextStyle(color: Colors.grey)),
                      const SizedBox(height: 16),
                      ElevatedButton(
                          onPressed: _loadReps,
                          child: const Text('إعادة المحاولة')),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadReps,
                  child: _filteredReps.isEmpty
                      ? const Center(child: Text('لا يوجد مندوبين'))
                      : ListView.builder(
                          itemCount: _filteredReps.length,
                          itemBuilder: (ctx, i) {
                            final r = _filteredReps[i];
                            return Card(
                              margin: const EdgeInsets.symmetric(
                                  horizontal: 12, vertical: 4),
                              child: ListTile(
                                leading: CircleAvatar(
                                  backgroundColor: Colors.indigo.shade100,
                                  child: Text(
                                    r.nameAr.isNotEmpty ? r.nameAr[0] : '?',
                                    style: TextStyle(
                                        color: Colors.indigo.shade700),
                                  ),
                                ),
                                title: Text(r.nameAr,
                                    style: const TextStyle(
                                        fontWeight: FontWeight.bold)),
                                subtitle: Column(
                                  crossAxisAlignment:
                                      CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                        '${r.code} ${r.phone != null ? "• ${r.phone}" : ""}',
                                        style:
                                            const TextStyle(fontSize: 12)),
                                    Text(
                                        'العمولة: ${r.commissionRate.toStringAsFixed(1)}%',
                                        style:
                                            const TextStyle(fontSize: 12)),
                                  ],
                                ),
                                trailing: Icon(
                                  Icons.circle,
                                  size: 12,
                                  color: r.isActive
                                      ? Colors.green
                                      : Colors.grey,
                                ),
                                onTap: () => _showRepDialog(rep: r),
                              ),
                            );
                          },
                        ),
                ),
    );
  }
}
