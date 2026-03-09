namespace MarcoERP.Application.DTOs.Printing
{
    /// <summary>
    /// Enumeration of all printable document types in the system.
    /// </summary>
    public enum PrintableDocumentType
    {
        SalesInvoice,
        PurchaseInvoice,
        SalesReturn,
        PurchaseReturn,
        SalesQuotation,
        PurchaseQuotation,
        CashReceipt,
        CashPayment,
        CashTransfer,
        JournalEntry,
        PriceList,
        PosReceipt
    }
}
