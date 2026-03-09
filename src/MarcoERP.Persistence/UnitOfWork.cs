using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MarcoERP.Persistence
{
    /// <summary>
    /// Unit of Work implementation wrapping MarcoDbContext.SaveChangesAsync.
    /// TRX-INT-01: One transaction per use case — SaveChangesAsync may be called multiple times within it.
    /// TRX-INT-02: Application layer initiates; Persistence layer (this class) executes.
    /// </summary>
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly MarcoDbContext _context;

        public UnitOfWork(MarcoDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Commits all pending changes tracked by the DbContext in a single transaction.
        /// Returns the number of state entries written to the database.
        /// Catches DbUpdateConcurrencyException and wraps it as ConcurrencyConflictException.
        /// Catches DbUpdateException with unique-constraint violations and wraps as DuplicateRecordException.
        /// </summary>
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var entry = ex.Entries.FirstOrDefault();
                var entityName = entry?.Entity.GetType().Name ?? "Unknown";
                throw new ConcurrencyConflictException(entityName, ex);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                throw new DuplicateRecordException(
                    ex.InnerException?.Message ?? ex.Message, ex);
            }
        }

        /// <summary>
        /// Checks whether a DbUpdateException represents a unique-constraint violation
        /// (SQL Server error codes 2601 / 2627).
        /// </summary>
        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            var message = exception.InnerException?.Message ?? exception.Message ?? string.Empty;
            return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("2601", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("2627", StringComparison.OrdinalIgnoreCase);
        }

        public async Task ExecuteInTransactionAsync(
            Func<Task> operation,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            await using var transaction = await _context.Database
                .BeginTransactionAsync(isolationLevel, cancellationToken);

            try
            {
                await operation();
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
