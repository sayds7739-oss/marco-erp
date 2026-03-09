using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Security;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.ToTable("Roles");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(r => r.NameAr)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(r => r.NameEn)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(r => r.Description)
                .HasMaxLength(200);

            builder.Property(r => r.IsSystem)
                .IsRequired()
                .HasDefaultValue(false);

            // ── Navigation ──────────────────────────────────────
            builder.HasMany(r => r.Permissions)
                .WithOne(rp => rp.Role)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(r => r.Users)
                .WithOne(u => u.Role)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(r => r.NameEn)
                .IsUnique()
                .HasDatabaseName("IX_Roles_NameEn");
        }
    }
}
