using System;

namespace MarcoERP.Application.DTOs.Settings
{
    /// <summary>
    /// Represents a deleted record in the recycle bin.
    /// </summary>
    public sealed class DeletedRecordDto
    {
        public int Id { get; set; }
        public string EntityType { get; set; }
        public string EntityTypeArabic { get; set; }
        public string DisplayName { get; set; }
        public string Code { get; set; }
        public string DeletedBy { get; set; }
        public DateTime DeletedAt { get; set; }
        public bool CanRestore { get; set; }
        public string RestoreBlockReason { get; set; }
    }
}
