using System;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces.Inventory;

namespace MarcoERP.Application.Common
{
    /// <summary>
    /// Specification for a single stock operation (increase or decrease).
    /// </summary>
    public sealed class StockOperation
    {
        public int ProductId { get; init; }
        public int WarehouseId { get; init; }
        public int UnitId { get; init; }
        public MovementType MovementType { get; init; }
        public decimal Quantity { get; init; }
        public decimal BaseQuantity { get; init; }
        public decimal CostPerBaseUnit { get; init; }
        public DateTime DocumentDate { get; init; }
        public string DocumentNumber { get; init; }
        public SourceType SourceType { get; init; }
        public int? SourceId { get; init; }
        public string Notes { get; init; }

        /// <summary>If true, creates the WarehouseProduct if it doesn't exist.</summary>
        public bool AllowCreate { get; init; } = true;

        /// <summary>If true, allows stock to go negative on decrease.</summary>
        public bool AllowNegativeStock { get; init; }
    }

    /// <summary>
    /// Centralises the WarehouseProduct adjustment + InventoryMovement creation
    /// pipeline that was duplicated across 10+ service methods.
    /// </summary>
    public sealed class StockManager
    {
        private readonly IWarehouseProductRepository _whProductRepo;
        private readonly IInventoryMovementRepository _movementRepo;

        public StockManager(
            IWarehouseProductRepository whProductRepo,
            IInventoryMovementRepository movementRepo)
        {
            _whProductRepo = whProductRepo ?? throw new ArgumentNullException(nameof(whProductRepo));
            _movementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
        }

        /// <summary>
        /// Increases stock and records an inbound inventory movement.
        /// Returns the updated WarehouseProduct.
        /// </summary>
        public async Task<WarehouseProduct> IncreaseAsync(StockOperation op, CancellationToken ct = default)
        {
            var whProduct = await _whProductRepo.GetOrCreateAsync(op.WarehouseId, op.ProductId, ct);

            whProduct.IncreaseStock(op.BaseQuantity);
            _whProductRepo.Update(whProduct);

            await RecordMovementAsync(whProduct, op, ct);
            return whProduct;
        }

        /// <summary>
        /// Decreases stock and records an outbound inventory movement.
        /// Returns the updated WarehouseProduct.
        /// </summary>
        public async Task<WarehouseProduct> DecreaseAsync(StockOperation op, CancellationToken ct = default)
        {
            var whProduct = op.AllowCreate
                ? await _whProductRepo.GetOrCreateAsync(op.WarehouseId, op.ProductId, ct)
                : await _whProductRepo.GetAsync(op.WarehouseId, op.ProductId, ct);

            if (whProduct == null)
                throw new Domain.Exceptions.Inventory.InventoryDomainException(
                    $"لا يوجد رصيد للصنف {op.ProductId} في المخزن {op.WarehouseId}.");

            if (op.AllowNegativeStock)
                whProduct.DecreaseStockAllowNegative(op.BaseQuantity);
            else
                whProduct.DecreaseStock(op.BaseQuantity);

            _whProductRepo.Update(whProduct);

            await RecordMovementAsync(whProduct, op, ct);
            return whProduct;
        }

        /// <summary>
        /// Returns total stock across all warehouses for the given product.
        /// </summary>
        public Task<decimal> GetTotalStockAsync(int productId, CancellationToken ct = default)
            => _whProductRepo.GetTotalStockAsync(productId, ct);

        private async Task RecordMovementAsync(WarehouseProduct whProduct, StockOperation op, CancellationToken ct)
        {
            var totalCost = Math.Round(op.BaseQuantity * op.CostPerBaseUnit, 4);

            var movement = new InventoryMovement(
                op.ProductId,
                op.WarehouseId,
                op.UnitId,
                op.MovementType,
                op.Quantity,
                op.BaseQuantity,
                op.CostPerBaseUnit,
                totalCost,
                op.DocumentDate,
                op.DocumentNumber,
                op.SourceType,
                sourceId: op.SourceId,
                notes: op.Notes);

            if (op.AllowNegativeStock)
                movement.SetBalanceAfterAllowNegative(whProduct.Quantity);
            else
                movement.SetBalanceAfter(whProduct.Quantity);
            await _movementRepo.AddAsync(movement, ct);
        }
    }
}
