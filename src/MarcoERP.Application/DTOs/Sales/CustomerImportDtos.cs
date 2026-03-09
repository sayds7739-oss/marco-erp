using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Sales
{
    /// <summary>
    /// Represents a single row from an Excel import file for customers, pre-validation.
    /// </summary>
    public sealed class CustomerImportRowDto
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
        public decimal CreditLimit { get; set; }
        public decimal DefaultDiscountPercent { get; set; }
        public string CustomerTypeName { get; set; }
        public string Notes { get; set; }

        // ── Validation State ──
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Result of a batch customer import operation.
    /// </summary>
    public sealed class CustomerImportResultDto
    {
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<CustomerImportRowDto> FailedRows { get; set; } = new();
        public List<string> GeneralErrors { get; set; } = new();
    }
}
