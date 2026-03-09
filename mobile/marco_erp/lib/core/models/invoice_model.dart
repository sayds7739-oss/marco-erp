class InvoiceListModel {
  final int id;
  final String invoiceNumber;
  final DateTime invoiceDate;
  final String? counterpartyNameAr;
  final double netTotal;
  final double vatTotal;
  final double grandTotal;
  final String status;

  InvoiceListModel({
    required this.id,
    required this.invoiceNumber,
    required this.invoiceDate,
    this.counterpartyNameAr,
    required this.netTotal,
    required this.vatTotal,
    required this.grandTotal,
    required this.status,
  });

  factory InvoiceListModel.fromJson(Map<String, dynamic> json) {
    return InvoiceListModel(
      id: json['id'] as int,
      invoiceNumber: json['invoiceNumber'] as String? ?? '',
      invoiceDate: DateTime.parse(json['invoiceDate'] as String),
      counterpartyNameAr: json['customerNameAr'] as String? ??
          json['supplierNameAr'] as String?,
      netTotal: _toDouble(json['netTotal']),
      vatTotal: _toDouble(json['vatTotal']),
      grandTotal: _toDouble(json['grandTotal']),
      status: json['status'] as String? ?? '',
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
      case 'draft': return 'مسودة';
      case 'posted': return 'مرحّلة';
      case 'cancelled': return 'ملغاة';
      default: return status;
    }
  }

  bool get isDraft => status.toLowerCase() == 'draft';
  bool get isPosted => status.toLowerCase() == 'posted';
}

class CustomerModel {
  final int id;
  final String code;
  final String nameAr;
  final String? nameEn;
  final String? phone;
  final String? email;
  final String? address;
  final bool isActive;

  CustomerModel({
    required this.id,
    required this.code,
    required this.nameAr,
    this.nameEn,
    this.phone,
    this.email,
    this.address,
    required this.isActive,
  });

  factory CustomerModel.fromJson(Map<String, dynamic> json) {
    return CustomerModel(
      id: json['id'] as int,
      code: json['code'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
      nameEn: json['nameEn'] as String?,
      phone: json['phone'] as String?,
      email: json['email'] as String?,
      address: json['address'] as String?,
      isActive: json['isActive'] as bool? ?? true,
    );
  }
}

class SupplierModel {
  final int id;
  final String code;
  final String nameAr;
  final String? phone;
  final String? email;
  final bool isActive;

  SupplierModel({
    required this.id,
    required this.code,
    required this.nameAr,
    this.phone,
    this.email,
    required this.isActive,
  });

  factory SupplierModel.fromJson(Map<String, dynamic> json) {
    return SupplierModel(
      id: json['id'] as int,
      code: json['code'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
      phone: json['phone'] as String?,
      email: json['email'] as String?,
      isActive: json['isActive'] as bool? ?? true,
    );
  }
}
