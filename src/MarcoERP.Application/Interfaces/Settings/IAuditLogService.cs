using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;

namespace MarcoERP.Application.Interfaces.Settings
{
    /// <summary>
    /// خدمة عرض سجل المراجعة (Audit Log Viewer).
    /// Implementation: Persistence layer.
    /// </summary>
    public interface IAuditLogService
    {
        /// <summary>
        /// Returns all audit log records ordered by timestamp descending.
        /// </summary>
        [RequiresPermission(PermissionKeys.AuditLogView)]
        Task<ServiceResult<IReadOnlyList<AuditLogDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Returns audit log records for a specific entity type and entity id.
        /// </summary>
        [RequiresPermission(PermissionKeys.AuditLogView)]
        Task<ServiceResult<IReadOnlyList<AuditLogDto>>> GetByEntityAsync(string entityType, int entityId, CancellationToken ct = default);

        /// <summary>
        /// Returns audit log records within a date range.
        /// </summary>
        [RequiresPermission(PermissionKeys.AuditLogView)]
        Task<ServiceResult<IReadOnlyList<AuditLogDto>>> GetByDateRangeAsync(DateTime start, DateTime end, CancellationToken ct = default);

        /// <summary>
        /// Returns audit log records performed by a specific user.
        /// </summary>
        [RequiresPermission(PermissionKeys.AuditLogView)]
        Task<ServiceResult<IReadOnlyList<AuditLogDto>>> GetByUserAsync(string username, CancellationToken ct = default);
    }
}
