using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;

namespace MarcoERP.Application.Interfaces.Sales
{
    /// <summary>
    /// Application service contract for Customer CRUD operations.
    /// </summary>
    public interface ICustomerService
    {
        /// <summary>Gets all customers.</summary>
        Task<ServiceResult<IReadOnlyList<CustomerDto>>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Gets a customer by ID.</summary>
        Task<ServiceResult<CustomerDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Searches customers by name or code.</summary>
        Task<ServiceResult<IReadOnlyList<CustomerSearchResultDto>>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);

        /// <summary>Gets the next auto-generated customer code.</summary>
        Task<ServiceResult<string>> GetNextCodeAsync(CancellationToken cancellationToken = default);

        /// <summary>Creates a new customer.</summary>
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult<CustomerDto>> CreateAsync(CreateCustomerDto dto, CancellationToken cancellationToken = default);

        /// <summary>Updates an existing customer.</summary>
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult<CustomerDto>> UpdateAsync(UpdateCustomerDto dto, CancellationToken cancellationToken = default);

        /// <summary>Activates a customer.</summary>
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult> ActivateAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Deactivates a customer.</summary>
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult> DeactivateAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Soft-deletes a customer.</summary>
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
