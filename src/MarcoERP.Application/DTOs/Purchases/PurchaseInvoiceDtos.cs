using System;
using System.Collections.Generic;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.DTOs.Purchases
{
    // ═══════════════════════════════════════════════════════════
    //  Purchase Invoice DTOs
    // ═══════════════════════════════════════════════════════════

    /// <summary>Read-only DTO for purchase invoice display.</summary>
    public sealed class PurchaseInvoiceDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public int? SupplierId { get; set; }
        public string SupplierNameAr { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseNameAr { get; set; }
        public string Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal NetTotal { get; set; }
        public string Notes { get; set; }
        public int? JournalEntryId { get; set; }
        public int? SalesRepresentativeId { get; set; }
        public CounterpartyType CounterpartyType { get; set; }
        public int? CounterpartyCustomerId { get; set; }
        public string CounterpartyCustomerNameAr { get; set; }
        public List<PurchaseInvoiceLineDto> Lines { get; set; } = new();
    }

    /// <summary>Read-only DTO for a purchase invoice line.</summary>
    public sealed class PurchaseInvoiceLineDto
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

    /// <summary>DTO for creating a new purchase invoice (header + lines).</summary>
    public sealed class CreatePurchaseInvoiceDto
    {
        public DateTime InvoiceDate { get; set; }
        public int? SupplierId { get; set; }
        public int WarehouseId { get; set; }
        public string Notes { get; set; }
        public int? SalesRepresentativeId { get; set; }
        public CounterpartyType CounterpartyType { get; set; }
        public int? CounterpartyCustomerId { get; set; }
        public List<CreatePurchaseInvoiceLineDto> Lines { get; set; } = new();
    }

    /// <summary>DTO for creating a purchase invoice line.</summary>
    public sealed class CreatePurchaseInvoiceLineDto
    {
        public int ProductId { get; set; }
        public int UnitId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
    }

    /// <summary>DTO for updating a draft purchase invoice.</summary>
    public sealed class UpdatePurchaseInvoiceDto
    {
        public int Id { get; set; }
        public DateTime InvoiceDate { get; set; }
        public int? SupplierId { get; set; }
        public int WarehouseId { get; set; }
        public string Notes { get; set; }
        public int? SalesRepresentativeId { get; set; }
        public CounterpartyType CounterpartyType { get; set; }
        public int? CounterpartyCustomerId { get; set; }
        public List<CreatePurchaseInvoiceLineDto> Lines { get; set; } = new();
    }

    /// <summary>Lightweight DTO for invoice list display.</summary>
    public sealed class PurchaseInvoiceListDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string SupplierNameAr { get; set; }
        public string Status { get; set; }
        public decimal NetTotal { get; set; }
    }
}
