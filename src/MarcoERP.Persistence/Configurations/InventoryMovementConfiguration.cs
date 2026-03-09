using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
    {
        public void Configure(EntityTypeBuilder<InventoryMovement> builder)
        {
            builder.ToTable("InventoryMovements", t =>
            {
                // FIN-CK-06: Base quantity must be positive
                t.HasCheckConstraint("CK_InventoryMovements_BaseQuantity",
                    DbProviderHelper.CheckExpr("{0} > 0", "QuantityInBaseUnit"));

                // FIN-CK-07: Total cost must be non-negative
                t.HasCheckConstraint("CK_InventoryMovements_TotalCost",
                    DbProviderHelper.CheckExpr("{0} >= 0", "TotalCost"));
            });

            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(m => m.MovementType)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(m => m.Quantity)
                .IsRequired()
                .HasPrecision(18, 4);

            builder.Property(m => m.QuantityInBaseUnit)
                .IsRequired()
                .HasPrecision(18, 4);

            builder.Property(m => m.UnitCost)
                .HasPrecision(18, 4);

            builder.Property(m => m.TotalCost)
                .HasPrecision(18, 4);

            builder.Property(m => m.MovementDate)
                .IsRequired();

            builder.Property(m => m.ReferenceNumber)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(m => m.SourceType)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(m => m.BalanceAfter)
                .HasPrecision(18, 4);

            builder.Property(m => m.Notes)
                .HasMaxLength(500);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(m => m.CreatedAt).IsRequired();
            builder.Property(m => m.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(m => m.ModifiedBy).HasMaxLength(100);

            // ── Relationships ───────────────────────────────────
            builder.HasOne(m => m.Product)
                .WithMany()
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(m => m.Warehouse)
                .WithMany()
                .HasForeignKey(m => m.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(m => m.Unit)
                .WithMany()
                .HasForeignKey(m => m.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(m => new { m.ProductId, m.WarehouseId, m.MovementDate })
                .HasDatabaseName("IX_InventoryMovements_StockCard");

            builder.HasIndex(m => new { m.SourceType, m.SourceId })
                .HasDatabaseName("IX_InventoryMovements_Source")
                .HasFilter(DbProviderHelper.IsNotNullFilter("SourceId"));

            builder.HasIndex(m => m.ReferenceNumber)
                .HasDatabaseName("IX_InventoryMovements_Reference");

            builder.HasIndex(m => m.ProductId)
                .HasDatabaseName("IX_InventoryMovements_ProductId");

            builder.HasIndex(m => m.WarehouseId)
                .HasDatabaseName("IX_InventoryMovements_WarehouseId");

            builder.HasIndex(m => m.MovementDate)
                .HasDatabaseName("IX_InventoryMovements_MovementDate");
        }
    }
}
