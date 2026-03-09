using System;

namespace MarcoERP.Application.DTOs.Treasury
{
    // ════════════════════════════════════════════════════════════
    //  CashTransfer DTOs (تحويل بين الخزن)
    // ════════════════════════════════════════════════════════════

    /// <summary>Full cash transfer details.</summary>
    public sealed class CashTransferDto
    {
        public int Id { get; set; }
        public string TransferNumber { get; set; }
        public DateTime TransferDate { get; set; }
        public int SourceCashboxId { get; set; }
        public string SourceCashboxName { get; set; }
        public int TargetCashboxId { get; set; }
        public string TargetCashboxName { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public string Notes { get; set; }
        public string Status { get; set; }
        public int? JournalEntryId { get; set; }
        public string WarningMessage { get; set; }
    }

    /// <summary>DTO for creating a new cash transfer.</summary>
    public sealed class CreateCashTransferDto
    {
        public DateTime TransferDate { get; set; }
        public int SourceCashboxId { get; set; }
        public int TargetCashboxId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>DTO for updating an existing cash transfer.</summary>
    public sealed class UpdateCashTransferDto
    {
        public int Id { get; set; }
        public DateTime TransferDate { get; set; }
        public int SourceCashboxId { get; set; }
        public int TargetCashboxId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>Lightweight DTO for list views.</summary>
    public sealed class CashTransferListDto
    {
        public int Id { get; set; }
        public string TransferNumber { get; set; }
        public DateTime TransferDate { get; set; }
        public string SourceCashboxName { get; set; }
        public string TargetCashboxName { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
    }
}
