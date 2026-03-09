using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Security;

namespace MarcoERP.Application.Interfaces.Security
{
    /// <summary>
    /// Application service for role management (read-only for system roles).
    /// Roles are seeded by the system; this service provides query access.
    /// </summary>
    public interface IRoleService
    {
        /// <summary>استرجاع جميع الأدوار — Gets all roles.</summary>
        Task<ServiceResult<IReadOnlyList<RoleListDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>استرجاع دور بالمعرّف مع الصلاحيات — Gets a role by ID with its permissions.</summary>
        Task<ServiceResult<RoleDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>إنشاء دور جديد — Creates a new role.</summary>
        [RequiresPermission(PermissionKeys.RolesManage)]
        Task<ServiceResult<RoleDto>> CreateAsync(CreateRoleDto dto, CancellationToken ct = default);

        /// <summary>تحديث دور — Updates an existing role.</summary>
        [RequiresPermission(PermissionKeys.RolesManage)]
        Task<ServiceResult<RoleDto>> UpdateAsync(UpdateRoleDto dto, CancellationToken ct = default);

        /// <summary>حذف دور — Deletes a role (non-system only).</summary>
        [RequiresPermission(PermissionKeys.RolesManage)]
        Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
    }
}
