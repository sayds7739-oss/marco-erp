using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces.Sales;

namespace MarcoERP.Persistence.Repositories.Sales
{
    /// <summary>
    /// EF Core implementation of IPosPaymentRepository.
    /// </summary>
    public sealed class PosPaymentRepository : IPosPaymentRepository
    {
        private readonly MarcoDbContext _context;

        public PosPaymentRepository(MarcoDbContext context)
        {
            _context = context;
        }

        // ── IRepository<PosPayment> ─────────────────────────────

        public async Task<PosPayment> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.PosPayments
                .AsNoTracking()
                .FirstOrDefaultAsync(pp => pp.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<PosPayment>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.PosPayments
                .AsNoTracking()
                .OrderByDescending(pp => pp.PaidAt)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(PosPayment entity, CancellationToken cancellationToken = default)
        {
            await _context.PosPayments.AddAsync(entity, cancellationToken);
        }

        public void Update(PosPayment entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            var local = _context.PosPayments.Local.FirstOrDefault(e => e.Id == entity.Id);
            if (local != null && local != entity)
                _context.Entry(local).CurrentValues.SetValues(entity);
            else
                _context.Entry(entity).State = EntityState.Modified;
        }

        public void Remove(PosPayment entity)
        {
            _context.PosPayments.Remove(entity);
        }

        // ── IPosPaymentRepository ───────────────────────────────

        public async Task<IReadOnlyList<PosPayment>> GetByInvoiceAsync(int salesInvoiceId, CancellationToken ct = default)
        {
            return await _context.PosPayments
                .AsNoTracking()
                .Where(pp => pp.SalesInvoiceId == salesInvoiceId)
                .OrderBy(pp => pp.PaidAt)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<PosPayment>> GetBySessionAsync(int posSessionId, CancellationToken ct = default)
        {
            return await _context.PosPayments
                .AsNoTracking()
                .Where(pp => pp.PosSessionId == posSessionId)
                .OrderBy(pp => pp.PaidAt)
                .ToListAsync(ct);
        }

        public async Task<decimal> GetSessionTotalByMethodAsync(int posSessionId, PaymentMethod method, CancellationToken ct = default)
        {
            return await _context.PosPayments
                .Where(pp => pp.PosSessionId == posSessionId && pp.PaymentMethod == method)
                .SumAsync(pp => pp.Amount, ct);
        }
    }
}
