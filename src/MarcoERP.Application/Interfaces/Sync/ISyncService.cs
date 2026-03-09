using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sync;

namespace MarcoERP.Application.Interfaces.Sync
{
    /// <summary>
    /// Orchestrates all sync operations between mobile/desktop clients and the server.
    /// </summary>
    public interface ISyncService
    {
        /// <summary>
        /// Returns all entity changes since the client's last known SyncVersion.
        /// </summary>
        Task<ServiceResult<SyncPullResponseDto>> PullChangesAsync(
            SyncPullRequestDto request, CancellationToken ct = default);

        /// <summary>
        /// Applies client-side changes to the server with conflict detection.
        /// Uses server-wins strategy; conflicts are logged for audit.
        /// </summary>
        Task<ServiceResult<SyncPushResponseDto>> PushChangesAsync(
            SyncPushRequestDto request, int userId, CancellationToken ct = default);

        /// <summary>
        /// Registers a device for sync and returns its current status.
        /// </summary>
        Task<ServiceResult<DeviceInfoDto>> RegisterDeviceAsync(
            RegisterDeviceDto request, int userId, CancellationToken ct = default);

        /// <summary>
        /// Returns sync metadata (current max SyncVersion, device info).
        /// </summary>
        Task<ServiceResult<SyncStatusDto>> GetSyncStatusAsync(
            string deviceId, CancellationToken ct = default);
    }

    public class SyncStatusDto
    {
        public long ServerSyncVersion { get; set; }
        public long DeviceLastSyncVersion { get; set; }
        public long PendingChanges { get; set; }
        public bool DeviceRegistered { get; set; }
    }
}
