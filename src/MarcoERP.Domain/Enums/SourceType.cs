namespace MarcoERP.Domain.Enums
{
    /// <summary>
    /// Identifies the origin of a journal entry.
    /// </summary>
    public enum SourceType
    {
        Manual = 0,
        SalesInvoice = 1,
        PurchaseInvoice = 2,
        CashReceipt = 3,
        CashPayment = 4,
        Inventory = 5,
        Adjustment = 6,
        Opening = 7,
        Closing = 8,
        PurchaseReturn = 9,
        SalesReturn = 10,
        CashTransfer = 11,
        SalesQuotation = 12,
        PurchaseQuotation = 13,
        PosSession = 14
    }
}
