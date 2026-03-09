using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Purchases;

namespace MarcoERP.Application.Interfaces.Purchases
{
    /// <summary>
    /// Application service contract for Supplier CRUD operations.
    /// </summary>
    public interface ISupplierService
    {
        /// <summary>Gets all suppliers.</summary>
        Task<ServiceResult<IReadOnlyList<SupplierDto>>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Gets a supplier by ID.</summary>
        Task<ServiceResult<SupplierDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Searches suppliers by name or code.</summary>
        Task<ServiceResult<IReadOnlyList<SupplierSearchResultDto>>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);

        /// <summary>Gets the next auto-generated supplier code.</summary>
        Task<ServiceResult<string>> GetNextCodeAsync(CancellationToken cancellationToken = default);

        /// <summary>Creates a new supplier.</summary>
        [RequiresPermission(PermissionKeys.PurchasesCreate)]
        Task<ServiceResult<SupplierDto>> CreateAsync(CreateSupplierDto dto, CancellationToken cancellationToken = default);

        /// <summary>Updates an existing supplier.</summary>
        [RequiresPermission(PermissionKeys.PurchasesCreate)]
        Task<ServiceResult<SupplierDto>> UpdateAsync(UpdateSupplierDto dto, CancellationToken cancellationToken = default);

        /// <summary>Activates a supplier.</summary>
        [RequiresPermission(PermissionKeys.PurchasesCreate)]
        Task<ServiceResult> ActivateAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Deactivates a supplier.</summary>
        [RequiresPermission(PermissionKeys.PurchasesCreate)]
        Task<ServiceResult> DeactivateAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Soft-deletes a supplier.</summary>
        [RequiresPermission(PermissionKeys.PurchasesCreate)]
        Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
