using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;

namespace MarcoERP.Persistence.Repositories
{
    /// <summary>
    /// EF Core implementation of IAccountRepository.
    /// Global soft-delete filter is applied automatically by MarcoDbContext.
    /// </summary>
    public sealed class AccountRepository : IAccountRepository
    {
        private readonly MarcoDbContext _context;

        public AccountRepository(MarcoDbContext context)
        {
            _context = context;
        }

        // ── IRepository<Account> ────────────────────────────────

        public async Task<Account> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .AsNoTracking()
                .OrderBy(a => a.AccountCode)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Account entity, CancellationToken cancellationToken = default)
        {
            await _context.Accounts.AddAsync(entity, cancellationToken);
        }

        public void Update(Account entity)
        {
            if (entity == null) return;

            var local = _context.Accounts.Local.FirstOrDefault(e => e.Id == entity.Id);
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

        public void Remove(Account entity)
        {
            _context.Accounts.Remove(entity);
        }

        // ── IAccountRepository ──────────────────────────────────

        public async Task<Account> GetByCodeAsync(string accountCode, CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountCode == accountCode, cancellationToken);
        }

        public async Task<IReadOnlyList<Account>> GetChildrenAsync(int parentAccountId, CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .AsNoTracking()
                .Where(a => a.ParentAccountId == parentAccountId)
                .OrderBy(a => a.AccountCode)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Account>> GetPostableAccountsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .AsNoTracking()
                .Where(a => a.IsLeaf && a.AllowPosting && a.IsActive)
                .OrderBy(a => a.AccountCode)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Account>> GetByTypeAsync(AccountType accountType, CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .AsNoTracking()
                .Where(a => a.AccountType == accountType)
                .OrderBy(a => a.AccountCode)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> CodeExistsAsync(string accountCode, CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .AnyAsync(a => a.AccountCode == accountCode, cancellationToken);
        }

        public async Task<bool> HasChildrenAsync(int accountId, CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .AnyAsync(a => a.ParentAccountId == accountId, cancellationToken);
        }
    }
}
