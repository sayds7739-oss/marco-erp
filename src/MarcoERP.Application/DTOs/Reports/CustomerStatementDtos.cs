using System;
using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Reports
{
    public sealed class CustomerStatementRequestDto
    {
        public int CustomerId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    public sealed class CustomerStatementDto
    {
        public string CustomerNameAr { get; set; }
        public string CustomerPhone { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal ClosingBalance { get; set; }
        public List<CustomerStatementLineDto> Lines { get; set; } = new();
    }

    public sealed class CustomerStatementLineDto
    {
        public DateTime Date { get; set; }
        public string DocumentType { get; set; }
        public string DocumentNumber { get; set; }
        public string Description { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
    }
}
