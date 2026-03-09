using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.ToTable("Categories");

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(c => c.NameAr)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(c => c.NameEn)
                .HasMaxLength(100);

            builder.Property(c => c.Level)
                .IsRequired();

            builder.Property(c => c.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(c => c.Description)
                .HasMaxLength(500);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(c => c.CreatedAt).IsRequired();
            builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(c => c.ModifiedBy).HasMaxLength(100);

            // ── Self-referencing Relationship ────────────────────
            builder.HasOne(c => c.ParentCategory)
                .WithMany(c => c.Children)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(c => new { c.NameAr, c.ParentCategoryId })
                .IsUnique()
                .HasDatabaseName("IX_Categories_NameAr_Parent");
        }
    }
}
