using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;

namespace MarcoERP.Application.Interfaces.Settings
{
    /// <summary>
    /// Service for viewing and managing soft-deleted records (recycle bin).
    /// </summary>
    public interface IRecycleBinService
    {
        /// <summary>
        /// Gets all deleted records across supported entity types.
        /// </summary>
        [RequiresPermission(PermissionKeys.RecycleBinView)]
        Task<ServiceResult<IReadOnlyList<DeletedRecordDto>>> GetAllDeletedAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets deleted records for a specific entity type.
        /// </summary>
        [RequiresPermission(PermissionKeys.RecycleBinView)]
        Task<ServiceResult<IReadOnlyList<DeletedRecordDto>>> GetByEntityTypeAsync(string entityType, CancellationToken ct = default);

        /// <summary>
        /// Restores a deleted record (sets IsDeleted = false).
        /// </summary>
        [RequiresPermission(PermissionKeys.RecycleBinRestore)]
        Task<ServiceResult> RestoreAsync(string entityType, int entityId, CancellationToken ct = default);

        /// <summary>
        /// Gets the list of supported entity types for the recycle bin.
        /// </summary>
        IReadOnlyList<(string Key, string ArabicName)> GetSupportedEntityTypes();
    }
}
