using System;
using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Sales
{
    // ═══════════════════════════════════════════════════════════
    //  Sales Quotation DTOs (عروض أسعار البيع)
    // ═══════════════════════════════════════════════════════════

    /// <summary>Read-only DTO for sales quotation display.</summary>
    public sealed class SalesQuotationDto
    {
        public int Id { get; set; }
        public string QuotationNumber { get; set; }
        public DateTime QuotationDate { get; set; }
        public DateTime ValidUntil { get; set; }
        public int CustomerId { get; set; }
        public string CustomerNameAr { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseNameAr { get; set; }
        public int? SalesRepresentativeId { get; set; }
        public string Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal NetTotal { get; set; }
        public string Notes { get; set; }
        public int? ConvertedToInvoiceId { get; set; }
        public DateTime? ConvertedDate { get; set; }
        public List<SalesQuotationLineDto> Lines { get; set; } = new();
    }

    /// <summary>Read-only DTO for a sales quotation line.</summary>
    public sealed class SalesQuotationLineDto
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

    /// <summary>DTO for creating a new sales quotation.</summary>
    public sealed class CreateSalesQuotationDto
    {
        public DateTime QuotationDate { get; set; }
        public DateTime ValidUntil { get; set; }
        public int CustomerId { get; set; }
        public int WarehouseId { get; set; }
        public int? SalesRepresentativeId { get; set; }
        public string Notes { get; set; }
        public List<CreateSalesQuotationLineDto> Lines { get; set; } = new();
    }

    /// <summary>DTO for creating a sales quotation line.</summary>
    public sealed class CreateSalesQuotationLineDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int UnitId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
    }

    /// <summary>DTO for updating a draft sales quotation.</summary>
    public sealed class UpdateSalesQuotationDto
    {
        public int Id { get; set; }
        public DateTime QuotationDate { get; set; }
        public DateTime ValidUntil { get; set; }
        public int CustomerId { get; set; }
        public int WarehouseId { get; set; }
        public int? SalesRepresentativeId { get; set; }
        public string Notes { get; set; }
        public List<CreateSalesQuotationLineDto> Lines { get; set; } = new();
    }

    /// <summary>Lightweight DTO for quotation list display.</summary>
    public sealed class SalesQuotationListDto
    {
        public int Id { get; set; }
        public string QuotationNumber { get; set; }
        public DateTime QuotationDate { get; set; }
        public DateTime ValidUntil { get; set; }
        public string CustomerNameAr { get; set; }
        public string Status { get; set; }
        public decimal NetTotal { get; set; }
    }
}
