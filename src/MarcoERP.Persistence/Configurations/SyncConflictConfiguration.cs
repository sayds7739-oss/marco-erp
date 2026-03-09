using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Sync;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class SyncConflictConfiguration : IEntityTypeConfiguration<SyncConflict>
    {
        public void Configure(EntityTypeBuilder<SyncConflict> builder)
        {
            builder.ToTable("SyncConflicts");

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).UseIdentityColumn();

            builder.Property(c => c.EntityType)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(c => c.EntityId)
                .IsRequired();

            builder.Property(c => c.DeviceId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(c => c.ClientData)
                .IsRequired();

            builder.Property(c => c.ServerData)
                .IsRequired();

            builder.Property(c => c.Resolution)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(c => c.OccurredAt)
                .IsRequired();

            // Index for querying conflicts by entity
            builder.HasIndex(c => new { c.EntityType, c.EntityId })
                .HasDatabaseName("IX_SyncConflicts_Entity");

            // Index for querying conflicts by device
            builder.HasIndex(c => c.DeviceId)
                .HasDatabaseName("IX_SyncConflicts_DeviceId");
        }
    }
}
