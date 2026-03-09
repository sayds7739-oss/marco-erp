using System;
using MarcoERP.Domain.Entities.Common;

namespace MarcoERP.Domain.Entities.Sync
{
    /// <summary>
    /// Records a sync conflict when a client pushes a change that conflicts
    /// with the server's current state (e.g., concurrent edits to the same entity).
    /// Server-wins strategy is applied automatically; this log enables audit and manual review.
    /// </summary>
    public sealed class SyncConflict : BaseEntity
    {
        private SyncConflict() { }

        public SyncConflict(
            string entityType,
            int entityId,
            string deviceId,
            string clientData,
            string serverData,
            string resolution)
        {
            EntityType = entityType;
            EntityId = entityId;
            DeviceId = deviceId;
            ClientData = clientData;
            ServerData = serverData;
            Resolution = resolution;
            OccurredAt = DateTime.UtcNow;
        }

        /// <summary>Full entity type name (e.g., "Product", "SalesInvoice").</summary>
        public string EntityType { get; private set; }

        /// <summary>The primary key of the conflicting entity.</summary>
        public int EntityId { get; private set; }

        /// <summary>Which device triggered the conflict.</summary>
        public string DeviceId { get; private set; }

        /// <summary>JSON snapshot of what the client tried to push.</summary>
        public string ClientData { get; private set; }

        /// <summary>JSON snapshot of the server's current state.</summary>
        public string ServerData { get; private set; }

        /// <summary>How the conflict was resolved (e.g., "ServerWins", "Merged").</summary>
        public string Resolution { get; private set; }

        /// <summary>When the conflict was detected.</summary>
        public DateTime OccurredAt { get; private set; }
    }
}
