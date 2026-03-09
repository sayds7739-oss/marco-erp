using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;

namespace MarcoERP.Application.Interfaces.Settings
{
    /// <summary>
    /// Phase 6: Controlled Migration Engine — orchestrates backup → migrate → log.
    /// Does NOT replace EF Core MigrateAsync; only adds a control layer.
    /// </summary>
    public interface IMigrationExecutionService
    {
        /// <summary>
        /// Returns pending EF Core migration names (not yet applied to DB).
        /// </summary>
        Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken ct = default);

        /// <summary>
        /// Executes all pending migrations with safety protocol:
        /// 1. Create backup  2. Record MigrationExecution  3. MigrateAsync  4. Mark result.
        /// </summary>
        [RequiresPermission(PermissionKeys.SettingsManage)]
        Task<ServiceResult> ExecuteMigrationsAsync(CancellationToken ct = default);

        /// <summary>
        /// Returns the history of migration executions ordered by date descending.
        /// </summary>
        Task<IReadOnlyList<MigrationExecutionDto>> GetExecutionHistoryAsync(CancellationToken ct = default);
    }
}
