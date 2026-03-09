using System;
using System.Collections.Generic;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.DTOs.Purchases
{
    // ═══════════════════════════════════════════════════════════
    //  Purchase Return DTOs
    // ═══════════════════════════════════════════════════════════

    /// <summary>Read-only DTO for purchase return display.</summary>
    public sealed class PurchaseReturnDto
    {
        public int Id { get; set; }
        public string ReturnNumber { get; set; }
        public DateTime ReturnDate { get; set; }
        public int? SupplierId { get; set; }
        public string SupplierNameAr { get; set; }
        public CounterpartyType CounterpartyType { get; set; }
        public int? CounterpartyCustomerId { get; set; }
        public string CounterpartyCustomerNameAr { get; set; }
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
        public List<PurchaseReturnLineDto> Lines { get; set; } = new();
    }

    /// <summary>Read-only DTO for a purchase return line.</summary>
    public sealed class PurchaseReturnLineDto
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

    /// <summary>DTO for creating a new purchase return.</summary>
    public sealed class CreatePurchaseReturnDto
    {
        public DateTime ReturnDate { get; set; }
        public int? SupplierId { get; set; }
        public CounterpartyType CounterpartyType { get; set; }
        public int? CounterpartyCustomerId { get; set; }
        public int? SalesRepresentativeId { get; set; }
        public int WarehouseId { get; set; }
        public int? OriginalInvoiceId { get; set; }
        public string Notes { get; set; }
        public List<CreatePurchaseReturnLineDto> Lines { get; set; } = new();
    }

    /// <summary>DTO for creating a purchase return line.</summary>
    public sealed class CreatePurchaseReturnLineDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int UnitId { get; set; }
        public decimal Quantity { get; set; } = 1m;
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
    }

    /// <summary>DTO for updating a draft purchase return.</summary>
    public sealed class UpdatePurchaseReturnDto
    {
        public int Id { get; set; }
        public DateTime ReturnDate { get; set; }
        public int? SupplierId { get; set; }
        public CounterpartyType CounterpartyType { get; set; }
        public int? CounterpartyCustomerId { get; set; }
        public int? SalesRepresentativeId { get; set; }
        public int WarehouseId { get; set; }
        public int? OriginalInvoiceId { get; set; }
        public string Notes { get; set; }
        public List<CreatePurchaseReturnLineDto> Lines { get; set; } = new();
    }

    /// <summary>Lightweight DTO for return list display.</summary>
    public sealed class PurchaseReturnListDto
    {
        public int Id { get; set; }
        public string ReturnNumber { get; set; }
        public DateTime ReturnDate { get; set; }
        public string SupplierNameAr { get; set; }
        public string Status { get; set; }
        public decimal NetTotal { get; set; }
    }
}
