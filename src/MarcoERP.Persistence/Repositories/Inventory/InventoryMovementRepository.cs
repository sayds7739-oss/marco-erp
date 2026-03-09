using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces.Inventory;

namespace MarcoERP.Persistence.Repositories.Inventory
{
    public sealed class InventoryMovementRepository : IInventoryMovementRepository
    {
        private readonly MarcoDbContext _context;

        public InventoryMovementRepository(MarcoDbContext context) => _context = context;

        public async Task<InventoryMovement> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => await _context.InventoryMovements
                .AsNoTracking()
                .Include(m => m.Product)
                .Include(m => m.Warehouse)
                .Include(m => m.Unit)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        public async Task<IReadOnlyList<InventoryMovement>> GetAllAsync(CancellationToken cancellationToken = default)
            => await _context.InventoryMovements
                .AsNoTracking()
                .OrderByDescending(m => m.MovementDate)
                .ThenByDescending(m => m.Id)
            .ToListAsync(cancellationToken);

        public async Task AddAsync(InventoryMovement entity, CancellationToken cancellationToken = default)
            => await _context.InventoryMovements.AddAsync(entity, cancellationToken);

        public void Update(InventoryMovement entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            var local = _context.InventoryMovements.Local.FirstOrDefault(e => e.Id == entity.Id);
            if (local != null && local != entity)
                _context.Entry(local).CurrentValues.SetValues(entity);
            else
                _context.Entry(entity).State = EntityState.Modified;
        }
        public void Remove(InventoryMovement entity) => _context.InventoryMovements.Remove(entity);

        public async Task<IReadOnlyList<InventoryMovement>> GetStockCardAsync(
            int productId, int warehouseId,
            DateTime? fromDate = null, DateTime? toDate = null,
            CancellationToken ct = default)
        {
            var query = _context.InventoryMovements
                .AsNoTracking()
                .Include(m => m.Unit)
                .Where(m => m.ProductId == productId && m.WarehouseId == warehouseId);

            if (fromDate.HasValue) query = query.Where(m => m.MovementDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(m => m.MovementDate <= toDate.Value);

            return await query
                .OrderBy(m => m.MovementDate).ThenBy(m => m.Id)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<InventoryMovement>> GetByProductAsync(
            int productId, DateTime? fromDate = null, DateTime? toDate = null,
            CancellationToken ct = default)
        {
            var query = _context.InventoryMovements
                .AsNoTracking()
                .Include(m => m.Warehouse)
                .Include(m => m.Unit)
                .Where(m => m.ProductId == productId);

            if (fromDate.HasValue) query = query.Where(m => m.MovementDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(m => m.MovementDate <= toDate.Value);

            return await query
                .OrderBy(m => m.MovementDate).ThenBy(m => m.Id)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<InventoryMovement>> GetBySourceAsync(
            SourceType sourceType, int sourceId, CancellationToken ct = default)
            => await _context.InventoryMovements
                .AsNoTracking()
                .Include(m => m.Product)
                .Include(m => m.Warehouse)
                .Include(m => m.Unit)
                .Where(m => m.SourceType == sourceType && m.SourceId == sourceId)
                .OrderBy(m => m.Id)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<InventoryMovement>> GetByDateRangeAndTypeAsync(
            DateTime fromDate, DateTime toDate, MovementType movementType, CancellationToken ct = default)
            => await _context.InventoryMovements
                .AsNoTracking()
                .Include(m => m.Product)
                .Where(m => m.MovementType == movementType
                         && m.MovementDate >= fromDate
                         && m.MovementDate < toDate)
                .OrderBy(m => m.MovementDate).ThenBy(m => m.Id)
                .ToListAsync(ct);
    }
}
