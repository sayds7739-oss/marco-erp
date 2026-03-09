using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class InventoryAdjustmentLineConfiguration : IEntityTypeConfiguration<InventoryAdjustmentLine>
    {
        public void Configure(EntityTypeBuilder<InventoryAdjustmentLine> builder)
        {
            builder.ToTable("InventoryAdjustmentLines");

            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).UseIdentityColumn();
            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(l => l.InventoryAdjustmentId).IsRequired();
            builder.Property(l => l.ProductId).IsRequired();
            builder.Property(l => l.UnitId).IsRequired();
            builder.Property(l => l.SystemQuantity).IsRequired().HasPrecision(18, 4);
            builder.Property(l => l.ActualQuantity).IsRequired().HasPrecision(18, 4);
            builder.Property(l => l.DifferenceQuantity).IsRequired().HasPrecision(18, 4);
            builder.Property(l => l.ConversionFactor).IsRequired().HasPrecision(18, 6);
            builder.Property(l => l.DifferenceInBaseUnit).IsRequired().HasPrecision(18, 4);
            builder.Property(l => l.UnitCost).IsRequired().HasPrecision(18, 4);
            builder.Property(l => l.CostDifference).IsRequired().HasPrecision(18, 4);

            // Relationships
            builder.HasOne(l => l.Product).WithMany()
                .HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(l => l.Unit).WithMany()
                .HasForeignKey(l => l.UnitId).OnDelete(DeleteBehavior.Restrict);

            // Indexes
            builder.HasIndex(l => l.InventoryAdjustmentId)
                .HasDatabaseName("IX_InventoryAdjustmentLines_AdjustmentId");
            builder.HasIndex(l => l.ProductId)
                .HasDatabaseName("IX_InventoryAdjustmentLines_ProductId");
        }
    }
}
