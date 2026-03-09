using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Security;

namespace MarcoERP.Domain.Interfaces.Security
{
    /// <summary>
    /// Repository contract for Role entity.
    /// </summary>
    public interface IRoleRepository : IRepository<Role>
    {
        /// <summary>Gets a role by ID with all permissions loaded (no tracking).</summary>
        Task<Role> GetByIdWithPermissionsAsync(int id, CancellationToken ct = default);

        /// <summary>Gets a role by ID with all permissions loaded (tracked for updates).</summary>
        Task<Role> GetByIdWithPermissionsTrackedAsync(int id, CancellationToken ct = default);

        /// <summary>Gets all roles with their permissions loaded.</summary>
        Task<IReadOnlyList<Role>> GetAllWithPermissionsAsync(CancellationToken ct = default);

        /// <summary>Gets a role by its English name.</summary>
        Task<Role> GetByNameEnAsync(string nameEn, CancellationToken ct = default);

        /// <summary>Checks if a role name already exists (checks both NameAr and NameEn).</summary>
        Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken ct = default);
    }
}
