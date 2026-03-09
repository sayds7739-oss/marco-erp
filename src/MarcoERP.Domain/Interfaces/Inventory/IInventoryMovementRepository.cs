using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Domain.Interfaces.Inventory
{
    /// <summary>
    /// Repository contract for InventoryMovement entity.
    /// </summary>
    public interface IInventoryMovementRepository : IRepository<InventoryMovement>
    {
        /// <summary>Gets stock card — all movements for a product in a specific warehouse, ordered by date.</summary>
        Task<IReadOnlyList<InventoryMovement>> GetStockCardAsync(
            int productId,
            int warehouseId,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default);

        /// <summary>Gets all movements for a specific product across all warehouses.</summary>
        Task<IReadOnlyList<InventoryMovement>> GetByProductAsync(
            int productId,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default);

        /// <summary>Gets movements linked to a specific source document.</summary>
        Task<IReadOnlyList<InventoryMovement>> GetBySourceAsync(
            SourceType sourceType,
            int sourceId,
            CancellationToken ct = default);

        /// <summary>Gets movements by date range and movement type (e.g., SalesOut for COGS).</summary>
        Task<IReadOnlyList<InventoryMovement>> GetByDateRangeAndTypeAsync(
            DateTime fromDate,
            DateTime toDate,
            MovementType movementType,
            CancellationToken ct = default);
    }
}
