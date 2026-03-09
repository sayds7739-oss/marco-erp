using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Repositories.Settings
{
    /// <summary>
    /// Repository implementation for SystemVersion and FeatureVersion entities.
    /// Phase 5: Version &amp; Integrity Engine.
    /// </summary>
    public sealed class VersionRepository : IVersionRepository
    {
        private readonly MarcoDbContext _context;

        public VersionRepository(MarcoDbContext context)
        {
            _context = context;
        }

        public async Task<SystemVersion> GetLatestVersionAsync(CancellationToken ct = default)
        {
            return await _context.SystemVersions
                .AsNoTracking()
                .OrderByDescending(v => v.AppliedAt)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<bool> VersionExistsAsync(string versionNumber, CancellationToken ct = default)
        {
            return await _context.SystemVersions
                .AnyAsync(v => v.VersionNumber == versionNumber, ct);
        }

        public async Task AddAsync(SystemVersion version, CancellationToken ct = default)
        {
            await _context.SystemVersions.AddAsync(version, ct);
        }

        public async Task<FeatureVersion> GetFeatureVersionAsync(string featureKey, CancellationToken ct = default)
        {
            return await _context.FeatureVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(fv => fv.FeatureKey == featureKey, ct);
        }
    }
}
