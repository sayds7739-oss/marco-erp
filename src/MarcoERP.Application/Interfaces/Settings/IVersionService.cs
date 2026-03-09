using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;

namespace MarcoERP.Application.Interfaces.Settings
{
    /// <summary>
    /// Manages system version tracking.
    /// Phase 5: Version &amp; Integrity Engine — tracking only.
    /// </summary>
    public interface IVersionService
    {
        /// <summary>Gets the current (latest) registered version number.</summary>
        Task<string> GetCurrentVersionAsync(CancellationToken ct = default);

        /// <summary>
        /// Registers a new system version.
        /// Only called explicitly when publishing an official release.
        /// </summary>
        [RequiresPermission(PermissionKeys.SettingsManage)]
        Task<ServiceResult> RegisterNewVersionAsync(string version, string description, CancellationToken ct = default);
    }
}
