namespace MarcoERP.Domain.Enums
{
    /// <summary>
    /// نوع بند في الأرصدة الافتتاحية.
    /// يحدد المصدر الفرعي للرصيد الافتتاحي.
    /// </summary>
    public enum OpeningBalanceLineType
    {
        /// <summary>حساب عام (دفتر أستاذ) — رصيد حساب مباشر.</summary>
        Account = 0,

        /// <summary>عميل — ذمم مدينة (مدين).</summary>
        Customer = 1,

        /// <summary>مورد — ذمم دائنة (دائن).</summary>
        Supplier = 2,

        /// <summary>مخزون — كمية × تكلفة الوحدة = قيمة المخزون.</summary>
        Inventory = 3,

        /// <summary>صندوق نقدي — نقدية في الخزنة.</summary>
        Cashbox = 4,

        /// <summary>حساب بنكي — رصيد بنك.</summary>
        BankAccount = 5
    }
}
