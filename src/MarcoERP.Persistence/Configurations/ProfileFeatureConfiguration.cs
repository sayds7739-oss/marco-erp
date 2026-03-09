using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for the ProfileFeature entity.
    /// Phase 3: Progressive Complexity Layer.
    /// </summary>
    public sealed class ProfileFeatureConfiguration : IEntityTypeConfiguration<ProfileFeature>
    {
        public void Configure(EntityTypeBuilder<ProfileFeature> builder)
        {
            builder.ToTable("ProfileFeatures");

            builder.HasKey(pf => pf.Id);
            builder.Property(pf => pf.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(pf => pf.ProfileId)
                .IsRequired();

            builder.Property(pf => pf.FeatureKey)
                .IsRequired()
                .HasMaxLength(100);

            // ── Relationships ───────────────────────────────────
            builder.HasOne(pf => pf.Profile)
                .WithMany()
                .HasForeignKey(pf => pf.ProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(pf => new { pf.ProfileId, pf.FeatureKey })
                .IsUnique()
                .HasDatabaseName("IX_ProfileFeatures_ProfileId_FeatureKey");
        }
    }
}
