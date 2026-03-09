class DashboardModel {
  final double totalSalesToday;
  final double totalPurchasesToday;
  final double totalCashBalance;
  final double totalBankBalance;
  final int pendingInvoices;
  final double dailyNetProfit;
  final int lowStockProducts;
  final int totalCustomers;
  final int totalSuppliers;
  final int totalProducts;

  DashboardModel({
    required this.totalSalesToday,
    required this.totalPurchasesToday,
    required this.totalCashBalance,
    required this.totalBankBalance,
    required this.pendingInvoices,
    required this.dailyNetProfit,
    required this.lowStockProducts,
    required this.totalCustomers,
    required this.totalSuppliers,
    required this.totalProducts,
  });

  factory DashboardModel.fromJson(Map<String, dynamic> json) {
    return DashboardModel(
      totalSalesToday: _d(json['totalSalesToday']),
      totalPurchasesToday: _d(json['totalPurchasesToday']),
      totalCashBalance: _d(json['totalCashBalance']),
      totalBankBalance: _d(json['totalBankBalance']),
      pendingInvoices: json['pendingInvoices'] as int? ?? 0,
      dailyNetProfit: _d(json['dailyNetProfit']),
      lowStockProducts: json['lowStockProducts'] as int? ?? 0,
      totalCustomers: json['totalCustomers'] as int? ?? 0,
      totalSuppliers: json['totalSuppliers'] as int? ?? 0,
      totalProducts: json['totalProducts'] as int? ?? 0,
    );
  }

  static double _d(dynamic v) {
    if (v == null) return 0;
    if (v is double) return v;
    if (v is int) return v.toDouble();
    return double.tryParse(v.toString()) ?? 0;
  }
}
