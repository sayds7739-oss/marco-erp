using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class WarehouseProductConfiguration : IEntityTypeConfiguration<WarehouseProduct>
    {
        public void Configure(EntityTypeBuilder<WarehouseProduct> builder)
        {
            builder.ToTable("WarehouseProducts");

            builder.HasKey(wp => wp.Id);
            builder.Property(wp => wp.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(wp => wp.Quantity)
                .IsRequired()
                .HasPrecision(18, 4)
                .HasDefaultValue(0m);

            // ── Relationships ───────────────────────────────────
            builder.HasOne(wp => wp.Warehouse)
                .WithMany()
                .HasForeignKey(wp => wp.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(wp => wp.Product)
                .WithMany()
                .HasForeignKey(wp => wp.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(wp => new { wp.WarehouseId, wp.ProductId })
                .IsUnique()
                .HasDatabaseName("IX_WarehouseProducts_Warehouse_Product");
        }
    }
}
