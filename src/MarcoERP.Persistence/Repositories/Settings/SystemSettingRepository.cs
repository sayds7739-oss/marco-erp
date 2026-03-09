using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Settings;
using MarcoERP.Domain.Interfaces.Settings;

namespace MarcoERP.Persistence.Repositories.Settings
{
    /// <summary>
    /// EF Core implementation of ISystemSettingRepository.
    /// </summary>
    public sealed class SystemSettingRepository : ISystemSettingRepository
    {
        private readonly MarcoDbContext _context;

        public SystemSettingRepository(MarcoDbContext context) => _context = context;

        // ── IRepository<SystemSetting> ───────────────────────────

        public async Task<SystemSetting> GetByIdAsync(int id, CancellationToken ct = default)
            => await _context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, ct);

        public async Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken ct = default)
            => await _context.SystemSettings
                .AsNoTracking()
                .OrderBy(s => s.GroupName)
                .ThenBy(s => s.SettingKey)
                .ToListAsync(ct);

        public async Task AddAsync(SystemSetting entity, CancellationToken ct = default)
            => await _context.SystemSettings.AddAsync(entity, ct);

        public void Update(SystemSetting entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            var local = _context.SystemSettings.Local.FirstOrDefault(e => e.Id == entity.Id);
            if (local != null && local != entity)
                _context.Entry(local).CurrentValues.SetValues(entity);
            else
                _context.Entry(entity).State = EntityState.Modified;
        }
        public void Remove(SystemSetting entity) => _context.SystemSettings.Remove(entity);

        // ── ISystemSettingRepository ─────────────────────────────

        public async Task<SystemSetting> GetByKeyAsync(string settingKey, CancellationToken ct = default)
            => await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.SettingKey == settingKey, ct);

        public async Task<IReadOnlyList<SystemSetting>> GetByGroupAsync(string groupName, CancellationToken ct = default)
            => await _context.SystemSettings
                .AsNoTracking()
                .Where(s => s.GroupName == groupName)
                .OrderBy(s => s.SettingKey)
                .ToListAsync(ct);

        public async Task<bool> KeyExistsAsync(string settingKey, CancellationToken ct = default)
            => await _context.SystemSettings.AnyAsync(s => s.SettingKey == settingKey, ct);
    }
}
