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
    /// EF Core implementation of ISalesQuotationRepository.
    /// SalesQuotation is soft-deletable — global query filter applied via DbContext.
    /// </summary>
    public sealed class SalesQuotationRepository : ISalesQuotationRepository
    {
        private readonly MarcoDbContext _context;
        private readonly IDateTimeProvider _dateTime;

        public SalesQuotationRepository(MarcoDbContext context, IDateTimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        // ── IRepository<SalesQuotation> ─────────────────────────

        public async Task<SalesQuotation> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.SalesQuotations
                .AsNoTracking()
                .Include(q => q.Customer)
                .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<SalesQuotation>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SalesQuotations
                .AsNoTracking()
                .Include(q => q.Customer)
                .OrderByDescending(q => q.QuotationDate)
                .ThenByDescending(q => q.QuotationNumber)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(SalesQuotation entity, CancellationToken cancellationToken = default)
        {
            await _context.SalesQuotations.AddAsync(entity, cancellationToken);
        }

        public void Update(SalesQuotation entity)
        {
            if (entity == null) return;

            var local = _context.SalesQuotations.Local.FirstOrDefault(e => e.Id == entity.Id);
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

        public void Remove(SalesQuotation entity)
        {
            _context.SalesQuotations.Remove(entity);
        }

        // ── ISalesQuotationRepository ───────────────────────────

        public async Task<SalesQuotation> GetWithLinesAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.SalesQuotations
                .AsNoTracking()
                .Include(q => q.Customer)
                .Include(q => q.Lines)
                    .ThenInclude(l => l.Product)
                .Include(q => q.Lines)
                    .ThenInclude(l => l.Unit)
                .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        }

        public async Task<SalesQuotation> GetWithLinesTrackedAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.SalesQuotations
                .Include(q => q.Customer)
                .Include(q => q.Lines)
                    .ThenInclude(l => l.Product)
                .Include(q => q.Lines)
                    .ThenInclude(l => l.Unit)
                .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        }

        public async Task<SalesQuotation> GetByNumberAsync(string quotationNumber, CancellationToken cancellationToken = default)
        {
            return await _context.SalesQuotations
                .AsNoTracking()
                .Include(q => q.Customer)
                .FirstOrDefaultAsync(q => q.QuotationNumber == quotationNumber, cancellationToken);
        }

        public async Task<bool> NumberExistsAsync(string quotationNumber, CancellationToken cancellationToken = default)
        {
            return await _context.SalesQuotations
                .AnyAsync(q => q.QuotationNumber == quotationNumber, cancellationToken);
        }

        public async Task<IReadOnlyList<SalesQuotation>> GetByStatusAsync(QuotationStatus status, CancellationToken cancellationToken = default)
        {
            return await _context.SalesQuotations
                .AsNoTracking()
                .Include(q => q.Customer)
                .Where(q => q.Status == status)
                .OrderByDescending(q => q.QuotationDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SalesQuotation>> GetByCustomerAsync(int customerId, CancellationToken cancellationToken = default)
        {
            return await _context.SalesQuotations
                .AsNoTracking()
                .Include(q => q.Customer)
                .Where(q => q.CustomerId == customerId)
                .OrderByDescending(q => q.QuotationDate)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Generates the next quotation number in format SQ-YYYYMM-####.
        /// Example: SQ-202602-0001, SQ-202602-0002, ...
        /// </summary>
        public async Task<string> GetNextNumberAsync(CancellationToken cancellationToken = default)
        {
            var prefix = $"SQ-{_dateTime.UtcNow:yyyyMM}-";

            var lastNumber = await _context.SalesQuotations
                .AsNoTracking()
                .Where(q => q.QuotationNumber.StartsWith(prefix))
                .OrderByDescending(q => q.QuotationNumber)
                .Select(q => q.QuotationNumber)
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
