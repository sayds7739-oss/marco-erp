using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;

namespace MarcoERP.Application.Interfaces.Sales
{
    /// <summary>
    /// Application service contract for Sales Return operations.
    /// </summary>
    public interface ISalesReturnService
    {
        /// <summary>Gets all sales returns (list view).</summary>
        Task<ServiceResult<IReadOnlyList<SalesReturnListDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Gets a sales return with all lines.</summary>
        Task<ServiceResult<SalesReturnDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>Gets the next auto-generated return number.</summary>
        Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default);

        /// <summary>Creates a new draft sales return.</summary>
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult<SalesReturnDto>> CreateAsync(CreateSalesReturnDto dto, CancellationToken ct = default);

        /// <summary>Updates a draft sales return.</summary>
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult<SalesReturnDto>> UpdateAsync(UpdateSalesReturnDto dto, CancellationToken ct = default);

        /// <summary>Posts a return: reversal revenue journal + reversal COGS journal, stock re-addition.</summary>
        [RequiresPermission(PermissionKeys.SalesPost)]
        Task<ServiceResult<SalesReturnDto>> PostAsync(int id, CancellationToken ct = default);

        /// <summary>Cancels a posted return.</summary>
        [RequiresPermission(PermissionKeys.SalesPost)]
        Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default);

        /// <summary>Deletes a draft return.</summary>
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default);
    }
}
