namespace MarcoERP.Domain.Enums
{
    /// <summary>
    /// Payment method used in transactions.
    /// </summary>
    public enum PaymentMethod
    {
        /// <summary>نقدي.</summary>
        Cash = 0,

        /// <summary>بطاقة ائتمان/خصم مباشر.</summary>
        Card = 1,

        /// <summary>على الحساب (آجل).</summary>
        OnAccount = 2,

        /// <summary>شيك.</summary>
        Check = 3,

        /// <summary>تحويل بنكي.</summary>
        BankTransfer = 4,

        /// <summary>محفظة إلكترونية (فودافون كاش، إنستا باي، إلخ).</summary>
        EWallet = 5
    }
}
