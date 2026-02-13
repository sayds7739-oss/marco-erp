using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Interfaces.Inventory;

namespace MarcoERP.Persistence.Repositories.Inventory
{
    public sealed class WarehouseProductRepository : IWarehouseProductRepository
    {
        private readonly MarcoDbContext _context;

        public WarehouseProductRepository(MarcoDbContext context) => _context = context;

        public async Task<WarehouseProduct> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => await _context.WarehouseProducts.FirstOrDefaultAsync(wp => wp.Id == id, cancellationToken);

        public async Task<IReadOnlyList<WarehouseProduct>> GetAllAsync(CancellationToken cancellationToken = default)
            => await _context.WarehouseProducts.ToListAsync(cancellationToken);

        public async Task AddAsync(WarehouseProduct entity, CancellationToken cancellationToken = default)
            => await _context.WarehouseProducts.AddAsync(entity, cancellationToken);

        public void Update(WarehouseProduct entity)
        {
            var entry = _context.Entry(entity);
            if (entry.State == EntityState.Added)
                return;

            if (entry.State == EntityState.Detached)
            {
                if (entity.Id == 0)
                    _context.WarehouseProducts.Add(entity);
                else
                    _context.WarehouseProducts.Update(entity);

                return;
            }

            _context.WarehouseProducts.Update(entity);
        }
        public void Remove(WarehouseProduct entity) => _context.WarehouseProducts.Remove(entity);

        public async Task<WarehouseProduct> GetAsync(int warehouseId, int productId, CancellationToken ct = default)
            => await _context.WarehouseProducts
                .FirstOrDefaultAsync(wp => wp.WarehouseId == warehouseId && wp.ProductId == productId, ct);

        public async Task<WarehouseProduct> GetOrCreateAsync(int warehouseId, int productId, CancellationToken ct = default)
        {
            var existing = await GetAsync(warehouseId, productId, ct);
            if (existing != null) return existing;

            var newWp = new WarehouseProduct(warehouseId, productId, 0);
            await _context.WarehouseProducts.AddAsync(newWp, ct);
            return newWp;
        }

        public async Task<decimal> GetTotalStockAsync(int productId, CancellationToken ct = default)
            => await _context.WarehouseProducts
                .Where(wp => wp.ProductId == productId)
                .SumAsync(wp => wp.Quantity, ct);

        public async Task<IReadOnlyList<WarehouseProduct>> GetByProductAsync(int productId, CancellationToken ct = default)
            => await _context.WarehouseProducts
                .Include(wp => wp.Warehouse)
                .Include(wp => wp.Product).ThenInclude(p => p.BaseUnit)
                .Where(wp => wp.ProductId == productId)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<WarehouseProduct>> GetByWarehouseAsync(int warehouseId, CancellationToken ct = default)
            => await _context.WarehouseProducts
                .Include(wp => wp.Product).ThenInclude(p => p.BaseUnit)
                .Include(wp => wp.Warehouse)
                .Where(wp => wp.WarehouseId == warehouseId)
                .OrderBy(wp => wp.Product.NameAr)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<WarehouseProduct>> GetBelowMinimumStockAsync(CancellationToken ct = default)
            => await _context.WarehouseProducts
                .Include(wp => wp.Product).ThenInclude(p => p.BaseUnit)
                .Include(wp => wp.Warehouse)
                .Where(wp => wp.Quantity < wp.Product.MinimumStock)
                .OrderBy(wp => wp.Product.NameAr)
                .ToListAsync(ct);
    }
}
