using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core configuration for SalesRepresentative entity.
    /// </summary>
    public sealed class SalesRepresentativeConfiguration : IEntityTypeConfiguration<SalesRepresentative>
    {
        public void Configure(EntityTypeBuilder<SalesRepresentative> builder)
        {
            builder.ToTable("SalesRepresentatives");

            builder.HasKey(sr => sr.Id);
            builder.Property(sr => sr.Id).UseIdentityColumn();

            // ── Concurrency Token ───────────────────────────────
            DbProviderHelper.ConfigureRowVersion(builder);

            // ── Required Fields ─────────────────────────────────
            builder.Property(sr => sr.Code)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(sr => sr.NameAr)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(sr => sr.NameEn)
                .HasMaxLength(200);

            builder.Property(sr => sr.Phone)
                .HasMaxLength(30);

            builder.Property(sr => sr.Mobile)
                .HasMaxLength(30);

            builder.Property(sr => sr.Email)
                .HasMaxLength(200);

            builder.Property(sr => sr.CommissionRate)
                .HasPrecision(5, 2);

            builder.Property(sr => sr.CommissionBasedOn)
                .IsRequired()
                .HasConversion<int>()
                .HasDefaultValue(CommissionBasis.Sales);

            builder.Property(sr => sr.Notes)
                .HasMaxLength(1000);

            // ── Soft Delete Fields ──────────────────────────────
            builder.Property(sr => sr.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(sr => sr.DeletedBy)
                .HasMaxLength(100);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(sr => sr.CreatedAt).IsRequired();
            builder.Property(sr => sr.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(sr => sr.ModifiedBy).HasMaxLength(100);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(sr => sr.Code)
                .IsUnique()
                .HasDatabaseName("IX_SalesRepresentatives_Code");

            builder.HasIndex(sr => sr.IsActive)
                .HasDatabaseName("IX_SalesRepresentatives_IsActive");
        }
    }
}
