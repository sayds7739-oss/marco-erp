using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Sync;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
    {
        public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
        {
            builder.ToTable("IdempotencyRecords");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).UseIdentityColumn();

            builder.Property(r => r.IdempotencyKey)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(r => r.RequestPath)
                .HasMaxLength(500);

            builder.Property(r => r.RequestBody);

            builder.Property(r => r.ResponseStatusCode);

            builder.Property(r => r.ResponseBody);

            builder.Property(r => r.UserId);

            builder.Property(r => r.CreatedAt)
                .IsRequired();

            builder.Property(r => r.ExpiresAt)
                .IsRequired();

            // Unique index on IdempotencyKey + UserId for user-scoped lookups
            builder.HasIndex(r => new { r.IdempotencyKey, r.UserId })
                .IsUnique()
                .HasDatabaseName("IX_IdempotencyRecords_Key_UserId");

            // Index on ExpiresAt for cleanup jobs
            builder.HasIndex(r => r.ExpiresAt)
                .HasDatabaseName("IX_IdempotencyRecords_ExpiresAt");
        }
    }
}
