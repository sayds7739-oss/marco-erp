using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces.Treasury;
using MarcoERP.Application.Interfaces;

namespace MarcoERP.Persistence.Repositories.Treasury
{
    /// <summary>
    /// EF Core implementation of ICashReceiptRepository.
    /// Number format: CR-YYYYMM-####
    /// </summary>
    public sealed class CashReceiptRepository : ICashReceiptRepository
    {
        private readonly MarcoDbContext _context;
        private readonly IDateTimeProvider _dateTime;

        public CashReceiptRepository(MarcoDbContext context, IDateTimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        // ── IRepository<CashReceipt> ─────────────────────────────

        public async Task<CashReceipt> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => await _context.CashReceipts
                .AsNoTracking()
                .Include(cr => cr.Cashbox)
            .FirstOrDefaultAsync(cr => cr.Id == id, cancellationToken);

        public async Task<IReadOnlyList<CashReceipt>> GetAllAsync(CancellationToken cancellationToken = default)
            => await _context.CashReceipts
                .AsNoTracking()
                .Include(cr => cr.Cashbox)
                .Include(cr => cr.Account)
                .Include(cr => cr.Customer)
                .OrderByDescending(cr => cr.ReceiptDate)
                .ThenByDescending(cr => cr.ReceiptNumber)
            .ToListAsync(cancellationToken);

        public async Task AddAsync(CashReceipt entity, CancellationToken cancellationToken = default)
            => await _context.CashReceipts.AddAsync(entity, cancellationToken);

        public void Update(CashReceipt entity)
        {
            if (entity == null) return;

            var local = _context.CashReceipts.Local.FirstOrDefault(e => e.Id == entity.Id);
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
        public void Remove(CashReceipt entity) => throw new NotSupportedException(
            "Hard delete is not supported for financial aggregate 'CashReceipt'. Use lifecycle operations (Cancel/SoftDelete draft) instead.");

        // ── ICashReceiptRepository ───────────────────────────────

        public async Task<CashReceipt> GetWithDetailsAsync(int id, CancellationToken ct = default)
            => await _context.CashReceipts
                .AsNoTracking()
                .Include(cr => cr.Cashbox)
                .Include(cr => cr.Account)
                .Include(cr => cr.Customer)
                .FirstOrDefaultAsync(cr => cr.Id == id, ct);

        public async Task<CashReceipt> GetWithDetailsTrackedAsync(int id, CancellationToken ct = default)
            => await _context.CashReceipts
                .Include(cr => cr.Cashbox)
                .Include(cr => cr.Account)
                .Include(cr => cr.Customer)
                .FirstOrDefaultAsync(cr => cr.Id == id, ct);

        public async Task<CashReceipt> GetByNumberAsync(string receiptNumber, CancellationToken ct = default)
            => await _context.CashReceipts
                .AsNoTracking()
                .Include(cr => cr.Cashbox)
                .FirstOrDefaultAsync(cr => cr.ReceiptNumber == receiptNumber, ct);

        public async Task<bool> NumberExistsAsync(string receiptNumber, CancellationToken ct = default)
            => await _context.CashReceipts.AnyAsync(cr => cr.ReceiptNumber == receiptNumber, ct);

        public async Task<IReadOnlyList<CashReceipt>> GetByStatusAsync(InvoiceStatus status, CancellationToken ct = default)
            => await _context.CashReceipts
                .AsNoTracking()
                .Include(cr => cr.Cashbox)
                .Include(cr => cr.Account)
                .Include(cr => cr.Customer)
                .Where(cr => cr.Status == status)
                .OrderByDescending(cr => cr.ReceiptDate)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<CashReceipt>> GetByCashboxAsync(int cashboxId, CancellationToken ct = default)
            => await _context.CashReceipts
                .AsNoTracking()
                .Include(cr => cr.Cashbox)
                .Where(cr => cr.CashboxId == cashboxId)
                .OrderByDescending(cr => cr.ReceiptDate)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<CashReceipt>> GetByCustomerAsync(int customerId, CancellationToken ct = default)
            => await _context.CashReceipts
                .AsNoTracking()
                .Include(cr => cr.Cashbox)
                .Where(cr => cr.CustomerId == customerId)
                .OrderByDescending(cr => cr.ReceiptDate)
                .ToListAsync(ct);

        /// <summary>
        /// Generates the next receipt number in format CR-YYYYMM-####.
        /// Example: CR-202602-0001, CR-202602-0002, ...
        /// </summary>
        public async Task<string> GetNextNumberAsync(CancellationToken ct = default)
        {
            var prefix = $"CR-{_dateTime.UtcNow:yyyyMM}-";

            var lastNumber = await _context.CashReceipts
                .AsNoTracking()
                .Where(cr => cr.ReceiptNumber.StartsWith(prefix))
                .OrderByDescending(cr => cr.ReceiptNumber)
                .Select(cr => cr.ReceiptNumber)
                .FirstOrDefaultAsync(ct);

            if (lastNumber == null)
                return $"{prefix}0001";

            var seqPart = lastNumber.Substring(prefix.Length);
            if (int.TryParse(seqPart, NumberStyles.None, CultureInfo.InvariantCulture, out var seq))
                return $"{prefix}{(seq + 1):D4}";

            return $"{prefix}0001";
        }
    }
}
