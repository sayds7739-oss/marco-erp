namespace MarcoERP.Domain.Enums
{
    /// <summary>
    /// حالة الأرصدة الافتتاحية.
    /// Draft → Posted (أحادي الاتجاه — حماية مالية).
    /// </summary>
    public enum OpeningBalanceStatus
    {
        /// <summary>مسودة — قابلة للتعديل.</summary>
        Draft = 0,

        /// <summary>مرحّلة — لا يمكن تعديلها.</summary>
        Posted = 1
    }
}
