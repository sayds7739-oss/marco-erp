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
    /// EF Core implementation of ISalesInvoiceRepository.
    /// SalesInvoice is not soft-deletable — only Draft invoices can be hard-deleted.
    /// </summary>
    public sealed class SalesInvoiceRepository : ISalesInvoiceRepository
    {
        private readonly MarcoDbContext _context;
        private readonly IDateTimeProvider _dateTime;

        public SalesInvoiceRepository(MarcoDbContext context, IDateTimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        // ── IRepository<SalesInvoice> ───────────────────────────

        public async Task<SalesInvoice> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.SalesInvoices
                .AsNoTracking()
                .Include(si => si.Customer)
                .FirstOrDefaultAsync(si => si.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<SalesInvoice>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SalesInvoices
                .AsNoTracking()
                .Where(si => !si.IsDeleted)
                .Include(si => si.Customer)
                .Include(si => si.Warehouse)
                .Include(si => si.CounterpartySupplier)
                .Include(si => si.SalesRepresentative)
                .OrderByDescending(si => si.InvoiceDate)
                .ThenByDescending(si => si.InvoiceNumber)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(SalesInvoice entity, CancellationToken cancellationToken = default)
        {
            await _context.SalesInvoices.AddAsync(entity, cancellationToken);
        }

        public void Update(SalesInvoice entity)
        {
            if (entity == null) return;

            var local = _context.SalesInvoices.Local.FirstOrDefault(e => e.Id == entity.Id);
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

        public void Remove(SalesInvoice entity)
        {
            throw new NotSupportedException(
                "Hard delete is not supported for financial aggregate 'SalesInvoice'. Use lifecycle operations (Cancel/SoftDelete draft) instead.");
        }

        // ── ISalesInvoiceRepository ─────────────────────────────

        public async Task<SalesInvoice> GetWithLinesAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.SalesInvoices
                .AsNoTracking()
                .Include(si => si.Customer)
                .Include(si => si.Warehouse)
                .Include(si => si.CounterpartySupplier)
                .Include(si => si.SalesRepresentative)
                .Include(si => si.Lines).ThenInclude(l => l.Product)
                .Include(si => si.Lines).ThenInclude(l => l.Unit)
                .FirstOrDefaultAsync(si => si.Id == id, cancellationToken);
        }

        public async Task<SalesInvoice> GetWithLinesTrackedAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.SalesInvoices
                .Include(si => si.Customer)
                .Include(si => si.Warehouse)
                .Include(si => si.CounterpartySupplier)
                .Include(si => si.SalesRepresentative)
                .Include(si => si.Lines).ThenInclude(l => l.Product)
                .Include(si => si.Lines).ThenInclude(l => l.Unit)
                .FirstOrDefaultAsync(si => si.Id == id, cancellationToken);
        }

        public async Task<SalesInvoice> GetByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            return await _context.SalesInvoices
                .AsNoTracking()
                .Include(si => si.Customer)
                .FirstOrDefaultAsync(si => si.InvoiceNumber == invoiceNumber, cancellationToken);
        }

        public async Task<bool> NumberExistsAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            return await _context.SalesInvoices
                .AnyAsync(si => si.InvoiceNumber == invoiceNumber, cancellationToken);
        }

        public async Task<IReadOnlyList<SalesInvoice>> GetByStatusAsync(InvoiceStatus status, CancellationToken cancellationToken = default)
        {
            return await _context.SalesInvoices
                .AsNoTracking()
                .Include(si => si.Customer)
                .Where(si => si.Status == status)
                .OrderByDescending(si => si.InvoiceDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SalesInvoice>> GetByCustomerAsync(int customerId, CancellationToken cancellationToken = default)
        {
            return await _context.SalesInvoices
                .AsNoTracking()
                .Include(si => si.Customer)
                .Where(si => si.CustomerId == customerId)
                .OrderByDescending(si => si.InvoiceDate)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Generates the next invoice number in format SI-YYYYMM-####.
        /// Example: SI-202602-0001, SI-202602-0002, ...
        /// Excludes soft-deleted invoices to prevent number conflicts.
        /// </summary>
        public async Task<string> GetNextNumberAsync(CancellationToken cancellationToken = default)
        {
            var prefix = $"SI-{_dateTime.UtcNow:yyyyMM}-";

            var lastNumber = await _context.SalesInvoices
                .AsNoTracking()
                .Where(si => si.InvoiceNumber.StartsWith(prefix) && !si.IsDeleted)
                .OrderByDescending(si => si.InvoiceNumber)
                .Select(si => si.InvoiceNumber)
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
