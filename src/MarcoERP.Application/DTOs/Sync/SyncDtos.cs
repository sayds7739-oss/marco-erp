using System;
using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Sync
{
    // ═══════════════════════════════════════════════════════════
    // Pull (Server → Client) DTOs
    // ═══════════════════════════════════════════════════════════

    /// <summary>Client sends this to request changes since its last checkpoint.</summary>
    public class SyncPullRequestDto
    {
        /// <summary>Client's unique device identifier.</summary>
        public string DeviceId { get; set; }

        /// <summary>Last SyncVersion the client successfully received.</summary>
        public long LastSyncVersion { get; set; }

        /// <summary>Optional: entity types to sync (null = all syncable types).</summary>
        public List<string> EntityTypes { get; set; }

        /// <summary>Max number of records per entity type (default 500).</summary>
        public int PageSize { get; set; } = 500;
    }

    /// <summary>Server returns this with all changed records since LastSyncVersion.</summary>
    public class SyncPullResponseDto
    {
        /// <summary>The maximum SyncVersion in this batch. Client saves this as new checkpoint.</summary>
        public long CurrentSyncVersion { get; set; }

        /// <summary>True if there are more changes beyond this batch.</summary>
        public bool HasMore { get; set; }

        /// <summary>Changed entities grouped by type.</summary>
        public Dictionary<string, List<SyncEntityDto>> Changes { get; set; } = new();
    }

    /// <summary>A single entity record for sync transport.</summary>
    public class SyncEntityDto
    {
        public int Id { get; set; }
        public long SyncVersion { get; set; }
        public bool IsDeleted { get; set; }

        /// <summary>Full entity data as JSON object (not serialized string).</summary>
        public object Data { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    // Push (Client → Server) DTOs
    // ═══════════════════════════════════════════════════════════

    /// <summary>Client sends this to push locally created/modified records to server.</summary>
    public class SyncPushRequestDto
    {
        public string DeviceId { get; set; }

        /// <summary>Client-generated idempotency key to prevent duplicate processing.</summary>
        public string IdempotencyKey { get; set; }

        /// <summary>Batch of changes grouped by entity type.</summary>
        public Dictionary<string, List<SyncPushEntityDto>> Changes { get; set; } = new();
    }

    /// <summary>A single entity the client wants to create or update.</summary>
    public class SyncPushEntityDto
    {
        /// <summary>Server ID (0 for new records created offline).</summary>
        public int Id { get; set; }

        /// <summary>Client-side temporary ID for new records.</summary>
        public string ClientTempId { get; set; }

        /// <summary>The SyncVersion the client last saw for this entity.</summary>
        public long BaseSyncVersion { get; set; }

        /// <summary>UTC timestamp when the client created/modified this record.</summary>
        public DateTime? ClientTimestamp { get; set; }

        /// <summary>Full entity data as JSON dictionary.</summary>
        public Dictionary<string, object> Data { get; set; }
    }

    /// <summary>Server returns this after processing a push.</summary>
    public class SyncPushResponseDto
    {
        /// <summary>How many records were successfully applied.</summary>
        public int AppliedCount { get; set; }

        /// <summary>How many conflicts were detected (server-wins applied).</summary>
        public int ConflictCount { get; set; }

        /// <summary>Mapping of client temp IDs to server-assigned IDs for new records.</summary>
        public Dictionary<string, int> IdMappings { get; set; } = new();

        /// <summary>Details of any conflicts (for client UI notification).</summary>
        public List<SyncConflictDto> Conflicts { get; set; } = new();
    }

    public class SyncConflictDto
    {
        public string EntityType { get; set; }
        public int EntityId { get; set; }
        public string Resolution { get; set; }
        public string Message { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    // Device Registration DTOs
    // ═══════════════════════════════════════════════════════════

    public class RegisterDeviceDto
    {
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string DeviceType { get; set; }
    }

    public class DeviceInfoDto
    {
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string DeviceType { get; set; }
        public long LastSyncVersion { get; set; }
        public DateTime? LastSyncAt { get; set; }
        public bool IsActive { get; set; }
    }
}
