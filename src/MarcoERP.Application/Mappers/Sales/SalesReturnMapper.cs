using System.Linq;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.Mappers.Sales
{
    /// <summary>
    /// Manual mapper for SalesReturn entities ↔ DTOs.
    /// </summary>
    public static class SalesReturnMapper
    {
        /// <summary>Maps SalesReturn entity → SalesReturnDto (with lines).</summary>
        public static SalesReturnDto ToDto(SalesReturn entity)
        {
            if (entity == null) return null;

            return new SalesReturnDto
            {
                Id = entity.Id,
                ReturnNumber = entity.ReturnNumber,
                ReturnDate = entity.ReturnDate,
                CustomerId = entity.CustomerId,
                CustomerNameAr = entity.Customer?.NameAr,
                CounterpartyType = entity.CounterpartyType,
                SupplierId = entity.SupplierId,
                SupplierNameAr = entity.CounterpartySupplier?.NameAr,
                SalesRepresentativeId = entity.SalesRepresentativeId,
                WarehouseId = entity.WarehouseId,
                WarehouseNameAr = entity.Warehouse?.NameAr,
                OriginalInvoiceId = entity.OriginalInvoiceId,
                OriginalInvoiceNumber = entity.OriginalInvoice?.InvoiceNumber,
                Status = entity.Status.ToString(),
                Subtotal = entity.Subtotal,
                DiscountTotal = entity.DiscountTotal,
                VatTotal = entity.VatTotal,
                NetTotal = entity.NetTotal,
                Notes = entity.Notes,
                JournalEntryId = entity.JournalEntryId,
                CogsJournalEntryId = entity.CogsJournalEntryId,
                Lines = entity.Lines?.Select(ToLineDto).ToList() ?? new()
            };
        }

        /// <summary>Maps SalesReturnLine entity → SalesReturnLineDto.</summary>
        public static SalesReturnLineDto ToLineDto(SalesReturnLine line)
        {
            if (line == null) return null;

            return new SalesReturnLineDto
            {
                Id = line.Id,
                ProductId = line.ProductId,
                ProductNameAr = line.Product?.NameAr,
                ProductCode = line.Product?.Code,
                UnitId = line.UnitId,
                UnitNameAr = line.Unit?.NameAr,
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
        public static SalesReturnListDto ToListDto(SalesReturn entity)
        {
            if (entity == null) return null;

            var counterpartyName = entity.CounterpartyType == CounterpartyType.Supplier
                ? entity.CounterpartySupplier?.NameAr
                : entity.Customer?.NameAr;

            return new SalesReturnListDto
            {
                Id = entity.Id,
                ReturnNumber = entity.ReturnNumber,
                ReturnDate = entity.ReturnDate,
                CustomerNameAr = counterpartyName,
                Status = entity.Status.ToString(),
                NetTotal = entity.NetTotal
            };
        }
    }
}
