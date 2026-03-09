using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Interfaces.Treasury;

namespace MarcoERP.Persistence.Repositories.Treasury
{
    /// <summary>
    /// EF Core implementation of IBankAccountRepository.
    /// Code format: BNK-####
    /// </summary>
    public sealed class BankAccountRepository : IBankAccountRepository
    {
        private readonly MarcoDbContext _context;

        public BankAccountRepository(MarcoDbContext context) => _context = context;

        // ── IRepository<BankAccount> ─────────────────────────────

        public async Task<BankAccount> GetByIdAsync(int id, CancellationToken ct = default)
            => await _context.BankAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id, ct);

        public async Task<IReadOnlyList<BankAccount>> GetAllAsync(CancellationToken ct = default)
            => await _context.BankAccounts
                .AsNoTracking()
                .OrderBy(b => b.Code)
                .ToListAsync(ct);

        public async Task AddAsync(BankAccount entity, CancellationToken ct = default)
            => await _context.BankAccounts.AddAsync(entity, ct);

        public void Update(BankAccount entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            var local = _context.BankAccounts.Local.FirstOrDefault(e => e.Id == entity.Id);
            if (local != null && local != entity)
                _context.Entry(local).CurrentValues.SetValues(entity);
            else
                _context.Entry(entity).State = EntityState.Modified;
        }
        public void Remove(BankAccount entity) => _context.BankAccounts.Remove(entity);

        // ── IBankAccountRepository ───────────────────────────────

        public async Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken ct = default)
        {
            var query = _context.BankAccounts.Where(b => b.Code == code);
            if (excludeId.HasValue)
                query = query.Where(b => b.Id != excludeId.Value);
            return await query.AnyAsync(ct);
        }

        public async Task<BankAccount> GetDefaultAsync(CancellationToken ct = default)
            => await _context.BankAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.IsDefault, ct);

        public async Task<IReadOnlyList<BankAccount>> GetActiveAsync(CancellationToken ct = default)
            => await _context.BankAccounts
                .AsNoTracking()
                .Where(b => b.IsActive)
                .OrderBy(b => b.Code)
                .ToListAsync(ct);

        /// <summary>
        /// Generates the next bank account code in format BNK-####.
        /// Example: BNK-0001, BNK-0002, ...
        /// </summary>
        public async Task<string> GetNextCodeAsync(CancellationToken ct = default)
        {
            const string prefix = "BNK-";

            var lastCode = await _context.BankAccounts
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(b => b.Code.StartsWith(prefix))
                .OrderByDescending(b => b.Code)
                .Select(b => b.Code)
                .FirstOrDefaultAsync(ct);

            if (lastCode == null)
                return $"{prefix}0001";

            var seqPart = lastCode.Substring(prefix.Length);
            if (int.TryParse(seqPart, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var seq))
                return $"{prefix}{(seq + 1):D4}";

            return $"{prefix}0001";
        }
    }
}
