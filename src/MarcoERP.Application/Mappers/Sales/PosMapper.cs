using System.Linq;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;

namespace MarcoERP.Application.Mappers.Sales
{
    /// <summary>
    /// Manual mapper for POS entities ↔ DTOs.
    /// </summary>
    public static class PosMapper
    {
        /// <summary>Maps PosSession entity → PosSessionDto.</summary>
        public static PosSessionDto ToSessionDto(PosSession entity)
        {
            if (entity == null) return null;

            return new PosSessionDto
            {
                Id = entity.Id,
                SessionNumber = entity.SessionNumber,
                UserId = entity.UserId,
                UserName = entity.User?.FullNameAr,
                CashboxId = entity.CashboxId,
                CashboxNameAr = entity.Cashbox?.NameAr,
                WarehouseId = entity.WarehouseId,
                WarehouseNameAr = entity.Warehouse?.NameAr,
                OpeningBalance = entity.OpeningBalance,
                TotalSales = entity.TotalSales,
                TotalCashReceived = entity.TotalCashReceived,
                TotalCardReceived = entity.TotalCardReceived,
                TotalOnAccount = entity.TotalOnAccount,
                TransactionCount = entity.TransactionCount,
                ClosingBalance = entity.ClosingBalance,
                Variance = entity.Variance,
                Status = entity.Status.ToString(),
                OpenedAt = entity.OpenedAt,
                ClosedAt = entity.ClosedAt,
                ClosingNotes = entity.ClosingNotes
            };
        }

        /// <summary>Maps PosSession → Lightweight list DTO.</summary>
        public static PosSessionListDto ToSessionListDto(PosSession entity)
        {
            if (entity == null) return null;

            return new PosSessionListDto
            {
                Id = entity.Id,
                SessionNumber = entity.SessionNumber,
                UserName = entity.User?.FullNameAr,
                Status = entity.Status.ToString(),
                TotalSales = entity.TotalSales,
                TransactionCount = entity.TransactionCount,
                OpenedAt = entity.OpenedAt,
                ClosedAt = entity.ClosedAt
            };
        }

        /// <summary>Maps Product entity → POS product lookup DTO.</summary>
        public static PosProductLookupDto ToProductLookupDto(Product product)
        {
            if (product == null) return null;

            return new PosProductLookupDto
            {
                Id = product.Id,
                Code = product.Code,
                NameAr = product.NameAr,
                Barcode = product.Barcode,
                DefaultSalePrice = product.DefaultSalePrice,
                WeightedAverageCost = product.WeightedAverageCost,
                VatRate = product.VatRate,
                Units = product.ProductUnits?.Select(pu => new PosProductUnitDto
                {
                    UnitId = pu.UnitId,
                    UnitNameAr = pu.Unit?.NameAr,
                    ConversionFactor = pu.ConversionFactor,
                    SalePrice = pu.SalePrice,
                    Barcode = pu.Barcode,
                    IsDefault = pu.IsDefault
                }).ToList() ?? new()
            };
        }
    }
}
