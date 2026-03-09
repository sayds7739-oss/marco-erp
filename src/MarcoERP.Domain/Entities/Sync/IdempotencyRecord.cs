using System;
using MarcoERP.Domain.Entities.Common;

namespace MarcoERP.Domain.Entities.Sync
{
    /// <summary>
    /// Stores idempotency keys to prevent duplicate operations from mobile clients.
    /// When a client retries a request (e.g., due to network timeout),
    /// the server returns the cached response instead of re-processing.
    /// Records expire after 24 hours.
    /// </summary>
    public sealed class IdempotencyRecord : BaseEntity
    {
        private IdempotencyRecord() { }

        public IdempotencyRecord(string idempotencyKey, string requestPath, string requestBody, int? userId = null)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));

            IdempotencyKey = idempotencyKey.Trim();
            RequestPath = requestPath;
            RequestBody = requestBody;
            UserId = userId;
            CreatedAt = DateTime.UtcNow;
            ExpiresAt = DateTime.UtcNow.AddHours(24);
        }

        /// <summary>Client-generated unique key (UUID) for this operation.</summary>
        public string IdempotencyKey { get; private set; }

        /// <summary>The user who made the request (prevents cross-user replay).</summary>
        public int? UserId { get; private set; }

        /// <summary>HTTP request path (e.g., "/api/sync/push").</summary>
        public string RequestPath { get; private set; }

        /// <summary>Serialized request body (for audit).</summary>
        public string RequestBody { get; private set; }

        /// <summary>HTTP status code of the cached response.</summary>
        public int ResponseStatusCode { get; private set; }

        /// <summary>Serialized response body to replay on retry.</summary>
        public string ResponseBody { get; private set; }

        /// <summary>When the record was first created.</summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>When this idempotency record expires (24h after creation).</summary>
        public DateTime ExpiresAt { get; private set; }

        public void SetResponse(int statusCode, string responseBody)
        {
            ResponseStatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}
