using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for the FeatureChangeLog entity.
    /// Phase 2: Feature Governance Engine.
    /// </summary>
    public sealed class FeatureChangeLogConfiguration : IEntityTypeConfiguration<FeatureChangeLog>
    {
        public void Configure(EntityTypeBuilder<FeatureChangeLog> builder)
        {
            builder.ToTable("FeatureChangeLogs");

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(c => c.FeatureId)
                .IsRequired();

            builder.Property(c => c.FeatureKey)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(c => c.OldValue)
                .IsRequired();

            builder.Property(c => c.NewValue)
                .IsRequired();

            builder.Property(c => c.ChangedBy)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(c => c.ChangedAt)
                .IsRequired();

            // ── Relationships ───────────────────────────────────
            builder.HasOne(c => c.Feature)
                .WithMany()
                .HasForeignKey(c => c.FeatureId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(c => c.FeatureId)
                .HasDatabaseName("IX_FeatureChangeLogs_FeatureId");

            builder.HasIndex(c => c.ChangedAt)
                .HasDatabaseName("IX_FeatureChangeLogs_ChangedAt");
        }
    }
}
