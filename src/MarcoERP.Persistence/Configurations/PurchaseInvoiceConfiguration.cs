using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Purchases;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
    {
        public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
        {
            builder.ToTable("PurchaseInvoices");

            builder.HasKey(pi => pi.Id);
            builder.Property(pi => pi.Id).UseIdentityColumn();

            builder.Property(pi => pi.RowVersion).IsRowVersion().IsConcurrencyToken();

            builder.Property(pi => pi.InvoiceNumber).IsRequired().HasMaxLength(30).IsUnicode(false);
            builder.Property(pi => pi.InvoiceDate).IsRequired().HasColumnType("date");
            builder.Property(pi => pi.Status).IsRequired().HasConversion<int>();
            builder.Property(pi => pi.Subtotal).IsRequired().HasPrecision(18, 4);
            builder.Property(pi => pi.DiscountTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(pi => pi.VatTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(pi => pi.NetTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(pi => pi.PaidAmount).IsRequired().HasPrecision(18, 4).HasDefaultValue(0m);
            builder.Property(pi => pi.PaymentStatus).IsRequired().HasConversion<int>();
            builder.Ignore(pi => pi.BalanceDue);
            builder.Property(pi => pi.Notes).HasMaxLength(1000);

            // Audit
            builder.Property(pi => pi.CreatedAt).IsRequired();
            builder.Property(pi => pi.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(pi => pi.ModifiedBy).HasMaxLength(100);

            // Soft Delete Fields
            builder.Property(pi => pi.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(pi => pi.DeletedAt);

            builder.Property(pi => pi.DeletedBy)
                .HasMaxLength(100);

            // Relationships
            builder.HasOne(pi => pi.Supplier).WithMany()
                .HasForeignKey(pi => pi.SupplierId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne<Warehouse>().WithMany()
                .HasForeignKey(pi => pi.WarehouseId).OnDelete(DeleteBehavior.Restrict);

            // Counterparty type
            builder.Property(pi => pi.CounterpartyType).IsRequired().HasConversion<int>()
                .HasDefaultValue(Domain.Enums.CounterpartyType.Supplier);

            // Counterparty Customer (optional — when buying from a customer)
            builder.HasOne(pi => pi.CounterpartyCustomer).WithMany()
                .HasForeignKey(pi => pi.CounterpartyCustomerId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne(pi => pi.SalesRepresentative).WithMany()
                .HasForeignKey(pi => pi.SalesRepresentativeId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne<MarcoERP.Domain.Entities.Accounting.JournalEntry>().WithMany()
                .HasForeignKey(pi => pi.JournalEntryId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasMany(pi => pi.Lines).WithOne()
                .HasForeignKey(l => l.PurchaseInvoiceId).OnDelete(DeleteBehavior.Restrict);

            // Indexes
            // Unique index with filter: allows reusing invoice numbers for soft-deleted records
            builder.HasIndex(pi => pi.InvoiceNumber).IsUnique()
                .HasDatabaseName("IX_PurchaseInvoices_InvoiceNumber")
                .HasFilter("[IsDeleted] = 0");
            builder.HasIndex(pi => pi.InvoiceDate)
                .HasDatabaseName("IX_PurchaseInvoices_InvoiceDate");
            builder.HasIndex(pi => pi.SupplierId)
                .HasDatabaseName("IX_PurchaseInvoices_SupplierId")
                .HasFilter("[SupplierId] IS NOT NULL");
            builder.HasIndex(pi => pi.WarehouseId)
                .HasDatabaseName("IX_PurchaseInvoices_WarehouseId");
            builder.HasIndex(pi => pi.Status)
                .HasDatabaseName("IX_PurchaseInvoices_Status");
            builder.HasIndex(pi => pi.JournalEntryId)
                .HasDatabaseName("IX_PurchaseInvoices_JournalEntryId")
                .HasFilter("[JournalEntryId] IS NOT NULL");
            builder.HasIndex(pi => pi.CounterpartyCustomerId)
                .HasDatabaseName("IX_PurchaseInvoices_CounterpartyCustomerId")
                .HasFilter("[CounterpartyCustomerId] IS NOT NULL");
            builder.HasIndex(pi => pi.SalesRepresentativeId)
                .HasDatabaseName("IX_PurchaseInvoices_SalesRepresentativeId")
                .HasFilter("[SalesRepresentativeId] IS NOT NULL");
        }
    }
}
