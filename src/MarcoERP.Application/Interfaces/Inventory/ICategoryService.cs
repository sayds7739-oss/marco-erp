using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;

namespace MarcoERP.Application.Interfaces.Inventory
{
    /// <summary>
    /// Application service for Category management.
    /// </summary>
    public interface ICategoryService
    {
        /// <summary>استرجاع جميع التصنيفات — Gets all categories.</summary>
        Task<ServiceResult<IReadOnlyList<CategoryDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>استرجاع تصنيف بالمعرّف — Gets a category by ID.</summary>
        Task<ServiceResult<CategoryDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>استرجاع التصنيفات الجذرية (المستوى الأول) — Gets root-level categories.</summary>
        Task<ServiceResult<IReadOnlyList<CategoryDto>>> GetRootCategoriesAsync(CancellationToken ct = default);

        /// <summary>استرجاع التصنيفات الفرعية — Gets child categories of a parent.</summary>
        Task<ServiceResult<IReadOnlyList<CategoryDto>>> GetChildrenAsync(int parentId, CancellationToken ct = default);

        /// <summary>إنشاء تصنيف جديد — Creates a new category.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult<CategoryDto>> CreateAsync(CreateCategoryDto dto, CancellationToken ct = default);

        /// <summary>تعديل تصنيف — Updates an existing category.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult<CategoryDto>> UpdateAsync(UpdateCategoryDto dto, CancellationToken ct = default);

        /// <summary>تفعيل تصنيف — Activates a category.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult> ActivateAsync(int id, CancellationToken ct = default);

        /// <summary>تعطيل تصنيف — Deactivates a category.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct = default);
    }
}
