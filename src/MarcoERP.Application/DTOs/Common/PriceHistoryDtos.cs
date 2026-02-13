using System;

namespace MarcoERP.Application.DTOs.Common
{
    /// <summary>
    /// Lightweight DTO for recent unit price history.
    /// </summary>
    public sealed class PriceHistoryRowDto
    {
        public DateTime InvoiceDate { get; set; }
        public string DocumentNumber { get; set; }
        public string CounterpartyNameAr { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
    }
}
