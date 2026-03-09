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
    /// EF Core implementation of ICashboxRepository.
    /// Code format: CBX-####
    /// </summary>
    public sealed class CashboxRepository : ICashboxRepository
    {
        private readonly MarcoDbContext _context;

        public CashboxRepository(MarcoDbContext context) => _context = context;

        // ── IRepository<Cashbox> ─────────────────────────────────

        public async Task<Cashbox> GetByIdAsync(int id, CancellationToken ct = default)
            => await _context.Cashboxes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public async Task<IReadOnlyList<Cashbox>> GetAllAsync(CancellationToken ct = default)
            => await _context.Cashboxes
                .AsNoTracking()
                .OrderBy(c => c.Code)
                .ToListAsync(ct);

        public async Task AddAsync(Cashbox entity, CancellationToken ct = default)
            => await _context.Cashboxes.AddAsync(entity, ct);

        public void Update(Cashbox entity)
        {
            if (entity == null) return;

            // Check if already tracked to avoid dual-tracking conflicts.
            // This happens when the entity is loaded tracked via Include (in
            // CashPayment/CashReceipt) and then separately loaded AsNoTracking
            // via GetByIdAsync in posting flows.
            var local = _context.Cashboxes.Local.FirstOrDefault(c => c.Id == entity.Id);
            if (local != null && !ReferenceEquals(local, entity))
            {
                _context.Entry(local).CurrentValues.SetValues(entity);
                return;
            }
            if (local != null)
            {
                var existing = _context.Entry(local);
                if (existing.State == EntityState.Unchanged)
                    existing.State = EntityState.Modified;
                return;
            }

            _context.Cashboxes.Update(entity);
        }
        public void Remove(Cashbox entity) => _context.Cashboxes.Remove(entity);

        // ── ICashboxRepository ───────────────────────────────────

        public async Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken ct = default)
        {
            var query = _context.Cashboxes.Where(c => c.Code == code);
            if (excludeId.HasValue)
                query = query.Where(c => c.Id != excludeId.Value);
            return await query.AnyAsync(ct);
        }

        public async Task<Cashbox> GetDefaultAsync(CancellationToken ct = default)
            => await _context.Cashboxes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.IsDefault, ct);

        public async Task<IReadOnlyList<Cashbox>> GetActiveAsync(CancellationToken ct = default)
            => await _context.Cashboxes
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Code)
                .ToListAsync(ct);

        /// <summary>
        /// Generates the next cashbox code in format CBX-####.
        /// Example: CBX-0001, CBX-0002, ...
        /// </summary>
        public async Task<string> GetNextCodeAsync(CancellationToken ct = default)
        {
            const string prefix = "CBX-";

            var lastCode = await _context.Cashboxes
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.Code.StartsWith(prefix))
                .OrderByDescending(c => c.Code)
                .Select(c => c.Code)
                .FirstOrDefaultAsync(ct);

            if (lastCode == null)
                return $"{prefix}0001";

            var seqPart = lastCode.Substring(prefix.Length);
            if (int.TryParse(seqPart, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var seq))
                return $"{prefix}{(seq + 1):D4}";

            return $"{prefix}0001";
        }

        /// <inheritdoc />
        /// <summary>
        /// CSH-03: Gets the posted GL balance (Debit − Credit) for the cashbox's linked account.
        /// Cash accounts have a Debit natural balance — positive means funds available.
        /// </summary>
        public async Task<decimal> GetGLBalanceAsync(int cashboxId, CancellationToken ct = default)
        {
            var cashbox = await _context.Cashboxes.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cashboxId, ct);
            if (cashbox?.AccountId == null) return 0m;

            var accountId = cashbox.AccountId.Value;

            var balance = await _context.Set<Domain.Entities.Accounting.JournalEntry>()
                .AsNoTracking()
                .Where(e => e.Status == Domain.Enums.JournalEntryStatus.Posted)
                .SelectMany(e => e.Lines)
                .Where(l => l.AccountId == accountId)
                .Select(l => (decimal?)(l.DebitAmount - l.CreditAmount))
                .SumAsync(ct) ?? 0m;

            return balance;
        }
    }
}
