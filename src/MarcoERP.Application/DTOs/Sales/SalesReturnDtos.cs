using System;
using System.Collections.Generic;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.DTOs.Sales
{
    // ═══════════════════════════════════════════════════════════
    //  Sales Return DTOs
    // ═══════════════════════════════════════════════════════════

    /// <summary>Read-only DTO for sales return display.</summary>
    public sealed class SalesReturnDto
    {
        public int Id { get; set; }
        public string ReturnNumber { get; set; }
        public DateTime ReturnDate { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerNameAr { get; set; }
        public CounterpartyType CounterpartyType { get; set; }
        public int? SupplierId { get; set; }
        public string SupplierNameAr { get; set; }
        public int? SalesRepresentativeId { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseNameAr { get; set; }
        public int? OriginalInvoiceId { get; set; }
        public string OriginalInvoiceNumber { get; set; }
        public string Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal NetTotal { get; set; }
        public string Notes { get; set; }
        public int? JournalEntryId { get; set; }
        public int? CogsJournalEntryId { get; set; }
        public List<SalesReturnLineDto> Lines { get; set; } = new();
    }

    /// <summary>Read-only DTO for a sales return line.</summary>
    public sealed class SalesReturnLineDto
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

    /// <summary>DTO for creating a new sales return.</summary>
    public sealed class CreateSalesReturnDto
    {
        public DateTime ReturnDate { get; set; }
        public int? CustomerId { get; set; }
        public CounterpartyType CounterpartyType { get; set; }
        public int? SupplierId { get; set; }
        public int? SalesRepresentativeId { get; set; }
        public int WarehouseId { get; set; }
        public int? OriginalInvoiceId { get; set; }
        public string Notes { get; set; }
        public List<CreateSalesReturnLineDto> Lines { get; set; } = new();
    }

    /// <summary>DTO for creating a sales return line.</summary>
    public sealed class CreateSalesReturnLineDto
    {
        public int ProductId { get; set; }
        public int UnitId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
    }

    /// <summary>DTO for updating a draft sales return.</summary>
    public sealed class UpdateSalesReturnDto
    {
        public int Id { get; set; }
        public DateTime ReturnDate { get; set; }
        public int? CustomerId { get; set; }
        public CounterpartyType CounterpartyType { get; set; }
        public int? SupplierId { get; set; }
        public int? SalesRepresentativeId { get; set; }
        public int WarehouseId { get; set; }
        public int? OriginalInvoiceId { get; set; }
        public string Notes { get; set; }
        public List<CreateSalesReturnLineDto> Lines { get; set; } = new();
    }

    /// <summary>Lightweight DTO for return list display.</summary>
    public sealed class SalesReturnListDto
    {
        public int Id { get; set; }
        public string ReturnNumber { get; set; }
        public DateTime ReturnDate { get; set; }
        public string CustomerNameAr { get; set; }
        public string Status { get; set; }
        public decimal NetTotal { get; set; }
    }
}
