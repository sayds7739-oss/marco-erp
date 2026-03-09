using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces.Purchases;
using MarcoERP.Application.Interfaces;

namespace MarcoERP.Persistence.Repositories.Purchases
{
    /// <summary>
    /// EF Core implementation of IPurchaseQuotationRepository.
    /// PurchaseQuotation is soft-deletable — global query filter applied via DbContext.
    /// </summary>
    public sealed class PurchaseQuotationRepository : IPurchaseQuotationRepository
    {
        private readonly MarcoDbContext _context;
        private readonly IDateTimeProvider _dateTime;

        public PurchaseQuotationRepository(MarcoDbContext context, IDateTimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        // ── IRepository<PurchaseQuotation> ──────────────────────

        public async Task<PurchaseQuotation> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseQuotations
                .AsNoTracking()
                .Include(q => q.Supplier)
                .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<PurchaseQuotation>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseQuotations
                .AsNoTracking()
                .Include(q => q.Supplier)
                .OrderByDescending(q => q.QuotationDate)
                .ThenByDescending(q => q.QuotationNumber)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(PurchaseQuotation entity, CancellationToken cancellationToken = default)
        {
            await _context.PurchaseQuotations.AddAsync(entity, cancellationToken);
        }

        public void Update(PurchaseQuotation entity)
        {
            if (entity == null) return;

            var local = _context.PurchaseQuotations.Local.FirstOrDefault(e => e.Id == entity.Id);
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

        public void Remove(PurchaseQuotation entity)
        {
            _context.PurchaseQuotations.Remove(entity);
        }

        // ── IPurchaseQuotationRepository ────────────────────────

        public async Task<PurchaseQuotation> GetWithLinesAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseQuotations
                .AsNoTracking()
                .Include(q => q.Supplier)
                .Include(q => q.Warehouse)
                .Include(q => q.Lines)
                    .ThenInclude(l => l.Product)
                .Include(q => q.Lines)
                    .ThenInclude(l => l.Unit)
                .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        }

        public async Task<PurchaseQuotation> GetWithLinesTrackedAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseQuotations
                .Include(q => q.Supplier)
                .Include(q => q.Warehouse)
                .Include(q => q.Lines)
                    .ThenInclude(l => l.Product)
                .Include(q => q.Lines)
                    .ThenInclude(l => l.Unit)
                .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        }

        public async Task<PurchaseQuotation> GetByNumberAsync(string quotationNumber, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseQuotations
                .AsNoTracking()
                .Include(q => q.Supplier)
                .FirstOrDefaultAsync(q => q.QuotationNumber == quotationNumber, cancellationToken);
        }

        public async Task<bool> NumberExistsAsync(string quotationNumber, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseQuotations
                .AnyAsync(q => q.QuotationNumber == quotationNumber, cancellationToken);
        }

        public async Task<IReadOnlyList<PurchaseQuotation>> GetByStatusAsync(QuotationStatus status, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseQuotations
                .AsNoTracking()
                .Include(q => q.Supplier)
                .Where(q => q.Status == status)
                .OrderByDescending(q => q.QuotationDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PurchaseQuotation>> GetBySupplierAsync(int supplierId, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseQuotations
                .AsNoTracking()
                .Include(q => q.Supplier)
                .Where(q => q.SupplierId == supplierId)
                .OrderByDescending(q => q.QuotationDate)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Generates the next quotation number in format PQ-YYYYMM-####.
        /// Example: PQ-202602-0001, PQ-202602-0002, ...
        /// </summary>
        public async Task<string> GetNextNumberAsync(CancellationToken cancellationToken = default)
        {
            var prefix = $"PQ-{_dateTime.UtcNow:yyyyMM}-";

            var lastNumber = await _context.PurchaseQuotations
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
