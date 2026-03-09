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
    public sealed class CategoryRepository : ICategoryRepository
    {
        private readonly MarcoDbContext _context;

        public CategoryRepository(MarcoDbContext context) => _context = context;

        public async Task<Category> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => await _context.Categories
                .AsNoTracking()
                .Include(c => c.ParentCategory)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
            => await _context.Categories
                .AsNoTracking()
                .Include(c => c.ParentCategory)
                .OrderBy(c => c.Level).ThenBy(c => c.NameAr)
            .ToListAsync(cancellationToken);

        public async Task AddAsync(Category entity, CancellationToken cancellationToken = default)
            => await _context.Categories.AddAsync(entity, cancellationToken);

        public void Update(Category entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            var local = _context.Categories.Local.FirstOrDefault(e => e.Id == entity.Id);
            if (local != null && local != entity)
                _context.Entry(local).CurrentValues.SetValues(entity);
            else
                _context.Entry(entity).State = EntityState.Modified;
        }
        public void Remove(Category entity) => _context.Categories.Remove(entity);

        public async Task<IReadOnlyList<Category>> GetRootCategoriesAsync(CancellationToken ct = default)
            => await _context.Categories
                .AsNoTracking()
                .Where(c => c.Level == 1)
                .OrderBy(c => c.NameAr)
            .ToListAsync(ct);

        public async Task<IReadOnlyList<Category>> GetChildrenAsync(int parentId, CancellationToken ct = default)
            => await _context.Categories
                .AsNoTracking()
                .Where(c => c.ParentCategoryId == parentId)
                .OrderBy(c => c.NameAr)
            .ToListAsync(ct);

        public async Task<bool> NameExistsAsync(string nameAr, int? parentId, int? excludeId = null, CancellationToken ct = default)
        {
            var query = _context.Categories
                .Where(c => c.NameAr == nameAr && c.ParentCategoryId == parentId);

            if (excludeId.HasValue)
                query = query.Where(c => c.Id != excludeId.Value);

            return await query.AnyAsync(ct);
        }
    }
}
