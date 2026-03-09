using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
    {
        public void Configure(EntityTypeBuilder<PriceList> builder)
        {
            builder.ToTable("PriceLists");

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).UseIdentityColumn();
            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(p => p.Code).IsRequired().HasMaxLength(20);
            builder.Property(p => p.NameAr).IsRequired().HasMaxLength(200);
            builder.Property(p => p.NameEn).HasMaxLength(200);
            builder.Property(p => p.Description).HasMaxLength(1000);
            builder.Property(p => p.ValidFrom).HasColumnType("date");
            builder.Property(p => p.ValidTo).HasColumnType("date");
            builder.Property(p => p.IsActive).IsRequired().HasDefaultValue(true);

            // Soft Delete
            builder.Property(p => p.IsDeleted).IsRequired().HasDefaultValue(false);
            builder.Property(p => p.DeletedBy).HasMaxLength(100);

            // Audit
            builder.Property(p => p.CreatedAt).IsRequired();
            builder.Property(p => p.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(p => p.ModifiedBy).HasMaxLength(100);

            // Relationships
            builder.HasMany(p => p.Tiers).WithOne(t => t.PriceList)
                .HasForeignKey(t => t.PriceListId).OnDelete(DeleteBehavior.Restrict);

            // Indexes
            builder.HasIndex(p => p.Code).IsUnique().HasDatabaseName("IX_PriceLists_Code");
            builder.HasIndex(p => p.IsActive).HasDatabaseName("IX_PriceLists_IsActive");
        }
    }
}
