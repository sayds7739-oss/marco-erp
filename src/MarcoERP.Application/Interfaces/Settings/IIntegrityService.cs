using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;

namespace MarcoERP.Application.Interfaces.Settings
{
    /// <summary>
    /// خدمة فحص سلامة البيانات المالية والمخزنية.
    /// Implementation: Persistence layer (uses EF Core / raw SQL).
    /// </summary>
    public interface IIntegrityService
    {
        /// <summary>
        /// Verifies total debits == total credits across all posted journal entries.
        /// </summary>
        [RequiresPermission(PermissionKeys.SettingsManage)]
        Task<ServiceResult<TrialBalanceCheckResult>> CheckTrialBalanceAsync(CancellationToken ct = default);

        /// <summary>
        /// Verifies each posted journal entry has balanced DR/CR.
        /// </summary>
        [RequiresPermission(PermissionKeys.SettingsManage)]
        Task<ServiceResult<JournalBalanceCheckResult>> CheckJournalBalancesAsync(CancellationToken ct = default);

        /// <summary>
        /// Verifies warehouse quantities match sum of inventory movements.
        /// </summary>
        [RequiresPermission(PermissionKeys.SettingsManage)]
        Task<ServiceResult<InventoryCheckResult>> CheckInventoryReconciliationAsync(CancellationToken ct = default);

        /// <summary>
        /// Runs all 3 integrity checks and returns a combined report.
        /// </summary>
        [RequiresPermission(PermissionKeys.SettingsManage)]
        Task<ServiceResult<IntegrityReportDto>> RunFullCheckAsync(CancellationToken ct = default);
    }
}
