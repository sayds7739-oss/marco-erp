using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Purchases;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class PurchaseReturnConfiguration : IEntityTypeConfiguration<PurchaseReturn>
    {
        public void Configure(EntityTypeBuilder<PurchaseReturn> builder)
        {
            builder.ToTable("PurchaseReturns");

            builder.HasKey(pr => pr.Id);
            builder.Property(pr => pr.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(pr => pr.ReturnNumber).IsRequired().HasMaxLength(30).IsUnicode(false);
            builder.Property(pr => pr.ReturnDate).IsRequired().HasColumnType("date");
            builder.Property(pr => pr.Status).IsRequired().HasConversion<int>();
            builder.Property(pr => pr.Subtotal).IsRequired().HasPrecision(18, 4);
            builder.Property(pr => pr.DiscountTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(pr => pr.VatTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(pr => pr.NetTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(pr => pr.DeliveryFee).IsRequired().HasPrecision(18, 4).HasDefaultValue(0m);
            builder.Property(pr => pr.Notes).HasMaxLength(1000);

            // Counterparty type
            builder.Property(pr => pr.CounterpartyType).IsRequired().HasConversion<int>()
                .HasDefaultValue(Domain.Enums.CounterpartyType.Supplier);

            // Audit
            builder.Property(pr => pr.CreatedAt).IsRequired();
            builder.Property(pr => pr.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(pr => pr.ModifiedBy).HasMaxLength(100);

            // Soft Delete Fields
            builder.Property(pr => pr.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(pr => pr.DeletedAt);

            builder.Property(pr => pr.DeletedBy)
                .HasMaxLength(100);

            // Relationships
            builder.HasOne(pr => pr.Supplier).WithMany()
                .HasForeignKey(pr => pr.SupplierId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne(pr => pr.CounterpartyCustomer).WithMany()
                .HasForeignKey(pr => pr.CounterpartyCustomerId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne(pr => pr.Warehouse).WithMany()
                .HasForeignKey(pr => pr.WarehouseId).OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(pr => pr.OriginalInvoice).WithMany()
                .HasForeignKey(pr => pr.OriginalInvoiceId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne(pr => pr.SalesRepresentative).WithMany()
                .HasForeignKey(pr => pr.SalesRepresentativeId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne<MarcoERP.Domain.Entities.Accounting.JournalEntry>().WithMany()
                .HasForeignKey(pr => pr.JournalEntryId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasMany(pr => pr.Lines).WithOne()
                .HasForeignKey(l => l.PurchaseReturnId).OnDelete(DeleteBehavior.Restrict);

            // Indexes
            builder.HasIndex(pr => new { pr.CompanyId, pr.ReturnNumber }).IsUnique()
                .HasDatabaseName("IX_PurchaseReturns_Company_ReturnNumber")
                .HasFilter(DbProviderHelper.SoftDeleteFilter());
            builder.HasIndex(pr => pr.ReturnDate)
                .HasDatabaseName("IX_PurchaseReturns_ReturnDate");
            builder.HasIndex(pr => pr.SupplierId)
                .HasDatabaseName("IX_PurchaseReturns_SupplierId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("SupplierId"));
            builder.HasIndex(pr => pr.CounterpartyCustomerId)
                .HasDatabaseName("IX_PurchaseReturns_CounterpartyCustomerId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("CounterpartyCustomerId"));
            builder.HasIndex(pr => pr.WarehouseId)
                .HasDatabaseName("IX_PurchaseReturns_WarehouseId");
            builder.HasIndex(pr => pr.OriginalInvoiceId)
                .HasDatabaseName("IX_PurchaseReturns_OriginalInvoiceId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("OriginalInvoiceId"));
            builder.HasIndex(pr => pr.Status)
                .HasDatabaseName("IX_PurchaseReturns_Status");
            builder.HasIndex(pr => pr.JournalEntryId)
                .HasDatabaseName("IX_PurchaseReturns_JournalEntryId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("JournalEntryId"));
            builder.HasIndex(pr => pr.SalesRepresentativeId)
                .HasDatabaseName("IX_PurchaseReturns_SalesRepresentativeId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("SalesRepresentativeId"));
        }
    }
}
