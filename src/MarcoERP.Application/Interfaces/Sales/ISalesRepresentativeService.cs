using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;

namespace MarcoERP.Application.Interfaces.Sales
{
    public interface ISalesRepresentativeService
    {
        Task<ServiceResult<IReadOnlyList<SalesRepresentativeDto>>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<ServiceResult<SalesRepresentativeDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<ServiceResult<IReadOnlyList<SalesRepresentativeSearchResultDto>>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
        Task<ServiceResult<IReadOnlyList<SalesRepresentativeDto>>> GetActiveAsync(CancellationToken cancellationToken = default);
        Task<ServiceResult<string>> GetNextCodeAsync(CancellationToken cancellationToken = default);
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult<SalesRepresentativeDto>> CreateAsync(CreateSalesRepresentativeDto dto, CancellationToken cancellationToken = default);
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult<SalesRepresentativeDto>> UpdateAsync(UpdateSalesRepresentativeDto dto, CancellationToken cancellationToken = default);
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult> ActivateAsync(int id, CancellationToken cancellationToken = default);
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult> DeactivateAsync(int id, CancellationToken cancellationToken = default);
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
