using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;

namespace MarcoERP.Application.Interfaces.Settings
{
    /// <summary>
    /// Performs selective cleanup of transactional and master data.
    /// </summary>
    public interface IDataPurgeService
    {
        /// <summary>
        /// Purges data inside a single transaction according to selected keep options.
        /// </summary>
        [RequiresPermission(PermissionKeys.SettingsManage)]
        Task<ServiceResult<DataPurgeResultDto>> PurgeAsync(DataPurgeOptionsDto options, CancellationToken ct = default);
    }
}
