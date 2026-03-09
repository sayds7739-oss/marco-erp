using System;
using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Settings
{
    public sealed class DataPurgeOptionsDto
    {
        public bool KeepCustomers { get; set; } = true;
        public bool KeepSuppliers { get; set; } = true;
        public bool KeepProducts { get; set; } = true;
        public bool KeepSalesRepresentatives { get; set; } = true;
    }

    public sealed class DataPurgeItemResultDto
    {
        public string EntityName { get; set; } = string.Empty;
        public int DeletedRows { get; set; }
    }

    public sealed class DataPurgeResultDto
    {
        public DateTime ExecutedAtUtc { get; set; }
        public int TotalDeletedRows { get; set; }
        public IReadOnlyList<DataPurgeItemResultDto> Items { get; set; } = Array.Empty<DataPurgeItemResultDto>();
    }
}
