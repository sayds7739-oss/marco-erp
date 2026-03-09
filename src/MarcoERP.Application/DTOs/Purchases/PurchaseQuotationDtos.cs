using System;
using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Purchases
{
    // ═══════════════════════════════════════════════════════════
    //  Purchase Quotation DTOs (طلبات الشراء)
    // ═══════════════════════════════════════════════════════════

    /// <summary>Read-only DTO for purchase quotation display.</summary>
    public sealed class PurchaseQuotationDto
    {
        public int Id { get; set; }
        public string QuotationNumber { get; set; }
        public DateTime QuotationDate { get; set; }
        public DateTime ValidUntil { get; set; }
        public int SupplierId { get; set; }
        public string SupplierNameAr { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseNameAr { get; set; }
        public string Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal NetTotal { get; set; }
        public string Notes { get; set; }
        public int? ConvertedToInvoiceId { get; set; }
        public DateTime? ConvertedDate { get; set; }
        public List<PurchaseQuotationLineDto> Lines { get; set; } = new();
    }

    /// <summary>Read-only DTO for a purchase quotation line.</summary>
    public sealed class PurchaseQuotationLineDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductNameAr { get; set; }
        public string ProductCode { get; set; }
        public int UnitId { get; set; }
        public string UnitNameAr { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal ConversionFactor { get; set; }
        public decimal BaseQuantity { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal SubTotal { get; set; }
        public decimal NetTotal { get; set; }
        public decimal VatRate { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalWithVat { get; set; }
    }

    /// <summary>DTO for creating a new purchase quotation.</summary>
    public sealed class CreatePurchaseQuotationDto
    {
        public DateTime QuotationDate { get; set; }
        public DateTime ValidUntil { get; set; }
        public int SupplierId { get; set; }
        public int WarehouseId { get; set; }
        public string Notes { get; set; }
        public List<CreatePurchaseQuotationLineDto> Lines { get; set; } = new();
    }

    /// <summary>DTO for creating a purchase quotation line.</summary>
    public sealed class CreatePurchaseQuotationLineDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int UnitId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
    }

    /// <summary>DTO for updating a draft purchase quotation.</summary>
    public sealed class UpdatePurchaseQuotationDto
    {
        public int Id { get; set; }
        public DateTime QuotationDate { get; set; }
        public DateTime ValidUntil { get; set; }
        public int SupplierId { get; set; }
        public int WarehouseId { get; set; }
        public string Notes { get; set; }
        public List<CreatePurchaseQuotationLineDto> Lines { get; set; } = new();
    }

    /// <summary>Lightweight DTO for quotation list display.</summary>
    public sealed class PurchaseQuotationListDto
    {
        public int Id { get; set; }
        public string QuotationNumber { get; set; }
        public DateTime QuotationDate { get; set; }
        public DateTime ValidUntil { get; set; }
        public string SupplierNameAr { get; set; }
        public string Status { get; set; }
        public decimal NetTotal { get; set; }
    }
}
