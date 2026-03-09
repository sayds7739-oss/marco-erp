using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for the Feature entity.
    /// Phase 2: Feature Governance Engine.
    /// </summary>
    public sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature>
    {
        public void Configure(EntityTypeBuilder<Feature> builder)
        {
            builder.ToTable("Features");

            builder.HasKey(f => f.Id);
            builder.Property(f => f.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(f => f.FeatureKey)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(f => f.NameAr)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(f => f.NameEn)
                .HasMaxLength(200);

            builder.Property(f => f.Description)
                .HasMaxLength(500);

            builder.Property(f => f.IsEnabled)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(f => f.RiskLevel)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Medium");

            builder.Property(f => f.DependsOn)
                .HasMaxLength(500);

            // ── Impact Analysis (Phase 4) ───────────────────────
            builder.Property(f => f.AffectsData)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(f => f.RequiresMigration)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(f => f.AffectsAccounting)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(f => f.AffectsInventory)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(f => f.AffectsReporting)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(f => f.ImpactDescription)
                .HasMaxLength(1000);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(f => f.CreatedAt).IsRequired();
            builder.Property(f => f.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(f => f.ModifiedBy).HasMaxLength(100);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(f => f.FeatureKey)
                .IsUnique()
                .HasDatabaseName("IX_Features_FeatureKey");
        }
    }
}
