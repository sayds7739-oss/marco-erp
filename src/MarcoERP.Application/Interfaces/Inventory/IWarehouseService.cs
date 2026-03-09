using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;

namespace MarcoERP.Application.Interfaces.Inventory
{
    /// <summary>
    /// Application service for Warehouse management and stock queries.
    /// </summary>
    public interface IWarehouseService
    {
        // ── Warehouse CRUD ──────────────────────────────────────

        /// <summary>استرجاع جميع المخازن — Gets all warehouses.</summary>
        Task<ServiceResult<IReadOnlyList<WarehouseDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>استرجاع المخازن النشطة فقط — Gets only active warehouses.</summary>
        Task<ServiceResult<IReadOnlyList<WarehouseDto>>> GetActiveAsync(CancellationToken ct = default);

        /// <summary>استرجاع مخزن بالمعرّف — Gets a warehouse by ID.</summary>
        Task<ServiceResult<WarehouseDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>إنشاء مخزن جديد — Creates a new warehouse.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult<WarehouseDto>> CreateAsync(CreateWarehouseDto dto, CancellationToken ct = default);

        /// <summary>تعديل مخزن — Updates an existing warehouse.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult<WarehouseDto>> UpdateAsync(UpdateWarehouseDto dto, CancellationToken ct = default);

        /// <summary>تعيين مخزن كافتراضي — Sets a warehouse as the default.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult> SetDefaultAsync(int id, CancellationToken ct = default);

        /// <summary>تفعيل مخزن — Activates a warehouse.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult> ActivateAsync(int id, CancellationToken ct = default);

        /// <summary>تعطيل مخزن — Deactivates a warehouse.</summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct = default);

        // ── Stock Queries ───────────────────────────────────────

        /// <summary>استرجاع أرصدة مخزن معين — Gets stock balances for a specific warehouse.</summary>
        Task<ServiceResult<IReadOnlyList<StockBalanceDto>>> GetStockByWarehouseAsync(int warehouseId, CancellationToken ct = default);

        /// <summary>استرجاع أرصدة صنف عبر جميع المخازن — Gets stock balances for a product across all warehouses.</summary>
        Task<ServiceResult<IReadOnlyList<StockBalanceDto>>> GetStockByProductAsync(int productId, CancellationToken ct = default);

        /// <summary>استرجاع الأصناف تحت الحد الأدنى — Gets products below minimum stock level.</summary>
        Task<ServiceResult<IReadOnlyList<StockBalanceDto>>> GetBelowMinimumStockAsync(CancellationToken ct = default);
    }
}
