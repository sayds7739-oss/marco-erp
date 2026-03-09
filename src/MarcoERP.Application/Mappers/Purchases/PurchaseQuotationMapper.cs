using System.Linq;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Domain.Entities.Purchases;

namespace MarcoERP.Application.Mappers.Purchases
{
    public static class PurchaseQuotationMapper
    {
        public static PurchaseQuotationDto ToDto(PurchaseQuotation entity)
        {
            if (entity == null) return null;
            return new PurchaseQuotationDto
            {
                Id = entity.Id,
                QuotationNumber = entity.QuotationNumber,
                QuotationDate = entity.QuotationDate,
                ValidUntil = entity.ValidUntil,
                SupplierId = entity.SupplierId,
                SupplierNameAr = entity.Supplier?.NameAr,
                WarehouseId = entity.WarehouseId,
                WarehouseNameAr = entity.Warehouse?.NameAr,
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

        public static PurchaseQuotationLineDto ToLineDto(PurchaseQuotationLine line)
        {
            if (line == null) return null;
            return new PurchaseQuotationLineDto
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

        public static PurchaseQuotationListDto ToListDto(PurchaseQuotation entity)
        {
            if (entity == null) return null;
            return new PurchaseQuotationListDto
            {
                Id = entity.Id,
                QuotationNumber = entity.QuotationNumber,
                QuotationDate = entity.QuotationDate,
                ValidUntil = entity.ValidUntil,
                SupplierNameAr = entity.Supplier?.NameAr,
                Status = entity.Status.ToString(),
                NetTotal = entity.NetTotal
            };
        }
    }
}
