using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;

namespace MarcoERP.Application.Interfaces.Settings
{
    /// <summary>
    /// Application service for system settings management.
    /// Reads and updates key-value configuration pairs.
    /// </summary>
    public interface ISystemSettingsService
    {
        /// <summary>Gets all settings.</summary>
        Task<ServiceResult<IReadOnlyList<SystemSettingDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Gets all settings organized by group.</summary>
        Task<ServiceResult<IReadOnlyList<SettingGroupDto>>> GetAllGroupedAsync(CancellationToken ct = default);

        /// <summary>Gets a single setting by key.</summary>
        Task<ServiceResult<SystemSettingDto>> GetByKeyAsync(string key, CancellationToken ct = default);

        /// <summary>Updates a single setting value.</summary>
        [RequiresPermission(PermissionKeys.SettingsManage)]
        Task<ServiceResult> UpdateAsync(UpdateSystemSettingDto dto, CancellationToken ct = default);

        /// <summary>Batch-updates multiple settings.</summary>
        [RequiresPermission(PermissionKeys.SettingsManage)]
        Task<ServiceResult> UpdateBatchAsync(IEnumerable<UpdateSystemSettingDto> dtos, CancellationToken ct = default);
    }
}
