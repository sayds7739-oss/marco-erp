using System.Linq;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.Mappers.Purchases
{
    /// <summary>
    /// Manual mapper for PurchaseReturn entities ↔ DTOs.
    /// </summary>
    public static class PurchaseReturnMapper
    {
        /// <summary>Maps PurchaseReturn entity → PurchaseReturnDto (with lines).</summary>
        public static PurchaseReturnDto ToDto(PurchaseReturn entity)
        {
            if (entity == null) return null;

            return new PurchaseReturnDto
            {
                Id = entity.Id,
                ReturnNumber = entity.ReturnNumber,
                ReturnDate = entity.ReturnDate,
                SupplierId = entity.SupplierId,
                SupplierNameAr = entity.Supplier?.NameAr,
                CounterpartyType = entity.CounterpartyType,
                CounterpartyCustomerId = entity.CounterpartyCustomerId,
                CounterpartyCustomerNameAr = entity.CounterpartyCustomer?.NameAr,
                SalesRepresentativeId = entity.SalesRepresentativeId,
                WarehouseId = entity.WarehouseId,
                OriginalInvoiceId = entity.OriginalInvoiceId,
                OriginalInvoiceNumber = entity.OriginalInvoice?.InvoiceNumber,
                Status = entity.Status.ToString(),
                Subtotal = entity.Subtotal,
                DiscountTotal = entity.DiscountTotal,
                VatTotal = entity.VatTotal,
                NetTotal = entity.NetTotal,
                Notes = entity.Notes,
                JournalEntryId = entity.JournalEntryId,
                Lines = entity.Lines?.Select(ToLineDto).ToList() ?? new()
            };
        }

        /// <summary>Maps PurchaseReturnLine entity → PurchaseReturnLineDto.</summary>
        public static PurchaseReturnLineDto ToLineDto(PurchaseReturnLine line)
        {
            if (line == null) return null;

            return new PurchaseReturnLineDto
            {
                Id = line.Id,
                ProductId = line.ProductId,
                UnitId = line.UnitId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                ConversionFactor = line.ConversionFactor,
                BaseQuantity = line.BaseQuantity,
                DiscountPercent = line.DiscountPercent,
                DiscountAmount = line.DiscountAmount,
                SubTotal = line.SubTotal,
                NetTotal = line.NetTotal,
                VatRate = line.VatRate,
                VatAmount = line.VatAmount,
                TotalWithVat = line.TotalWithVat
            };
        }

        /// <summary>Maps to lightweight list DTO.</summary>
        public static PurchaseReturnListDto ToListDto(PurchaseReturn entity)
        {
            if (entity == null) return null;

            var counterpartyName = entity.CounterpartyType == CounterpartyType.Customer
                ? entity.CounterpartyCustomer?.NameAr
                : entity.Supplier?.NameAr;

            return new PurchaseReturnListDto
            {
                Id = entity.Id,
                ReturnNumber = entity.ReturnNumber,
                ReturnDate = entity.ReturnDate,
                SupplierNameAr = counterpartyName,
                Status = entity.Status.ToString(),
                NetTotal = entity.NetTotal
            };
        }
    }
}
