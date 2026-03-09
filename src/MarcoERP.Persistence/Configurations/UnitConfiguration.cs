using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
    {
        public void Configure(EntityTypeBuilder<Unit> builder)
        {
            builder.ToTable("Units");

            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(u => u.NameAr)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(u => u.NameEn)
                .HasMaxLength(50);

            builder.Property(u => u.AbbreviationAr)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(u => u.AbbreviationEn)
                .HasMaxLength(10);

            builder.Property(u => u.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(u => u.CreatedAt).IsRequired();
            builder.Property(u => u.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(u => u.ModifiedBy).HasMaxLength(100);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(u => u.NameAr)
                .IsUnique()
                .HasDatabaseName("IX_Units_NameAr");
        }
    }
}
