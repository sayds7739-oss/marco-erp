using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Domain.Entities.Treasury;

namespace MarcoERP.Application.Mappers.Treasury
{
    /// <summary>Manual mapper for Cashbox entity ↔ DTOs.</summary>
    public static class CashboxMapper
    {
        public static CashboxDto ToDto(Cashbox entity)
        {
            if (entity == null) return null;

            return new CashboxDto
            {
                Id = entity.Id,
                Code = entity.Code,
                NameAr = entity.NameAr,
                NameEn = entity.NameEn,
                AccountId = entity.AccountId,
                AccountName = entity.Account?.AccountNameAr,
                Balance = entity.Balance,
                IsActive = entity.IsActive,
                IsDefault = entity.IsDefault
            };
        }
    }
}
