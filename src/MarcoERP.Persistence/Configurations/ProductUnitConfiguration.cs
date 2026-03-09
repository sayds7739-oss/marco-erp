using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class ProductUnitConfiguration : IEntityTypeConfiguration<ProductUnit>
    {
        public void Configure(EntityTypeBuilder<ProductUnit> builder)
        {
            builder.ToTable("ProductUnits");

            builder.HasKey(pu => pu.Id);
            builder.Property(pu => pu.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(pu => pu.ConversionFactor)
                .IsRequired()
                .HasPrecision(18, 6);

            builder.Property(pu => pu.SalePrice)
                .HasPrecision(18, 4);

            builder.Property(pu => pu.PurchasePrice)
                .HasPrecision(18, 4);

            builder.Property(pu => pu.Barcode)
                .HasMaxLength(50);

            builder.Property(pu => pu.IsDefault)
                .IsRequired()
                .HasDefaultValue(false);

            // ── Relationships ───────────────────────────────────
            // Product → ProductUnit is configured on ProductConfiguration (HasMany)

            builder.HasOne(pu => pu.Unit)
                .WithMany()
                .HasForeignKey(pu => pu.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(pu => new { pu.ProductId, pu.UnitId })
                .IsUnique()
                .HasDatabaseName("IX_ProductUnits_Product_Unit");

            builder.HasIndex(pu => pu.Barcode)
                .HasDatabaseName("IX_ProductUnits_Barcode")
                .HasFilter(DbProviderHelper.IsNotNullFilter("Barcode"));
        }
    }
}
