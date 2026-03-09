using System;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Exceptions.Treasury;

namespace MarcoERP.Domain.Entities.Treasury
{
    /// <summary>
    /// Represents a bank account (حساب بنكي) linked to a GL leaf account.
    /// Master-data entity: CRUD with activate/deactivate.
    /// </summary>
    public sealed class BankAccount : CompanyAwareEntity
    {
        // ── Constructors ─────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private BankAccount() { }

        /// <summary>
        /// Creates a new BankAccount with full invariant validation.
        /// </summary>
        public BankAccount(
            string code,
            string nameAr,
            string nameEn,
            string bankName,
            string accountNumber,
            string iban,
            int? accountId = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new TreasuryDomainException("كود الحساب البنكي مطلوب.");

            if (string.IsNullOrWhiteSpace(nameAr))
                throw new TreasuryDomainException("اسم الحساب البنكي بالعربي مطلوب.");

            Code = code.Trim();
            NameAr = nameAr.Trim();
            NameEn = nameEn?.Trim();
            BankName = bankName?.Trim();
            AccountNumber = accountNumber?.Trim();
            IBAN = iban?.Trim();
            AccountId = accountId;
            IsActive = true;
            IsDefault = false;
        }

        // ── Properties ───────────────────────────────────────────

        /// <summary>Unique bank account code (auto-generated BNK-####).</summary>
        public string Code { get; private set; }

        /// <summary>Arabic account name.</summary>
        public string NameAr { get; private set; }

        /// <summary>English account name (optional).</summary>
        public string NameEn { get; private set; }

        /// <summary>Bank name (اسم البنك).</summary>
        public string BankName { get; private set; }

        /// <summary>Bank account number (رقم الحساب).</summary>
        public string AccountNumber { get; private set; }

        /// <summary>IBAN number (رقم الآيبان).</summary>
        public string IBAN { get; private set; }

        /// <summary>FK to GL Account under 1110 Cash and Banks.</summary>
        public int? AccountId { get; private set; }

        /// <summary>Navigation to linked GL Account.</summary>
        public Account Account { get; private set; }

        /// <summary>Whether this bank account is active.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Whether this is the default bank account.</summary>
        public bool IsDefault { get; private set; }

        // ── Domain Methods ───────────────────────────────────────

        /// <summary>Updates bank account information.</summary>
        public void Update(string nameAr, string nameEn, string bankName,
            string accountNumber, string iban, int? accountId)
        {
            if (string.IsNullOrWhiteSpace(nameAr))
                throw new TreasuryDomainException("اسم الحساب البنكي بالعربي مطلوب.");

            NameAr = nameAr.Trim();
            NameEn = nameEn?.Trim();
            BankName = bankName?.Trim();
            AccountNumber = accountNumber?.Trim();
            IBAN = iban?.Trim();
            AccountId = accountId;
        }

        /// <summary>Sets this bank account as the default.</summary>
        public void SetAsDefault() => IsDefault = true;

        /// <summary>Removes default status.</summary>
        public void ClearDefault() => IsDefault = false;

        /// <summary>Deactivates the bank account. Default bank account cannot be deactivated.</summary>
        public void Deactivate()
        {
            if (IsDefault)
                throw new TreasuryDomainException("لا يمكن تعطيل الحساب البنكي الافتراضي.");
            IsActive = false;
        }

        /// <summary>Activates the bank account.</summary>
        public void Activate() => IsActive = true;
    }
}
