namespace MarcoERP.Application.DTOs.Treasury
{
    // ════════════════════════════════════════════════════════════
    //  Cashbox DTOs
    // ════════════════════════════════════════════════════════════

    /// <summary>Full cashbox details.</summary>
    public sealed class CashboxDto
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string NameAr { get; set; }
        public string NameEn { get; set; }
        public int? AccountId { get; set; }
        public string AccountName { get; set; }
        public decimal Balance { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
    }

    /// <summary>DTO for creating a new cashbox.</summary>
    public sealed class CreateCashboxDto
    {
        public string NameAr { get; set; }
        public string NameEn { get; set; }
        public int? AccountId { get; set; }
    }

    /// <summary>DTO for updating an existing cashbox.</summary>
    public sealed class UpdateCashboxDto
    {
        public int Id { get; set; }
        public string NameAr { get; set; }
        public string NameEn { get; set; }
        public int? AccountId { get; set; }
    }
}
