using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.DTOs.Sales
{
    // ═══════════════════════════════════════════════════════════
    //  Customer DTOs
    // ═══════════════════════════════════════════════════════════

    /// <summary>Read-only DTO returned to the UI / API.</summary>
    public sealed class CustomerDto
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
        public CustomerType CustomerType { get; set; }
        public string CommercialRegister { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }
        public string ContactPerson { get; set; }
        public string Website { get; set; }
        public decimal DefaultDiscountPercent { get; set; }
        public decimal PreviousBalance { get; set; }
        public decimal CreditLimit { get; set; }
        public int? DaysAllowed { get; set; }
        public bool BlockedOnOverdue { get; set; }
        public int? PriceListId { get; set; }
        public string PriceListName { get; set; }
        public bool IsActive { get; set; }
        public string Notes { get; set; }
        public int? AccountId { get; set; }
    }

    /// <summary>DTO for creating a new customer.</summary>
    public sealed class CreateCustomerDto
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
        public CustomerType CustomerType { get; set; }
        public string CommercialRegister { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }
        public string ContactPerson { get; set; }
        public string Website { get; set; }
        public decimal DefaultDiscountPercent { get; set; }
        public decimal PreviousBalance { get; set; }
        public decimal CreditLimit { get; set; }
        public int? DaysAllowed { get; set; }
        public bool BlockedOnOverdue { get; set; }
        public int? PriceListId { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>DTO for updating an existing customer.</summary>
    public sealed class UpdateCustomerDto
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
        public CustomerType CustomerType { get; set; }
        public string CommercialRegister { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }
        public string ContactPerson { get; set; }
        public string Website { get; set; }
        public decimal DefaultDiscountPercent { get; set; }
        public decimal CreditLimit { get; set; }
        public int? DaysAllowed { get; set; }
        public bool BlockedOnOverdue { get; set; }
        public int? PriceListId { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>Lightweight DTO for search results / dropdowns.</summary>
    public sealed class CustomerSearchResultDto
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string NameAr { get; set; }
        public string Phone { get; set; }
        public CustomerType CustomerType { get; set; }
        public bool IsActive { get; set; }
    }
}
