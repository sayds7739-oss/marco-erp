using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Application.Interfaces;

namespace MarcoERP.Persistence.Repositories.Sales
{
    /// <summary>
    /// EF Core implementation of ISalesReturnRepository.
    /// SalesReturn is not soft-deletable — only Draft returns can be hard-deleted.
    /// </summary>
    public sealed class SalesReturnRepository : ISalesReturnRepository
    {
        private readonly MarcoDbContext _context;
        private readonly IDateTimeProvider _dateTime;

        public SalesReturnRepository(MarcoDbContext context, IDateTimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        // ── IRepository<SalesReturn> ────────────────────────────

        public async Task<SalesReturn> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.SalesReturns
                .AsNoTracking()
                .Include(sr => sr.Customer)
                .FirstOrDefaultAsync(sr => sr.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<SalesReturn>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SalesReturns
                .AsNoTracking()
                .Include(sr => sr.Customer)
                .Include(sr => sr.Warehouse)
                .Include(sr => sr.CounterpartySupplier)
                .Include(sr => sr.SalesRepresentative)
                .OrderByDescending(sr => sr.ReturnDate)
                .ThenByDescending(sr => sr.ReturnNumber)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(SalesReturn entity, CancellationToken cancellationToken = default)
        {
            await _context.SalesReturns.AddAsync(entity, cancellationToken);
        }

        public void Update(SalesReturn entity)
        {
            if (entity == null) return;

            var local = _context.SalesReturns.Local.FirstOrDefault(e => e.Id == entity.Id);
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

        public void Remove(SalesReturn entity)
        {
            throw new NotSupportedException(
                "Hard delete is not supported for financial aggregate 'SalesReturn'. Use lifecycle operations (Cancel/SoftDelete draft) instead.");
        }

        // ── ISalesReturnRepository ──────────────────────────────

        public async Task<SalesReturn> GetWithLinesAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.SalesReturns
                .AsNoTracking()
                .Include(sr => sr.Customer)
                .Include(sr => sr.Warehouse)
                .Include(sr => sr.CounterpartySupplier)
                .Include(sr => sr.SalesRepresentative)
                .Include(sr => sr.OriginalInvoice)
                .Include(sr => sr.Lines).ThenInclude(l => l.Product)
                .Include(sr => sr.Lines).ThenInclude(l => l.Unit)
                .FirstOrDefaultAsync(sr => sr.Id == id, cancellationToken);
        }

        public async Task<SalesReturn> GetWithLinesTrackedAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.SalesReturns
                .Include(sr => sr.Customer)
                .Include(sr => sr.Warehouse)
                .Include(sr => sr.CounterpartySupplier)
                .Include(sr => sr.SalesRepresentative)
                .Include(sr => sr.OriginalInvoice)
                .Include(sr => sr.Lines).ThenInclude(l => l.Product)
                .Include(sr => sr.Lines).ThenInclude(l => l.Unit)
                .FirstOrDefaultAsync(sr => sr.Id == id, cancellationToken);
        }

        public async Task<SalesReturn> GetByNumberAsync(string returnNumber, CancellationToken cancellationToken = default)
        {
            return await _context.SalesReturns
                .AsNoTracking()
                .Include(sr => sr.Customer)
                .FirstOrDefaultAsync(sr => sr.ReturnNumber == returnNumber, cancellationToken);
        }

        public async Task<bool> NumberExistsAsync(string returnNumber, CancellationToken cancellationToken = default)
        {
            return await _context.SalesReturns
                .AnyAsync(sr => sr.ReturnNumber == returnNumber, cancellationToken);
        }

        public async Task<IReadOnlyList<SalesReturn>> GetByStatusAsync(InvoiceStatus status, CancellationToken cancellationToken = default)
        {
            return await _context.SalesReturns
                .AsNoTracking()
                .Include(sr => sr.Customer)
                .Where(sr => sr.Status == status)
                .OrderByDescending(sr => sr.ReturnDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SalesReturn>> GetByCustomerAsync(int customerId, CancellationToken cancellationToken = default)
        {
            return await _context.SalesReturns
                .AsNoTracking()
                .Include(sr => sr.Customer)
                .Where(sr => sr.CustomerId == customerId)
                .OrderByDescending(sr => sr.ReturnDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SalesReturn>> GetByOriginalInvoiceAsync(int invoiceId, CancellationToken cancellationToken = default)
        {
            return await _context.SalesReturns
                .AsNoTracking()
                .Include(sr => sr.Customer)
                .Include(sr => sr.Lines)
                .Where(sr => sr.OriginalInvoiceId == invoiceId)
                .OrderByDescending(sr => sr.ReturnDate)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Generates the next return number in format SR-YYYYMM-####.
        /// Example: SR-202602-0001, SR-202602-0002, ...
        /// </summary>
        public async Task<string> GetNextNumberAsync(CancellationToken cancellationToken = default)
        {
            var prefix = $"SR-{_dateTime.UtcNow:yyyyMM}-";

            var lastNumber = await _context.SalesReturns
                .AsNoTracking()
                .Where(sr => sr.ReturnNumber.StartsWith(prefix))
                .OrderByDescending(sr => sr.ReturnNumber)
                .Select(sr => sr.ReturnNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastNumber == null)
                return $"{prefix}0001";

            var seqPart = lastNumber.Substring(prefix.Length);
            if (int.TryParse(seqPart, NumberStyles.None, CultureInfo.InvariantCulture, out var seq))
                return $"{prefix}{(seq + 1):D4}";

            return $"{prefix}0001";
        }
    }
}
