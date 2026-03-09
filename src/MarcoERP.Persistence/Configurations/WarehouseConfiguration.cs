using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
    {
        public void Configure(EntityTypeBuilder<Warehouse> builder)
        {
            builder.ToTable("Warehouses");

            builder.HasKey(w => w.Id);
            builder.Property(w => w.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(w => w.Code)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(w => w.NameAr)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(w => w.NameEn)
                .HasMaxLength(100);

            builder.Property(w => w.Address)
                .HasMaxLength(300);

            builder.Property(w => w.Phone)
                .HasMaxLength(20);

            builder.Property(w => w.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(w => w.IsDefault)
                .IsRequired()
                .HasDefaultValue(false);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(w => w.CreatedAt).IsRequired();
            builder.Property(w => w.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(w => w.ModifiedBy).HasMaxLength(100);

            // ── Soft Delete Fields ──────────────────────────────
            builder.Property(w => w.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(w => w.DeletedAt);

            builder.Property(w => w.DeletedBy)
                .HasMaxLength(100);

            // ── Optional relationship to GL Account ─────────────
            // No navigation property on Account side — just store FK
            builder.Property(w => w.AccountId);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(w => w.Code)
                .IsUnique()
                .HasDatabaseName("IX_Warehouses_Code");
        }
    }
}
