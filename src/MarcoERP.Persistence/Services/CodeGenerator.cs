using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Interfaces;
using MarcoERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;

namespace MarcoERP.Persistence.Services
{
    /// <summary>
    /// Generates sequential document codes using the CodeSequence table.
    /// Supports all document types: JV, SI, PI, CR, CP, CT, SR, PR, IT.
    /// SEQ-03: Must run within Serializable isolation (ensured by the calling service).
    /// P-02 Fix: Enforces active transaction, uses optimistic-concurrency retry with RowVersion.
    /// </summary>
    public sealed class CodeGenerator : ICodeGenerator
    {
        private readonly MarcoDbContext _context;
        private const int MaxRetries = 3;

        public CodeGenerator(MarcoDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<string> NextCodeAsync(string documentType, int fiscalYearId,
            CancellationToken cancellationToken = default)
        {
            if (_context.Database.CurrentTransaction == null)
            {
                throw new InvalidOperationException(
                    "Code generation requires an active database transaction.");
            }

            var attempt = 0;
            while (attempt < MaxRetries)
            {
                attempt++;

                var sequence = await _context.CodeSequences
                    .FirstOrDefaultAsync(
                        s => s.DocumentType == documentType && s.FiscalYearId == fiscalYearId,
                        cancellationToken);

                if (sequence == null)
                {
                    var fiscalYear = await _context.FiscalYears
                        .AsNoTracking()
                        .FirstAsync(f => f.Id == fiscalYearId, cancellationToken);

                    var prefix = $"{documentType}-{fiscalYear.Year}-";
                    sequence = new CodeSequence(documentType, fiscalYearId, prefix, 0);
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
                    // Detach stale entry and retry
                    foreach (var entry in _context.ChangeTracker.Entries()
                        .Where(e => e.Entity is CodeSequence))
                    {
                        entry.State = EntityState.Detached;
                    }
                }
                catch (DbUpdateException ex) when (attempt < MaxRetries &&
                    ex.InnerException?.Message.Contains("IX_CodeSequences_DocType_FiscalYear",
                        StringComparison.OrdinalIgnoreCase) == true)
                {
                    // Unique constraint race — detach and retry
                    foreach (var entry in _context.ChangeTracker.Entries()
                        .Where(e => e.Entity is CodeSequence))
                    {
                        entry.State = EntityState.Detached;
                    }
                }
            }

            throw new InvalidOperationException(
                $"Failed to generate code for {documentType} after {MaxRetries} retries.");
        }
    }
}
