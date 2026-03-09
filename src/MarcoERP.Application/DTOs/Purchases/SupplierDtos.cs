namespace MarcoERP.Application.DTOs.Purchases
{
    // ═══════════════════════════════════════════════════════════
    //  Supplier DTOs
    // ═══════════════════════════════════════════════════════════

    /// <summary>Read-only DTO returned to the UI / API.</summary>
    public sealed class SupplierDto
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string NameAr { get; set; }
        public string NameEn { get; set; }
        public string Phone { get; set; }
        public string Mobile { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string TaxNumber { get; set; }
        public string Email { get; set; }
        public string CommercialRegister { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }
        public string ContactPerson { get; set; }
        public string Website { get; set; }
        public decimal CreditLimit { get; set; }
        public int? DaysAllowed { get; set; }
        public string BankName { get; set; }
        public string BankAccountName { get; set; }
        public string BankAccountNumber { get; set; }
        public string IBAN { get; set; }
        public decimal PreviousBalance { get; set; }
        public bool IsActive { get; set; }
        public string Notes { get; set; }
        public int? AccountId { get; set; }
    }

    /// <summary>DTO for creating a new supplier.</summary>
    public sealed class CreateSupplierDto
    {
        public string Code { get; set; }
        public string NameAr { get; set; }
        public string NameEn { get; set; }
        public string Phone { get; set; }
        public string Mobile { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string TaxNumber { get; set; }
        public string Email { get; set; }
        public string CommercialRegister { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }
        public string ContactPerson { get; set; }
        public string Website { get; set; }
        public decimal CreditLimit { get; set; }
        public int? DaysAllowed { get; set; }
        public string BankName { get; set; }
        public string BankAccountName { get; set; }
        public string BankAccountNumber { get; set; }
        public string IBAN { get; set; }
        public decimal PreviousBalance { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>DTO for updating an existing supplier.</summary>
    public sealed class UpdateSupplierDto
    {
        public int Id { get; set; }
        public string NameAr { get; set; }
        public string NameEn { get; set; }
        public string Phone { get; set; }
        public string Mobile { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string TaxNumber { get; set; }
        public string Email { get; set; }
        public string CommercialRegister { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }
        public string ContactPerson { get; set; }
        public string Website { get; set; }
        public decimal CreditLimit { get; set; }
        public int? DaysAllowed { get; set; }
        public string BankName { get; set; }
        public string BankAccountName { get; set; }
        public string BankAccountNumber { get; set; }
        public string IBAN { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>Lightweight DTO for search results / dropdowns.</summary>
    public sealed class SupplierSearchResultDto
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string NameAr { get; set; }
        public string Phone { get; set; }
        public bool IsActive { get; set; }
    }
}
