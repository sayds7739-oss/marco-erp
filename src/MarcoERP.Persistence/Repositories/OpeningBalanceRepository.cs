using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Interfaces;

namespace MarcoERP.Persistence.Repositories
{
    /// <summary>
    /// Repository implementation for OpeningBalance aggregate.
    /// </summary>
    public sealed class OpeningBalanceRepository : IOpeningBalanceRepository
    {
        private readonly MarcoDbContext _context;

        public OpeningBalanceRepository(MarcoDbContext context)
        {
            _context = context;
        }

        public async Task<OpeningBalance> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.OpeningBalances
                .AsNoTracking()
                .Include(ob => ob.FiscalYear)
                .FirstOrDefaultAsync(ob => ob.Id == id, ct);
        }

        public async Task<System.Collections.Generic.IReadOnlyList<OpeningBalance>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.OpeningBalances
                .AsNoTracking()
                .Include(ob => ob.FiscalYear)
                .OrderByDescending(ob => ob.BalanceDate)
                .ToListAsync(ct);
        }

        public async Task AddAsync(OpeningBalance entity, CancellationToken ct = default)
        {
            await _context.OpeningBalances.AddAsync(entity, ct);
        }

        public void Update(OpeningBalance entity)
        {
            if (entity == null) return;

            var local = _context.OpeningBalances.Local.FirstOrDefault(e => e.Id == entity.Id);
            if (local != null && !ReferenceEquals(local, entity))
            {
                _context.Entry(local).CurrentValues.SetValues(entity);
                return;
            }
            _context.OpeningBalances.Update(entity);
        }

        public void Remove(OpeningBalance entity)
        {
            _context.OpeningBalances.Remove(entity);
        }

        public async Task<OpeningBalance> GetWithLinesAsync(int id, CancellationToken ct = default)
        {
            return await _context.OpeningBalances
                .AsNoTracking()
                .Include(ob => ob.FiscalYear)
                .Include(ob => ob.Lines)
                .FirstOrDefaultAsync(ob => ob.Id == id, ct);
        }

        public async Task<OpeningBalance> GetWithLinesTrackedAsync(int id, CancellationToken ct = default)
        {
            return await _context.OpeningBalances
                .Include(ob => ob.FiscalYear)
                .Include(ob => ob.Lines)
                .FirstOrDefaultAsync(ob => ob.Id == id, ct);
        }

        public async Task<OpeningBalance> GetByFiscalYearAsync(int fiscalYearId, CancellationToken ct = default)
        {
            return await _context.OpeningBalances
                .AsNoTracking()
                .Include(ob => ob.FiscalYear)
                .FirstOrDefaultAsync(ob => ob.FiscalYearId == fiscalYearId, ct);
        }

        public async Task<OpeningBalance> GetByFiscalYearWithLinesAsync(int fiscalYearId, CancellationToken ct = default)
        {
            return await _context.OpeningBalances
                .AsNoTracking()
                .Include(ob => ob.FiscalYear)
                .Include(ob => ob.Lines)
                .FirstOrDefaultAsync(ob => ob.FiscalYearId == fiscalYearId, ct);
        }

        public async Task<bool> ExistsForFiscalYearAsync(int fiscalYearId, CancellationToken ct = default)
        {
            return await _context.OpeningBalances
                .AnyAsync(ob => ob.FiscalYearId == fiscalYearId, ct);
        }
    }
}
