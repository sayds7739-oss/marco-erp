using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;

namespace MarcoERP.Application.Interfaces.Settings
{
    /// <summary>
    /// Application service contract for Feature Governance.
    /// Phase 2: Feature Governance Engine.
    /// </summary>
    public interface IFeatureService
    {
        /// <summary>Gets all features.</summary>
        Task<ServiceResult<IReadOnlyList<FeatureDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Gets a feature by its unique key.</summary>
        Task<ServiceResult<FeatureDto>> GetByKeyAsync(string featureKey, CancellationToken ct = default);

        /// <summary>Checks if a feature is enabled.</summary>
        Task<ServiceResult<bool>> IsEnabledAsync(string featureKey, CancellationToken ct = default);

        /// <summary>Toggles a feature on/off. Records change in FeatureChangeLog.</summary>
        [RequiresPermission(PermissionKeys.SettingsManage)]
        Task<ServiceResult> ToggleAsync(ToggleFeatureDto dto, CancellationToken ct = default);
    }
}
