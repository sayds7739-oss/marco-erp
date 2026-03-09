using System;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Exceptions.Accounting;

namespace MarcoERP.Domain.Entities.Accounting
{
    /// <summary>
    /// Represents one line (سطر) in a journal entry.
    /// Each line targets exactly one account and has either a debit or credit (never both).
    /// Immutable financial record — cannot be deleted (RECORD_PROTECTION_POLICY).
    /// </summary>
    public sealed class JournalEntryLine : BaseEntity, IImmutableFinancialRecord
    {
        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private JournalEntryLine() { }

        private JournalEntryLine(
            int accountId,
            int lineNumber,
            decimal debitAmount,
            decimal creditAmount,
            string description,
            int? costCenterId,
            int? warehouseId,
            DateTime createdAt)
        {
            AccountId = accountId;
            LineNumber = lineNumber;
            DebitAmount = debitAmount;
            CreditAmount = creditAmount;
            Description = description ?? string.Empty;
            CostCenterId = costCenterId;
            WarehouseId = warehouseId;
            CreatedAt = createdAt;
        }

        // ── Properties ──────────────────────────────────────────

        /// <summary>FK to parent JournalEntry.</summary>
        public int JournalEntryId { get; private set; }

        /// <summary>Sequential line number within the entry (1, 2, 3, …).</summary>
        public int LineNumber { get; private set; }

        /// <summary>FK to Account (must be active leaf with AllowPosting).</summary>
        public int AccountId { get; private set; }

        /// <summary>Debit amount (0.00 if credit line). Non-negative, max 2 decimals.</summary>
        public decimal DebitAmount { get; private set; }

        /// <summary>Credit amount (0.00 if debit line). Non-negative, max 2 decimals.</summary>
        public decimal CreditAmount { get; private set; }

        /// <summary>Line-level narrative.</summary>
        public string Description { get; private set; }

        /// <summary>Optional cost center (overrides header cost center).</summary>
        public int? CostCenterId { get; private set; }

        /// <summary>Warehouse reference (for inventory-related entries).</summary>
        public int? WarehouseId { get; private set; }

        /// <summary>UTC timestamp of creation.</summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>Username of creator.</summary>
        public string CreatedBy { get; private set; }

        // ── Factory Method ──────────────────────────────────────

        /// <summary>
        /// Creates a validated journal entry line.
        /// Enforces: no negative amounts, not both sides, not zero on both sides, 2-decimal precision.
        /// </summary>
        public static JournalEntryLine Create(
            int accountId,
            int lineNumber,
            decimal debitAmount,
            decimal creditAmount,
            string description = null,
            int? costCenterId = null,
            int? warehouseId = null,
            DateTime createdAt = default)
        {
            if (accountId <= 0)
                throw new JournalEntryDomainException("معرّف الحساب مطلوب لسطر القيد.");

            if (lineNumber < 1)
                throw new JournalEntryDomainException("رقم السطر يجب أن يكون 1 أو أكثر.");

            if (createdAt == default)
                throw new JournalEntryDomainException("تاريخ إنشاء السطر مطلوب.");

            EnsureMoneyPrecision(debitAmount, "المدين");
            EnsureMoneyPrecision(creditAmount, "الدائن");

            // JE-INV-05: No negative amounts
            if (debitAmount < 0 || creditAmount < 0)
                throw new JournalEntryDomainException("المبالغ السالبة غير مسموحة في سطور القيد.");

            // JE-INV-03: Not both sides
            if (debitAmount > 0 && creditAmount > 0)
                throw new JournalEntryDomainException("السطر يجب أن يكون إما مدين أو دائن — لا يمكن الاثنين معاً.");

            // JE-INV-04: Not both zero
            if (debitAmount == 0 && creditAmount == 0)
                throw new JournalEntryDomainException("لا يمكن أن يكون المدين والدائن صفر في نفس السطر.");

            return new JournalEntryLine(accountId, lineNumber, debitAmount, creditAmount,
                description?.Trim(), costCenterId, warehouseId, createdAt);
        }

        // ── Mutation Methods ────────────────────────────────────

        /// <summary>
        /// Updates the amounts on this line (draft only — enforced by parent).
        /// </summary>
        public void UpdateAmount(decimal debitAmount, decimal creditAmount)
        {
            EnsureMoneyPrecision(debitAmount, "المدين");
            EnsureMoneyPrecision(creditAmount, "الدائن");

            if (debitAmount < 0 || creditAmount < 0)
                throw new JournalEntryDomainException("المبالغ السالبة غير مسموحة.");

            if (debitAmount > 0 && creditAmount > 0)
                throw new JournalEntryDomainException("السطر يجب أن يكون إما مدين أو دائن.");

            if (debitAmount == 0 && creditAmount == 0)
                throw new JournalEntryDomainException("لا يمكن أن يكون المدين والدائن صفر.");

            DebitAmount = debitAmount;
            CreditAmount = creditAmount;
        }

        /// <summary>
        /// Updates the description text.
        /// </summary>
        public void UpdateDescription(string description)
        {
            Description = description?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Updates the line number (used for re-sequencing after line removal).
        /// </summary>
        internal void SetLineNumber(int lineNumber)
        {
            LineNumber = lineNumber;
        }

        // ── Validation Helpers ──────────────────────────────────

        /// <summary>
        /// Ensures that the monetary value has at most 4 decimal places.
        /// Allows higher precision from invoice line calculations (4dp)
        /// while still preventing arbitrary decimal explosion.
        /// </summary>
        private static void EnsureMoneyPrecision(decimal value, string fieldName)
        {
            if (GetDecimalPlaces(value) > 4)
                throw new JournalEntryDomainException($"{fieldName}: المبالغ المالية يجب ألا تتجاوز أربع منازل عشرية.");
        }

        private static int GetDecimalPlaces(decimal value)
        {
            var bits = decimal.GetBits(value);
            return (bits[3] >> 16) & 0x7F;
        }
    }
}
