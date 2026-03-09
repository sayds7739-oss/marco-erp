using System;

namespace MarcoERP.Domain.Entities.Security
{
    /// <summary>
    /// Persistent refresh token for API authentication.
    /// Stored in database to survive application restarts.
    /// </summary>
    public class RefreshToken
    {
        public int Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime? RevokedAt { get; set; }

        public User User { get; set; } = null!;
    }
}
