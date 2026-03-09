using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Purchases
{
    /// <summary>
    /// Represents a single row from an Excel supplier import file, pre-validation.
    /// </summary>
    public sealed class SupplierImportRowDto
    {
        /// <summary>Excel row number (1-based, excluding header).</summary>
        public int RowNumber { get; set; }

        // ── Fields ──
        public string Code { get; set; }
        public string NameAr { get; set; }
        public string NameEn { get; set; }
        public string Phone { get; set; }
        public string Mobile { get; set; }
        public string Email { get; set; }
        public string TaxNumber { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public int PaymentTermDays { get; set; }
        public string Notes { get; set; }

        // ── Validation State ──
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Result of a batch supplier import operation.
    /// </summary>
    public sealed class SupplierImportResultDto
    {
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<SupplierImportRowDto> FailedRows { get; set; } = new();
        public List<string> GeneralErrors { get; set; } = new();
    }
}
