using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("Products");

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            // ── Properties ──────────────────────────────────────
            builder.Property(p => p.Code)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(p => p.NameAr)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(p => p.NameEn)
                .HasMaxLength(200);

            builder.Property(p => p.CostPrice)
                .HasPrecision(18, 4);

            builder.Property(p => p.DefaultSalePrice)
                .HasPrecision(18, 4);

            builder.Property(p => p.WeightedAverageCost)
                .HasPrecision(18, 4);

            builder.Property(p => p.WholesalePrice)
                .HasPrecision(18, 4);

            builder.Property(p => p.RetailPrice)
                .HasPrecision(18, 4);

            builder.Property(p => p.MaximumStock)
                .HasPrecision(18, 4);

            builder.Property(p => p.MinimumStock)
                .HasPrecision(18, 4);

            builder.Property(p => p.ReorderLevel)
                .HasPrecision(18, 4);

            builder.Property(p => p.VatRate)
                .HasPrecision(5, 2);

            builder.Property(p => p.Barcode)
                .HasMaxLength(50);

            builder.Property(p => p.Description)
                .HasMaxLength(500);

            builder.Property(p => p.ImagePath)
                .HasMaxLength(500);

            builder.Property(p => p.Status)
                .IsRequired()
                .HasConversion<int>();

            // ── Soft Delete Fields ──────────────────────────────
            builder.Property(p => p.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(p => p.DeletedBy).HasMaxLength(100);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(p => p.CreatedAt).IsRequired();
            builder.Property(p => p.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(p => p.ModifiedBy).HasMaxLength(100);

            // ── Relationships ───────────────────────────────────
            builder.HasOne(p => p.Category)
                .WithMany()
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.BaseUnit)
                .WithMany()
                .HasForeignKey(p => p.BaseUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(p => p.ProductUnits)
                .WithOne(pu => pu.Product)
                .HasForeignKey(pu => pu.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.DefaultSupplier)
                .WithMany()
                .HasForeignKey(p => p.DefaultSupplierId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(p => p.Code)
                .IsUnique()
                .HasDatabaseName("IX_Products_Code");

            builder.HasIndex(p => p.Barcode)
                .HasDatabaseName("IX_Products_Barcode")
                .HasFilter(DbProviderHelper.IsNotNullFilter("Barcode"));

            builder.HasIndex(p => p.CategoryId)
                .HasDatabaseName("IX_Products_CategoryId");

            builder.HasIndex(p => p.NameAr)
                .HasDatabaseName("IX_Products_NameAr");

            builder.HasIndex(p => p.Status)
                .HasDatabaseName("IX_Products_Status");

            builder.HasIndex(p => p.DefaultSupplierId)
                .HasDatabaseName("IX_Products_DefaultSupplierId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("DefaultSupplierId"));
        }
    }
}
