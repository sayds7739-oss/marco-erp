namespace MarcoERP.Domain.Enums
{
    /// <summary>
    /// نوع الفاتورة من حيث السداد.
    /// </summary>
    public enum InvoiceType
    {
        /// <summary>فاتورة نقدية - يتم السداد فوراً.</summary>
        Cash = 0,

        /// <summary>فاتورة آجلة - يتم السداد لاحقاً.</summary>
        Credit = 1
    }
}
