using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sync;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Sync;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Sync;
using MarcoERP.Persistence;

namespace MarcoERP.Persistence.Services.Sync
{
    /// <summary>
    /// Server-side sync engine. Handles pull (delta queries) and push (conflict detection).
    /// All queries bypass the soft-delete global filter via IgnoreQueryFilters()
    /// so that deleted records are also sent to clients for local removal.
    /// </summary>
    public sealed class SyncService : ISyncService
    {
        private readonly MarcoDbContext _db;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<SyncService> _logger;

        // Entity types eligible for sync — maps type name to DbSet accessor
        private static readonly Dictionary<string, Type> SyncableTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            // Inventory
            ["Product"] = typeof(Domain.Entities.Inventory.Product),
            ["Category"] = typeof(Domain.Entities.Inventory.Category),
            ["Unit"] = typeof(Domain.Entities.Inventory.Unit),
            ["Warehouse"] = typeof(Domain.Entities.Inventory.Warehouse),
            ["WarehouseProduct"] = typeof(Domain.Entities.Inventory.WarehouseProduct),

            // Sales
            ["Customer"] = typeof(Domain.Entities.Sales.Customer),
            ["SalesInvoice"] = typeof(Domain.Entities.Sales.SalesInvoice),
            ["SalesInvoiceLine"] = typeof(Domain.Entities.Sales.SalesInvoiceLine),
            ["SalesReturn"] = typeof(Domain.Entities.Sales.SalesReturn),
            ["SalesReturnLine"] = typeof(Domain.Entities.Sales.SalesReturnLine),
            ["SalesRepresentative"] = typeof(Domain.Entities.Sales.SalesRepresentative),

            // Purchases
            ["Supplier"] = typeof(Domain.Entities.Purchases.Supplier),
            ["PurchaseInvoice"] = typeof(Domain.Entities.Purchases.PurchaseInvoice),
            ["PurchaseInvoiceLine"] = typeof(Domain.Entities.Purchases.PurchaseInvoiceLine),
            ["PurchaseReturn"] = typeof(Domain.Entities.Purchases.PurchaseReturn),
            ["PurchaseReturnLine"] = typeof(Domain.Entities.Purchases.PurchaseReturnLine),

            // Treasury
            ["Cashbox"] = typeof(Domain.Entities.Treasury.Cashbox),
            ["CashReceipt"] = typeof(Domain.Entities.Treasury.CashReceipt),
            ["CashPayment"] = typeof(Domain.Entities.Treasury.CashPayment),
            ["CashTransfer"] = typeof(Domain.Entities.Treasury.CashTransfer),
            ["BankAccount"] = typeof(Domain.Entities.Treasury.BankAccount),

            // Accounting
            ["Account"] = typeof(Domain.Entities.Accounting.Account),
            ["JournalEntry"] = typeof(Domain.Entities.Accounting.JournalEntry),
            ["JournalEntryLine"] = typeof(Domain.Entities.Accounting.JournalEntryLine),
            ["FiscalYear"] = typeof(Domain.Entities.Accounting.FiscalYear),
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public SyncService(MarcoDbContext db, IDateTimeProvider dateTimeProvider, ILogger<SyncService> logger)
        {
            _db = db;
            _dateTimeProvider = dateTimeProvider;
            _logger = logger;
        }

        public async Task<ServiceResult<SyncPullResponseDto>> PullChangesAsync(
            SyncPullRequestDto request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.DeviceId))
                return ServiceResult<SyncPullResponseDto>.Failure("DeviceId مطلوب.");

            // Verify device is registered
            var device = await _db.SyncDevices
                .FirstOrDefaultAsync(d => d.DeviceId == request.DeviceId && d.IsActive, ct);

            if (device == null)
                return ServiceResult<SyncPullResponseDto>.Failure("الجهاز غير مسجل. سجّل الجهاز أولاً.");

            var lastVersion = request.LastSyncVersion;
            var pageSize = Math.Clamp(request.PageSize, 50, 2000);

            // Determine which entity types to query
            var typesToSync = request.EntityTypes?.Count > 0
                ? SyncableTypes.Where(kv => request.EntityTypes.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                               .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase)
                : SyncableTypes;

            var response = new SyncPullResponseDto();
            long maxVersion = lastVersion;
            bool hasMore = false;

            foreach (var (typeName, clrType) in typesToSync)
            {
                var (entities, entityHasMore) = await QueryChangedEntitiesAsync(
                    clrType, lastVersion, pageSize, ct);

                if (entities.Count > 0)
                {
                    response.Changes[typeName] = entities;
                    var entityMaxVersion = entities.Max(e => e.SyncVersion);
                    if (entityMaxVersion > maxVersion)
                        maxVersion = entityMaxVersion;
                }

                if (entityHasMore)
                    hasMore = true;
            }

            response.CurrentSyncVersion = maxVersion;
            response.HasMore = hasMore;

            // Update device checkpoint
            device.UpdateSyncCheckpoint(maxVersion, _dateTimeProvider.UtcNow);
            await _db.SaveChangesAsync(ct);

            return ServiceResult<SyncPullResponseDto>.Success(response);
        }

        public async Task<ServiceResult<SyncPushResponseDto>> PushChangesAsync(
            SyncPushRequestDto request, int userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.DeviceId))
                return ServiceResult<SyncPushResponseDto>.Failure("DeviceId مطلوب.");

            var device = await _db.SyncDevices
                .FirstOrDefaultAsync(d => d.DeviceId == request.DeviceId && d.IsActive, ct);

            if (device == null)
                return ServiceResult<SyncPushResponseDto>.Failure("الجهاز غير مسجل.");

            var response = new SyncPushResponseDto();

            // Wrap entire push in a transaction for atomicity
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                foreach (var (typeName, pushEntities) in request.Changes)
                {
                    if (!SyncableTypes.TryGetValue(typeName, out var clrType))
                        continue;

                    foreach (var pushEntity in pushEntities)
                    {
                        try
                        {
                            if (pushEntity.Id == 0)
                            {
                                // New record created offline
                                var newId = await CreateEntityFromPushAsync(clrType, pushEntity, ct);
                                response.AppliedCount++;

                                if (!string.IsNullOrEmpty(pushEntity.ClientTempId))
                                    response.IdMappings[pushEntity.ClientTempId] = newId;
                            }
                            else
                            {
                                // Update existing record — check for conflict
                                var conflictResult = await UpdateEntityFromPushAsync(
                                    clrType, typeName, pushEntity, request.DeviceId, ct);

                                if (conflictResult != null)
                                {
                                    response.ConflictCount++;
                                    response.Conflicts.Add(conflictResult);
                                }
                                else
                                {
                                    response.AppliedCount++;
                                }
                            }
                        }
                        catch (DbUpdateConcurrencyException ex)
                        {
                            _logger.LogWarning(ex, "Concurrency conflict for {EntityType} Id={EntityId}",
                                typeName, pushEntity.Id);
                            response.ConflictCount++;
                            response.Conflicts.Add(new SyncConflictDto
                            {
                                EntityType = typeName,
                                EntityId = pushEntity.Id,
                                Resolution = "ConcurrencyConflict",
                                Message = "تم تعديل السجل بواسطة مستخدم آخر."
                            });
                        }
                        catch (DbUpdateException ex)
                        {
                            _logger.LogWarning(ex, "Database error during push for {EntityType} Id={EntityId}",
                                typeName, pushEntity.Id);
                            response.Conflicts.Add(new SyncConflictDto
                            {
                                EntityType = typeName,
                                EntityId = pushEntity.Id,
                                Resolution = "DatabaseError",
                                Message = "خطأ في قاعدة البيانات أثناء المزامنة."
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error during push for {EntityType} Id={EntityId}",
                                typeName, pushEntity.Id);
                            response.Conflicts.Add(new SyncConflictDto
                            {
                                EntityType = typeName,
                                EntityId = pushEntity.Id,
                                Resolution = "Error",
                                Message = "خطأ غير متوقع أثناء المزامنة."
                            });
                        }
                    }
                }

                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Push transaction failed for device {DeviceId}", request.DeviceId);
                await transaction.RollbackAsync(ct);
                return ServiceResult<SyncPushResponseDto>.Failure("فشلت عملية المزامنة. يرجى المحاولة مرة أخرى.");
            }

            return ServiceResult<SyncPushResponseDto>.Success(response);
        }

        public async Task<ServiceResult<DeviceInfoDto>> RegisterDeviceAsync(
            RegisterDeviceDto request, int userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.DeviceId))
                return ServiceResult<DeviceInfoDto>.Failure("DeviceId مطلوب.");

            var existing = await _db.SyncDevices
                .FirstOrDefaultAsync(d => d.DeviceId == request.DeviceId, ct);

            if (existing != null)
            {
                existing.Activate();
                await _db.SaveChangesAsync(ct);

                return ServiceResult<DeviceInfoDto>.Success(MapToDeviceInfo(existing));
            }

            var device = new SyncDevice(
                request.DeviceId,
                request.DeviceName,
                request.DeviceType,
                userId);

            _db.SyncDevices.Add(device);
            await _db.SaveChangesAsync(ct);

            return ServiceResult<DeviceInfoDto>.Success(MapToDeviceInfo(device));
        }

        public async Task<ServiceResult<SyncStatusDto>> GetSyncStatusAsync(
            string deviceId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return ServiceResult<SyncStatusDto>.Failure("DeviceId مطلوب.");

            var device = await _db.SyncDevices
                .FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct);

            // Get server's max SyncVersion across all entities using a raw SQL query
            long serverMaxVersion = 0;
            foreach (var (_, clrType) in SyncableTypes.Take(1))
            {
                // Sample from the first syncable type to get approximate max
                serverMaxVersion = await GetMaxSyncVersionAsync(clrType, ct);
                break;
            }

            // Get a more accurate max by checking all types
            foreach (var (_, clrType) in SyncableTypes)
            {
                var typeMax = await GetMaxSyncVersionAsync(clrType, ct);
                if (typeMax > serverMaxVersion)
                    serverMaxVersion = typeMax;
            }

            var deviceLastVersion = device?.LastSyncVersion ?? 0;

            return ServiceResult<SyncStatusDto>.Success(new SyncStatusDto
            {
                ServerSyncVersion = serverMaxVersion,
                DeviceLastSyncVersion = deviceLastVersion,
                PendingChanges = serverMaxVersion - deviceLastVersion,
                DeviceRegistered = device != null
            });
        }

        // ═══════════════════════════════════════════════════════════
        // Private Helpers
        // ═══════════════════════════════════════════════════════════

        private async Task<(List<SyncEntityDto> Entities, bool HasMore)> QueryChangedEntitiesAsync(
            Type clrType, long sinceVersion, int pageSize, CancellationToken ct)
        {
            // Use raw SQL to query entities with SyncVersion > sinceVersion
            // IgnoreQueryFilters so we also get soft-deleted records
            var query = _db.Model.FindEntityType(clrType);
            if (query == null)
                return (new List<SyncEntityDto>(), false);

            var tableName = query.GetTableName();
            var schemaName = query.GetSchema() ?? "dbo";

            // Query using FormattableString for parameterized SQL (prevents SQL injection)
            var sql = $@"SELECT TOP ({pageSize + 1}) *
                         FROM [{schemaName}].[{tableName}]
                         WHERE [SyncVersion] > @p0
                         ORDER BY [SyncVersion] ASC";

            var entities = new List<SyncEntityDto>();
            bool hasMore = false;

            var connection = _db.Database.GetDbConnection();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            var param = command.CreateParameter();
            param.ParameterName = "@p0";
            param.Value = sinceVersion;
            command.Parameters.Add(param);

            await _db.Database.OpenConnectionAsync(ct);
            try
            {
                using var reader = await command.ExecuteReaderAsync(ct);
                int count = 0;
                while (await reader.ReadAsync(ct))
                {
                    count++;
                    if (count > pageSize)
                    {
                        hasMore = true;
                        break;
                    }

                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var columnName = reader.GetName(i);
                        var value = reader.IsDBNull(i) ? null : reader.GetValue(i);

                        // Convert byte[] (RowVersion) to Base64
                        if (value is byte[] bytes)
                            value = Convert.ToBase64String(bytes);

                        row[ToCamelCase(columnName)] = value;
                    }

                    entities.Add(new SyncEntityDto
                    {
                        Id = row.ContainsKey("id") ? Convert.ToInt32(row["id"]) : 0,
                        SyncVersion = row.ContainsKey("syncVersion") ? Convert.ToInt64(row["syncVersion"]) : 0,
                        IsDeleted = row.ContainsKey("isDeleted") && Convert.ToBoolean(row["isDeleted"]),
                        Data = row
                    });
                }
            }
            finally
            {
                await _db.Database.CloseConnectionAsync();
            }

            return (entities, hasMore);
        }

        private async Task<int> CreateEntityFromPushAsync(
            Type clrType, SyncPushEntityDto pushEntity, CancellationToken ct)
        {
            // Create entity instance and set properties from push data
            var entity = Activator.CreateInstance(clrType, nonPublic: true);

            if (entity is SoftDeletableEntity soft && pushEntity.Data != null)
            {
                SetEntityProperties(entity, clrType, pushEntity.Data);
            }

            _db.Add(entity);
            await _db.SaveChangesAsync(ct);

            return (entity as BaseEntity)?.Id ?? 0;
        }

        private async Task<SyncConflictDto> UpdateEntityFromPushAsync(
            Type clrType, string typeName, SyncPushEntityDto pushEntity,
            string deviceId, CancellationToken ct)
        {
            var existing = await _db.FindAsync(clrType, pushEntity.Id);
            if (existing == null)
                return new SyncConflictDto
                {
                    EntityType = typeName,
                    EntityId = pushEntity.Id,
                    Resolution = "NotFound",
                    Message = "السجل غير موجود على الخادم."
                };

            if (existing is SoftDeletableEntity softEntity)
            {
                // Conflict detection: if server's SyncVersion > client's BaseSyncVersion,
                // someone else already modified this record
                if (softEntity.SyncVersion > pushEntity.BaseSyncVersion)
                {
                    // Server-wins: log the conflict but don't apply client changes
                    var conflict = new SyncConflict(
                        typeName,
                        pushEntity.Id,
                        deviceId,
                        JsonSerializer.Serialize(pushEntity.Data, JsonOptions),
                        JsonSerializer.Serialize(SerializeEntity(existing), JsonOptions),
                        "ServerWins");

                    _db.SyncConflicts.Add(conflict);

                    return new SyncConflictDto
                    {
                        EntityType = typeName,
                        EntityId = pushEntity.Id,
                        Resolution = "ServerWins",
                        Message = "تم تعديل السجل بواسطة مستخدم آخر. تم تطبيق نسخة الخادم."
                    };
                }

                // No conflict — apply changes
                SetEntityProperties(existing, clrType, pushEntity.Data);
            }

            return null;
        }

        private async Task<long> GetMaxSyncVersionAsync(Type clrType, CancellationToken ct)
        {
            var entityType = _db.Model.FindEntityType(clrType);
            if (entityType == null) return 0;

            var tableName = entityType.GetTableName();
            var schemaName = entityType.GetSchema() ?? "dbo";

            var sql = $"SELECT ISNULL(MAX([SyncVersion]), 0) FROM [{schemaName}].[{tableName}]";

            var connection = _db.Database.GetDbConnection();
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            await _db.Database.OpenConnectionAsync(ct);
            try
            {
                var result = await command.ExecuteScalarAsync(ct);
                return result != null && result != DBNull.Value ? Convert.ToInt64(result) : 0;
            }
            finally
            {
                await _db.Database.CloseConnectionAsync();
            }
        }

        private static void SetEntityProperties(object entity, Type clrType, Dictionary<string, object> data)
        {
            // Skip system fields that shouldn't be set from client data
            var skipFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "id", "rowVersion", "syncVersion", "createdAt", "createdBy",
                "modifiedAt", "modifiedBy", "deletedAt", "deletedBy", "companyId"
            };

            foreach (var (key, value) in data)
            {
                if (skipFields.Contains(key)) continue;

                // Convert camelCase key to PascalCase for property lookup
                var propName = char.ToUpper(key[0]) + key[1..];
                var prop = clrType.GetProperty(propName,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance);

                if (prop == null || !prop.CanWrite) continue;

                try
                {
                    if (value == null)
                    {
                        if (Nullable.GetUnderlyingType(prop.PropertyType) != null || !prop.PropertyType.IsValueType)
                            prop.SetValue(entity, null);
                        continue;
                    }

                    var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                    if (value is JsonElement jsonElement)
                    {
                        var converted = JsonSerializer.Deserialize(jsonElement.GetRawText(), targetType, JsonOptions);
                        prop.SetValue(entity, converted);
                    }
                    else
                    {
                        prop.SetValue(entity, Convert.ChangeType(value, targetType));
                    }
                }
                catch
                {
                    // Skip properties that can't be set — this is expected for type mismatches
                    // from client data (e.g., null for non-nullable, wrong format)
                }
            }
        }

        private static Dictionary<string, object> SerializeEntity(object entity)
        {
            var json = JsonSerializer.Serialize(entity, JsonOptions);
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOptions);
        }

        private static DeviceInfoDto MapToDeviceInfo(SyncDevice device) => new()
        {
            DeviceId = device.DeviceId,
            DeviceName = device.DeviceName,
            DeviceType = device.DeviceType,
            LastSyncVersion = device.LastSyncVersion,
            LastSyncAt = device.LastSyncAt,
            IsActive = device.IsActive
        };

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return char.ToLowerInvariant(name[0]) + name[1..];
        }
    }
}
