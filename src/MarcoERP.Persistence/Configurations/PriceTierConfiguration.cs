using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class PriceTierConfiguration : IEntityTypeConfiguration<PriceTier>
    {
        public void Configure(EntityTypeBuilder<PriceTier> builder)
        {
            builder.ToTable("PriceTiers");

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).UseIdentityColumn();
            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(t => t.PriceListId).IsRequired();
            builder.Property(t => t.ProductId).IsRequired();
            builder.Property(t => t.MinimumQuantity).IsRequired().HasPrecision(18, 4);
            builder.Property(t => t.Price).IsRequired().HasPrecision(18, 4);

            // Relationships
            builder.HasOne(t => t.Product).WithMany()
                .HasForeignKey(t => t.ProductId).OnDelete(DeleteBehavior.Restrict);

            // Indexes
            builder.HasIndex(t => t.PriceListId).HasDatabaseName("IX_PriceTiers_PriceListId");
            builder.HasIndex(t => t.ProductId).HasDatabaseName("IX_PriceTiers_ProductId");
            builder.HasIndex(t => new { t.PriceListId, t.ProductId, t.MinimumQuantity })
                .IsUnique().HasDatabaseName("IX_PriceTiers_List_Product_MinQty");
        }
    }
}
