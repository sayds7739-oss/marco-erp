using System;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Exceptions.Treasury;

namespace MarcoERP.Domain.Entities.Treasury
{
    /// <summary>
    /// Represents a physical or virtual cashbox (خزنة) or bank account.
    /// Each cashbox is linked to a GL leaf account under 1110 (Cash and Banks).
    /// Tracks a running Balance that must never go negative.
    /// </summary>
    public sealed class Cashbox : CompanyAwareEntity
    {
        // ── Constructors ─────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private Cashbox() { }

        /// <summary>
        /// Creates a new Cashbox with full invariant validation.
        /// </summary>
        public Cashbox(
            string code,
            string nameAr,
            string nameEn,
            int? accountId = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new TreasuryDomainException("كود الخزنة مطلوب.");

            if (string.IsNullOrWhiteSpace(nameAr))
                throw new TreasuryDomainException("اسم الخزنة بالعربي مطلوب.");

            Code = code.Trim();
            NameAr = nameAr.Trim();
            NameEn = nameEn?.Trim();
            AccountId = accountId;
            IsActive = true;
            IsDefault = false;
            Balance = 0m;
        }

        // ── Properties ───────────────────────────────────────────

        /// <summary>Unique cashbox code (auto-generated CBX-####).</summary>
        public string Code { get; private set; }

        /// <summary>Arabic cashbox name.</summary>
        public string NameAr { get; private set; }

        /// <summary>English cashbox name (optional).</summary>
        public string NameEn { get; private set; }

        /// <summary>FK to GL Account under 1110 Cash and Banks (e.g. 1111 Main Cash).</summary>
        public int? AccountId { get; private set; }

        /// <summary>Navigation to linked GL Account.</summary>
        public Account Account { get; private set; }

        /// <summary>Whether this cashbox is active and can receive transactions.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Whether this is the default cashbox for new transactions.</summary>
        public bool IsDefault { get; private set; }

        /// <summary>
        /// Running cashbox balance (HasPrecision 18,4).
        /// Updated atomically during posting within a Serializable transaction.
        /// Must never go negative.
        /// </summary>
        public decimal Balance { get; private set; }

        // ── Domain Methods ───────────────────────────────────────

        /// <summary>Updates cashbox information.</summary>
        public void Update(string nameAr, string nameEn, int? accountId)
        {
            if (string.IsNullOrWhiteSpace(nameAr))
                throw new TreasuryDomainException("اسم الخزنة بالعربي مطلوب.");

            NameAr = nameAr.Trim();
            NameEn = nameEn?.Trim();
            AccountId = accountId;
        }

        /// <summary>Sets this cashbox as the default.</summary>
        public void SetAsDefault() => IsDefault = true;

        /// <summary>Removes default status.</summary>
        public void ClearDefault() => IsDefault = false;

        /// <summary>Deactivates the cashbox. Default cashbox cannot be deactivated.</summary>
        public void Deactivate()
        {
            if (IsDefault)
                throw new TreasuryDomainException("لا يمكن تعطيل الخزنة الافتراضية.");
            IsActive = false;
        }

        /// <summary>Activates the cashbox.</summary>
        public void Activate() => IsActive = true;

        // ── Balance Operations ──────────────────────────────────

        /// <summary>
        /// Increases the cashbox balance (e.g. cash receipt posted).
        /// Amount must be positive.
        /// </summary>
        public void IncreaseBalance(decimal amount)
        {
            if (amount <= 0)
                throw new TreasuryDomainException("مبلغ الزيادة يجب أن يكون أكبر من صفر.");

            Balance += amount;
        }

        /// <summary>
        /// Decreases the cashbox balance (e.g. cash payment posted).
        /// Amount must be positive and must not exceed current balance.
        /// Throws TreasuryDomainException if balance would go negative.
        /// </summary>
        public void DecreaseBalance(decimal amount)
        {
            if (amount <= 0)
                throw new TreasuryDomainException("مبلغ النقص يجب أن يكون أكبر من صفر.");

            if (amount > Balance)
                throw new TreasuryDomainException(
                    $"رصيد الخزنة '{NameAr}' غير كافٍ. الرصيد الحالي: {Balance:N4}، المبلغ المطلوب: {amount:N4}");

            Balance -= amount;
        }

        /// <summary>
        /// Decreases the balance without enforcing non-negative protection.
        /// Used only when explicitly allowed by system settings.
        /// </summary>
        public void DecreaseBalanceAllowNegative(decimal amount)
        {
            if (amount <= 0)
                throw new TreasuryDomainException("مبلغ النقص يجب أن يكون أكبر من صفر.");

            Balance -= amount;
        }
    }
}
