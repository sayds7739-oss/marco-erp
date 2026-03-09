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
    /// EF Core implementation of IPosSessionRepository.
    /// </summary>
    public sealed class PosSessionRepository : IPosSessionRepository
    {
        private readonly MarcoDbContext _context;
        private readonly IDateTimeProvider _dateTime;

        public PosSessionRepository(MarcoDbContext context, IDateTimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        // ── IRepository<PosSession> ─────────────────────────────

        public async Task<PosSession> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.PosSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(ps => ps.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<PosSession>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.PosSessions
                .AsNoTracking()
                .OrderByDescending(ps => ps.OpenedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(PosSession entity, CancellationToken cancellationToken = default)
        {
            await _context.PosSessions.AddAsync(entity, cancellationToken);
        }

        public void Update(PosSession entity)
        {
            if (entity == null) return;

            var local = _context.PosSessions.Local.FirstOrDefault(e => e.Id == entity.Id);
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

        public void Remove(PosSession entity)
        {
            _context.PosSessions.Remove(entity);
        }

        // ── IPosSessionRepository ───────────────────────────────

        public async Task<PosSession> GetWithPaymentsAsync(int id, CancellationToken ct = default)
        {
            return await _context.PosSessions
                .AsNoTracking()
                .Include(ps => ps.Payments)
                .FirstOrDefaultAsync(ps => ps.Id == id, ct);
        }

        public async Task<PosSession> GetOpenSessionByUserAsync(int userId, CancellationToken ct = default)
        {
            return await _context.PosSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(ps => ps.UserId == userId && ps.Status == PosSessionStatus.Open, ct);
        }

        public async Task<bool> HasOpenSessionAsync(int userId, CancellationToken ct = default)
        {
            return await _context.PosSessions
                .AnyAsync(ps => ps.UserId == userId && ps.Status == PosSessionStatus.Open, ct);
        }

        public async Task<IReadOnlyList<PosSession>> GetByStatusAsync(PosSessionStatus status, CancellationToken ct = default)
        {
            return await _context.PosSessions
                .AsNoTracking()
                .Where(ps => ps.Status == status)
                .OrderByDescending(ps => ps.OpenedAt)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<PosSession>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        {
            return await _context.PosSessions
                .AsNoTracking()
                .Where(ps => ps.OpenedAt >= fromDate && ps.OpenedAt <= toDate)
                .OrderByDescending(ps => ps.OpenedAt)
                .ToListAsync(ct);
        }

        public async Task<string> GetNextSessionNumberAsync(CancellationToken ct = default)
        {
            var today = _dateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var prefix = $"POS-{today}-";

            var lastNumber = await _context.PosSessions
                .AsNoTracking()
                .Where(ps => ps.SessionNumber.StartsWith(prefix))
                .OrderByDescending(ps => ps.SessionNumber)
                .Select(ps => ps.SessionNumber)
                .FirstOrDefaultAsync(ct);

            if (lastNumber == null)
                return prefix + "0001";

            var numPart = lastNumber.Substring(prefix.Length);
            if (int.TryParse(numPart, out int current))
                return prefix + (current + 1).ToString("D4");

            return prefix + "0001";
        }
    }
}
