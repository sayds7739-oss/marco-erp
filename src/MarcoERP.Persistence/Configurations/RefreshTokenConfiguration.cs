using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Security;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("RefreshTokens");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).UseIdentityColumn();

            builder.Property(t => t.Token).IsRequired().HasMaxLength(256);
            builder.HasIndex(t => t.Token).IsUnique();

            builder.Property(t => t.UserId).IsRequired();
            builder.Property(t => t.CreatedAt).IsRequired();
            builder.Property(t => t.ExpiresAt).IsRequired();
            builder.Property(t => t.IsRevoked).IsRequired().HasDefaultValue(false);

            builder.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for cleanup queries
            builder.HasIndex(t => t.ExpiresAt);
            builder.HasIndex(t => t.UserId);
        }
    }
}
