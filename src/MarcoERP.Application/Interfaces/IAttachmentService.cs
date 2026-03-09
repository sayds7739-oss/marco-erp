using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;

namespace MarcoERP.Application.Interfaces
{
    /// <summary>
    /// Manages file attachments for any entity.
    /// </summary>
    public interface IAttachmentService
    {
        /// <summary>Gets all attachments for a specific entity.</summary>
        Task<ServiceResult<IReadOnlyList<AttachmentDto>>> GetAttachmentsAsync(string entityType, int entityId, CancellationToken ct = default);

        /// <summary>Uploads a file and creates an attachment record.</summary>
        Task<ServiceResult<AttachmentDto>> UploadAsync(string entityType, int entityId, string sourceFilePath, CancellationToken ct = default);

        /// <summary>Deletes an attachment (file + DB record).</summary>
        Task<ServiceResult> DeleteAsync(int attachmentId, CancellationToken ct = default);

        /// <summary>Opens an attachment file in the default system application.</summary>
        Task<ServiceResult> OpenAsync(int attachmentId, CancellationToken ct = default);
    }

    public sealed class AttachmentDto
    {
        public int Id { get; set; }
        public string EntityType { get; set; }
        public int EntityId { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }
        public string UploadedBy { get; set; }
        public System.DateTime UploadedAt { get; set; }

        /// <summary>Human-readable file size.</summary>
        public string FileSizeDisplay => FileSize switch
        {
            < 1024 => $"{FileSize} B",
            < 1024 * 1024 => $"{FileSize / 1024.0:F1} KB",
            _ => $"{FileSize / (1024.0 * 1024.0):F1} MB"
        };
    }
}
