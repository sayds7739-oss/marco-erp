using System;
using System.Collections.Generic;
using System.Linq;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Security;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Sales;

namespace MarcoERP.Domain.Entities.Sales
{
    /// <summary>
    /// Represents a POS cashier session (جلسة نقطة بيع).
    /// Lifecycle: Open → Closed. Tracks cash flow and reconciliation.
    /// </summary>
    public sealed class PosSession : CompanyAwareEntity
    {
        private readonly List<PosPayment> _payments = new();

        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private PosSession() { }

        /// <summary>
        /// Opens a new POS session.
        /// </summary>
        public PosSession(
            string sessionNumber,
            int userId,
            int cashboxId,
            int warehouseId,
            decimal openingBalance,
            DateTime openedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(sessionNumber))
                throw new SalesInvoiceDomainException("رقم الجلسة مطلوب.");
            if (userId <= 0)
                throw new SalesInvoiceDomainException("المستخدم مطلوب.");
            if (cashboxId <= 0)
                throw new SalesInvoiceDomainException("الخزنة مطلوبة.");
            if (warehouseId <= 0)
                throw new SalesInvoiceDomainException("المستودع مطلوب.");
            if (openingBalance < 0)
                throw new SalesInvoiceDomainException("الرصيد الافتتاحي لا يمكن أن يكون سالباً.");

            SessionNumber = sessionNumber.Trim();
            UserId = userId;
            CashboxId = cashboxId;
            WarehouseId = warehouseId;
            OpeningBalance = openingBalance;
            Status = PosSessionStatus.Open;
            OpenedAt = openedAtUtc;

            TotalSales = 0;
            TotalCashReceived = 0;
            TotalCardReceived = 0;
            TotalOnAccount = 0;
            TransactionCount = 0;
            ClosingBalance = 0;
            Variance = 0;
        }

        // ── Properties ──────────────────────────────────────────

        /// <summary>Unique session number (POS-YYYYMMDD-####).</summary>
        public string SessionNumber { get; private set; }

        /// <summary>FK to User (cashier).</summary>
        public int UserId { get; private set; }

        /// <summary>Navigation to User.</summary>
        public User User { get; private set; }

        /// <summary>FK to Cashbox.</summary>
        public int CashboxId { get; private set; }

        /// <summary>Navigation to Cashbox.</summary>
        public Cashbox Cashbox { get; private set; }

        /// <summary>FK to Warehouse this POS draws stock from.</summary>
        public int WarehouseId { get; private set; }

        /// <summary>Navigation to Warehouse.</summary>
        public Warehouse Warehouse { get; private set; }

        /// <summary>Cash in the drawer at session start.</summary>
        public decimal OpeningBalance { get; private set; }

        /// <summary>Sum of all invoice NetTotals in this session.</summary>
        public decimal TotalSales { get; private set; }

        /// <summary>Sum of cash payments received.</summary>
        public decimal TotalCashReceived { get; private set; }

        /// <summary>Sum of card payments received.</summary>
        public decimal TotalCardReceived { get; private set; }

        /// <summary>Sum of on-account (AR) charges.</summary>
        public decimal TotalOnAccount { get; private set; }

        /// <summary>Number of completed POS transactions.</summary>
        public int TransactionCount { get; private set; }

        /// <summary>Actual cash counted at session close.</summary>
        public decimal ClosingBalance { get; private set; }

        /// <summary>Variance = ClosingBalance - (OpeningBalance + TotalCashReceived).</summary>
        public decimal Variance { get; private set; }

        /// <summary>Session lifecycle status.</summary>
        public PosSessionStatus Status { get; private set; }

        /// <summary>UTC timestamp when session was opened.</summary>
        public DateTime OpenedAt { get; private set; }

        /// <summary>UTC timestamp when session was closed.</summary>
        public DateTime? ClosedAt { get; private set; }

        /// <summary>Optional closing notes.</summary>
        public string ClosingNotes { get; private set; }

        /// <summary>Payments made within this session.</summary>
        public IReadOnlyCollection<PosPayment> Payments => _payments.AsReadOnly();

        // ── Domain Methods ──────────────────────────────────────

        /// <summary>Returns true if session is open.</summary>
        public bool IsOpen => Status == PosSessionStatus.Open;

        /// <summary>
        /// Records a completed POS sale in the session totals.
        /// Called after each successful invoice posting.
        /// </summary>
        public void RecordSale(decimal invoiceNetTotal, decimal cashAmount, decimal cardAmount, decimal onAccountAmount)
        {
            EnsureOpen("لا يمكن تسجيل بيع في جلسة مغلقة.");

            if (invoiceNetTotal <= 0)
                throw new SalesInvoiceDomainException("مبلغ الفاتورة يجب أن يكون أكبر من صفر.");

            TotalSales += invoiceNetTotal;
            TotalCashReceived += cashAmount;
            TotalCardReceived += cardAmount;
            TotalOnAccount += onAccountAmount;
            TransactionCount++;
        }

        /// <summary>
        /// Reverses a sale from session totals (on cancellation).
        /// </summary>
        public void ReverseSale(decimal invoiceNetTotal, decimal cashAmount, decimal cardAmount, decimal onAccountAmount)
        {
            EnsureOpen("لا يمكن عكس بيع في جلسة مغلقة.");

            if (invoiceNetTotal <= 0)
                throw new SalesInvoiceDomainException("مبلغ الفاتورة يجب أن يكون أكبر من صفر.");
            if (TotalSales < invoiceNetTotal)
                throw new SalesInvoiceDomainException("لا يمكن عكس مبلغ أكبر من إجمالي المبيعات.");
            if (TransactionCount <= 0)
                throw new SalesInvoiceDomainException("لا توجد معاملات لعكسها.");

            TotalSales -= invoiceNetTotal;
            TotalCashReceived -= cashAmount;
            TotalCardReceived -= cardAmount;
            TotalOnAccount -= onAccountAmount;
            TransactionCount--;
        }

        /// <summary>
        /// Closes the session with a reconciliation count.
        /// </summary>
        public void Close(decimal actualClosingBalance, string notes, DateTime closedAtUtc)
        {
            EnsureOpen("الجلسة مغلقة بالفعل.");

            if (actualClosingBalance < 0)
                throw new SalesInvoiceDomainException("رصيد الإغلاق لا يمكن أن يكون سالباً.");

            ClosingBalance = actualClosingBalance;
            Variance = actualClosingBalance - (OpeningBalance + TotalCashReceived);
            ClosingNotes = notes?.Trim();
            Status = PosSessionStatus.Closed;
            ClosedAt = closedAtUtc;
        }

        // ── Private Helpers ─────────────────────────────────────

        private void EnsureOpen(string message)
        {
            if (Status != PosSessionStatus.Open)
                throw new SalesInvoiceDomainException(message);
        }
    }
}
