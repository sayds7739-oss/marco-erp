using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class BackupHistoryConfiguration : IEntityTypeConfiguration<BackupHistory>
    {
        public void Configure(EntityTypeBuilder<BackupHistory> builder)
        {
            builder.ToTable("BackupHistory");

            builder.HasKey(b => b.Id);
            builder.Property(b => b.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(b => b.FilePath)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(b => b.FileSizeBytes)
                .IsRequired();

            builder.Property(b => b.BackupDate)
                .IsRequired();

            builder.Property(b => b.PerformedBy)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(b => b.BackupType)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(b => b.IsSuccessful)
                .IsRequired();

            builder.Property(b => b.ErrorMessage)
                .HasMaxLength(2000);

            builder.Property(b => b.CreatedAt)
                .IsRequired();

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(b => b.BackupDate)
                .HasDatabaseName("IX_BackupHistory_BackupDate");
        }
    }
}
