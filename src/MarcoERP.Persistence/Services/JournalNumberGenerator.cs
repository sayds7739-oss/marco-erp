using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using Microsoft.EntityFrameworkCore;

namespace MarcoERP.Persistence.Services
{
    /// <summary>
    /// Generates sequential journal numbers using the CodeSequence table.
    /// SEQ-03: Must run within Serializable isolation (ensured by the calling service).
    /// The caller (JournalEntryService) wraps the posting in a Serializable transaction.
    /// This generator enforces active transaction usage and increments sequence with
    /// optimistic-concurrency retry using RowVersion.
    /// </summary>
    public sealed class JournalNumberGenerator : IJournalNumberGenerator
    {
        private readonly MarcoDbContext _context;
        private const string DocumentType = "JV";
        private const int MaxRetries = 3;

        public JournalNumberGenerator(MarcoDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns the next sequential journal number for the given fiscal year.
        /// Format: JV-{Year}-{Sequence:D5} (e.g., JV-2026-00001).
        /// Must be called inside an active database transaction.
        /// </summary>
        public async Task<string> NextNumberAsync(int fiscalYearId, CancellationToken cancellationToken = default)
        {
            if (_context.Database.CurrentTransaction == null)
            {
                throw new InvalidOperationException(
                    "توليد رقم القيد يتطلب وجود معاملة قاعدة بيانات نشطة.");
            }

            var attempt = 0;
            while (attempt < MaxRetries)
            {
                attempt++;

                var sequence = await _context.CodeSequences
                    .FirstOrDefaultAsync(
                        s => s.DocumentType == DocumentType && s.FiscalYearId == fiscalYearId,
                        cancellationToken);

                if (sequence == null)
                {
                    var fiscalYear = await _context.FiscalYears
                        .AsNoTracking()
                        .FirstAsync(f => f.Id == fiscalYearId, cancellationToken);

                    var prefix = $"{DocumentType}-{fiscalYear.Year}-";
                    sequence = new CodeSequence(DocumentType, fiscalYearId, prefix, 0);
                    await _context.CodeSequences.AddAsync(sequence, cancellationToken);
                }

                var nextCode = sequence.NextCode();

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    return nextCode;
                }
                catch (DbUpdateConcurrencyException) when (attempt < MaxRetries)
                {
                    foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.Entity is CodeSequence))
                    {
                        entry.State = EntityState.Detached;
                    }
                }
                catch (DbUpdateException ex) when (attempt < MaxRetries &&
                    ex.InnerException?.Message.Contains("IX_CodeSequences_DocType_FiscalYear", StringComparison.OrdinalIgnoreCase) == true)
                {
                    foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.Entity is CodeSequence))
                    {
                        entry.State = EntityState.Detached;
                    }
                }
            }

            throw new InvalidOperationException("فشل توليد رقم قيد فريد بعد عدة محاولات.");
        }
    }
}
