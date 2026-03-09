using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Interfaces.Inventory;

namespace MarcoERP.Persistence.Repositories.Inventory
{
    public sealed class ProductRepository : IProductRepository
    {
        private readonly MarcoDbContext _context;

        public ProductRepository(MarcoDbContext context) => _context = context;

        public async Task<Product> GetByIdAsync(int id, CancellationToken ct = default)
            => await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);

        public async Task<Product> GetByIdWithUnitsAsync(int id, CancellationToken ct = default)
            => await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.BaseUnit)
                .Include(p => p.DefaultSupplier)
                .Include(p => p.ProductUnits).ThenInclude(pu => pu.Unit)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

        public async Task<Product> GetByCodeAsync(string code, CancellationToken ct = default)
            => await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.BaseUnit)
                .Include(p => p.DefaultSupplier)
                .Include(p => p.ProductUnits).ThenInclude(pu => pu.Unit)
                .FirstOrDefaultAsync(p => p.Code == code, ct);

        public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
            => await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.BaseUnit)
                .OrderBy(p => p.Code)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<Product>> GetAllWithUnitsAsync(CancellationToken ct = default)
            => await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.BaseUnit)
                .Include(p => p.DefaultSupplier)
                .Include(p => p.ProductUnits).ThenInclude(pu => pu.Unit)
                .OrderBy(p => p.Code)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<Product>> GetByCategoryAsync(int categoryId, CancellationToken ct = default)
            => await _context.Products
                .AsNoTracking()
                .Include(p => p.BaseUnit)
                .Where(p => p.CategoryId == categoryId)
                .OrderBy(p => p.Code)
                .ToListAsync(ct);

        public async Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken ct = default)
        {
            var query = _context.Products.Where(p => p.Code == code);
            if (excludeId.HasValue)
                query = query.Where(p => p.Id != excludeId.Value);
            return await query.AnyAsync(ct);
        }

        public async Task<IReadOnlyList<Product>> SearchAsync(string searchTerm, CancellationToken ct = default)
            => await _context.Products
                .AsNoTracking()
                .Include(p => p.BaseUnit)
                .Include(p => p.ProductUnits).ThenInclude(pu => pu.Unit)
                .Where(p => p.Status == Domain.Enums.ProductStatus.Active &&
                    (p.Code.Contains(searchTerm) ||
                     p.NameAr.Contains(searchTerm) ||
                     p.NameEn.Contains(searchTerm) ||
                     p.Barcode == searchTerm))
                .OrderBy(p => p.NameAr)
                .Take(50)
                .ToListAsync(ct);

        public async Task<Product> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
        {
            // First check product barcode
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.BaseUnit)
                .Include(p => p.ProductUnits).ThenInclude(pu => pu.Unit)
                .FirstOrDefaultAsync(p => p.Barcode == barcode, ct);

            if (product != null) return product;

            // Then check ProductUnit barcodes
            var pu = await _context.ProductUnits
                .AsNoTracking()
                .Include(x => x.Product).ThenInclude(p => p.Category)
                .Include(x => x.Product).ThenInclude(p => p.BaseUnit)
                .Include(x => x.Product).ThenInclude(p => p.ProductUnits).ThenInclude(u => u.Unit)
                .FirstOrDefaultAsync(x => x.Barcode == barcode, ct);

            return pu?.Product;
        }

        public async Task AddAsync(Product entity, CancellationToken ct = default)
            => await _context.Products.AddAsync(entity, ct);

        public void Update(Product entity)
        {
            if (entity == null)
                return;

            var local = _context.Products.Local.FirstOrDefault(p => p.Id == entity.Id);
            if (local != null && !ReferenceEquals(local, entity))
            {
                _context.Entry(local).CurrentValues.SetValues(entity);
                return;
            }

            if (local != null)
            {
                // Same reference already tracked — ensure Modified state.
                var existing = _context.Entry(local);
                if (existing.State == EntityState.Unchanged)
                    existing.State = EntityState.Modified;
                return;
            }

            // CRITICAL: Do NOT use _context.Products.Update(entity) here.
            // Update() recursively traverses the entire navigation graph
            // (Category, BaseUnit, DefaultSupplier, ProductUnits → Units)
            // and attempts to attach every related entity. When multiple
            // products share the same Category or BaseUnit, the second
            // Update() call triggers: "cannot be tracked because another
            // instance with the same key is already being tracked".
            // Using Entry().State only attaches the root entity without
            // graph traversal, avoiding the conflict entirely.
            _context.Entry(entity).State = EntityState.Modified;
        }
        public void Remove(Product entity) => _context.Products.Remove(entity);

        public async Task<string> GetNextCodeAsync(CancellationToken ct = default)
        {
            var lastCode = await _context.Products
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(p => p.Code.StartsWith("PRD-"))
                .OrderByDescending(p => p.Code)
                .Select(p => p.Code)
                .FirstOrDefaultAsync(ct);

            if (lastCode == null)
                return "PRD-0001";

            var numPart = lastCode.Replace("PRD-", "");
            if (int.TryParse(numPart, out var num))
                return $"PRD-{(num + 1):D4}";

            return "PRD-0001";
        }
    }
}
