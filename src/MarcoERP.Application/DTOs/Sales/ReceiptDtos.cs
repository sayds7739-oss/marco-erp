using System;
using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Sales
{
    public sealed class ReceiptDto
    {
        public string InvoiceNumber { get; set; }
        public DateTime DateTime { get; set; }
        public List<ReceiptItemDto> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal Vat { get; set; }
        public decimal NetTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Change { get; set; }
        public string CashierName { get; set; }
    }

    public sealed class ReceiptItemDto
    {
        public string Name { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }
}
