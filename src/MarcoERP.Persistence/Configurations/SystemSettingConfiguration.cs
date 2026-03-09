using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
    {
        public void Configure(EntityTypeBuilder<SystemSetting> builder)
        {
            builder.ToTable("SystemSettings");

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(s => s.SettingKey)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(s => s.SettingValue)
                .HasMaxLength(500);

            builder.Property(s => s.Description)
                .HasMaxLength(300);

            builder.Property(s => s.GroupName)
                .HasMaxLength(100);

            builder.Property(s => s.DataType)
                .HasMaxLength(20)
                .HasDefaultValue("string");

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(s => s.SettingKey)
                .IsUnique()
                .HasDatabaseName("IX_SystemSettings_SettingKey");
        }
    }
}
