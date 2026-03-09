using System.Linq;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Domain.Entities.Sales;

namespace MarcoERP.Application.Mappers.Sales
{
    public static class SalesQuotationMapper
    {
        public static SalesQuotationDto ToDto(SalesQuotation entity)
        {
            if (entity == null) return null;
            return new SalesQuotationDto
            {
                Id = entity.Id,
                QuotationNumber = entity.QuotationNumber,
                QuotationDate = entity.QuotationDate,
                ValidUntil = entity.ValidUntil,
                CustomerId = entity.CustomerId,
                CustomerNameAr = entity.Customer?.NameAr,
                WarehouseId = entity.WarehouseId,
                SalesRepresentativeId = entity.SalesRepresentativeId,
                Status = entity.Status.ToString(),
                Subtotal = entity.Subtotal,
                DiscountTotal = entity.DiscountTotal,
                VatTotal = entity.VatTotal,
                NetTotal = entity.NetTotal,
                Notes = entity.Notes,
                ConvertedToInvoiceId = entity.ConvertedToInvoiceId,
                ConvertedDate = entity.ConvertedDate,
                Lines = entity.Lines?.Select(ToLineDto).ToList() ?? new()
            };
        }

        public static SalesQuotationLineDto ToLineDto(SalesQuotationLine line)
        {
            if (line == null) return null;
            return new SalesQuotationLineDto
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

        public static SalesQuotationListDto ToListDto(SalesQuotation entity)
        {
            if (entity == null) return null;
            return new SalesQuotationListDto
            {
                Id = entity.Id,
                QuotationNumber = entity.QuotationNumber,
                QuotationDate = entity.QuotationDate,
                ValidUntil = entity.ValidUntil,
                CustomerNameAr = entity.Customer?.NameAr,
                Status = entity.Status.ToString(),
                NetTotal = entity.NetTotal
            };
        }
    }
}
