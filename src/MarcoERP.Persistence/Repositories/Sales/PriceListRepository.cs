using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Application.Interfaces;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Interfaces.Sales;

namespace MarcoERP.Persistence.Repositories.Sales
{
    public sealed class PriceListRepository : IPriceListRepository
    {
        private readonly MarcoDbContext _context;
        private readonly IDateTimeProvider _dateTime;

        public PriceListRepository(MarcoDbContext context, IDateTimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        public async Task<PriceList> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.PriceLists
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);
        }

        public async Task<IReadOnlyList<PriceList>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.PriceLists
                .AsNoTracking()
                .Include(p => p.Tiers)
                .OrderBy(p => p.Code)
                .ToListAsync(ct);
        }

        public async Task AddAsync(PriceList entity, CancellationToken ct = default)
        {
            await _context.PriceLists.AddAsync(entity, ct);
        }

        public void Update(PriceList entity)
        {
            if (entity == null) return;

            var local = _context.PriceLists.Local.FirstOrDefault(e => e.Id == entity.Id);
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
        public void Remove(PriceList entity) => _context.PriceLists.Remove(entity);

        public async Task<PriceList> GetWithTiersAsync(int id, CancellationToken ct = default)
        {
            return await _context.PriceLists
                .AsNoTracking()
                .Include(p => p.Tiers).ThenInclude(t => t.Product)
                .FirstOrDefaultAsync(p => p.Id == id, ct);
        }

        public async Task<PriceList> GetByCodeAsync(string code, CancellationToken ct = default)
        {
            return await _context.PriceLists
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == code, ct);
        }

        public async Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
        {
            return await _context.PriceLists.AnyAsync(p => p.Code == code, ct);
        }

        public async Task<string> GetNextCodeAsync(CancellationToken ct = default)
        {
            var prefix = "PL-";
            var lastCode = await _context.PriceLists
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.Code.StartsWith(prefix))
                .OrderByDescending(p => p.Code)
                .Select(p => p.Code)
                .FirstOrDefaultAsync(ct);

            if (lastCode == null) return $"{prefix}0001";

            var seqPart = lastCode.Substring(prefix.Length);
            if (int.TryParse(seqPart, NumberStyles.None, CultureInfo.InvariantCulture, out var seq))
                return $"{prefix}{(seq + 1):D4}";

            return $"{prefix}0001";
        }

        public async Task<IReadOnlyList<PriceList>> GetActiveListsAsync(DateTime date, CancellationToken ct = default)
        {
            return await _context.PriceLists
                .AsNoTracking()
                .Include(p => p.Tiers)
                .Where(p => p.IsActive
                    && (!p.ValidFrom.HasValue || p.ValidFrom.Value <= date)
                    && (!p.ValidTo.HasValue || p.ValidTo.Value >= date))
                .ToListAsync(ct);
        }

        public async Task<decimal?> GetBestPriceAsync(int productId, decimal quantity, DateTime date, CancellationToken ct = default)
        {
            var activeLists = await GetActiveListsAsync(date, ct);

            decimal? bestPrice = null;
            foreach (var list in activeLists)
            {
                var tier = list.GetBestPrice(productId, quantity);
                if (tier != null && (!bestPrice.HasValue || tier.Price < bestPrice.Value))
                    bestPrice = tier.Price;
            }

            return bestPrice;
        }
    }
}
