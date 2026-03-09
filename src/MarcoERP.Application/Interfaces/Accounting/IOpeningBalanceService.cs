using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Accounting;

namespace MarcoERP.Application.Interfaces.Accounting
{
    /// <summary>
    /// خدمة الأرصدة الافتتاحية — إدارة أرصدة بداية السنة المالية.
    /// تشمل: حسابات عامة، عملاء، موردين، مخزون، صناديق، بنوك.
    /// مستند واحد فقط لكل سنة مالية.
    /// </summary>
    public interface IOpeningBalanceService
    {
        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets all opening balances (list view).</summary>
        [RequiresPermission(PermissionKeys.OpeningBalanceView)]
        Task<ServiceResult<IReadOnlyList<OpeningBalanceListDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Gets an opening balance by ID with all lines.</summary>
        [RequiresPermission(PermissionKeys.OpeningBalanceView)]
        Task<ServiceResult<OpeningBalanceDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>Gets the opening balance for a specific fiscal year.</summary>
        [RequiresPermission(PermissionKeys.OpeningBalanceView)]
        Task<ServiceResult<OpeningBalanceDto>> GetByFiscalYearAsync(int fiscalYearId, CancellationToken ct = default);

        // ── Commands ────────────────────────────────────────────

        /// <summary>Creates a new opening balance document (draft).</summary>
        [RequiresPermission(PermissionKeys.OpeningBalanceManage)]
        Task<ServiceResult<OpeningBalanceDto>> CreateAsync(CreateOpeningBalanceDto dto, CancellationToken ct = default);

        /// <summary>Updates a draft opening balance (replaces all lines).</summary>
        [RequiresPermission(PermissionKeys.OpeningBalanceManage)]
        Task<ServiceResult<OpeningBalanceDto>> UpdateAsync(UpdateOpeningBalanceDto dto, CancellationToken ct = default);

        /// <summary>
        /// Posts the opening balance — generates journal entry, updates subsidiary ledgers.
        /// Requires balanced debits/credits.
        /// </summary>
        [RequiresPermission(PermissionKeys.OpeningBalanceManage)]
        Task<ServiceResult<OpeningBalanceDto>> PostAsync(int id, CancellationToken ct = default);

        /// <summary>Deletes a draft opening balance.</summary>
        [RequiresPermission(PermissionKeys.OpeningBalanceManage)]
        Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default);
    }
}
