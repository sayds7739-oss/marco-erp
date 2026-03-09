using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class SalesQuotationConfiguration : IEntityTypeConfiguration<SalesQuotation>
    {
        public void Configure(EntityTypeBuilder<SalesQuotation> builder)
        {
            builder.ToTable("SalesQuotations");

            builder.HasKey(q => q.Id);
            builder.Property(q => q.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(q => q.QuotationNumber).IsRequired().HasMaxLength(30).IsUnicode(false);
            builder.Property(q => q.QuotationDate).IsRequired().HasColumnType("date");
            builder.Property(q => q.ValidUntil).IsRequired().HasColumnType("date");
            builder.Property(q => q.Status).IsRequired().HasConversion<int>();
            builder.Property(q => q.Subtotal).IsRequired().HasPrecision(18, 4);
            builder.Property(q => q.DiscountTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(q => q.VatTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(q => q.NetTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(q => q.Notes).HasMaxLength(1000);

            // Converted invoice tracking
            builder.Property(q => q.ConvertedToInvoiceId);
            builder.Property(q => q.ConvertedDate);

            // Audit
            builder.Property(q => q.CreatedAt).IsRequired();
            builder.Property(q => q.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(q => q.ModifiedBy).HasMaxLength(100);

            // Soft Delete Fields
            builder.Property(q => q.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(q => q.DeletedAt);

            builder.Property(q => q.DeletedBy)
                .HasMaxLength(100);

            // Relationships
            builder.HasOne(q => q.Customer).WithMany()
                .HasForeignKey(q => q.CustomerId).OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Warehouse>().WithMany()
                .HasForeignKey(q => q.WarehouseId).OnDelete(DeleteBehavior.Restrict);

            // Sales Representative (optional)
            builder.HasOne(q => q.SalesRepresentative).WithMany()
                .HasForeignKey(q => q.SalesRepresentativeId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Converted invoice (optional)
            builder.HasOne<SalesInvoice>().WithMany()
                .HasForeignKey(q => q.ConvertedToInvoiceId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasMany(q => q.Lines).WithOne()
                .HasForeignKey(l => l.SalesQuotationId).OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(q => new { q.CompanyId, q.QuotationNumber })
                .IsUnique()
                .HasFilter(DbProviderHelper.SoftDeleteFilter())
                .HasDatabaseName("IX_SalesQuotations_CompanyId_QuotationNumber");
            builder.HasIndex(q => q.QuotationDate)
                .HasDatabaseName("IX_SalesQuotations_QuotationDate");
            builder.HasIndex(q => q.CustomerId)
                .HasDatabaseName("IX_SalesQuotations_CustomerId");
            builder.HasIndex(q => q.WarehouseId)
                .HasDatabaseName("IX_SalesQuotations_WarehouseId");
            builder.HasIndex(q => q.Status)
                .HasDatabaseName("IX_SalesQuotations_Status");
            builder.HasIndex(q => q.SalesRepresentativeId)
                .HasDatabaseName("IX_SalesQuotations_SalesRepresentativeId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("SalesRepresentativeId"));
            builder.HasIndex(q => q.ConvertedToInvoiceId)
                .HasDatabaseName("IX_SalesQuotations_ConvertedToInvoiceId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("ConvertedToInvoiceId"));
        }
    }
}
