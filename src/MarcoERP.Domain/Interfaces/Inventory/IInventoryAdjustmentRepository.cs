using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Domain.Interfaces.Inventory
{
    /// <summary>
    /// Repository interface for InventoryAdjustment aggregate.
    /// </summary>
    public interface IInventoryAdjustmentRepository : IRepository<InventoryAdjustment>
    {
        /// <summary>Gets an adjustment with all its lines loaded (no tracking — read-only).</summary>
        Task<InventoryAdjustment> GetWithLinesAsync(int id, CancellationToken ct = default);

        /// <summary>Gets an adjustment with all its lines loaded WITH change tracking (for updates).</summary>
        Task<InventoryAdjustment> GetWithLinesTrackedAsync(int id, CancellationToken ct = default);

        /// <summary>Gets an adjustment by number.</summary>
        Task<InventoryAdjustment> GetByNumberAsync(string number, CancellationToken ct = default);

        /// <summary>Checks if a number already exists.</summary>
        Task<bool> NumberExistsAsync(string number, CancellationToken ct = default);

        /// <summary>Gets adjustments by status.</summary>
        Task<IReadOnlyList<InventoryAdjustment>> GetByStatusAsync(InvoiceStatus status, CancellationToken ct = default);

        /// <summary>Gets adjustments by warehouse.</summary>
        Task<IReadOnlyList<InventoryAdjustment>> GetByWarehouseAsync(int warehouseId, CancellationToken ct = default);

        /// <summary>Gets the next auto-generated adjustment number.</summary>
        Task<string> GetNextNumberAsync(CancellationToken ct = default);
    }
}
