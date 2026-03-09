using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;

namespace MarcoERP.Application.Interfaces.Inventory
{
    /// <summary>
    /// Application service for Product management.
    /// Handles CRUD with multi-unit support, barcode search, etc.
    /// </summary>
    public interface IProductService
    {
        // ── Queries ─────────────────────────────────────────────

        /// <summary>استرجاع جميع الأصناف — Gets all products.</summary>
        Task<ServiceResult<IReadOnlyList<ProductDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>استرجاع صنف بالمعرّف — Gets a product by ID.</summary>
        Task<ServiceResult<ProductDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>استرجاع صنف بالكود — Gets a product by its unique code.</summary>
        Task<ServiceResult<ProductDto>> GetByCodeAsync(string code, CancellationToken ct = default);

        /// <summary>استرجاع أصناف حسب التصنيف — Gets products by category.</summary>
        Task<ServiceResult<IReadOnlyList<ProductDto>>> GetByCategoryAsync(int categoryId, CancellationToken ct = default);

        /// <summary>بحث الأصناف بالاسم أو الكود أو الباركود — Searches products by name, code, or barcode.</summary>
        Task<ServiceResult<IReadOnlyList<ProductSearchResultDto>>> SearchAsync(string searchTerm, CancellationToken ct = default);

        /// <summary>استرجاع الكود التالي للأصناف — Gets the next auto-generated product code.</summary>
        Task<ServiceResult<string>> GetNextCodeAsync(CancellationToken ct = default);

        // ── Commands ────────────────────────────────────────────

        /// <summary>إنشاء صنف جديد — Creates a new product.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult<ProductDto>> CreateAsync(CreateProductDto dto, CancellationToken ct = default);

        /// <summary>تعديل صنف — Updates an existing product.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult<ProductDto>> UpdateAsync(UpdateProductDto dto, CancellationToken ct = default);

        /// <summary>تفعيل صنف — Activates a product.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult> ActivateAsync(int id, CancellationToken ct = default);

        /// <summary>تعطيل صنف — Deactivates a product.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct = default);

        /// <summary>حذف صنف — Soft-deletes a product.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
    }
}
