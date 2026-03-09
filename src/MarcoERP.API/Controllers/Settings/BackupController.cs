using MarcoERP.Application.Interfaces.Settings;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Settings
{
    /// <summary>
    /// API controller for database backup and restore operations.
    /// </summary>
    public class BackupController : ApiControllerBase
    {
        private readonly IBackupService _backupService;
        private readonly IConfiguration _configuration;

        /// <summary>Allowed base directory for backup files. Configured via "Backup:BasePath" in appsettings.</summary>
        private string AllowedBasePath => _configuration.GetValue<string>("Backup:BasePath")
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");

        public BackupController(IBackupService backupService, IConfiguration configuration)
        {
            _backupService = backupService;
            _configuration = configuration;
        }

        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets the history of backup operations.</summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(CancellationToken ct)
        {
            var result = await _backupService.GetHistoryAsync(ct);
            return FromResult(result);
        }

        // ── Commands ────────────────────────────────────────────

        /// <summary>Creates a full backup of the database.</summary>
        [HttpPost]
        public async Task<IActionResult> Backup([FromQuery] string? backupPath, CancellationToken ct)
        {
            // SECURITY: Restrict backup path to configured base directory
            var safePath = SanitizePath(backupPath, AllowedBasePath);
            if (safePath == null)
                return BadRequest(new { success = false, errors = new[] { "مسار النسخ الاحتياطي غير مسموح به." } });

            var result = await _backupService.BackupAsync(safePath, ct);
            return FromResult(result);
        }

        /// <summary>Restores the database from a backup file.</summary>
        [HttpPost("restore")]
        public async Task<IActionResult> Restore([FromQuery] string backupFilePath, CancellationToken ct)
        {
            // SECURITY: Restrict restore file path to configured base directory
            var safePath = SanitizePath(backupFilePath, AllowedBasePath);
            if (safePath == null)
                return BadRequest(new { success = false, errors = new[] { "مسار ملف النسخة الاحتياطية غير مسموح به." } });

            var result = await _backupService.RestoreAsync(safePath, ct);
            return FromResult(result);
        }

        /// <summary>
        /// Validates that the resolved path stays within the allowed base directory.
        /// Returns the canonical path if safe, or null if path traversal is detected.
        /// </summary>
        private static string? SanitizePath(string? userPath, string allowedBase)
        {
            if (string.IsNullOrWhiteSpace(userPath))
                return allowedBase; // default to allowed base

            var resolvedBase = Path.GetFullPath(allowedBase).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var resolvedPath = Path.GetFullPath(userPath);

            if (!resolvedPath.StartsWith(resolvedBase, StringComparison.OrdinalIgnoreCase))
                return null; // path traversal attempt

            return resolvedPath;
        }
    }
}
