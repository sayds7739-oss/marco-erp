using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Domain.Entities.Purchases;

namespace MarcoERP.Application.Mappers.Purchases
{
    /// <summary>
    /// Manual mapper for Supplier entity ↔ DTOs.
    /// No AutoMapper — per governance ARCHITECTURE §7.
    /// </summary>
    public static class SupplierMapper
    {
        /// <summary>Maps Supplier entity → SupplierDto.</summary>
        public static SupplierDto ToDto(Supplier entity)
        {
            if (entity == null) return null;

            return new SupplierDto
            {
                Id = entity.Id,
                Code = entity.Code,
                NameAr = entity.NameAr,
                NameEn = entity.NameEn,
                Phone = entity.Phone,
                Mobile = entity.Mobile,
                Address = entity.Address,
                City = entity.City,
                TaxNumber = entity.TaxNumber,
                Email = entity.Email,
                CommercialRegister = entity.CommercialRegister,
                Country = entity.Country,
                PostalCode = entity.PostalCode,
                ContactPerson = entity.ContactPerson,
                Website = entity.Website,
                CreditLimit = entity.CreditLimit,
                DaysAllowed = entity.DaysAllowed,
                BankName = entity.BankName,
                BankAccountName = entity.BankAccountName,
                BankAccountNumber = entity.BankAccountNumber,
                IBAN = entity.IBAN,
                PreviousBalance = entity.PreviousBalance,
                IsActive = entity.IsActive,
                Notes = entity.Notes,
                AccountId = entity.AccountId
            };
        }

        /// <summary>Maps Supplier entity → SupplierSearchResultDto.</summary>
        public static SupplierSearchResultDto ToSearchResult(Supplier entity)
        {
            if (entity == null) return null;

            return new SupplierSearchResultDto
            {
                Id = entity.Id,
                Code = entity.Code,
                NameAr = entity.NameAr,
                Phone = entity.Phone,
                IsActive = entity.IsActive
            };
        }
    }
}
