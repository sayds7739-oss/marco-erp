using System;
using System.Collections.Generic;
using System.Linq;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Accounting;

namespace MarcoERP.Domain.Entities.Accounting
{
    /// <summary>
    /// الأرصدة الافتتاحية — مستند يسجل أرصدة بداية الفترة المالية.
    /// يتضمن: حسابات عامة، عملاء، موردين، مخزون، صناديق، بنوك.
    /// القاعدة: مستند واحد فقط لكل سنة مالية.
    /// الدورة: مسودة → مرحّلة (أحادية الاتجاه — لا يمكن التراجع).
    /// عند الترحيل: ينشئ قيد يومية تلقائي بنوع SourceType.Opening.
    /// </summary>
    public sealed class OpeningBalance : AuditableEntity
    {
        private readonly List<OpeningBalanceLine> _lines = new();

        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private OpeningBalance() { }

        /// <summary>
        /// Creates a new opening balance document in Draft status.
        /// </summary>
        /// <param name="fiscalYearId">السنة المالية.</param>
        /// <param name="balanceDate">تاريخ الأرصدة الافتتاحية (عادة 01/01 من السنة المالية).</param>
        /// <param name="notes">ملاحظات اختيارية.</param>
        public OpeningBalance(int fiscalYearId, DateTime balanceDate, string notes = null)
        {
            if (fiscalYearId <= 0)
                throw new OpeningBalanceDomainException("السنة المالية مطلوبة.");

            FiscalYearId = fiscalYearId;
            BalanceDate = balanceDate;
            Notes = notes?.Trim();
            Status = OpeningBalanceStatus.Draft;
            TotalDebit = 0;
            TotalCredit = 0;
        }

        // ── Properties ──────────────────────────────────────────

        /// <summary>FK to the fiscal year these opening balances belong to.</summary>
        public int FiscalYearId { get; private set; }

        /// <summary>Navigation property.</summary>
        public FiscalYear FiscalYear { get; private set; }

        /// <summary>تاريخ الأرصدة الافتتاحية (يجب أن يكون ضمن السنة المالية).</summary>
        public DateTime BalanceDate { get; private set; }

        /// <summary>حالة المستند: مسودة أو مرحّلة.</summary>
        public OpeningBalanceStatus Status { get; private set; }

        /// <summary>FK to the auto-generated journal entry (set on posting).</summary>
        public int? JournalEntryId { get; private set; }

        /// <summary>ملاحظات اختيارية.</summary>
        public string Notes { get; private set; }

        /// <summary>من قام بالترحيل.</summary>
        public string PostedBy { get; private set; }

        /// <summary>تاريخ الترحيل.</summary>
        public DateTime? PostedAt { get; private set; }

        /// <summary>إجمالي المدين — محسوب من البنود.</summary>
        public decimal TotalDebit { get; private set; }

        /// <summary>إجمالي الدائن — محسوب من البنود.</summary>
        public decimal TotalCredit { get; private set; }

        /// <summary>بنود الأرصدة الافتتاحية.</summary>
        public IReadOnlyCollection<OpeningBalanceLine> Lines => _lines.AsReadOnly();

        // ── Domain Methods ──────────────────────────────────────

        /// <summary>
        /// Adds a generic GL account opening balance line.
        /// Only balance sheet accounts should be used (validated in service).
        /// </summary>
        public OpeningBalanceLine AddAccountLine(
            int accountId,
            decimal debitAmount,
            decimal creditAmount,
            string notes = null)
        {
            EnsureDraft("لا يمكن إضافة بنود لأرصدة افتتاحية مرحّلة.");
            ValidateAmounts(debitAmount, creditAmount);

            var line = new OpeningBalanceLine(
                OpeningBalanceLineType.Account,
                accountId,
                debitAmount,
                creditAmount,
                notes: notes);

            _lines.Add(line);
            RecalculateTotals();
            return line;
        }

        /// <summary>
        /// Adds a customer opening balance line (ذمم مدينة).
        /// Positive amount = customer owes us (debit to AR control account).
        /// Negative amount = advance payment from customer (credit).
        /// </summary>
        public OpeningBalanceLine AddCustomerLine(
            int customerId,
            int accountId,
            decimal amount,
            string notes = null)
        {
            EnsureDraft("لا يمكن إضافة بنود لأرصدة افتتاحية مرحّلة.");

            if (customerId <= 0)
                throw new OpeningBalanceDomainException("العميل مطلوب.");

            if (amount == 0)
                throw new OpeningBalanceDomainException("المبلغ لا يمكن أن يكون صفراً.");

            // Duplicate check
            if (_lines.Any(l => l.LineType == OpeningBalanceLineType.Customer && l.CustomerId == customerId))
                throw new OpeningBalanceDomainException("العميل مضاف مسبقاً في الأرصدة الافتتاحية.");

            var debit = amount > 0 ? amount : 0;
            var credit = amount < 0 ? Math.Abs(amount) : 0;

            var line = new OpeningBalanceLine(
                OpeningBalanceLineType.Customer,
                accountId,
                debit,
                credit,
                customerId: customerId,
                notes: notes);

            _lines.Add(line);
            RecalculateTotals();
            return line;
        }

        /// <summary>
        /// Adds a supplier opening balance line (ذمم دائنة).
        /// Positive amount = we owe supplier (credit to AP control account).
        /// Negative amount = advance payment to supplier (debit).
        /// </summary>
        public OpeningBalanceLine AddSupplierLine(
            int supplierId,
            int accountId,
            decimal amount,
            string notes = null)
        {
            EnsureDraft("لا يمكن إضافة بنود لأرصدة افتتاحية مرحّلة.");

            if (supplierId <= 0)
                throw new OpeningBalanceDomainException("المورد مطلوب.");

            if (amount == 0)
                throw new OpeningBalanceDomainException("المبلغ لا يمكن أن يكون صفراً.");

            if (_lines.Any(l => l.LineType == OpeningBalanceLineType.Supplier && l.SupplierId == supplierId))
                throw new OpeningBalanceDomainException("المورد مضاف مسبقاً في الأرصدة الافتتاحية.");

            var debit = amount < 0 ? Math.Abs(amount) : 0;
            var credit = amount > 0 ? amount : 0;

            var line = new OpeningBalanceLine(
                OpeningBalanceLineType.Supplier,
                accountId,
                debit,
                credit,
                supplierId: supplierId,
                notes: notes);

            _lines.Add(line);
            RecalculateTotals();
            return line;
        }

        /// <summary>
        /// Adds an inventory opening balance line.
        /// Creates stock in a specific warehouse for a product.
        /// Debit = Quantity × UnitCost (inventory is an asset).
        /// </summary>
        public OpeningBalanceLine AddInventoryLine(
            int productId,
            int warehouseId,
            int accountId,
            decimal quantity,
            decimal unitCost,
            string notes = null)
        {
            EnsureDraft("لا يمكن إضافة بنود لأرصدة افتتاحية مرحّلة.");

            if (productId <= 0)
                throw new OpeningBalanceDomainException("الصنف مطلوب.");
            if (warehouseId <= 0)
                throw new OpeningBalanceDomainException("المخزن مطلوب.");
            if (quantity <= 0)
                throw new OpeningBalanceDomainException("الكمية يجب أن تكون أكبر من صفر.");
            if (unitCost < 0)
                throw new OpeningBalanceDomainException("تكلفة الوحدة لا يمكن أن تكون سالبة.");

            // Duplicate check: same product+warehouse
            if (_lines.Any(l => l.LineType == OpeningBalanceLineType.Inventory
                               && l.ProductId == productId
                               && l.WarehouseId == warehouseId))
                throw new OpeningBalanceDomainException("الصنف مضاف مسبقاً لنفس المخزن.");

            var totalCost = Math.Round(quantity * unitCost, 4);

            var line = new OpeningBalanceLine(
                OpeningBalanceLineType.Inventory,
                accountId,
                debitAmount: totalCost,
                creditAmount: 0,
                productId: productId,
                warehouseId: warehouseId,
                quantity: quantity,
                unitCost: unitCost,
                notes: notes);

            _lines.Add(line);
            RecalculateTotals();
            return line;
        }

        /// <summary>
        /// Adds a cashbox opening balance line.
        /// Debit = cash amount (cashbox is an asset).
        /// </summary>
        public OpeningBalanceLine AddCashboxLine(
            int cashboxId,
            int accountId,
            decimal amount,
            string notes = null)
        {
            EnsureDraft("لا يمكن إضافة بنود لأرصدة افتتاحية مرحّلة.");

            if (cashboxId <= 0)
                throw new OpeningBalanceDomainException("الصندوق مطلوب.");
            if (amount <= 0)
                throw new OpeningBalanceDomainException("مبلغ الصندوق يجب أن يكون أكبر من صفر.");

            if (_lines.Any(l => l.LineType == OpeningBalanceLineType.Cashbox && l.CashboxId == cashboxId))
                throw new OpeningBalanceDomainException("الصندوق مضاف مسبقاً في الأرصدة الافتتاحية.");

            var line = new OpeningBalanceLine(
                OpeningBalanceLineType.Cashbox,
                accountId,
                debitAmount: amount,
                creditAmount: 0,
                cashboxId: cashboxId,
                notes: notes);

            _lines.Add(line);
            RecalculateTotals();
            return line;
        }

        /// <summary>
        /// Adds a bank account opening balance line.
        /// Debit = bank balance (bank account is an asset).
        /// </summary>
        public OpeningBalanceLine AddBankAccountLine(
            int bankAccountId,
            int accountId,
            decimal amount,
            string notes = null)
        {
            EnsureDraft("لا يمكن إضافة بنود لأرصدة افتتاحية مرحّلة.");

            if (bankAccountId <= 0)
                throw new OpeningBalanceDomainException("الحساب البنكي مطلوب.");
            if (amount <= 0)
                throw new OpeningBalanceDomainException("رصيد البنك يجب أن يكون أكبر من صفر.");

            if (_lines.Any(l => l.LineType == OpeningBalanceLineType.BankAccount && l.BankAccountId == bankAccountId))
                throw new OpeningBalanceDomainException("الحساب البنكي مضاف مسبقاً في الأرصدة الافتتاحية.");

            var line = new OpeningBalanceLine(
                OpeningBalanceLineType.BankAccount,
                accountId,
                debitAmount: amount,
                creditAmount: 0,
                bankAccountId: bankAccountId,
                notes: notes);

            _lines.Add(line);
            RecalculateTotals();
            return line;
        }

        /// <summary>Removes a line from the opening balance.</summary>
        public void RemoveLine(int lineId)
        {
            EnsureDraft("لا يمكن حذف بنود من أرصدة افتتاحية مرحّلة.");

            var line = _lines.FirstOrDefault(l => l.Id == lineId);
            if (line == null)
                throw new OpeningBalanceDomainException("البند غير موجود.");

            _lines.Remove(line);
            RecalculateTotals();
        }

        /// <summary>Updates draft-level fields.</summary>
        public void UpdateDraft(DateTime balanceDate, string notes)
        {
            EnsureDraft("لا يمكن تعديل أرصدة افتتاحية مرحّلة.");

            BalanceDate = balanceDate;
            Notes = notes?.Trim();
        }

        /// <summary>
        /// Posts the opening balance with the auto-generated journal entry.
        /// Validates that total debit equals total credit.
        /// </summary>
        public void Post(int journalEntryId, string postedBy, DateTime postedAt)
        {
            EnsureDraft("الأرصدة الافتتاحية مرحّلة بالفعل.");

            if (!_lines.Any())
                throw new OpeningBalanceDomainException("لا يمكن ترحيل أرصدة افتتاحية بدون بنود.");

            if (journalEntryId <= 0)
                throw new OpeningBalanceDomainException("معرّف القيد المحاسبي غير صالح.");

            if (string.IsNullOrWhiteSpace(postedBy))
                throw new OpeningBalanceDomainException("المستخدم الذي قام بالترحيل مطلوب.");

            if (TotalDebit != TotalCredit)
                throw new OpeningBalanceDomainException(
                    $"الأرصدة الافتتاحية غير متوازنة. المدين: {TotalDebit:N4}، الدائن: {TotalCredit:N4}. " +
                    "يجب أن يتساوى إجمالي المدين مع إجمالي الدائن.");

            Status = OpeningBalanceStatus.Posted;
            JournalEntryId = journalEntryId;
            PostedBy = postedBy;
            PostedAt = postedAt;
        }

        /// <summary>Gets the balance difference (TotalDebit - TotalCredit).</summary>
        public decimal GetDifference() => TotalDebit - TotalCredit;

        /// <summary>Checks if the opening balance is balanced.</summary>
        public bool IsBalanced => TotalDebit == TotalCredit;

        // ── Private Helpers ─────────────────────────────────────

        private void EnsureDraft(string errorMessage)
        {
            if (Status != OpeningBalanceStatus.Draft)
                throw new OpeningBalanceDomainException(errorMessage);
        }

        private static void ValidateAmounts(decimal debitAmount, decimal creditAmount)
        {
            if (debitAmount < 0)
                throw new OpeningBalanceDomainException("المبلغ المدين لا يمكن أن يكون سالباً.");
            if (creditAmount < 0)
                throw new OpeningBalanceDomainException("المبلغ الدائن لا يمكن أن يكون سالباً.");
            if (debitAmount == 0 && creditAmount == 0)
                throw new OpeningBalanceDomainException("يجب تحديد مبلغ مدين أو دائن.");
            if (debitAmount > 0 && creditAmount > 0)
                throw new OpeningBalanceDomainException("لا يمكن أن يكون البند مديناً ودائناً في نفس الوقت.");
        }

        private void RecalculateTotals()
        {
            TotalDebit = _lines.Sum(l => l.DebitAmount);
            TotalCredit = _lines.Sum(l => l.CreditAmount);
        }
    }
}
