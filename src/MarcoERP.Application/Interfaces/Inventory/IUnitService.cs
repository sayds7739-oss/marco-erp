using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;

namespace MarcoERP.Application.Interfaces.Inventory
{
    /// <summary>
    /// Application service for Unit of Measure management.
    /// </summary>
    public interface IUnitService
    {
        /// <summary>استرجاع جميع الوحدات — Gets all units of measure.</summary>
        Task<ServiceResult<IReadOnlyList<UnitDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>استرجاع الوحدات النشطة فقط — Gets only active units.</summary>
        Task<ServiceResult<IReadOnlyList<UnitDto>>> GetActiveAsync(CancellationToken ct = default);

        /// <summary>استرجاع وحدة بالمعرّف — Gets a unit by ID.</summary>
        Task<ServiceResult<UnitDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>إنشاء وحدة قياس جديدة — Creates a new unit of measure.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult<UnitDto>> CreateAsync(CreateUnitDto dto, CancellationToken ct = default);

        /// <summary>تعديل وحدة قياس — Updates an existing unit.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult<UnitDto>> UpdateAsync(UpdateUnitDto dto, CancellationToken ct = default);

        /// <summary>تفعيل وحدة — Activates a unit.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult> ActivateAsync(int id, CancellationToken ct = default);

        /// <summary>تعطيل وحدة — Deactivates a unit.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct = default);
    }
}
