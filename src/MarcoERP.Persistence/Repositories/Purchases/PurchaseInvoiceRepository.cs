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
    /// EF Core implementation of IPurchaseInvoiceRepository.
    /// PurchaseInvoice is not soft-deletable — only Draft invoices can be hard-deleted.
    /// </summary>
    public sealed class PurchaseInvoiceRepository : IPurchaseInvoiceRepository
    {
        private readonly MarcoDbContext _context;
        private readonly IDateTimeProvider _dateTime;

        public PurchaseInvoiceRepository(MarcoDbContext context, IDateTimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        // ── IRepository<PurchaseInvoice> ────────────────────────

        public async Task<PurchaseInvoice> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseInvoices
                .AsNoTracking()
                .Include(pi => pi.Supplier)
                .Include(pi => pi.CounterpartyCustomer)
                .FirstOrDefaultAsync(pi => pi.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<PurchaseInvoice>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseInvoices
                .AsNoTracking()
                .Include(pi => pi.Supplier)
                .Include(pi => pi.CounterpartyCustomer)
                .OrderByDescending(pi => pi.InvoiceDate)
                .ThenByDescending(pi => pi.InvoiceNumber)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(PurchaseInvoice entity, CancellationToken cancellationToken = default)
        {
            await _context.PurchaseInvoices.AddAsync(entity, cancellationToken);
        }

        public void Update(PurchaseInvoice entity)
        {
            if (entity == null) return;

            var local = _context.PurchaseInvoices.Local.FirstOrDefault(e => e.Id == entity.Id);
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

        public void Remove(PurchaseInvoice entity)
        {
            throw new NotSupportedException(
                "Hard delete is not supported for financial aggregate 'PurchaseInvoice'. Use lifecycle operations (Cancel/SoftDelete draft) instead.");
        }

        // ── IPurchaseInvoiceRepository ──────────────────────────

        public async Task<PurchaseInvoice> GetWithLinesAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseInvoices
                .AsNoTracking()
                .Include(pi => pi.Supplier)
                .Include(pi => pi.CounterpartyCustomer)
                .Include(pi => pi.Warehouse)
                .Include(pi => pi.Lines)
                    .ThenInclude(l => l.Product)
                .Include(pi => pi.Lines)
                    .ThenInclude(l => l.Unit)
                .FirstOrDefaultAsync(pi => pi.Id == id, cancellationToken);
        }

        public async Task<PurchaseInvoice> GetWithLinesTrackedAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseInvoices
                .Include(pi => pi.Supplier)
                .Include(pi => pi.CounterpartyCustomer)
                .Include(pi => pi.Warehouse)
                .Include(pi => pi.Lines)
                    .ThenInclude(l => l.Product)
                .Include(pi => pi.Lines)
                    .ThenInclude(l => l.Unit)
                .FirstOrDefaultAsync(pi => pi.Id == id, cancellationToken);
        }

        public async Task<PurchaseInvoice> GetByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseInvoices
                .AsNoTracking()
                .Include(pi => pi.Supplier)
                .FirstOrDefaultAsync(pi => pi.InvoiceNumber == invoiceNumber, cancellationToken);
        }

        public async Task<bool> NumberExistsAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseInvoices
                .AnyAsync(pi => pi.InvoiceNumber == invoiceNumber, cancellationToken);
        }

        public async Task<IReadOnlyList<PurchaseInvoice>> GetByStatusAsync(InvoiceStatus status, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseInvoices
                .AsNoTracking()
                .Include(pi => pi.Supplier)
                .Where(pi => pi.Status == status)
                .OrderByDescending(pi => pi.InvoiceDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PurchaseInvoice>> GetBySupplierAsync(int supplierId, CancellationToken cancellationToken = default)
        {
            return await _context.PurchaseInvoices
                .AsNoTracking()
                .Include(pi => pi.Supplier)
                .Where(pi => pi.SupplierId == supplierId)
                .OrderByDescending(pi => pi.InvoiceDate)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Generates the next invoice number in format PI-YYYYMM-####.
        /// Example: PI-202602-0001, PI-202602-0002, ...
        /// Excludes soft-deleted invoices to prevent number conflicts.
        /// </summary>
        public async Task<string> GetNextNumberAsync(CancellationToken cancellationToken = default)
        {
            var prefix = $"PI-{_dateTime.UtcNow:yyyyMM}-";

            var lastNumber = await _context.PurchaseInvoices
                .AsNoTracking()
                .Where(pi => pi.InvoiceNumber.StartsWith(prefix) && !pi.IsDeleted)
                .OrderByDescending(pi => pi.InvoiceNumber)
                .Select(pi => pi.InvoiceNumber)
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
