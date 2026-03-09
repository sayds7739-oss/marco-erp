using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;

namespace MarcoERP.Persistence.Repositories
{
    /// <summary>
    /// EF Core implementation of IJournalEntryRepository.
    /// Global soft-delete filter is applied automatically by MarcoDbContext.
    /// </summary>
    public sealed class JournalEntryRepository : IJournalEntryRepository
    {
        private readonly MarcoDbContext _context;

        public JournalEntryRepository(MarcoDbContext context)
        {
            _context = context;
        }

        // ── IRepository<JournalEntry> ───────────────────────────

        public async Task<JournalEntry> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.JournalEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<JournalEntry>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.JournalEntries
                .AsNoTracking()
                .OrderByDescending(j => j.JournalDate)
                .ThenByDescending(j => j.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(JournalEntry entity, CancellationToken cancellationToken = default)
        {
            await _context.JournalEntries.AddAsync(entity, cancellationToken);
        }

        public void Update(JournalEntry entity)
        {
            if (entity == null) return;

            var local = _context.JournalEntries.Local.FirstOrDefault(e => e.Id == entity.Id);
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

            // Safe: only marks root entity as Modified without graph traversal
            _context.Entry(entity).State = EntityState.Modified;
        }

        public void Remove(JournalEntry entity)
        {
            throw new NotSupportedException(
                "Hard delete is not supported for financial aggregate 'JournalEntry'. Use reversal/adjustment lifecycle operations instead.");
        }

        // ── IJournalEntryRepository ─────────────────────────────

        public async Task<JournalEntry> GetWithLinesAsync(int journalEntryId, CancellationToken cancellationToken = default)
        {
            return await _context.JournalEntries
                .Include(j => j.Lines)
                .FirstOrDefaultAsync(j => j.Id == journalEntryId, cancellationToken);
        }

        public async Task<IReadOnlyList<JournalEntry>> GetByPeriodAsync(int fiscalPeriodId, CancellationToken cancellationToken = default)
        {
            return await _context.JournalEntries
                .AsNoTracking()
                .Where(j => j.FiscalPeriodId == fiscalPeriodId)
                .OrderBy(j => j.JournalDate)
                .ThenBy(j => j.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<JournalEntry>> GetByStatusAsync(JournalEntryStatus status, CancellationToken cancellationToken = default)
        {
            return await _context.JournalEntries
                .AsNoTracking()
                .Where(j => j.Status == status)
                .OrderByDescending(j => j.JournalDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<JournalEntry>> GetDraftsByYearAsync(int fiscalYearId, CancellationToken cancellationToken = default)
        {
            return await _context.JournalEntries
                .AsNoTracking()
                .Where(j => j.FiscalYearId == fiscalYearId && j.Status == JournalEntryStatus.Draft)
                .OrderBy(j => j.JournalDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> HasEntriesForAccountAsync(int accountId, CancellationToken cancellationToken = default)
        {
            return await _context.JournalEntryLines
                .AnyAsync(l => l.AccountId == accountId, cancellationToken);
        }

        public async Task<IReadOnlyList<JournalEntry>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            return await _context.JournalEntries
                .AsNoTracking()
                .Where(j => j.JournalDate >= startDate && j.JournalDate <= endDate)
                .OrderBy(j => j.JournalDate)
                .ThenBy(j => j.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<JournalEntryLine>> GetPostedLinesByYearAsync(int fiscalYearId, CancellationToken cancellationToken = default)
        {
            return await _context.JournalEntryLines
                .AsNoTracking()
                .Where(l => _context.JournalEntries
                    .Any(j => j.Id == l.JournalEntryId
                           && j.FiscalYearId == fiscalYearId
                           && j.Status == JournalEntryStatus.Posted))
                .ToListAsync(cancellationToken);
        }
    }
}
