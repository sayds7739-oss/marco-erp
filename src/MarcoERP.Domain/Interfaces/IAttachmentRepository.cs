using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Common;

namespace MarcoERP.Domain.Interfaces
{
    public interface IAttachmentRepository
    {
        Task<IReadOnlyList<Attachment>> GetByEntityAsync(string entityType, int entityId, CancellationToken ct = default);
        Task<Attachment> GetByIdAsync(int id, CancellationToken ct = default);
        Task AddAsync(Attachment attachment, CancellationToken ct = default);
        Task DeleteAsync(Attachment attachment, CancellationToken ct = default);
    }
}
