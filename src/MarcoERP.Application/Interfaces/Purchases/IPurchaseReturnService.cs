using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Purchases;

namespace MarcoERP.Application.Interfaces.Purchases
{
    /// <summary>
    /// Application service contract for Purchase Return operations.
    /// </summary>
    public interface IPurchaseReturnService
    {
        /// <summary>Gets all purchase returns (list view).</summary>
        Task<ServiceResult<IReadOnlyList<PurchaseReturnListDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Gets a purchase return with all lines.</summary>
        Task<ServiceResult<PurchaseReturnDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>Gets the next auto-generated return number.</summary>
        Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default);

        /// <summary>Creates a new draft purchase return.</summary>
        [RequiresPermission(PermissionKeys.PurchasesCreate)]
        Task<ServiceResult<PurchaseReturnDto>> CreateAsync(CreatePurchaseReturnDto dto, CancellationToken ct = default);

        /// <summary>Updates a draft purchase return.</summary>
        [RequiresPermission(PermissionKeys.PurchasesCreate)]
        Task<ServiceResult<PurchaseReturnDto>> UpdateAsync(UpdatePurchaseReturnDto dto, CancellationToken ct = default);

        /// <summary>Posts a return: reversal journal, stock deduction.</summary>
        [RequiresPermission(PermissionKeys.PurchasesPost)]
        Task<ServiceResult<PurchaseReturnDto>> PostAsync(int id, CancellationToken ct = default);

        /// <summary>Cancels a posted return.</summary>
        [RequiresPermission(PermissionKeys.PurchasesPost)]
        Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default);

        /// <summary>Deletes a draft return.</summary>
        [RequiresPermission(PermissionKeys.PurchasesCreate)]
        Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default);
    }
}
