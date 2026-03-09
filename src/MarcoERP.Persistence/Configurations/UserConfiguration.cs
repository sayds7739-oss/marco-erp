using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Security;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(u => u.FullNameAr)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(u => u.FullNameEn)
                .HasMaxLength(100);

            builder.Property(u => u.Email)
                .HasMaxLength(200);

            builder.Property(u => u.Phone)
                .HasMaxLength(20);

            builder.Property(u => u.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(u => u.IsLocked)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(u => u.LockedAt);

            builder.Property(u => u.FailedLoginAttempts)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(u => u.MustChangePassword)
                .IsRequired()
                .HasDefaultValue(true);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(u => u.CreatedAt).IsRequired();
            builder.Property(u => u.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(u => u.ModifiedBy).HasMaxLength(100);

            // ── Relationships ───────────────────────────────────
            builder.HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(u => u.Username)
                .IsUnique()
                .HasDatabaseName("IX_Users_Username");

            builder.HasIndex(u => u.RoleId)
                .HasDatabaseName("IX_Users_RoleId");
        }
    }
}
