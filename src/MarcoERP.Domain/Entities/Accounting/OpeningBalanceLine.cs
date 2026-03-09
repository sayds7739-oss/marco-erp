using System;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Accounting;

namespace MarcoERP.Domain.Entities.Accounting
{
    /// <summary>
    /// بند رصيد افتتاحي — يمثل رصيداً واحداً ضمن مستند الأرصدة الافتتاحية.
    /// يدعم 6 أنواع: حساب عام، عميل، مورد، مخزون، صندوق، بنك.
    /// سجل مالي غير قابل للحذف بعد الترحيل (IImmutableFinancialRecord).
    /// </summary>
    public sealed class OpeningBalanceLine : BaseEntity, IImmutableFinancialRecord
    {
        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private OpeningBalanceLine() { }

        /// <summary>
        /// Creates a new opening balance line.
        /// </summary>
        internal OpeningBalanceLine(
            OpeningBalanceLineType lineType,
            int accountId,
            decimal debitAmount,
            decimal creditAmount,
            int? customerId = null,
            int? supplierId = null,
            int? productId = null,
            int? warehouseId = null,
            int? cashboxId = null,
            int? bankAccountId = null,
            decimal quantity = 0,
            decimal unitCost = 0,
            string notes = null)
        {
            if (accountId <= 0)
                throw new OpeningBalanceDomainException("الحساب مطلوب.");

            LineType = lineType;
            AccountId = accountId;
            DebitAmount = Math.Round(debitAmount, 4);
            CreditAmount = Math.Round(creditAmount, 4);
            CustomerId = customerId;
            SupplierId = supplierId;
            ProductId = productId;
            WarehouseId = warehouseId;
            CashboxId = cashboxId;
            BankAccountId = bankAccountId;
            Quantity = Math.Round(quantity, 4);
            UnitCost = Math.Round(unitCost, 4);
            Notes = notes?.Trim();
        }

        // ── Properties ──────────────────────────────────────────

        /// <summary>FK to parent OpeningBalance document.</summary>
        public int OpeningBalanceId { get; private set; }

        /// <summary>نوع البند (حساب، عميل، مورد، مخزون، صندوق، بنك).</summary>
        public OpeningBalanceLineType LineType { get; private set; }

        /// <summary>
        /// FK to GL Account. For subsidiary lines, this is the control account:
        /// Customer → AR (1121), Supplier → AP (2111),
        /// Inventory → Inv (1131), Cashbox/Bank → their respective accounts.
        /// </summary>
        public int AccountId { get; private set; }

        /// <summary>المبلغ المدين (0 إذا كان البند دائناً).</summary>
        public decimal DebitAmount { get; private set; }

        /// <summary>المبلغ الدائن (0 إذا كان البند مديناً).</summary>
        public decimal CreditAmount { get; private set; }

        /// <summary>FK to Customer (only for LineType.Customer).</summary>
        public int? CustomerId { get; private set; }

        /// <summary>FK to Supplier (only for LineType.Supplier).</summary>
        public int? SupplierId { get; private set; }

        /// <summary>FK to Product (only for LineType.Inventory).</summary>
        public int? ProductId { get; private set; }

        /// <summary>FK to Warehouse (only for LineType.Inventory).</summary>
        public int? WarehouseId { get; private set; }

        /// <summary>FK to Cashbox (only for LineType.Cashbox).</summary>
        public int? CashboxId { get; private set; }

        /// <summary>FK to BankAccount (only for LineType.BankAccount).</summary>
        public int? BankAccountId { get; private set; }

        /// <summary>الكمية (فقط للمخزون).</summary>
        public decimal Quantity { get; private set; }

        /// <summary>تكلفة الوحدة (فقط للمخزون).</summary>
        public decimal UnitCost { get; private set; }

        /// <summary>ملاحظات اختيارية.</summary>
        public string Notes { get; private set; }

        // ── Helper Properties ───────────────────────────────────

        /// <summary>صافي المبلغ (مدين - دائن).</summary>
        public decimal NetAmount => DebitAmount - CreditAmount;
    }
}
