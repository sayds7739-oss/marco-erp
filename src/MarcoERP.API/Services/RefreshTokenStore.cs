using MarcoERP.Domain.Entities.Security;
using MarcoERP.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarcoERP.API.Services;

/// <summary>
/// Database-backed refresh token store. Tokens survive application restarts.
/// Registered as Scoped (uses DbContext).
/// </summary>
public class RefreshTokenStore
{
    private readonly MarcoDbContext _db;

    public RefreshTokenStore(MarcoDbContext db)
    {
        _db = db;
    }

    public async Task StoreAsync(string token, int userId, DateTime expiresAt)
    {
        // Cleanup expired tokens for this user
        var expired = await _db.RefreshTokens
            .Where(t => t.UserId == userId && (t.ExpiresAt < DateTime.UtcNow || t.IsRevoked))
            .ToListAsync();
        _db.RefreshTokens.RemoveRange(expired);

        _db.RefreshTokens.Add(new RefreshToken
        {
            Token = token,
            UserId = userId,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        });

        await _db.SaveChangesAsync();
    }

    public async Task<RefreshTokenEntry?> ValidateAsync(string token)
    {
        var entry = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow);

        if (entry == null) return null;

        return new RefreshTokenEntry
        {
            UserId = entry.UserId,
            ExpiresAt = entry.ExpiresAt,
            CreatedAt = entry.CreatedAt,
            IsRevoked = entry.IsRevoked
        };
    }

    public async Task RevokeAsync(string token)
    {
        var entry = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == token);
        if (entry != null)
        {
            entry.IsRevoked = true;
            entry.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task RevokeAllForUserAsync(int userId)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync();
        foreach (var t in tokens)
        {
            t.IsRevoked = true;
            t.RevokedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }
}

public class RefreshTokenEntry
{
    public int UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRevoked { get; set; }
}
