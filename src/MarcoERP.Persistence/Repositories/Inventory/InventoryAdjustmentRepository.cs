using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Application.Interfaces;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces.Inventory;

namespace MarcoERP.Persistence.Repositories.Inventory
{
    public sealed class InventoryAdjustmentRepository : IInventoryAdjustmentRepository
    {
        private readonly MarcoDbContext _context;
        private readonly IDateTimeProvider _dateTime;

        public InventoryAdjustmentRepository(MarcoDbContext context, IDateTimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        public async Task<InventoryAdjustment> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.InventoryAdjustments
                .AsNoTracking()
                .Include(a => a.Warehouse)
                .FirstOrDefaultAsync(a => a.Id == id, ct);
        }

        public async Task<IReadOnlyList<InventoryAdjustment>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.InventoryAdjustments
                .AsNoTracking()
                .Include(a => a.Warehouse)
                .OrderByDescending(a => a.AdjustmentDate)
                .ThenByDescending(a => a.AdjustmentNumber)
                .ToListAsync(ct);
        }

        public async Task AddAsync(InventoryAdjustment entity, CancellationToken ct = default)
        {
            await _context.InventoryAdjustments.AddAsync(entity, ct);
        }

        public void Update(InventoryAdjustment entity)
        {
            if (entity == null) return;

            var local = _context.InventoryAdjustments.Local.FirstOrDefault(e => e.Id == entity.Id);
            if (local != null && !ReferenceEquals(local, entity))
            {
                _context.Entry(local).CurrentValues.SetValues(entity);
                return;
            }
            if (local != null)
            {
                if (_context.Entry(local).State == EntityState.Unchanged)
                    _context.Entry(local).State = EntityState.Modified;
                return;
            }

            _context.Entry(entity).State = EntityState.Modified;
        }
        public void Remove(InventoryAdjustment entity) => _context.InventoryAdjustments.Remove(entity);

        public async Task<InventoryAdjustment> GetWithLinesAsync(int id, CancellationToken ct = default)
        {
            return await _context.InventoryAdjustments
                .AsNoTracking()
                .Include(a => a.Warehouse)
                .Include(a => a.Lines).ThenInclude(l => l.Product)
                .Include(a => a.Lines).ThenInclude(l => l.Unit)
                .FirstOrDefaultAsync(a => a.Id == id, ct);
        }

        public async Task<InventoryAdjustment> GetWithLinesTrackedAsync(int id, CancellationToken ct = default)
        {
            return await _context.InventoryAdjustments
                .Include(a => a.Warehouse)
                .Include(a => a.Lines).ThenInclude(l => l.Product)
                .Include(a => a.Lines).ThenInclude(l => l.Unit)
                .FirstOrDefaultAsync(a => a.Id == id, ct);
        }

        public async Task<InventoryAdjustment> GetByNumberAsync(string number, CancellationToken ct = default)
        {
            return await _context.InventoryAdjustments
                .AsNoTracking()
                .Include(a => a.Warehouse)
                .FirstOrDefaultAsync(a => a.AdjustmentNumber == number, ct);
        }

        public async Task<bool> NumberExistsAsync(string number, CancellationToken ct = default)
        {
            return await _context.InventoryAdjustments.AnyAsync(a => a.AdjustmentNumber == number, ct);
        }

        public async Task<IReadOnlyList<InventoryAdjustment>> GetByStatusAsync(InvoiceStatus status, CancellationToken ct = default)
        {
            return await _context.InventoryAdjustments
                .AsNoTracking()
                .Include(a => a.Warehouse)
                .Where(a => a.Status == status)
                .OrderByDescending(a => a.AdjustmentDate)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<InventoryAdjustment>> GetByWarehouseAsync(int warehouseId, CancellationToken ct = default)
        {
            return await _context.InventoryAdjustments
                .AsNoTracking()
                .Include(a => a.Warehouse)
                .Where(a => a.WarehouseId == warehouseId)
                .OrderByDescending(a => a.AdjustmentDate)
                .ToListAsync(ct);
        }

        public async Task<string> GetNextNumberAsync(CancellationToken ct = default)
        {
            var prefix = $"ADJ-{_dateTime.UtcNow:yyyyMM}-";
            var lastNumber = await _context.InventoryAdjustments
                .AsNoTracking()
                .Where(a => a.AdjustmentNumber.StartsWith(prefix))
                .OrderByDescending(a => a.AdjustmentNumber)
                .Select(a => a.AdjustmentNumber)
                .FirstOrDefaultAsync(ct);

            if (lastNumber == null) return $"{prefix}0001";

            var seqPart = lastNumber.Substring(prefix.Length);
            if (int.TryParse(seqPart, NumberStyles.None, CultureInfo.InvariantCulture, out var seq))
                return $"{prefix}{(seq + 1):D4}";

            return $"{prefix}0001";
        }
    }
}
