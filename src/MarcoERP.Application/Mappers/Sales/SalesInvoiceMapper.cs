using System.Linq;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Domain.Entities.Sales;

namespace MarcoERP.Application.Mappers.Sales
{
    /// <summary>
    /// Manual mapper for SalesInvoice entities ↔ DTOs.
    /// </summary>
    public static class SalesInvoiceMapper
    {
        /// <summary>Maps SalesInvoice entity → SalesInvoiceDto (with lines).</summary>
        public static SalesInvoiceDto ToDto(SalesInvoice entity)
        {
            if (entity == null) return null;

            return new SalesInvoiceDto
            {
                Id = entity.Id,
                InvoiceNumber = entity.InvoiceNumber,
                InvoiceDate = entity.InvoiceDate,
                CustomerId = entity.CustomerId,
                CustomerNameAr = entity.Customer?.NameAr,
                WarehouseId = entity.WarehouseId,
                Status = entity.Status.ToString(),
                Subtotal = entity.Subtotal,
                DiscountTotal = entity.DiscountTotal,
                VatTotal = entity.VatTotal,
                NetTotal = entity.NetTotal,
                Notes = entity.Notes,
                JournalEntryId = entity.JournalEntryId,
                CogsJournalEntryId = entity.CogsJournalEntryId,
                SalesRepresentativeId = entity.SalesRepresentativeId,
                CounterpartyType = entity.CounterpartyType,
                SupplierId = entity.SupplierId,
                SupplierNameAr = entity.CounterpartySupplier?.NameAr,
                Lines = entity.Lines?.Select(ToLineDto).ToList() ?? new()
            };
        }

        /// <summary>Maps SalesInvoiceLine entity → SalesInvoiceLineDto.</summary>
        public static SalesInvoiceLineDto ToLineDto(SalesInvoiceLine line)
        {
            if (line == null) return null;

            return new SalesInvoiceLineDto
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
        public static SalesInvoiceListDto ToListDto(SalesInvoice entity)
        {
            if (entity == null) return null;

            var counterpartyName = entity.CounterpartyType == Domain.Enums.CounterpartyType.Supplier
                ? entity.CounterpartySupplier?.NameAr
                : entity.Customer?.NameAr;

            return new SalesInvoiceListDto
            {
                Id = entity.Id,
                InvoiceNumber = entity.InvoiceNumber,
                InvoiceDate = entity.InvoiceDate,
                CustomerNameAr = counterpartyName,
                Status = entity.Status.ToString(),
                NetTotal = entity.NetTotal
            };
        }
    }
}
