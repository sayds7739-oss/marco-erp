using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class SalesInvoiceLineConfiguration : IEntityTypeConfiguration<SalesInvoiceLine>
    {
        public void Configure(EntityTypeBuilder<SalesInvoiceLine> builder)
        {
            builder.ToTable("SalesInvoiceLines", t =>
            {
                // FIN-CK-04: Quantity must be positive
                t.HasCheckConstraint("CK_SalesInvoiceLines_Quantity",
                    DbProviderHelper.CheckExpr("{0} > 0", "Quantity"));

                // FIN-CK-05: UnitPrice must be non-negative
                t.HasCheckConstraint("CK_SalesInvoiceLines_UnitPrice",
                    DbProviderHelper.CheckExpr("{0} >= 0", "UnitPrice"));
            });

            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(l => l.SalesInvoiceId).IsRequired();
            builder.Property(l => l.ProductId).IsRequired();
            builder.Property(l => l.UnitId).IsRequired();
            builder.Property(l => l.Quantity).IsRequired().HasPrecision(18, 4);
            builder.Property(l => l.UnitPrice).IsRequired().HasPrecision(18, 4);
            builder.Property(l => l.ConversionFactor).IsRequired().HasPrecision(18, 6);
            builder.Property(l => l.BaseQuantity).IsRequired().HasPrecision(18, 4);
            builder.Property(l => l.DiscountPercent).IsRequired().HasPrecision(5, 2);
            builder.Property(l => l.DiscountAmount).IsRequired().HasPrecision(18, 4);
            builder.Property(l => l.SubTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(l => l.NetTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(l => l.VatRate).IsRequired().HasPrecision(5, 2);
            builder.Property(l => l.VatAmount).IsRequired().HasPrecision(18, 4);
            builder.Property(l => l.TotalWithVat).IsRequired().HasPrecision(18, 4);

            // Relationships
            builder.HasOne(l => l.Product).WithMany()
                .HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(l => l.Unit).WithMany()
                .HasForeignKey(l => l.UnitId).OnDelete(DeleteBehavior.Restrict);

            // Indexes
            builder.HasIndex(l => l.SalesInvoiceId)
                .HasDatabaseName("IX_SalesInvoiceLines_InvoiceId");
            builder.HasIndex(l => l.ProductId)
                .HasDatabaseName("IX_SalesInvoiceLines_ProductId");
        }
    }
}
