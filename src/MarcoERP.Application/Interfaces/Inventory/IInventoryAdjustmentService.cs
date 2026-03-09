using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;

namespace MarcoERP.Application.Interfaces.Inventory
{
    /// <summary>
    /// Application service for inventory adjustments.
    /// </summary>
    public interface IInventoryAdjustmentService
    {
        [RequiresPermission(PermissionKeys.InventoryAdjustmentView)]
        Task<ServiceResult<IReadOnlyList<InventoryAdjustmentListDto>>> GetAllAsync(CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.InventoryAdjustmentView)]
        Task<ServiceResult<InventoryAdjustmentDto>> GetByIdAsync(int id, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.InventoryAdjustmentView)]
        Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.InventoryAdjustmentCreate)]
        Task<ServiceResult<InventoryAdjustmentDto>> CreateAsync(CreateInventoryAdjustmentDto dto, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.InventoryAdjustmentCreate)]
        Task<ServiceResult<InventoryAdjustmentDto>> UpdateAsync(UpdateInventoryAdjustmentDto dto, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.InventoryAdjustmentPost)]
        Task<ServiceResult<InventoryAdjustmentDto>> PostAsync(int id, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.InventoryAdjustmentPost)]
        Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.InventoryAdjustmentCreate)]
        Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default);
    }
}
