using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;

namespace MarcoERP.Application.Interfaces.Inventory
{
    /// <summary>
    /// Service for bulk price update operations.
    /// </summary>
    public interface IBulkPriceUpdateService
    {
        /// <summary>
        /// Preview the price changes before applying.
        /// </summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult<IReadOnlyList<BulkPricePreviewItemDto>>> PreviewAsync(
            BulkPriceUpdateRequestDto request, CancellationToken ct = default);

        /// <summary>
        /// Apply the bulk price update. Audits all changes.
        /// </summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult<BulkPriceUpdateResultDto>> ApplyAsync(
            BulkPriceUpdateRequestDto request, CancellationToken ct = default);
    }
}
