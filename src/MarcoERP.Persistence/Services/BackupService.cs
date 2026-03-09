using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Services
{
    /// <summary>
    /// Persistence-layer backup service.
    /// SQL Server: Executes raw BACKUP/RESTORE commands.
    /// PostgreSQL: Uses pg_dump / pg_restore via external process.
    /// </summary>
    public sealed class BackupService : IBackupService
    {
        private readonly MarcoDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;

        private bool IsPostgreSql => _context.Database.ProviderName?.Contains("Npgsql") == true;

        public BackupService(MarcoDbContext context, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
        }

        /// <inheritdoc />
        private static readonly System.Text.RegularExpressions.Regex SafeDbNameRegex =
            new(@"^[a-zA-Z0-9_]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

        public async Task<ServiceResult<BackupResultDto>> BackupAsync(string backupPath, CancellationToken ct = default)
        {
            var dbName = GetDatabaseName();
            if (string.IsNullOrWhiteSpace(dbName))
                return ServiceResult<BackupResultDto>.Failure("تعذر تحديد اسم قاعدة البيانات.");

            if (!SafeDbNameRegex.IsMatch(dbName))
                return ServiceResult<BackupResultDto>.Failure("اسم قاعدة البيانات يحتوي على أحرف غير مسموح بها.");

            var userName = _currentUser.Username ?? "System";

            try
            {
                // Build backup file path
                if (string.IsNullOrWhiteSpace(backupPath))
                    backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");

                if (!Directory.Exists(backupPath))
                    Directory.CreateDirectory(backupPath);

                string fullPath;

                if (IsPostgreSql)
                {
                    // PostgreSQL: use pg_dump external process
                    var fileName = $"{dbName}_{_dateTime.UtcNow:yyyyMMdd_HHmmss}.sql";
                    fullPath = Path.Combine(backupPath, fileName);

                    var connStr = _context.Database.GetConnectionString();
                    var connBuilder = new DbConnectionStringBuilder { ConnectionString = connStr };
                    var host = connBuilder.ContainsKey("Host") ? connBuilder["Host"]?.ToString() : "localhost";
                    var port = connBuilder.ContainsKey("Port") ? connBuilder["Port"]?.ToString() : "5432";
                    var user = connBuilder.ContainsKey("Username") ? connBuilder["Username"]?.ToString() : "postgres";
                    var pass = connBuilder.ContainsKey("Password") ? connBuilder["Password"]?.ToString() : "";

                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "pg_dump",
                        Arguments = $"--host={host} --port={port} --username={user} --format=plain --file=\"{fullPath}\" {dbName}",
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    psi.Environment["PGPASSWORD"] = pass;

                    using var process = System.Diagnostics.Process.Start(psi);
                    if (process == null)
                        return ServiceResult<BackupResultDto>.Failure("تعذر تشغيل pg_dump. تأكد من تثبيت PostgreSQL.");

                    var stderr = await process.StandardError.ReadToEndAsync(ct);
                    await process.WaitForExitAsync(ct);

                    if (process.ExitCode != 0)
                        return ServiceResult<BackupResultDto>.Failure($"فشل pg_dump: {stderr}");
                }
                else
                {
                    // SQL Server: BACKUP DATABASE
                    var fileName = $"{dbName}_{_dateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
                    fullPath = Path.Combine(backupPath, fileName);

                    var safeDbName = dbName.Replace("]", "]]");
                    var safePath = fullPath.Replace("'", "''");

                    var sql = $"BACKUP DATABASE [{safeDbName}] TO DISK = N'{safePath}' WITH FORMAT, INIT, NAME = N'{safeDbName}-Full Backup'";
                    await _context.Database.ExecuteSqlRawAsync(sql, ct);
                }

                // Get file size
                var fileInfo = new FileInfo(fullPath);
                var fileSize = fileInfo.Exists ? fileInfo.Length : 0;

                // Record history
                var history = new BackupHistory(
                    fullPath,
                    fileSize,
                    _dateTime.UtcNow,
                    userName,
                    "Full",
                    true);

                _context.Set<BackupHistory>().Add(history);
                await _context.SaveChangesAsync(ct);

                return ServiceResult<BackupResultDto>.Success(new BackupResultDto
                {
                    Id = history.Id,
                    FilePath = fullPath,
                    FileSizeBytes = fileSize,
                    BackupDate = history.BackupDate,
                    PerformedBy = userName
                });
            }
            catch (Exception ex)
            {
                // Record failed attempt
                try
                {
                    var failedHistory = new BackupHistory(
                        backupPath ?? "Unknown",
                        0,
                        _dateTime.UtcNow,
                        userName,
                        "Full",
                        false,
                        ex.Message);

                    _context.Set<BackupHistory>().Add(failedHistory);
                    await _context.SaveChangesAsync(ct);
                }
                catch
                {
                    // Ignore logging failure
                }

                return ServiceResult<BackupResultDto>.Failure($"فشل النسخ الاحتياطي: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public async Task<ServiceResult> RestoreAsync(string backupFilePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
                return ServiceResult.Failure("ملف النسخة الاحتياطية غير موجود.");

            var dbName = GetDatabaseName();
            if (string.IsNullOrWhiteSpace(dbName))
                return ServiceResult.Failure("تعذر تحديد اسم قاعدة البيانات.");

            if (!SafeDbNameRegex.IsMatch(dbName))
                return ServiceResult.Failure("اسم قاعدة البيانات يحتوي على أحرف غير مسموح بها.");

            if (IsPostgreSql)
            {
                return await RestorePostgreSqlAsync(backupFilePath, dbName, ct);
            }

            return await RestoreSqlServerAsync(backupFilePath, dbName, ct);
        }

        private async Task<ServiceResult> RestorePostgreSqlAsync(string backupFilePath, string dbName, CancellationToken ct)
        {
            try
            {
                var connStr = _context.Database.GetConnectionString();
                var connBuilder = new DbConnectionStringBuilder { ConnectionString = connStr };
                var host = connBuilder.ContainsKey("Host") ? connBuilder["Host"]?.ToString() : "localhost";
                var port = connBuilder.ContainsKey("Port") ? connBuilder["Port"]?.ToString() : "5432";
                var user = connBuilder.ContainsKey("Username") ? connBuilder["Username"]?.ToString() : "postgres";
                var pass = connBuilder.ContainsKey("Password") ? connBuilder["Password"]?.ToString() : "";

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "psql",
                    Arguments = $"--host={host} --port={port} --username={user} --dbname={dbName} --file=\"{backupFilePath}\"",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                psi.Environment["PGPASSWORD"] = pass;

                using var process = System.Diagnostics.Process.Start(psi);
                if (process == null)
                    return ServiceResult.Failure("تعذر تشغيل psql. تأكد من تثبيت PostgreSQL.");

                var stderr = await process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);

                if (process.ExitCode != 0)
                    return ServiceResult.Failure($"فشل الاستعادة: {stderr}");

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"فشل الاستعادة: {ex.Message}");
            }
        }

        private async Task<ServiceResult> RestoreSqlServerAsync(string backupFilePath, string dbName, CancellationToken ct)
        {
            // Sanitise identifiers to prevent SQL injection
            var safeDbName = dbName.Replace("]", "]]");
            var safePath = backupFilePath.Replace("'", "''");

            // Build a master-database connection string so we don't kill our own session
            var originalConnStr = _context.Database.GetConnectionString();
            var masterConnBuilder = new DbConnectionStringBuilder { ConnectionString = originalConnStr };
            masterConnBuilder["Initial Catalog"] = "master";
            masterConnBuilder["Database"] = "master";

            var masterOptions = new DbContextOptionsBuilder<MarcoDbContext>()
                .UseSqlServer(masterConnBuilder.ConnectionString)
                .Options;

            try
            {
                using var masterCtx = new MarcoDbContext(masterOptions);

                // Set single user mode to drop existing connections
                var setSingleUser = $"ALTER DATABASE [{safeDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
                await masterCtx.Database.ExecuteSqlRawAsync(setSingleUser, ct);

                // Execute RESTORE DATABASE
                var restoreSql = $"RESTORE DATABASE [{safeDbName}] FROM DISK = N'{safePath}' WITH REPLACE";
                await masterCtx.Database.ExecuteSqlRawAsync(restoreSql, ct);

                // Set back to multi-user mode
                var setMultiUser = $"ALTER DATABASE [{safeDbName}] SET MULTI_USER";
                await masterCtx.Database.ExecuteSqlRawAsync(setMultiUser, ct);

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                // Attempt to restore multi-user mode via master connection
                try
                {
                    using var recoveryCtx = new MarcoDbContext(masterOptions);
                    var setMultiUser = $"ALTER DATABASE [{safeDbName}] SET MULTI_USER";
                    await recoveryCtx.Database.ExecuteSqlRawAsync(setMultiUser, CancellationToken.None);
                }
                catch
                {
                    // Ignore — manual intervention may be needed
                }

                return ServiceResult.Failure($"فشل الاستعادة: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public async Task<ServiceResult<IReadOnlyList<BackupHistoryDto>>> GetHistoryAsync(CancellationToken ct = default)
        {
            try
            {
                var history = await _context.Set<BackupHistory>()
                    .AsNoTracking()
                    .OrderByDescending(h => h.BackupDate)
                    .Select(h => new BackupHistoryDto
                    {
                        Id = h.Id,
                        FilePath = h.FilePath,
                        FileSizeBytes = h.FileSizeBytes,
                        BackupDate = h.BackupDate,
                        PerformedBy = h.PerformedBy,
                        BackupType = h.BackupType,
                        IsSuccessful = h.IsSuccessful,
                        ErrorMessage = h.ErrorMessage
                    })
                    .ToListAsync(ct);

                return ServiceResult<IReadOnlyList<BackupHistoryDto>>.Success(history);
            }
            catch (Exception ex)
            {
                return ServiceResult<IReadOnlyList<BackupHistoryDto>>.Failure($"خطأ في تحميل السجل: {ex.Message}");
            }
        }

        // ── Helpers ─────────────────────────────────────────────

        /// <summary>
        /// Extracts the database name from the connection string.
        /// </summary>
        private string GetDatabaseName()
        {
            var connectionString = _context.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
                return null;

            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            // Try "Initial Catalog" first, then "Database"
            if (builder.TryGetValue("Initial Catalog", out var catalog))
                return catalog?.ToString();
            if (builder.TryGetValue("Database", out var database))
                return database?.ToString();

            return null;
        }
    }
}
