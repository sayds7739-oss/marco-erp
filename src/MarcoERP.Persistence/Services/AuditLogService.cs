using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;

namespace MarcoERP.Persistence.Services
{
    /// <summary>
    /// Queries the AuditLogs table for the Audit Log Viewer (D.1).
    /// Read-only service — no mutations.
    /// </summary>
    public sealed class AuditLogService : IAuditLogService
    {
        private readonly MarcoDbContext _db;
        private readonly ICurrentUserService _currentUser;

        public AuditLogService(MarcoDbContext db, ICurrentUserService currentUser)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<ServiceResult<IReadOnlyList<AuditLogDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var logs = await _db.AuditLogs
                .AsNoTracking()
                .OrderByDescending(a => a.Timestamp)
                .Select(a => MapToDto(a))
                .ToListAsync(ct);

            return ServiceResult<IReadOnlyList<AuditLogDto>>.Success(logs);
        }

        public async Task<ServiceResult<IReadOnlyList<AuditLogDto>>> GetByEntityAsync(string entityType, int entityId, CancellationToken ct = default)
        {
            var logs = await _db.AuditLogs
                .AsNoTracking()
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .OrderByDescending(a => a.Timestamp)
                .Select(a => MapToDto(a))
                .ToListAsync(ct);

            return ServiceResult<IReadOnlyList<AuditLogDto>>.Success(logs);
        }

        public async Task<ServiceResult<IReadOnlyList<AuditLogDto>>> GetByDateRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
        {
            var logs = await _db.AuditLogs
                .AsNoTracking()
                .Where(a => a.Timestamp >= start && a.Timestamp <= end)
                .OrderByDescending(a => a.Timestamp)
                .Select(a => MapToDto(a))
                .ToListAsync(ct);

            return ServiceResult<IReadOnlyList<AuditLogDto>>.Success(logs);
        }

        public async Task<ServiceResult<IReadOnlyList<AuditLogDto>>> GetByUserAsync(string username, CancellationToken ct = default)
        {
            var logs = await _db.AuditLogs
                .AsNoTracking()
                .Where(a => a.PerformedBy == username)
                .OrderByDescending(a => a.Timestamp)
                .Select(a => MapToDto(a))
                .ToListAsync(ct);

            return ServiceResult<IReadOnlyList<AuditLogDto>>.Success(logs);
        }

        private static AuditLogDto MapToDto(Domain.Entities.Accounting.AuditLog a)
        {
            return new AuditLogDto
            {
                Id = a.Id,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Action = a.Action,
                PerformedBy = a.PerformedBy,
                Details = a.Details,
                Timestamp = a.Timestamp,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                ChangedColumns = a.ChangedColumns
            };
        }
    }
}
