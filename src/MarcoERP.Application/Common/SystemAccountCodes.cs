namespace MarcoERP.Application.Common
{
    /// <summary>
    /// Centralized GL account code constants used across all posting services.
    /// Prevents magic strings scattered across PosService, SalesInvoiceService, etc.
    /// </summary>
    public static class SystemAccountCodes
    {
        public const string Cash                 = "1111";
        public const string Card                 = "1112";
        public const string AccountsReceivable   = "1121";
        public const string Inventory            = "1131";
        public const string VatInput             = "1141";
        public const string AccountsPayable      = "2111";
        public const string VatOutput            = "2121";
        public const string SalesRevenue         = "4111";
        public const string Cogs                 = "5111";
        public const string CommissionPayable    = "2301";
        public const string RetainedEarnings     = "3121";
        public const string CommissionExpense    = "6201";
        public const string CashOverShort        = "7201";
    }
}
