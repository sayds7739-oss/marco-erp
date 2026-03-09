using System.Linq;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.Mappers.Purchases
{
    /// <summary>
    /// Manual mapper for PurchaseInvoice entities ↔ DTOs.
    /// </summary>
    public static class PurchaseInvoiceMapper
    {
        /// <summary>Maps PurchaseInvoice entity → PurchaseInvoiceDto (with lines).</summary>
        public static PurchaseInvoiceDto ToDto(PurchaseInvoice entity)
        {
            if (entity == null) return null;

            return new PurchaseInvoiceDto
            {
                Id = entity.Id,
                InvoiceNumber = entity.InvoiceNumber,
                InvoiceDate = entity.InvoiceDate,
                SupplierId = entity.SupplierId,
                SupplierNameAr = entity.Supplier?.NameAr,
                WarehouseId = entity.WarehouseId,
                WarehouseNameAr = entity.Warehouse?.NameAr,
                Status = entity.Status.ToString(),
                Subtotal = entity.Subtotal,
                DiscountTotal = entity.DiscountTotal,
                VatTotal = entity.VatTotal,
                NetTotal = entity.NetTotal,
                HeaderDiscountPercent = entity.HeaderDiscountPercent,
                HeaderDiscountAmount = entity.HeaderDiscountAmount,
                DeliveryFee = entity.DeliveryFee,
                Notes = entity.Notes,
                JournalEntryId = entity.JournalEntryId,
                SalesRepresentativeId = entity.SalesRepresentativeId,
                CounterpartyType = entity.CounterpartyType,
                CounterpartyCustomerId = entity.CounterpartyCustomerId,
                CounterpartyCustomerNameAr = entity.CounterpartyCustomer?.NameAr,
                InvoiceType = entity.InvoiceType,
                PaymentMethod = entity.PaymentMethod,
                DueDate = entity.DueDate,
                PaidAmount = entity.PaidAmount,
                BalanceDue = entity.BalanceDue,
                PaymentStatus = entity.PaymentStatus.ToString(),
                Lines = entity.Lines?.Select(ToLineDto).ToList() ?? new()
            };
        }

        /// <summary>Maps PurchaseInvoiceLine entity → PurchaseInvoiceLineDto.</summary>
        public static PurchaseInvoiceLineDto ToLineDto(PurchaseInvoiceLine line)
        {
            if (line == null) return null;

            return new PurchaseInvoiceLineDto
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
        public static PurchaseInvoiceListDto ToListDto(PurchaseInvoice entity)
        {
            if (entity == null) return null;

            var counterpartyName = entity.CounterpartyType == CounterpartyType.Customer
                ? entity.CounterpartyCustomer?.NameAr
                : entity.Supplier?.NameAr;

            return new PurchaseInvoiceListDto
            {
                Id = entity.Id,
                InvoiceNumber = entity.InvoiceNumber,
                InvoiceDate = entity.InvoiceDate,
                SupplierNameAr = counterpartyName,
                Status = entity.Status.ToString(),
                InvoiceType = entity.InvoiceType,
                PaymentMethod = entity.PaymentMethod,
                DueDate = entity.DueDate,
                NetTotal = entity.NetTotal,
                PaidAmount = entity.PaidAmount,
                BalanceDue = entity.BalanceDue,
                PaymentStatus = entity.PaymentStatus.ToString()
            };
        }
    }
}
