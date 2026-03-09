using System;
using System.Collections.Generic;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.DTOs.Accounting
{
    // ════════════════════════════════════════════════════════════
    //  Opening Balance DTOs — الأرصدة الافتتاحية
    // ════════════════════════════════════════════════════════════

    // ── Read DTOs ───────────────────────────────────────────────

    /// <summary>DTO for opening balance list view.</summary>
    public sealed class OpeningBalanceListDto
    {
        public int Id { get; set; }
        public int FiscalYearId { get; set; }
        public int FiscalYear { get; set; }
        public DateTime BalanceDate { get; set; }
        public OpeningBalanceStatus Status { get; set; }
        public string StatusText { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal Difference { get; set; }
        public int LineCount { get; set; }
        public string PostedBy { get; set; }
        public DateTime? PostedAt { get; set; }
    }

    /// <summary>DTO for opening balance detail view (with all lines).</summary>
    public sealed class OpeningBalanceDto
    {
        public int Id { get; set; }
        public int FiscalYearId { get; set; }
        public int FiscalYear { get; set; }
        public DateTime BalanceDate { get; set; }
        public OpeningBalanceStatus Status { get; set; }
        public string StatusText { get; set; }
        public int? JournalEntryId { get; set; }
        public string Notes { get; set; }
        public string PostedBy { get; set; }
        public DateTime? PostedAt { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal Difference { get; set; }
        public bool IsBalanced { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<OpeningBalanceLineDto> Lines { get; set; } = new();
    }

    /// <summary>DTO for a single opening balance line in detail view.</summary>
    public sealed class OpeningBalanceLineDto
    {
        public int Id { get; set; }
        public OpeningBalanceLineType LineType { get; set; }
        public string LineTypeText { get; set; }
        public int AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }

        // ── Subsidiary entity details ───────────────────────
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public int? SupplierId { get; set; }
        public string SupplierName { get; set; }
        public int? ProductId { get; set; }
        public string ProductName { get; set; }
        public int? WarehouseId { get; set; }
        public string WarehouseName { get; set; }
        public int? CashboxId { get; set; }
        public string CashboxName { get; set; }
        public int? BankAccountId { get; set; }
        public string BankAccountName { get; set; }

        // ── Inventory-specific ──────────────────────────────
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }

        public string Notes { get; set; }
    }

    // ── Write DTOs ──────────────────────────────────────────────

    /// <summary>DTO for creating a new opening balance document.</summary>
    public sealed class CreateOpeningBalanceDto
    {
        /// <summary>السنة المالية.</summary>
        public int FiscalYearId { get; set; }

        /// <summary>تاريخ الأرصدة الافتتاحية.</summary>
        public DateTime BalanceDate { get; set; }

        /// <summary>ملاحظات اختيارية.</summary>
        public string Notes { get; set; }

        /// <summary>البنود (يمكن إضافتها لاحقاً أيضاً).</summary>
        public List<CreateOpeningBalanceLineDto> Lines { get; set; } = new();
    }

    /// <summary>DTO for updating an opening balance (draft only).</summary>
    public sealed class UpdateOpeningBalanceDto
    {
        public int Id { get; set; }
        public DateTime BalanceDate { get; set; }
        public string Notes { get; set; }
        public List<CreateOpeningBalanceLineDto> Lines { get; set; } = new();
    }

    /// <summary>DTO for a single opening balance line in create/update requests.</summary>
    public sealed class CreateOpeningBalanceLineDto
    {
        /// <summary>نوع البند.</summary>
        public OpeningBalanceLineType LineType { get; set; }

        /// <summary>
        /// حساب دفتر الأستاذ.
        /// للبنود الفرعية (عملاء/موردين/مخزون/صناديق/بنوك)
        /// يتم تحديده تلقائياً من الكيان إذا لم يُحدد.
        /// </summary>
        public int? AccountId { get; set; }

        /// <summary>المبلغ المدين (للحسابات العامة).</summary>
        public decimal DebitAmount { get; set; }

        /// <summary>المبلغ الدائن (للحسابات العامة).</summary>
        public decimal CreditAmount { get; set; }

        /// <summary>المبلغ (للعملاء/الموردين/الصناديق/البنوك). موجب = الاتجاه الطبيعي.</summary>
        public decimal Amount { get; set; }

        // ── Subsidiary entity IDs ───────────────────────────
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }
        public int? ProductId { get; set; }
        public int? WarehouseId { get; set; }
        public int? CashboxId { get; set; }
        public int? BankAccountId { get; set; }

        // ── Inventory-specific ──────────────────────────────
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }

        public string Notes { get; set; }
    }
}
