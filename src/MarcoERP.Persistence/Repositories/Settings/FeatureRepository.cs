using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Settings;
using MarcoERP.Domain.Interfaces.Settings;

namespace MarcoERP.Persistence.Repositories.Settings
{
    /// <summary>
    /// EF Core implementation of IFeatureRepository.
    /// Phase 2: Feature Governance Engine.
    /// </summary>
    public sealed class FeatureRepository : IFeatureRepository
    {
        private readonly MarcoDbContext _context;

        public FeatureRepository(MarcoDbContext context) => _context = context;

        // ── IRepository<Feature> ─────────────────────────────────

        public async Task<Feature> GetByIdAsync(int id, CancellationToken ct = default)
            => await _context.Features
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id, ct);

        public async Task<IReadOnlyList<Feature>> GetAllAsync(CancellationToken ct = default)
            => await _context.Features
                .AsNoTracking()
                .OrderBy(f => f.FeatureKey)
                .ToListAsync(ct);

        public async Task AddAsync(Feature entity, CancellationToken ct = default)
            => await _context.Features.AddAsync(entity, ct);

        public void Update(Feature entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            var local = _context.Features.Local.FirstOrDefault(e => e.Id == entity.Id);
            if (local != null && local != entity)
                _context.Entry(local).CurrentValues.SetValues(entity);
            else
                _context.Entry(entity).State = EntityState.Modified;
        }
        public void Remove(Feature entity) => _context.Features.Remove(entity);

        // ── IFeatureRepository ───────────────────────────────────

        public async Task<Feature> GetByKeyAsync(string featureKey, CancellationToken ct = default)
            => await _context.Features
                .FirstOrDefaultAsync(f => f.FeatureKey == featureKey, ct);

        public async Task<bool> KeyExistsAsync(string featureKey, CancellationToken ct = default)
            => await _context.Features.AnyAsync(f => f.FeatureKey == featureKey, ct);

        public async Task AddChangeLogAsync(FeatureChangeLog log, CancellationToken ct = default)
            => await _context.FeatureChangeLogs.AddAsync(log, ct);

        public async Task<IReadOnlyList<FeatureChangeLog>> GetChangeLogsAsync(int featureId, CancellationToken ct = default)
            => await _context.FeatureChangeLogs
                .AsNoTracking()
                .Where(c => c.FeatureId == featureId)
                .OrderByDescending(c => c.ChangedAt)
                .ToListAsync(ct);
    }
}
