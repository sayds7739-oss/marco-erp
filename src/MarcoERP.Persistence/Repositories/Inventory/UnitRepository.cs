using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Interfaces.Inventory;

namespace MarcoERP.Persistence.Repositories.Inventory
{
    public sealed class UnitRepository : IUnitRepository
    {
        private readonly MarcoDbContext _context;

        public UnitRepository(MarcoDbContext context) => _context = context;

        public async Task<Unit> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => await _context.Units
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        public async Task<IReadOnlyList<Unit>> GetAllAsync(CancellationToken cancellationToken = default)
            => await _context.Units
                .AsNoTracking()
                .OrderBy(u => u.NameAr)
                .ToListAsync(cancellationToken);

        public async Task AddAsync(Unit entity, CancellationToken cancellationToken = default)
            => await _context.Units.AddAsync(entity, cancellationToken);

        public void Update(Unit entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            var local = _context.Units.Local.FirstOrDefault(e => e.Id == entity.Id);
            if (local != null && local != entity)
                _context.Entry(local).CurrentValues.SetValues(entity);
            else
                _context.Entry(entity).State = EntityState.Modified;
        }
        public void Remove(Unit entity) => _context.Units.Remove(entity);

        public async Task<bool> NameExistsAsync(string nameAr, int? excludeId = null, CancellationToken ct = default)
        {
            var query = _context.Units.Where(u => u.NameAr == nameAr);
            if (excludeId.HasValue)
                query = query.Where(u => u.Id != excludeId.Value);
            return await query.AnyAsync(ct);
        }

        public async Task<IReadOnlyList<Unit>> GetActiveUnitsAsync(CancellationToken ct = default)
            => await _context.Units
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.NameAr)
                .ToListAsync(ct);
    }
}
