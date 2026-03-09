using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Domain.Entities.Treasury;

namespace MarcoERP.Application.Mappers.Treasury
{
    /// <summary>Manual mapper for CashPayment entity ↔ DTOs.</summary>
    public static class CashPaymentMapper
    {
        public static CashPaymentDto ToDto(CashPayment entity)
        {
            if (entity == null) return null;

            return new CashPaymentDto
            {
                Id = entity.Id,
                PaymentNumber = entity.PaymentNumber,
                PaymentDate = entity.PaymentDate,
                CashboxId = entity.CashboxId,
                CashboxName = entity.Cashbox?.NameAr,
                AccountId = entity.AccountId,
                AccountName = entity.Account?.AccountNameAr,
                SupplierId = entity.SupplierId,
                SupplierName = entity.Supplier?.NameAr,
                PurchaseInvoiceId = entity.PurchaseInvoiceId,
                Amount = entity.Amount,
                Description = entity.Description,
                Notes = entity.Notes,
                Status = entity.Status.ToString(),
                JournalEntryId = entity.JournalEntryId
            };
        }

        public static CashPaymentListDto ToListDto(CashPayment entity)
        {
            if (entity == null) return null;

            return new CashPaymentListDto
            {
                Id = entity.Id,
                PaymentNumber = entity.PaymentNumber,
                PaymentDate = entity.PaymentDate,
                CashboxName = entity.Cashbox?.NameAr,
                AccountName = entity.Account?.AccountNameAr,
                SupplierName = entity.Supplier?.NameAr,
                Amount = entity.Amount,
                Status = entity.Status.ToString()
            };
        }
    }
}
