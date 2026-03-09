class CashboxModel {
  final int id;
  final String code;
  final String nameAr;
  final double currentBalance;
  final bool isDefault;
  final bool isActive;

  CashboxModel({
    required this.id,
    required this.code,
    required this.nameAr,
    required this.currentBalance,
    required this.isDefault,
    required this.isActive,
  });

  factory CashboxModel.fromJson(Map<String, dynamic> json) {
    return CashboxModel(
      id: json['id'] as int,
      code: json['code'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
      currentBalance: _d(json['currentBalance']),
      isDefault: json['isDefault'] as bool? ?? false,
      isActive: json['isActive'] as bool? ?? true,
    );
  }

  static double _d(dynamic v) {
    if (v == null) return 0;
    if (v is double) return v;
    if (v is int) return v.toDouble();
    return double.tryParse(v.toString()) ?? 0;
  }
}

class CashReceiptListModel {
  final int id;
  final String receiptNumber;
  final DateTime receiptDate;
  final double amount;
  final String? customerNameAr;
  final String? cashboxNameAr;
  final String status;

  CashReceiptListModel({
    required this.id,
    required this.receiptNumber,
    required this.receiptDate,
    required this.amount,
    this.customerNameAr,
    this.cashboxNameAr,
    required this.status,
  });

  factory CashReceiptListModel.fromJson(Map<String, dynamic> json) {
    return CashReceiptListModel(
      id: json['id'] as int,
      receiptNumber: json['receiptNumber'] as String? ?? '',
      receiptDate: DateTime.parse(json['receiptDate'] as String),
      amount: _d(json['amount']),
      customerNameAr: json['customerNameAr'] as String?,
      cashboxNameAr: json['cashboxNameAr'] as String?,
      status: json['status'] as String? ?? '',
    );
  }

  static double _d(dynamic v) {
    if (v == null) return 0;
    if (v is double) return v;
    if (v is int) return v.toDouble();
    return double.tryParse(v.toString()) ?? 0;
  }
}
