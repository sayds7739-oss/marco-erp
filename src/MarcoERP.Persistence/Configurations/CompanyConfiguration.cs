using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Common;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for the Company entity.
    /// </summary>
    public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
    {
        public void Configure(EntityTypeBuilder<Company> builder)
        {
            builder.ToTable("Companies");

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(c => c.Code)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(c => c.NameAr)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(c => c.NameEn)
                .HasMaxLength(200);

            builder.Property(c => c.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(c => c.CreatedAt).IsRequired();
            builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(c => c.ModifiedBy).HasMaxLength(100);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(c => c.Code)
                .IsUnique()
                .HasDatabaseName("IX_Companies_Code");
        }
    }
}
