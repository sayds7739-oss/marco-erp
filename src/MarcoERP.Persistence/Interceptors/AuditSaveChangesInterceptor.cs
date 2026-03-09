using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Application.Interfaces;

namespace MarcoERP.Persistence.Interceptors
{
    /// <summary>
    /// EF Core SaveChanges interceptor that automatically populates audit fields
    /// (CreatedAt, CreatedBy, ModifiedAt, ModifiedBy) on entities inheriting AuditableEntity.
    /// AUD-02: Also creates AuditLog records with OldValues/NewValues/ChangedColumns
    /// for all Create, Update, and SoftDelete operations per DATABASE_POLICY.
    /// Runs inside the same transaction as the business operation (TRX-INT-05).
    /// </summary>
    public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IDateTimeProvider _dateTimeProvider;

        // P-01 fix: Use AsyncLocal to prevent thread-safety issues under concurrent SaveChanges
        private static readonly AsyncLocal<List<AuditEntry>> _asyncPendingEntries = new();

        private static List<AuditEntry> _pendingEntries
        {
            get => _asyncPendingEntries.Value ??= new List<AuditEntry>();
        }

        public AuditSaveChangesInterceptor(
            ICurrentUserService currentUserService,
            IDateTimeProvider dateTimeProvider)
        {
            _currentUserService = currentUserService;
            _dateTimeProvider = dateTimeProvider;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            ApplyAuditFields(eventData.Context);
            CaptureAuditEntries(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ApplyAuditFields(eventData.Context);
            CaptureAuditEntries(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            InsertAuditLogs(eventData.Context);
            return base.SavedChanges(eventData, result);
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            await InsertAuditLogsAsync(eventData.Context, cancellationToken);
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        /// <summary>
        /// Iterates all tracked entities inheriting AuditableEntity and sets audit timestamps/users.
        /// </summary>
        private void ApplyAuditFields(DbContext context)
        {
            if (context == null) return;

            var utcNow = _dateTimeProvider.UtcNow;
            var username = _currentUserService.IsAuthenticated
                ? _currentUserService.Username
                : "System";

            var entries = context.ChangeTracker
                .Entries<AuditableEntity>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = utcNow;
                    entry.Entity.CreatedBy = username;
                }

                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.ModifiedAt = utcNow;
                    entry.Entity.ModifiedBy = username;

                    // Prevent overwriting original creation info on update
                    entry.Property(nameof(AuditableEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(AuditableEntity.CreatedBy)).IsModified = false;
                }
            }
        }

        // P-07: Never log sensitive fields in audit log
        private static readonly HashSet<string> _sensitiveFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "PasswordHash", "PasswordResetToken", "ApiSecret", "RefreshToken", "TwoFactorKey", "SecurityStamp"
        };

        /// <summary>
        /// AUD-02: Captures OldValues/NewValues/ChangedColumns before SaveChanges commits.
        /// Must be called before save because EF replaces original values after commit.
        /// </summary>
        private void CaptureAuditEntries(DbContext context)
        {
            if (context == null) return;

            _pendingEntries.Clear();                               // thread-local, safe
            var utcNow = _dateTimeProvider.UtcNow;
            var username = _currentUserService.IsAuthenticated
                ? _currentUserService.Username
                : "System";

            var trackedEntries = context.ChangeTracker.Entries()
                .Where(e => e.Entity is not AuditLog &&
                            (e.State == EntityState.Added ||
                             e.State == EntityState.Modified ||
                             e.State == EntityState.Deleted))
                .ToList();

            foreach (var entry in trackedEntries)
            {
                var entityType = entry.Entity.GetType().Name;
                var action = entry.State switch
                {
                    EntityState.Added => "Create",
                    EntityState.Deleted => "SoftDelete",
                    EntityState.Modified => IsSoftDelete(entry) ? "SoftDelete" : "Update",
                    _ => "Unknown"
                };

                var auditEntry = new AuditEntry
                {
                    EntityType = entityType,
                    Action = action,
                    PerformedBy = username,
                    Timestamp = utcNow,
                    Entry = entry
                };

                foreach (var prop in entry.Properties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.EntityId = Convert.ToInt32(prop.CurrentValue ?? 0);
                        continue;
                    }

                    // Skip RowVersion / concurrency tokens
                    if (prop.Metadata.IsConcurrencyToken) continue;

                    // Skip sensitive fields — never log passwords, tokens, secrets
                    if (_sensitiveFields.Contains(prop.Metadata.Name)) continue;

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                            break;

                        case EntityState.Modified:
                            if (prop.IsModified)
                            {
                                auditEntry.OldValues[prop.Metadata.Name] = prop.OriginalValue;
                                auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                                auditEntry.ChangedColumns.Add(prop.Metadata.Name);
                            }
                            break;

                        case EntityState.Deleted:
                            auditEntry.OldValues[prop.Metadata.Name] = prop.OriginalValue;
                            break;
                    }
                }

                _pendingEntries.Add(auditEntry);
            }
        }

        /// <summary>
        /// Inserts captured AuditLog records after successful SaveChanges.
        /// For Added entities, the generated Id is now available.
        /// </summary>
        private void InsertAuditLogs(DbContext context)
        {
            if (context == null || _pendingEntries.Count == 0) return;

            // Copy then clear so retries cannot double-log
            var entriesToSave = new List<AuditEntry>(_pendingEntries);
            _pendingEntries.Clear();

            foreach (var auditEntry in entriesToSave)
            {
                // For added entities, get the generated PK now
                if (auditEntry.Action == "Create" && auditEntry.EntityId == 0)
                {
                    var pkProp = auditEntry.Entry?.Properties
                        .FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                    if (pkProp != null)
                        auditEntry.EntityId = Convert.ToInt32(pkProp.CurrentValue ?? 0);
                }

                var log = new AuditLog(
                    auditEntry.EntityType,
                    auditEntry.EntityId,
                    auditEntry.Action,
                    auditEntry.PerformedBy,
                    auditEntry.Timestamp,
                    details: null,
                    oldValues: auditEntry.OldValues.Count > 0
                        ? JsonSerializer.Serialize(auditEntry.OldValues) : null,
                    newValues: auditEntry.NewValues.Count > 0
                        ? JsonSerializer.Serialize(auditEntry.NewValues) : null,
                    changedColumns: auditEntry.ChangedColumns.Count > 0
                        ? JsonSerializer.Serialize(auditEntry.ChangedColumns) : null);

                context.Set<AuditLog>().Add(log);
            }

            // Save audit logs in the same transaction
            context.SaveChanges();
        }

        /// <summary>
        /// Async version of InsertAuditLogs — called from SavedChangesAsync to avoid sync-over-async.
        /// </summary>
        private async Task InsertAuditLogsAsync(DbContext context, CancellationToken cancellationToken)
        {
            if (context == null || _pendingEntries.Count == 0) return;

            // Copy then clear so retries cannot double-log
            var entriesToSave = new List<AuditEntry>(_pendingEntries);
            _pendingEntries.Clear();

            foreach (var auditEntry in entriesToSave)
            {
                // For added entities, get the generated PK now
                if (auditEntry.Action == "Create" && auditEntry.EntityId == 0)
                {
                    var pkProp = auditEntry.Entry?.Properties
                        .FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                    if (pkProp != null)
                        auditEntry.EntityId = Convert.ToInt32(pkProp.CurrentValue ?? 0);
                }

                var log = new AuditLog(
                    auditEntry.EntityType,
                    auditEntry.EntityId,
                    auditEntry.Action,
                    auditEntry.PerformedBy,
                    auditEntry.Timestamp,
                    details: null,
                    oldValues: auditEntry.OldValues.Count > 0
                        ? JsonSerializer.Serialize(auditEntry.OldValues) : null,
                    newValues: auditEntry.NewValues.Count > 0
                        ? JsonSerializer.Serialize(auditEntry.NewValues) : null,
                    changedColumns: auditEntry.ChangedColumns.Count > 0
                        ? JsonSerializer.Serialize(auditEntry.ChangedColumns) : null);

                context.Set<AuditLog>().Add(log);
            }

            // Save audit logs asynchronously in the same transaction
            await context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Detects soft delete by checking if IsDeleted changed to true.</summary>
        private static bool IsSoftDelete(EntityEntry entry)
        {
            var isDeletedProp = entry.Properties
                .FirstOrDefault(p => p.Metadata.Name == "IsDeleted");
            return isDeletedProp is { IsModified: true, CurrentValue: true };
        }

        /// <summary>Temporary structure to hold audit data between SavingChanges and SavedChanges.</summary>
        private sealed class AuditEntry
        {
            public string EntityType { get; set; }
            public int EntityId { get; set; }
            public string Action { get; set; }
            public string PerformedBy { get; set; }
            public DateTime Timestamp { get; set; }
            public EntityEntry Entry { get; set; }
            public Dictionary<string, object> OldValues { get; } = new();
            public Dictionary<string, object> NewValues { get; } = new();
            public List<string> ChangedColumns { get; } = new();
        }
    }
}
