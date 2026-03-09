using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Interfaces.Sales;

namespace MarcoERP.Persistence.Repositories.Sales
{
    /// <summary>
    /// EF Core implementation of ISalesRepresentativeRepository.
    /// Global soft-delete query filter applied by SalesRepresentativeConfiguration.
    /// </summary>
    public sealed class SalesRepresentativeRepository : ISalesRepresentativeRepository
    {
        private readonly MarcoDbContext _context;

        public SalesRepresentativeRepository(MarcoDbContext context)
        {
            _context = context;
        }

        // ── IRepository<SalesRepresentative> ────────────────────

        public async Task<SalesRepresentative> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.SalesRepresentatives
                .AsNoTracking()
                .FirstOrDefaultAsync(sr => sr.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<SalesRepresentative>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SalesRepresentatives
                .AsNoTracking()
                .OrderBy(sr => sr.Code)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(SalesRepresentative entity, CancellationToken cancellationToken = default)
        {
            await _context.SalesRepresentatives.AddAsync(entity, cancellationToken);
        }

        public void Update(SalesRepresentative entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            var local = _context.SalesRepresentatives.Local.FirstOrDefault(e => e.Id == entity.Id);
            if (local != null && local != entity)
                _context.Entry(local).CurrentValues.SetValues(entity);
            else
                _context.Entry(entity).State = EntityState.Modified;
        }

        public void Remove(SalesRepresentative entity)
        {
            _context.SalesRepresentatives.Remove(entity);
        }

        // ── ISalesRepresentativeRepository ──────────────────────

        public async Task<SalesRepresentative> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            return await _context.SalesRepresentatives
                .AsNoTracking()
                .FirstOrDefaultAsync(sr => sr.Code == code, cancellationToken);
        }

        public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
        {
            return await _context.SalesRepresentatives
                .AnyAsync(sr => sr.Code == code, cancellationToken);
        }

        public async Task<bool> CodeExistsForOtherAsync(string code, int excludeId, CancellationToken cancellationToken = default)
        {
            return await _context.SalesRepresentatives
                .AnyAsync(sr => sr.Code == code && sr.Id != excludeId, cancellationToken);
        }

        public async Task<IReadOnlyList<SalesRepresentative>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetAllAsync(cancellationToken);

            var pattern = $"%{searchTerm.Trim()}%";
            return await _context.SalesRepresentatives
                .AsNoTracking()
                .Where(sr => EF.Functions.Like(sr.Code, pattern)
                           || EF.Functions.Like(sr.NameAr, pattern)
                           || (sr.NameEn != null && EF.Functions.Like(sr.NameEn, pattern))
                           || (sr.Phone != null && EF.Functions.Like(sr.Phone, pattern))
                           || (sr.Mobile != null && EF.Functions.Like(sr.Mobile, pattern)))
                .OrderBy(sr => sr.Code)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SalesRepresentative>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SalesRepresentatives
                .AsNoTracking()
                .Where(sr => sr.IsActive)
                .OrderBy(sr => sr.Code)
                .ToListAsync(cancellationToken);
        }

        public async Task<string> GetNextCodeAsync(CancellationToken cancellationToken = default)
        {
            var lastCode = await _context.SalesRepresentatives
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(sr => sr.Code.StartsWith("REP-"))
                .OrderByDescending(sr => sr.Code)
                .Select(sr => sr.Code)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastCode == null)
                return "REP-0001";

            var numPart = lastCode.Replace("REP-", "");
            if (int.TryParse(numPart, out var num))
                return $"REP-{(num + 1):D4}";

            return $"REP-{1:D4}";
        }
    }
}
