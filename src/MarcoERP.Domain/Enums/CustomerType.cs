namespace MarcoERP.Domain.Enums
{
    /// <summary>
    /// نوع العميل (للفواتير الضريبية والسجل التجاري).
    /// </summary>
    public enum CustomerType
    {
        /// <summary>فرد / شخص طبيعي.</summary>
        Individual = 0,

        /// <summary>شركة / كيان اعتباري.</summary>
        Company = 1,

        /// <summary>جهة حكومية.</summary>
        Government = 2
    }
}
