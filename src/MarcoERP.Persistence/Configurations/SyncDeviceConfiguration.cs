using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Sync;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class SyncDeviceConfiguration : IEntityTypeConfiguration<SyncDevice>
    {
        public void Configure(EntityTypeBuilder<SyncDevice> builder)
        {
            builder.ToTable("SyncDevices");

            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).UseIdentityColumn();

            builder.Property(d => d.DeviceId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(d => d.DeviceName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(d => d.DeviceType)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(d => d.UserId)
                .IsRequired();

            builder.Property(d => d.LastSyncVersion)
                .IsRequired()
                .HasDefaultValue(0L);

            builder.Property(d => d.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Unique index on DeviceId — one registration per physical device
            builder.HasIndex(d => d.DeviceId)
                .IsUnique()
                .HasDatabaseName("IX_SyncDevices_DeviceId");

            // Index for querying by user
            builder.HasIndex(d => d.UserId)
                .HasDatabaseName("IX_SyncDevices_UserId");
        }
    }
}
