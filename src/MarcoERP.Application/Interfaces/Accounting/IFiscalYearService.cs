using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Accounting;

namespace MarcoERP.Application.Interfaces.Accounting
{
    /// <summary>
    /// Application service for fiscal year and period management.
    /// Handles year creation, activation, closure, and period lock/unlock.
    /// </summary>
    public interface IFiscalYearService
    {
        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets a fiscal year by ID with all 12 periods.</summary>
        Task<ServiceResult<FiscalYearDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Gets a fiscal year by calendar year number.</summary>
        Task<ServiceResult<FiscalYearDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default);

        /// <summary>Gets the currently active fiscal year.</summary>
        Task<ServiceResult<FiscalYearDto>> GetActiveYearAsync(CancellationToken cancellationToken = default);

        /// <summary>Gets all fiscal years.</summary>
        Task<ServiceResult<IReadOnlyList<FiscalYearDto>>> GetAllAsync(CancellationToken cancellationToken = default);

        // ── Fiscal Year Commands ────────────────────────────────

        /// <summary>Creates a new fiscal year with 12 monthly periods (FY-INV-04).</summary>
        [RequiresPermission(PermissionKeys.FiscalYearManage)]
        Task<ServiceResult<FiscalYearDto>> CreateAsync(CreateFiscalYearDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Activates a fiscal year (FY-INV-03: only one active at a time).
        /// Verifies no other year is currently active.
        /// </summary>
        [RequiresPermission(PermissionKeys.FiscalYearManage)]
        Task<ServiceResult> ActivateAsync(int fiscalYearId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes a fiscal year (FY-INV-06, FY-INV-07, FY-INV-08).
        /// Requires all 12 periods locked, no pending drafts.
        /// Closure is irreversible.
        /// </summary>
        [RequiresPermission(PermissionKeys.FiscalYearManage)]
        Task<ServiceResult> CloseAsync(int fiscalYearId, CancellationToken cancellationToken = default);

        // ── Period Commands ─────────────────────────────────────

        /// <summary>
        /// Locks a fiscal period (PER-01: sequential locking).
        /// Requires all drafts in the period to be resolved (FP-INV-04).
        /// </summary>
        [RequiresPermission(PermissionKeys.FiscalPeriodManage)]
        Task<ServiceResult> LockPeriodAsync(int fiscalPeriodId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unlocks the most recent locked period (PER-05: admin-only).
        /// Requires mandatory justification note (PER-06).
        /// </summary>
        [RequiresPermission(PermissionKeys.FiscalPeriodManage)]
        Task<ServiceResult> UnlockPeriodAsync(int fiscalPeriodId, string reason, CancellationToken cancellationToken = default);
    }
}
