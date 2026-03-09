using System;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Sales;

namespace MarcoERP.Domain.Entities.Sales
{
    /// <summary>
    /// Represents a customer (عميل) in the sales sub-ledger.
    /// Uses sub-ledger pattern: individual balance tracked via invoices/payments,
    /// GL control account = 1121 (المدينون — ذمم تجارية).
    /// </summary>
    public sealed class Customer : CompanyAwareEntity
    {
        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private Customer() { }

        /// <summary>
        /// Creates a new Customer with full invariant validation.
        /// </summary>
        public Customer(CustomerDraft draft)
        {
            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            if (string.IsNullOrWhiteSpace(draft.Code))
                throw new CustomerDomainException("كود العميل مطلوب.");

            if (string.IsNullOrWhiteSpace(draft.NameAr))
                throw new CustomerDomainException("اسم العميل بالعربي مطلوب.");

            if (draft.CreditLimit < 0)
                throw new CustomerDomainException("حد الائتمان لا يمكن أن يكون بالسالب.");

            Code = draft.Code.Trim();
            NameAr = draft.NameAr.Trim();
            NameEn = draft.NameEn?.Trim();
            Phone = draft.Phone?.Trim();
            Mobile = draft.Mobile?.Trim();
            Address = draft.Address?.Trim();
            City = draft.City?.Trim();
            TaxNumber = draft.TaxNumber?.Trim();
            Email = draft.Email?.Trim();
            CustomerType = draft.CustomerType;
            CommercialRegister = draft.CommercialRegister?.Trim();
            Country = draft.Country?.Trim();
            PostalCode = draft.PostalCode?.Trim();
            ContactPerson = draft.ContactPerson?.Trim();
            Website = draft.Website?.Trim();
            DefaultDiscountPercent = draft.DefaultDiscountPercent;
            PreviousBalance = draft.PreviousBalance;
            CreditLimit = draft.CreditLimit;
            DaysAllowed = draft.DaysAllowed;
            BlockedOnOverdue = draft.BlockedOnOverdue;
            PriceListId = draft.PriceListId;
            Notes = draft.Notes?.Trim();
            IsActive = true;
            DefaultSalesRepresentativeId = draft.DefaultSalesRepresentativeId;
            Account = null;
        }

        // ── Properties ──────────────────────────────────────────

        /// <summary>Unique customer code (auto-generated or user-set).</summary>
        public string Code { get; private set; }

        /// <summary>Arabic name (required).</summary>
        public string NameAr { get; private set; }

        /// <summary>English name (optional).</summary>
        public string NameEn { get; private set; }

        /// <summary>Phone number.</summary>
        public string Phone { get; private set; }

        /// <summary>Mobile number.</summary>
        public string Mobile { get; private set; }

        /// <summary>Full address.</summary>
        public string Address { get; private set; }

        /// <summary>City / Governorate.</summary>
        public string City { get; private set; }

        /// <summary>Tax registration number (الرقم الضريبي).</summary>
        public string TaxNumber { get; private set; }

        /// <summary>البريد الإلكتروني.</summary>
        public string Email { get; private set; }

        /// <summary>نوع العميل (فرد/شركة/حكومي).</summary>
        public CustomerType CustomerType { get; private set; }

        /// <summary>السجل التجاري.</summary>
        public string CommercialRegister { get; private set; }

        /// <summary>الدولة.</summary>
        public string Country { get; private set; }

        /// <summary>الرمز البريدي.</summary>
        public string PostalCode { get; private set; }

        /// <summary>اسم الشخص المسؤول (للشركات).</summary>
        public string ContactPerson { get; private set; }

        /// <summary>الموقع الإلكتروني.</summary>
        public string Website { get; private set; }

        /// <summary>نسبة الخصم الافتراضية للعميل.</summary>
        public decimal DefaultDiscountPercent { get; private set; }

        /// <summary>
        /// Opening balance brought forward from a previous system.
        /// Positive = customer owes us (debit). Negative = overpayment (credit).
        /// Set once at creation; adjustments done via journal entries.
        /// </summary>
        public decimal PreviousBalance { get; private set; }

        /// <summary>
        /// Maximum allowed outstanding balance before credit hold.
        /// Zero = unlimited credit.
        /// </summary>
        public decimal CreditLimit { get; private set; }

        /// <summary>
        /// Maximum number of allowed credit days before overdue block.
        /// Null = no day limit enforcement.
        /// </summary>
        public int? DaysAllowed { get; private set; }

        /// <summary>
        /// If true, customer is blocked from new invoices when any invoice is overdue.
        /// </summary>
        public bool BlockedOnOverdue { get; private set; }

        /// <summary>FK to default PriceList (optional). Used for tiered pricing.</summary>
        public int? PriceListId { get; private set; }

        /// <summary>Navigation property to default price list.</summary>
        public PriceList PriceList { get; private set; }

        /// <summary>Active flag — inactive customers cannot appear on new invoices.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Free-form notes.</summary>
        public string Notes { get; private set; }

        /// <summary>
        /// FK to the GL control account (Accounts Receivable — 1121).
        /// Linked automatically on creation.
        /// </summary>
        public int? AccountId { get; private set; }

        /// <summary>Navigation property to the linked GL account.</summary>
        public Account Account { get; private set; }

        /// <summary>FK to default sales representative (مندوب مبيعات افتراضي). Optional.</summary>
        public int? DefaultSalesRepresentativeId { get; private set; }

        /// <summary>Navigation property to sales representative.</summary>
        public SalesRepresentative DefaultSalesRepresentative { get; private set; }

        // ── Domain Methods ──────────────────────────────────────

        /// <summary>Updates customer information.</summary>
        public void Update(CustomerUpdate update)
        {
            if (update == null)
                throw new ArgumentNullException(nameof(update));

            if (string.IsNullOrWhiteSpace(update.NameAr))
                throw new CustomerDomainException("اسم العميل بالعربي مطلوب.");

            if (update.CreditLimit < 0)
                throw new CustomerDomainException("حد الائتمان لا يمكن أن يكون بالسالب.");

            NameAr = update.NameAr.Trim();
            NameEn = update.NameEn?.Trim();
            Phone = update.Phone?.Trim();
            Mobile = update.Mobile?.Trim();
            Address = update.Address?.Trim();
            City = update.City?.Trim();
            TaxNumber = update.TaxNumber?.Trim();
            Email = update.Email?.Trim();
            CustomerType = update.CustomerType;
            CommercialRegister = update.CommercialRegister?.Trim();
            Country = update.Country?.Trim();
            PostalCode = update.PostalCode?.Trim();
            ContactPerson = update.ContactPerson?.Trim();
            Website = update.Website?.Trim();
            DefaultDiscountPercent = update.DefaultDiscountPercent;
            CreditLimit = update.CreditLimit;
            DaysAllowed = update.DaysAllowed;
            BlockedOnOverdue = update.BlockedOnOverdue;
            PriceListId = update.PriceListId;
            Notes = update.Notes?.Trim();
            DefaultSalesRepresentativeId = update.DefaultSalesRepresentativeId;
        }

        /// <summary>Deactivates the customer. Cannot appear on new invoices.</summary>
        public void Deactivate()
        {
            IsActive = false;
        }

        /// <summary>Reactivates a previously deactivated customer.</summary>
        public void Activate()
        {
            IsActive = true;
        }

        /// <summary>
        /// Links the customer to a GL control account (Accounts Receivable).
        /// </summary>
        public void SetAccountId(int accountId)
        {
            if (accountId <= 0)
                throw new CustomerDomainException("معرف الحساب غير صالح.");

            AccountId = accountId;
        }

        /// <summary>
        /// Adjusts the previous (opening) balance. Use only for corrections.
        /// Normal balance changes happen through invoices/payments.
        /// </summary>
        public void AdjustPreviousBalance(decimal newBalance)
        {
            PreviousBalance = newBalance;
        }

        /// <summary>
        /// Override soft delete — cannot delete a customer with outstanding balance.
        /// The service layer should verify no unpaid invoices before calling this.
        /// </summary>
        public override void SoftDelete(string deletedBy, DateTime deletedAt)
        {
            base.SoftDelete(deletedBy, deletedAt);
        }
    
        public sealed class CustomerDraft
        {
            public string Code { get; init; }
            public string NameAr { get; init; }
            public string NameEn { get; init; }
            public string Phone { get; init; }
            public string Mobile { get; init; }
            public string Address { get; init; }
            public string City { get; init; }
            public string TaxNumber { get; init; }
            public string Email { get; init; }
            public CustomerType CustomerType { get; init; }
            public string CommercialRegister { get; init; }
            public string Country { get; init; }
            public string PostalCode { get; init; }
            public string ContactPerson { get; init; }
            public string Website { get; init; }
            public decimal DefaultDiscountPercent { get; init; }
            public decimal PreviousBalance { get; init; }
            public decimal CreditLimit { get; init; }
            public int? DaysAllowed { get; init; }
            public bool BlockedOnOverdue { get; init; }
            public int? PriceListId { get; init; }
            public string Notes { get; init; }
            public int? DefaultSalesRepresentativeId { get; init; }
        }
    
        public sealed class CustomerUpdate
        {
            public string NameAr { get; init; }
            public string NameEn { get; init; }
            public string Phone { get; init; }
            public string Mobile { get; init; }
            public string Address { get; init; }
            public string City { get; init; }
            public string TaxNumber { get; init; }
            public string Email { get; init; }
            public CustomerType CustomerType { get; init; }
            public string CommercialRegister { get; init; }
            public string Country { get; init; }
            public string PostalCode { get; init; }
            public string ContactPerson { get; init; }
            public string Website { get; init; }
            public decimal DefaultDiscountPercent { get; init; }
            public decimal CreditLimit { get; init; }
            public int? DaysAllowed { get; init; }
            public bool BlockedOnOverdue { get; init; }
            public int? PriceListId { get; init; }
            public string Notes { get; init; }
            public int? DefaultSalesRepresentativeId { get; init; }
        }
    }
}
