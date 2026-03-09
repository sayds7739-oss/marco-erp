using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Common;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
    {
        public void Configure(EntityTypeBuilder<Attachment> builder)
        {
            builder.ToTable("Attachments");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            // -- Business Fields --
            builder.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
            builder.Property(a => a.EntityId).IsRequired();
            builder.Property(a => a.FileName).IsRequired().HasMaxLength(500);
            builder.Property(a => a.ContentType).HasMaxLength(200);
            builder.Property(a => a.FileSize).IsRequired();
            builder.Property(a => a.StoragePath).IsRequired().HasMaxLength(1000);
            builder.Property(a => a.UploadedBy).HasMaxLength(100);
            builder.Property(a => a.UploadedAt).IsRequired();

            // -- Audit Fields --
            builder.Property(a => a.CreatedAt).IsRequired();
            builder.Property(a => a.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(a => a.ModifiedBy).HasMaxLength(100);

            // -- Indexes --
            builder.HasIndex(a => new { a.EntityType, a.EntityId })
                .HasDatabaseName("IX_Attachments_Entity");
        }
    }
}
