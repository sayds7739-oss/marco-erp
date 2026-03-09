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
    public sealed class WarehouseRepository : IWarehouseRepository
    {
        private readonly MarcoDbContext _context;

        public WarehouseRepository(MarcoDbContext context) => _context = context;

        public async Task<Warehouse> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        public async Task<IReadOnlyList<Warehouse>> GetAllAsync(CancellationToken cancellationToken = default)
            => await _context.Warehouses
                .AsNoTracking()
                .OrderBy(w => w.Code)
                .ToListAsync(cancellationToken);

        public async Task AddAsync(Warehouse entity, CancellationToken cancellationToken = default)
            => await _context.Warehouses.AddAsync(entity, cancellationToken);

        public void Update(Warehouse entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            var local = _context.Warehouses.Local.FirstOrDefault(e => e.Id == entity.Id);
            if (local != null && local != entity)
                _context.Entry(local).CurrentValues.SetValues(entity);
            else
                _context.Entry(entity).State = EntityState.Modified;
        }
        public void Remove(Warehouse entity) => _context.Warehouses.Remove(entity);

        public async Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken ct = default)
        {
            var query = _context.Warehouses.Where(w => w.Code == code);
            if (excludeId.HasValue)
                query = query.Where(w => w.Id != excludeId.Value);
            return await query.AnyAsync(ct);
        }

        public async Task<Warehouse> GetDefaultAsync(CancellationToken ct = default)
            => await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.IsDefault, ct);

        public async Task<IReadOnlyList<Warehouse>> GetActiveWarehousesAsync(CancellationToken ct = default)
            => await _context.Warehouses
                .AsNoTracking()
                .Where(w => w.IsActive)
                .OrderBy(w => w.Code)
                .ToListAsync(ct);
    }
}
