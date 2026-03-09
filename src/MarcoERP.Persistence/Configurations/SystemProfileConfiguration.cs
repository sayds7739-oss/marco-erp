using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for the SystemProfile entity.
    /// Phase 3: Progressive Complexity Layer.
    /// </summary>
    public sealed class SystemProfileConfiguration : IEntityTypeConfiguration<SystemProfile>
    {
        public void Configure(EntityTypeBuilder<SystemProfile> builder)
        {
            builder.ToTable("SystemProfiles");

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(p => p.ProfileName)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(p => p.Description)
                .HasMaxLength(500);

            builder.Property(p => p.IsActive)
                .IsRequired()
                .HasDefaultValue(false);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(p => p.CreatedAt).IsRequired();
            builder.Property(p => p.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(p => p.ModifiedBy).HasMaxLength(100);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(p => p.ProfileName)
                .IsUnique()
                .HasDatabaseName("IX_SystemProfiles_ProfileName");
        }
    }
}
