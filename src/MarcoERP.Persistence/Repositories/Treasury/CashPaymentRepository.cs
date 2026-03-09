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
    /// EF Core implementation of ICashPaymentRepository.
    /// Number format: CP-YYYYMM-####
    /// </summary>
    public sealed class CashPaymentRepository : ICashPaymentRepository
    {
        private readonly MarcoDbContext _context;
        private readonly IDateTimeProvider _dateTime;

        public CashPaymentRepository(MarcoDbContext context, IDateTimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        // ── IRepository<CashPayment> ─────────────────────────────

        public async Task<CashPayment> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => await _context.CashPayments
                .AsNoTracking()
                .Include(cp => cp.Cashbox)
                .Include(cp => cp.Account)
                .Include(cp => cp.Supplier)
            .FirstOrDefaultAsync(cp => cp.Id == id, cancellationToken);

        public async Task<IReadOnlyList<CashPayment>> GetAllAsync(CancellationToken cancellationToken = default)
            => await _context.CashPayments
                .AsNoTracking()
                .Include(cp => cp.Cashbox)
                .Include(cp => cp.Account)
                .Include(cp => cp.Supplier)
                .OrderByDescending(cp => cp.PaymentDate)
                .ThenByDescending(cp => cp.PaymentNumber)
            .ToListAsync(cancellationToken);

        public async Task AddAsync(CashPayment entity, CancellationToken cancellationToken = default)
            => await _context.CashPayments.AddAsync(entity, cancellationToken);

        public void Update(CashPayment entity)
        {
            if (entity == null) return;

            var local = _context.CashPayments.Local.FirstOrDefault(e => e.Id == entity.Id);
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
        public void Remove(CashPayment entity) => throw new NotSupportedException(
            "Hard delete is not supported for financial aggregate 'CashPayment'. Use lifecycle operations (Cancel/SoftDelete draft) instead.");

        // ── ICashPaymentRepository ───────────────────────────────

        public async Task<CashPayment> GetWithDetailsAsync(int id, CancellationToken ct = default)
            => await _context.CashPayments
                .AsNoTracking()
                .Include(cp => cp.Cashbox)
                .Include(cp => cp.Account)
                .Include(cp => cp.Supplier)
                .FirstOrDefaultAsync(cp => cp.Id == id, ct);

        public async Task<CashPayment> GetWithDetailsTrackedAsync(int id, CancellationToken ct = default)
            => await _context.CashPayments
                .Include(cp => cp.Cashbox)
                .Include(cp => cp.Account)
                .Include(cp => cp.Supplier)
                .FirstOrDefaultAsync(cp => cp.Id == id, ct);

        public async Task<CashPayment> GetByNumberAsync(string paymentNumber, CancellationToken ct = default)
            => await _context.CashPayments
                .AsNoTracking()
                .Include(cp => cp.Cashbox)
                .Include(cp => cp.Account)
                .Include(cp => cp.Supplier)
                .FirstOrDefaultAsync(cp => cp.PaymentNumber == paymentNumber, ct);

        public async Task<bool> NumberExistsAsync(string paymentNumber, CancellationToken ct = default)
            => await _context.CashPayments.AnyAsync(cp => cp.PaymentNumber == paymentNumber, ct);

        public async Task<IReadOnlyList<CashPayment>> GetByStatusAsync(InvoiceStatus status, CancellationToken ct = default)
            => await _context.CashPayments
                .AsNoTracking()
                .Include(cp => cp.Cashbox)
                .Include(cp => cp.Account)
                .Include(cp => cp.Supplier)
                .Where(cp => cp.Status == status)
                .OrderByDescending(cp => cp.PaymentDate)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<CashPayment>> GetByCashboxAsync(int cashboxId, CancellationToken ct = default)
            => await _context.CashPayments
                .AsNoTracking()
                .Include(cp => cp.Cashbox)
                .Include(cp => cp.Account)
                .Include(cp => cp.Supplier)
                .Where(cp => cp.CashboxId == cashboxId)
                .OrderByDescending(cp => cp.PaymentDate)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<CashPayment>> GetBySupplierAsync(int supplierId, CancellationToken ct = default)
            => await _context.CashPayments
                .AsNoTracking()
                .Include(cp => cp.Cashbox)
                .Include(cp => cp.Account)
                .Include(cp => cp.Supplier)
                .Where(cp => cp.SupplierId == supplierId)
                .OrderByDescending(cp => cp.PaymentDate)
                .ToListAsync(ct);

        /// <summary>
        /// Generates the next payment number in format CP-YYYYMM-####.
        /// Example: CP-202602-0001, CP-202602-0002, ...
        /// </summary>
        public async Task<string> GetNextNumberAsync(CancellationToken ct = default)
        {
            var prefix = $"CP-{_dateTime.UtcNow:yyyyMM}-";

            var lastNumber = await _context.CashPayments
                .AsNoTracking()
                .Where(cp => cp.PaymentNumber.StartsWith(prefix))
                .OrderByDescending(cp => cp.PaymentNumber)
                .Select(cp => cp.PaymentNumber)
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
