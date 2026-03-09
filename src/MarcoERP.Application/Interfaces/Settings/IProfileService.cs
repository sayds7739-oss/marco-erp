using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;

namespace MarcoERP.Application.Interfaces.Settings
{
    /// <summary>
    /// Application service contract for Profile management.
    /// Phase 3: Progressive Complexity Layer.
    /// </summary>
    public interface IProfileService
    {
        /// <summary>Gets all available profiles.</summary>
        Task<ServiceResult<IReadOnlyList<SystemProfileDto>>> GetAllProfilesAsync(CancellationToken ct = default);

        /// <summary>Gets the currently active profile name.</summary>
        Task<ServiceResult<string>> GetCurrentProfileAsync(CancellationToken ct = default);

        /// <summary>
        /// Applies a profile by name. Enables features mapped to the profile,
        /// disables features not mapped to it.
        /// </summary>
        [RequiresPermission(PermissionKeys.SettingsManage)]
        Task<ServiceResult> ApplyProfileAsync(string profileName, CancellationToken ct = default);

        /// <summary>Gets the feature keys that are enabled for a given profile.</summary>
        Task<ServiceResult<IReadOnlyList<string>>> GetProfileFeaturesAsync(string profileName, CancellationToken ct = default);
    }
}
