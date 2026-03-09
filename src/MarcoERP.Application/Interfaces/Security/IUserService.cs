using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Security;

namespace MarcoERP.Application.Interfaces.Security
{
    /// <summary>
    /// Application service for user CRUD management.
    /// Used by administrators to create, update, and manage user accounts.
    /// </summary>
    public interface IUserService
    {
        /// <summary>استرجاع جميع المستخدمين — Gets all users.</summary>
        Task<ServiceResult<IReadOnlyList<UserListDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>استرجاع مستخدم بالمعرّف — Gets a user by ID.</summary>
        Task<ServiceResult<UserDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>إنشاء مستخدم جديد — Creates a new user account.</summary>
        [RequiresPermission(PermissionKeys.UsersManage)]
        Task<ServiceResult<UserDto>> CreateAsync(CreateUserDto dto, CancellationToken ct = default);

        /// <summary>تعديل بيانات مستخدم — Updates user profile information.</summary>
        [RequiresPermission(PermissionKeys.UsersManage)]
        Task<ServiceResult<UserDto>> UpdateAsync(UpdateUserDto dto, CancellationToken ct = default);

        /// <summary>إعادة تعيين كلمة المرور — Resets a user's password (admin action).</summary>
        [RequiresPermission(PermissionKeys.UsersManage)]
        Task<ServiceResult> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default);

        /// <summary>تفعيل حساب مستخدم — Activates a user account.</summary>
        [RequiresPermission(PermissionKeys.UsersManage)]
        Task<ServiceResult> ActivateAsync(int id, CancellationToken ct = default);

        /// <summary>تعطيل حساب مستخدم — Deactivates a user account.</summary>
        [RequiresPermission(PermissionKeys.UsersManage)]
        Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct = default);

        /// <summary>فتح حساب مقفل — Unlocks a locked user account.</summary>
        [RequiresPermission(PermissionKeys.UsersManage)]
        Task<ServiceResult> UnlockAsync(int id, CancellationToken ct = default);
    }
}
