using System;

namespace MarcoERP.Application.DTOs.Reports
{
    /// <summary>
    /// DTO for the main Dashboard summary data.
    /// Provides today's snapshot + period totals.
    /// </summary>
    public sealed class DashboardSummaryDto
    {
        // ── Today's Figures ──
        public decimal TodaySales { get; set; }
        public decimal TodayPurchases { get; set; }
        public decimal TodayReceipts { get; set; }
        public decimal TodayPayments { get; set; }
        public int TodaySalesCount { get; set; }
        public int TodayPurchasesCount { get; set; }
        public decimal DailyNetProfit { get; set; }

        // ── Daily deltas vs yesterday ──
        public decimal TodaySalesDelta { get; set; }
        public decimal TodayPurchasesDelta { get; set; }
        public decimal TodayReceiptsDelta { get; set; }
        public decimal TodayPaymentsDelta { get; set; }

        // ── Period Totals (current month) ──
        public decimal MonthSales { get; set; }
        public decimal MonthPurchases { get; set; }
        public decimal MonthReceipts { get; set; }
        public decimal MonthPayments { get; set; }
        public decimal GrossMarginPercent { get; set; }

        // ── Month deltas vs last month ──
        public decimal MonthSalesDelta { get; set; }
        public decimal MonthPurchasesDelta { get; set; }
        public decimal MonthReceiptsDelta { get; set; }
        public decimal MonthPaymentsDelta { get; set; }

        // ── Overall Balances ──
        public decimal TotalCustomerBalance { get; set; }
        public decimal TotalSupplierBalance { get; set; }
        public decimal CashBalance { get; set; }
        public decimal MonthGrossProfit { get; set; }

        // ── Inventory Alerts ──
        public int LowStockCount { get; set; }
        public int TotalProducts { get; set; }

        // ── Drafts Pending ──
        public int PendingSalesInvoices { get; set; }
        public int PendingPurchaseInvoices { get; set; }
        public int PendingJournalEntries { get; set; }
    }

    /// <summary>Daily sales/purchase trend data point for charts.</summary>
    public sealed class DailyTrendPoint
    {
        public DateTime Date { get; set; }
        public string Label => Date.ToString("MM/dd");
        public decimal SalesTotal { get; set; }
        public decimal PurchaseTotal { get; set; }
    }

    /// <summary>Top product by sales amount for bar chart.</summary>
    public sealed class TopProductDto
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
    }
}
