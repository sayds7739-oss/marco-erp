using System;
using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Reports
{
    public sealed class JournalRegisterRequestDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    public sealed class JournalRegisterDto
    {
        public int EntryCount { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public List<JournalRegisterRowDto> Rows { get; set; } = new();
    }

    public sealed class JournalRegisterRowDto
    {
        public int JournalEntryId { get; set; }
        public string EntryNumber { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public string Status { get; set; }
        public string SourceType { get; set; }
    }
}
