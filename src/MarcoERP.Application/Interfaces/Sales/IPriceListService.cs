using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;

namespace MarcoERP.Application.Interfaces.Sales
{
    /// <summary>
    /// Application service for PriceList management.
    /// </summary>
    public interface IPriceListService
    {
        Task<ServiceResult<IReadOnlyList<PriceListListDto>>> GetAllAsync(CancellationToken ct = default);
        Task<ServiceResult<PriceListDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ServiceResult<string>> GetNextCodeAsync(CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult<PriceListDto>> CreateAsync(CreatePriceListDto dto, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult<PriceListDto>> UpdateAsync(UpdatePriceListDto dto, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.PriceListManage)]
        Task<ServiceResult> ActivateAsync(int id, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.PriceListManage)]
        Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Gets the best price for a product from all active price lists,
        /// considering the customer's assigned price list and quantity.
        /// </summary>
        Task<ServiceResult<decimal?>> GetBestPriceForCustomerAsync(
            int customerId, int productId, decimal quantity, CancellationToken ct = default);
    }
}
