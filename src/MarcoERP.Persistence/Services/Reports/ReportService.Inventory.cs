using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Reports;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Reports;
using MarcoERP.Domain.Enums;
using MarcoERP.Persistence;

namespace MarcoERP.Persistence.Services.Reports
{
    public sealed partial class ReportService
    {
        // ════════════════════════════════════════════════════════
        //  INVENTORY REPORT
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<IReadOnlyList<InventoryReportRowDto>>> GetInventoryReportAsync(
            int? warehouseId = null, CancellationToken ct = default)
        {
            var query = _db.Set<Domain.Entities.Inventory.WarehouseProduct>()
                .AsNoTracking()
                .Include(wp => wp.Product).ThenInclude(p => p.Category)
                .Include(wp => wp.Product).ThenInclude(p => p.BaseUnit)
                .Include(wp => wp.Warehouse)
                .Where(wp => !wp.Product.IsDeleted && wp.Quantity != 0);

            if (warehouseId.HasValue)
                query = query.Where(wp => wp.WarehouseId == warehouseId.Value);

            var rows = await query
                .OrderBy(wp => wp.Product.Code)
                .ThenBy(wp => wp.Warehouse.NameAr)
                .Select(wp => new InventoryReportRowDto
                {
                    ProductId = wp.ProductId,
                    ProductCode = wp.Product.Code,
                    ProductName = wp.Product.NameAr,
                    CategoryName = wp.Product.Category.NameAr,
                    WarehouseName = wp.Warehouse.NameAr,
                    UnitName = wp.Product.BaseUnit.NameAr,
                    Quantity = wp.Quantity,
                    CostPrice = wp.Product.WeightedAverageCost,
                    TotalValue = wp.Quantity * wp.Product.WeightedAverageCost,
                    MinimumStock = wp.Product.MinimumStock,
                    IsBelowMinimum = wp.Quantity < wp.Product.MinimumStock
                }).ToListAsync(ct);

            return ServiceResult<IReadOnlyList<InventoryReportRowDto>>.Success(rows);
        }

        // ════════════════════════════════════════════════════════
        //  STOCK CARD
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<StockCardReportDto>> GetStockCardAsync(
            int productId, int? warehouseId, DateTime fromDate, DateTime toDate,
            CancellationToken ct = default)
        {
            var product = await _db.Set<Domain.Entities.Inventory.Product>()
                .AsNoTracking()
                .Include(p => p.BaseUnit)
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, ct);

            if (product == null)
                return ServiceResult<StockCardReportDto>.Failure("الصنف غير موجود.");

            var movQuery = _db.Set<Domain.Entities.Inventory.InventoryMovement>()
                .AsNoTracking()
                .Include(m => m.Warehouse)
                .Where(m => m.ProductId == productId);

            if (warehouseId.HasValue)
                movQuery = movQuery.Where(m => m.WarehouseId == warehouseId.Value);

            // Opening balance = server-side sum of movements before fromDate
            var incomingTypes = new[]
            {
                Domain.Enums.MovementType.PurchaseIn,
                Domain.Enums.MovementType.SalesReturn,
                Domain.Enums.MovementType.AdjustmentIn,
                Domain.Enums.MovementType.TransferIn,
                Domain.Enums.MovementType.OpeningBalance
            };

            decimal openingBalance = await movQuery
                .Where(m => m.MovementDate < fromDate)
                .SumAsync(m => incomingTypes.Contains(m.MovementType)
                    ? m.QuantityInBaseUnit
                    : -m.QuantityInBaseUnit, ct);

            // Movements in range — projected to avoid materializing full entities
            var movements = await movQuery
                .Where(m => m.MovementDate >= fromDate && m.MovementDate <= toDate)
                .OrderBy(m => m.MovementDate)
                .ThenBy(m => m.Id)
                .Select(m => new
                {
                    m.MovementDate,
                    m.MovementType,
                    m.ReferenceNumber,
                    m.SourceType,
                    WarehouseName = m.Warehouse.NameAr,
                    m.QuantityInBaseUnit,
                    m.UnitCost
                })
                .ToListAsync(ct);

            var rows = new List<StockCardRowDto>();
            decimal running = openingBalance;

            foreach (var m in movements)
            {
                bool isIncoming = incomingTypes.Contains(m.MovementType);
                decimal qIn = isIncoming ? m.QuantityInBaseUnit : 0;
                decimal qOut = !isIncoming ? m.QuantityInBaseUnit : 0;
                running += qIn - qOut;

                rows.Add(new StockCardRowDto
                {
                    MovementDate = m.MovementDate,
                    MovementTypeName = GetMovementTypeName(m.MovementType),
                    ReferenceNumber = m.ReferenceNumber,
                    SourceTypeName = GetSourceTypeName(m.SourceType),
                    WarehouseName = m.WarehouseName,
                    QuantityIn = qIn,
                    QuantityOut = qOut,
                    UnitCost = m.UnitCost,
                    BalanceAfter = running
                });
            }

            var report = new StockCardReportDto
            {
                ProductId = product.Id,
                ProductCode = product.Code,
                ProductName = product.NameAr,
                UnitName = product.BaseUnit?.NameAr,
                OpeningBalance = openingBalance,
                TotalIn = rows.Sum(r => r.QuantityIn),
                TotalOut = rows.Sum(r => r.QuantityOut),
                ClosingBalance = running,
                Rows = rows
            };

            return ServiceResult<StockCardReportDto>.Success(report);
        }

    }
}
