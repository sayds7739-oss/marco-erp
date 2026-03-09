class ProductModel {
  final int id;
  final String code;
  final String nameAr;
  final String? nameEn;
  final String? categoryNameAr;
  final int categoryId;
  final int baseUnitId;
  final String? baseUnitNameAr;
  final double wholesalePrice;
  final double retailPrice;
  final double weightedAverageCost;
  final bool isActive;
  final String? barcode;
  final String? description;

  ProductModel({
    required this.id,
    required this.code,
    required this.nameAr,
    this.nameEn,
    this.categoryNameAr,
    required this.categoryId,
    required this.baseUnitId,
    this.baseUnitNameAr,
    required this.wholesalePrice,
    required this.retailPrice,
    required this.weightedAverageCost,
    required this.isActive,
    this.barcode,
    this.description,
  });

  factory ProductModel.fromJson(Map<String, dynamic> json) {
    return ProductModel(
      id: json['id'] as int,
      code: json['code'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
      nameEn: json['nameEn'] as String?,
      categoryNameAr: json['categoryNameAr'] as String?,
      categoryId: json['categoryId'] as int? ?? 0,
      baseUnitId: json['baseUnitId'] as int? ?? 0,
      baseUnitNameAr: json['baseUnitNameAr'] as String?,
      wholesalePrice: _toDouble(json['wholesalePrice']),
      retailPrice: _toDouble(json['retailPrice']),
      weightedAverageCost: _toDouble(json['weightedAverageCost']),
      isActive: json['isActive'] as bool? ?? true,
      barcode: json['barcode'] as String?,
      description: json['description'] as String?,
    );
  }

  static double _toDouble(dynamic value) {
    if (value == null) return 0.0;
    if (value is double) return value;
    if (value is int) return value.toDouble();
    return double.tryParse(value.toString()) ?? 0.0;
  }
}
