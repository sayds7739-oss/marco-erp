using System;
using MarcoERP.Domain.Entities.Common;

namespace MarcoERP.Domain.Entities.Common
{
    /// <summary>
    /// File attachment linked to any entity by EntityType + EntityId.
    /// </summary>
    public sealed class Attachment : AuditableEntity
    {
        private Attachment() { }

        public Attachment(string entityType, int entityId, string fileName, string contentType,
            long fileSize, string storagePath, string uploadedBy)
        {
            if (string.IsNullOrWhiteSpace(entityType))
                throw new ArgumentException("EntityType is required.", nameof(entityType));
            if (entityId <= 0)
                throw new ArgumentException("EntityId must be positive.", nameof(entityId));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("FileName is required.", nameof(fileName));
            if (string.IsNullOrWhiteSpace(storagePath))
                throw new ArgumentException("StoragePath is required.", nameof(storagePath));

            EntityType = entityType.Trim();
            EntityId = entityId;
            FileName = fileName.Trim();
            ContentType = contentType?.Trim();
            FileSize = fileSize;
            StoragePath = storagePath.Trim();
            UploadedBy = uploadedBy?.Trim();
            UploadedAt = DateTime.UtcNow;
        }

        /// <summary>The type of parent entity (e.g., "SalesInvoice", "PurchaseInvoice").</summary>
        public string EntityType { get; private set; }

        /// <summary>The Id of the parent entity.</summary>
        public int EntityId { get; private set; }

        /// <summary>Original file name.</summary>
        public string FileName { get; private set; }

        /// <summary>MIME content type.</summary>
        public string ContentType { get; private set; }

        /// <summary>File size in bytes.</summary>
        public long FileSize { get; private set; }

        /// <summary>Full path where the file is stored on disk.</summary>
        public string StoragePath { get; private set; }

        /// <summary>User who uploaded the file.</summary>
        public string UploadedBy { get; private set; }

        /// <summary>When the file was uploaded.</summary>
        public DateTime UploadedAt { get; private set; }
    }
}
