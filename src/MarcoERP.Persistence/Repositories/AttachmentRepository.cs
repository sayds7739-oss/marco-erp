using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Interfaces;

namespace MarcoERP.Persistence.Repositories
{
    public sealed class AttachmentRepository : IAttachmentRepository
    {
        private readonly MarcoDbContext _db;

        public AttachmentRepository(MarcoDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Attachment>> GetByEntityAsync(
            string entityType, int entityId, CancellationToken ct = default)
        {
            return await _db.Attachments
                .AsNoTracking()
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync(ct);
        }

        public async Task<Attachment> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _db.Attachments.FindAsync(new object[] { id }, ct);
        }

        public async Task AddAsync(Attachment attachment, CancellationToken ct = default)
        {
            await _db.Attachments.AddAsync(attachment, ct);
        }

        public Task DeleteAsync(Attachment attachment, CancellationToken ct = default)
        {
            _db.Attachments.Remove(attachment);
            return Task.CompletedTask;
        }
    }
}
