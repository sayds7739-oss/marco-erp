using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for the MigrationExecution entity.
    /// Phase 6: Controlled Migration Engine.
    /// </summary>
    public sealed class MigrationExecutionConfiguration : IEntityTypeConfiguration<MigrationExecution>
    {
        public void Configure(EntityTypeBuilder<MigrationExecution> builder)
        {
            builder.ToTable("MigrationExecutions");

            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(m => m.MigrationName)
                .IsRequired()
                .HasMaxLength(300);

            builder.Property(m => m.StartedAt)
                .IsRequired();

            builder.Property(m => m.ExecutedBy)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(m => m.ErrorMessage)
                .HasMaxLength(2000);

            builder.Property(m => m.BackupPath)
                .HasMaxLength(500);

            builder.HasIndex(m => m.MigrationName)
                .HasDatabaseName("IX_MigrationExecutions_Name");

            builder.HasIndex(m => m.StartedAt)
                .HasDatabaseName("IX_MigrationExecutions_StartedAt");
        }
    }
}
