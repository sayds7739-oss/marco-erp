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
    /// EF Core implementation of ICustomerRepository.
    /// Global soft-delete query filter applied by CustomerConfiguration.
    /// </summary>
    public sealed class CustomerRepository : ICustomerRepository
    {
        private readonly MarcoDbContext _context;

        public CustomerRepository(MarcoDbContext context)
        {
            _context = context;
        }

        // ── IRepository<Customer> ───────────────────────────────

        public async Task<Customer> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Customers
                .AsNoTracking()
                .OrderBy(c => c.Code)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Customer entity, CancellationToken cancellationToken = default)
        {
            await _context.Customers.AddAsync(entity, cancellationToken);
        }

        public void Update(Customer entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            var local = _context.Customers.Local.FirstOrDefault(e => e.Id == entity.Id);
            if (local != null && local != entity)
                _context.Entry(local).CurrentValues.SetValues(entity);
            else
                _context.Entry(entity).State = EntityState.Modified;
        }

        public void Remove(Customer entity)
        {
            _context.Customers.Remove(entity);
        }

        // ── ICustomerRepository ─────────────────────────────────

        public async Task<Customer> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            return await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
        }

        public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
        {
            return await _context.Customers
                .AnyAsync(c => c.Code == code, cancellationToken);
        }

        public async Task<bool> CodeExistsForOtherAsync(string code, int excludeId, CancellationToken cancellationToken = default)
        {
            return await _context.Customers
                .AnyAsync(c => c.Code == code && c.Id != excludeId, cancellationToken);
        }

        public async Task<IReadOnlyList<Customer>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetAllAsync(cancellationToken);

            var pattern = $"%{searchTerm.Trim()}%";
            return await _context.Customers
                .AsNoTracking()
                .Where(c => EF.Functions.Like(c.Code, pattern)
                          || EF.Functions.Like(c.NameAr, pattern)
                          || (c.NameEn != null && EF.Functions.Like(c.NameEn, pattern))
                          || (c.Phone != null && EF.Functions.Like(c.Phone, pattern))
                          || (c.Mobile != null && EF.Functions.Like(c.Mobile, pattern)))
                .OrderBy(c => c.Code)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Customer>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Customers
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Code)
                .ToListAsync(cancellationToken);
        }

        public async Task<string> GetNextCodeAsync(CancellationToken cancellationToken = default)
        {
            // Auto-generate: CUS-0001, CUS-0002, ...
            var lastCode = await _context.Customers
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(c => c.Code.StartsWith("CUS-"))
                .OrderByDescending(c => c.Code)
                .Select(c => c.Code)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastCode == null)
                return "CUS-0001";

            var numPart = lastCode.Replace("CUS-", "");
            if (int.TryParse(numPart, out var num))
                return $"CUS-{(num + 1):D4}";

            return $"CUS-{1:D4}";
        }
    }
}
