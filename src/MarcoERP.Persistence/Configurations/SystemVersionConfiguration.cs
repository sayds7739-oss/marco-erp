using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for the SystemVersion entity.
    /// Phase 5: Version &amp; Integrity Engine.
    /// </summary>
    public sealed class SystemVersionConfiguration : IEntityTypeConfiguration<SystemVersion>
    {
        public void Configure(EntityTypeBuilder<SystemVersion> builder)
        {
            builder.ToTable("SystemVersions");

            builder.HasKey(v => v.Id);
            builder.Property(v => v.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(v => v.VersionNumber)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(v => v.AppliedAt)
                .IsRequired();

            builder.Property(v => v.AppliedBy)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(v => v.Description)
                .HasMaxLength(500);

            builder.HasIndex(v => v.VersionNumber)
                .IsUnique()
                .HasDatabaseName("IX_SystemVersions_VersionNumber");
        }
    }
}
