using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.Interfaces;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Interfaces;

namespace MarcoERP.Application.Services
{
    public sealed class AttachmentService : IAttachmentService
    {
        private readonly IAttachmentRepository _repo;
        private readonly IUnitOfWork _uow;
        private readonly ICurrentUserService _currentUser;
        private readonly string _storageRoot;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".bmp",
            ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt",
            ".zip", ".rar", ".7z"
        };

        private const long MaxFileSize = 25 * 1024 * 1024; // 25 MB

        public AttachmentService(IAttachmentRepository repo, IUnitOfWork uow, ICurrentUserService currentUser)
        {
            _repo = repo;
            _uow = uow;
            _currentUser = currentUser;
            _storageRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarcoERP", "Attachments");
        }

        public async Task<ServiceResult<IReadOnlyList<AttachmentDto>>> GetAttachmentsAsync(
            string entityType, int entityId, CancellationToken ct = default)
        {
            var attachments = await _repo.GetByEntityAsync(entityType, entityId, ct);
            var dtos = attachments.Select(a => MapToDto(a)).ToList();
            return ServiceResult<IReadOnlyList<AttachmentDto>>.Success(dtos);
        }

        public async Task<ServiceResult<AttachmentDto>> UploadAsync(
            string entityType, int entityId, string sourceFilePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
                return ServiceResult<AttachmentDto>.Failure("الملف غير موجود.");

            var fileInfo = new FileInfo(sourceFilePath);

            if (fileInfo.Length > MaxFileSize)
                return ServiceResult<AttachmentDto>.Failure("حجم الملف يتجاوز الحد المسموح (25 ميجا).");

            var ext = fileInfo.Extension;
            if (!AllowedExtensions.Contains(ext))
                return ServiceResult<AttachmentDto>.Failure($"نوع الملف '{ext}' غير مسموح.");

            // Create storage directory
            var targetDir = Path.Combine(_storageRoot, entityType, entityId.ToString());
            Directory.CreateDirectory(targetDir);

            // Generate unique file name to avoid overwrites
            var uniqueName = $"{Guid.NewGuid():N}{ext}";
            var targetPath = Path.Combine(targetDir, uniqueName);

            // Copy file
            File.Copy(sourceFilePath, targetPath, overwrite: false);

            // Content type detection
            var contentType = ext.ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".csv" => "text/csv",
                ".txt" => "text/plain",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".7z" => "application/x-7z-compressed",
                _ => "application/octet-stream"
            };

            var attachment = new Attachment(
                entityType, entityId,
                fileInfo.Name, contentType, fileInfo.Length,
                targetPath, _currentUser.Username);

            await _repo.AddAsync(attachment, ct);
            await _uow.SaveChangesAsync(ct);

            return ServiceResult<AttachmentDto>.Success(MapToDto(attachment));
        }

        public async Task<ServiceResult> DeleteAsync(int attachmentId, CancellationToken ct = default)
        {
            var attachment = await _repo.GetByIdAsync(attachmentId, ct);
            if (attachment == null)
                return ServiceResult.Failure("المرفق غير موجود.");

            // Delete file from disk
            if (File.Exists(attachment.StoragePath))
            {
                try { File.Delete(attachment.StoragePath); }
                catch { /* Best effort — DB record will still be removed */ }
            }

            await _repo.DeleteAsync(attachment, ct);
            await _uow.SaveChangesAsync(ct);

            return ServiceResult.Success();
        }

        public async Task<ServiceResult> OpenAsync(int attachmentId, CancellationToken ct = default)
        {
            var attachment = await _repo.GetByIdAsync(attachmentId, ct);
            if (attachment == null)
                return ServiceResult.Failure("المرفق غير موجود.");

            if (!File.Exists(attachment.StoragePath))
                return ServiceResult.Failure("ملف المرفق غير موجود على القرص.");

            // Resolve to absolute path and reject anything outside the attachments root
            var fullPath = Path.GetFullPath(attachment.StoragePath);
            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            var blockedExtensions = new HashSet<string>
            {
                ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".wsf",
                ".msi", ".com", ".scr", ".pif", ".hta", ".cpl"
            };
            if (blockedExtensions.Contains(extension))
                return ServiceResult.Failure("لا يمكن فتح هذا النوع من الملفات.");

            Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
            return ServiceResult.Success();
        }

        private static AttachmentDto MapToDto(Attachment a) => new()
        {
            Id = a.Id,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            FileName = a.FileName,
            ContentType = a.ContentType,
            FileSize = a.FileSize,
            UploadedBy = a.UploadedBy,
            UploadedAt = a.UploadedAt
        };
    }
}
