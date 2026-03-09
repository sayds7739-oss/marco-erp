using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Domain.Entities.Treasury;

namespace MarcoERP.Application.Mappers.Treasury
{
    /// <summary>Manual mapper for CashReceipt entity ↔ DTOs.</summary>
    public static class CashReceiptMapper
    {
        public static CashReceiptDto ToDto(CashReceipt entity)
        {
            if (entity == null) return null;

            return new CashReceiptDto
            {
                Id = entity.Id,
                ReceiptNumber = entity.ReceiptNumber,
                ReceiptDate = entity.ReceiptDate,
                CashboxId = entity.CashboxId,
                CashboxName = entity.Cashbox?.NameAr,
                AccountId = entity.AccountId,
                AccountName = entity.Account?.AccountNameAr,
                CustomerId = entity.CustomerId,
                CustomerName = entity.Customer?.NameAr,
                SalesInvoiceId = entity.SalesInvoiceId,
                Amount = entity.Amount,
                Description = entity.Description,
                Notes = entity.Notes,
                Status = entity.Status.ToString(),
                JournalEntryId = entity.JournalEntryId
            };
        }

        public static CashReceiptListDto ToListDto(CashReceipt entity)
        {
            if (entity == null) return null;

            return new CashReceiptListDto
            {
                Id = entity.Id,
                ReceiptNumber = entity.ReceiptNumber,
                ReceiptDate = entity.ReceiptDate,
                CashboxName = entity.Cashbox?.NameAr,
                AccountName = entity.Account?.AccountNameAr,
                CustomerName = entity.Customer?.NameAr,
                Amount = entity.Amount,
                Status = entity.Status.ToString()
            };
        }
    }
}
