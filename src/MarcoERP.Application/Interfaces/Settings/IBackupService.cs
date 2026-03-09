using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;

namespace MarcoERP.Application.Interfaces.Settings
{
    /// <summary>
    /// خدمة النسخ الاحتياطي واستعادة قاعدة البيانات.
    /// Implementation: Persistence layer (uses raw SQL for BACKUP/RESTORE).
    /// </summary>
    public interface IBackupService
    {
        /// <summary>
        /// Creates a full backup of the database.
        /// </summary>
        /// <param name="backupPath">Target directory for the backup file. If null, uses default path.</param>
        /// <param name="ct">Cancellation token.</param>
        [RequiresPermission(PermissionKeys.SettingsManage)]
        Task<ServiceResult<BackupResultDto>> BackupAsync(string backupPath, CancellationToken ct = default);

        /// <summary>
        /// Restores the database from a backup file.
        /// WARNING: This will disconnect all users and replace the current database.
        /// </summary>
        /// <param name="backupFilePath">Full path to the .bak file.</param>
        /// <param name="ct">Cancellation token.</param>
        [RequiresPermission(PermissionKeys.SettingsManage)]
        Task<ServiceResult> RestoreAsync(string backupFilePath, CancellationToken ct = default);

        /// <summary>
        /// Returns the history of backup operations ordered by date descending.
        /// </summary>
        Task<ServiceResult<IReadOnlyList<BackupHistoryDto>>> GetHistoryAsync(CancellationToken ct = default);
    }
}
