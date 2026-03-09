using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for the FeatureVersion entity.
    /// Phase 5: Version &amp; Integrity Engine.
    /// </summary>
    public sealed class FeatureVersionConfiguration : IEntityTypeConfiguration<FeatureVersion>
    {
        public void Configure(EntityTypeBuilder<FeatureVersion> builder)
        {
            builder.ToTable("FeatureVersions");

            builder.HasKey(fv => fv.Id);
            builder.Property(fv => fv.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(fv => fv.FeatureKey)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(fv => fv.IntroducedInVersion)
                .IsRequired()
                .HasMaxLength(20);

            builder.HasIndex(fv => fv.FeatureKey)
                .IsUnique()
                .HasDatabaseName("IX_FeatureVersions_FeatureKey");
        }
    }
}
