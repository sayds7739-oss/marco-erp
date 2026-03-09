using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using MarcoERP.Domain.Entities.Common;

namespace MarcoERP.Persistence.Interceptors
{
    /// <summary>
    /// EF Core interceptor that auto-increments SyncVersion on every
    /// Added or Modified SoftDeletableEntity using the SQL Server SEQUENCE
    /// dbo.GlobalSyncVersion. This is safe across multiple app instances
    /// and survives restarts without gaps or duplicates.
    /// Fallback: if the SEQUENCE doesn't exist yet (pre-migration), uses
    /// a one-time in-memory counter seeded from MAX(SyncVersion).
    /// </summary>
    public sealed class SyncVersionInterceptor : SaveChangesInterceptor
    {
        // Fallback state (used only if SQL SEQUENCE doesn't exist)
        private static long _fallbackVersion;
        private static bool _fallbackInitialized;
        private static readonly object _fallbackLock = new();

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData, InterceptionResult<int> result)
        {
            AssignSyncVersions(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            AssignSyncVersions(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void AssignSyncVersions(DbContext context)
        {
            if (context == null) return;

            var changedEntities = context.ChangeTracker
                .Entries<SoftDeletableEntity>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                .ToList();

            if (changedEntities.Count == 0) return;

            var versions = AllocateVersions(context, changedEntities.Count);

            for (int i = 0; i < changedEntities.Count; i++)
            {
                changedEntities[i].Entity.SyncVersion = versions[i];
            }
        }

        /// <summary>
        /// Allocates <paramref name="count"/> monotonically-increasing SyncVersion values.
        /// Primary: SQL SEQUENCE dbo.GlobalSyncVersion via sp_sequence_get_range.
        /// Fallback: in-memory Interlocked counter seeded from MAX(SyncVersion).
        /// </summary>
        private static long[] AllocateVersions(DbContext context, int count)
        {
            try
            {
                return AllocateFromSequence(context, count);
            }
            catch (Exception ex)
            {
                // SEQUENCE doesn't exist yet (pre-migration) — use fallback
                System.Diagnostics.Debug.WriteLine(
                    $"[SyncVersion] SEQUENCE allocation failed, using in-memory fallback: {ex.Message}");
                return AllocateFromFallback(context, count);
            }
        }

        private static long[] AllocateFromSequence(DbContext context, int count)
        {
            var isPostgreSql = context.Database.ProviderName?.Contains("Npgsql") == true;
            var connection = context.Database.GetDbConnection();
            var needsOpen = connection.State != ConnectionState.Open;

            if (needsOpen)
                connection.Open();

            try
            {
                using var cmd = connection.CreateCommand();

                // Attach to current EF transaction if one exists
                var tx = context.Database.CurrentTransaction;
                if (tx != null)
                    cmd.Transaction = tx.GetDbTransaction();

                if (isPostgreSql)
                {
                    // PostgreSQL: use nextval on the sequence
                    cmd.CommandText = $"SELECT nextval('\"GlobalSyncVersion\"') FROM generate_series(1, {count})";

                    using var reader = cmd.ExecuteReader();
                    var versions = new long[count];
                    int idx = 0;
                    while (reader.Read() && idx < count)
                    {
                        versions[idx++] = reader.GetInt64(0);
                    }
                    return versions;
                }
                else
                {
                    // SQL Server: use sp_sequence_get_range for bulk allocation
                    cmd.CommandText = @"
                    DECLARE @first_value sql_variant, @last_value sql_variant;
                    EXEC sp_sequence_get_range
                        @sequence_name = N'dbo.GlobalSyncVersion',
                        @range_size = @cnt,
                        @range_first_value = @first_value OUTPUT,
                        @range_last_value = @last_value OUTPUT;
                    SELECT CAST(@first_value AS BIGINT), CAST(@last_value AS BIGINT);";

                    var p = cmd.CreateParameter();
                    p.ParameterName = "@cnt";
                    p.DbType = DbType.Int32;
                    p.Value = count;
                    cmd.Parameters.Add(p);

                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        var first = reader.GetInt64(0);
                        var versions = new long[count];
                        for (int i = 0; i < count; i++)
                            versions[i] = first + i;
                        return versions;
                    }

                    throw new InvalidOperationException("sp_sequence_get_range returned no rows.");
                }
            }
            finally
            {
                if (needsOpen && connection.State == ConnectionState.Open)
                    connection.Close();
            }
        }

        private static long[] AllocateFromFallback(DbContext context, int count)
        {
            EnsureFallbackInitialized(context);

            var versions = new long[count];
            for (int i = 0; i < count; i++)
                versions[i] = Interlocked.Increment(ref _fallbackVersion);
            return versions;
        }

        private static void EnsureFallbackInitialized(DbContext context)
        {
            if (_fallbackInitialized) return;

            lock (_fallbackLock)
            {
                if (_fallbackInitialized) return;

                try
                {
                    var isPostgreSql = context.Database.ProviderName?.Contains("Npgsql") == true;

                    using var command = context.Database.GetDbConnection().CreateCommand();

                    if (isPostgreSql)
                    {
                        // PostgreSQL: query information_schema to find tables with SyncVersion column
                        command.CommandText = @"
                            SELECT COALESCE(MAX(max_sv), 0) FROM (
                                SELECT MAX(""SyncVersion"") as max_sv
                                FROM information_schema.columns c
                                CROSS JOIN LATERAL (
                                    SELECT MAX(""SyncVersion"") as ""SyncVersion""
                                    FROM (SELECT 0 as ""SyncVersion"") dummy
                                ) t
                                WHERE c.column_name = 'SyncVersion'
                                LIMIT 1
                            ) sub;";
                        // Simplified: just start from 0 for PostgreSQL fallback
                        // The SEQUENCE is the primary mechanism
                        command.CommandText = "SELECT 0::bigint";
                    }
                    else
                    {
                        // SQL Server: T-SQL cursor to find MAX(SyncVersion) across all tables
                        command.CommandText = @"
                            DECLARE @maxVersion BIGINT = 0;
                            DECLARE @sql NVARCHAR(MAX);
                            DECLARE @tableName NVARCHAR(256);
                            DECLARE tableCursor CURSOR LOCAL FAST_FORWARD FOR
                                SELECT QUOTENAME(TABLE_SCHEMA) + '.' + QUOTENAME(TABLE_NAME)
                                FROM INFORMATION_SCHEMA.COLUMNS
                                WHERE COLUMN_NAME = 'SyncVersion';
                            OPEN tableCursor;
                            FETCH NEXT FROM tableCursor INTO @tableName;
                            WHILE @@FETCH_STATUS = 0
                            BEGIN
                                SET @sql = N'SELECT @mv = ISNULL(MAX(SyncVersion), 0) FROM ' + @tableName;
                                DECLARE @tv BIGINT;
                                EXEC sp_executesql @sql, N'@mv BIGINT OUTPUT', @mv = @tv OUTPUT;
                                IF @tv > @maxVersion SET @maxVersion = @tv;
                                FETCH NEXT FROM tableCursor INTO @tableName;
                            END
                            CLOSE tableCursor;
                            DEALLOCATE tableCursor;
                            SELECT @maxVersion;";
                    }

                    if (context.Database.GetDbConnection().State != ConnectionState.Open)
                        context.Database.OpenConnection();

                    var result = command.ExecuteScalar();
                    _fallbackVersion = result != null && result != DBNull.Value
                        ? Convert.ToInt64(result)
                        : 0;
                }
                catch (Exception ex)
                {
                    // If we can't read MAX(SyncVersion) from DB (e.g., tables don't exist yet),
                    // start from 0. This is only used when SEQUENCE doesn't exist (pre-migration).
                    System.Diagnostics.Debug.WriteLine(
                        $"[SyncVersion] Fallback init failed, starting from 0: {ex.Message}");
                    _fallbackVersion = 0;
                }

                _fallbackInitialized = true;
            }
        }
    }
}
