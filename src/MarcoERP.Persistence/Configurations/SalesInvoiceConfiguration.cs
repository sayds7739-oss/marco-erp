using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
    {
        public void Configure(EntityTypeBuilder<SalesInvoice> builder)
        {
            builder.ToTable("SalesInvoices");

            builder.HasKey(si => si.Id);
            builder.Property(si => si.Id).UseIdentityColumn();

            builder.Property(si => si.RowVersion).IsRowVersion().IsConcurrencyToken();

            builder.Property(si => si.InvoiceNumber).IsRequired().HasMaxLength(30).IsUnicode(false);
            builder.Property(si => si.InvoiceDate).IsRequired().HasColumnType("date");
            builder.Property(si => si.Status).IsRequired().HasConversion<int>();
            builder.Property(si => si.Subtotal).IsRequired().HasPrecision(18, 4);
            builder.Property(si => si.DiscountTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(si => si.VatTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(si => si.NetTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(si => si.PaidAmount).IsRequired().HasPrecision(18, 4).HasDefaultValue(0m);
            builder.Property(si => si.PaymentStatus).IsRequired().HasConversion<int>();
            builder.Ignore(si => si.BalanceDue);
            builder.Property(si => si.Notes).HasMaxLength(1000);

            // Audit
            builder.Property(si => si.CreatedAt).IsRequired();
            builder.Property(si => si.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(si => si.ModifiedBy).HasMaxLength(100);

            // Soft Delete Fields
            builder.Property(si => si.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(si => si.DeletedAt);

            builder.Property(si => si.DeletedBy)
                .HasMaxLength(100);

            // Relationships
            builder.HasOne(si => si.Customer).WithMany()
                .HasForeignKey(si => si.CustomerId).OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Warehouse>().WithMany()
                .HasForeignKey(si => si.WarehouseId).OnDelete(DeleteBehavior.Restrict);

            // Counterparty type
            builder.Property(si => si.CounterpartyType).IsRequired().HasConversion<int>()
                .HasDefaultValue(Domain.Enums.CounterpartyType.Customer);

            // Counterparty Supplier (optional — when selling to a supplier)
            builder.HasOne(si => si.CounterpartySupplier).WithMany()
                .HasForeignKey(si => si.SupplierId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Revenue journal entry
            builder.HasOne<MarcoERP.Domain.Entities.Accounting.JournalEntry>().WithMany()
                .HasForeignKey(si => si.JournalEntryId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // COGS journal entry
            builder.HasOne<MarcoERP.Domain.Entities.Accounting.JournalEntry>().WithMany()
                .HasForeignKey(si => si.CogsJournalEntryId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasMany(si => si.Lines).WithOne()
                .HasForeignKey(l => l.SalesInvoiceId).OnDelete(DeleteBehavior.Restrict);

            // Sales Representative (optional)
            builder.HasOne(si => si.SalesRepresentative).WithMany()
                .HasForeignKey(si => si.SalesRepresentativeId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Indexes
            // Unique index with filter: allows reusing invoice numbers for soft-deleted records
            builder.HasIndex(si => si.InvoiceNumber).IsUnique()
                .HasDatabaseName("IX_SalesInvoices_InvoiceNumber")
                .HasFilter("[IsDeleted] = 0");
            builder.HasIndex(si => si.InvoiceDate)
                .HasDatabaseName("IX_SalesInvoices_InvoiceDate");
            builder.HasIndex(si => si.CustomerId)
                .HasDatabaseName("IX_SalesInvoices_CustomerId");
            builder.HasIndex(si => si.WarehouseId)
                .HasDatabaseName("IX_SalesInvoices_WarehouseId");
            builder.HasIndex(si => si.Status)
                .HasDatabaseName("IX_SalesInvoices_Status");
            builder.HasIndex(si => si.JournalEntryId)
                .HasDatabaseName("IX_SalesInvoices_JournalEntryId")
                .HasFilter("[JournalEntryId] IS NOT NULL");
            builder.HasIndex(si => si.CogsJournalEntryId)
                .HasDatabaseName("IX_SalesInvoices_CogsJournalEntryId")
                .HasFilter("[CogsJournalEntryId] IS NOT NULL");
            builder.HasIndex(si => si.SalesRepresentativeId)
                .HasDatabaseName("IX_SalesInvoices_SalesRepresentativeId")
                .HasFilter("[SalesRepresentativeId] IS NOT NULL");
            builder.HasIndex(si => si.SupplierId)
                .HasDatabaseName("IX_SalesInvoices_SupplierId")
                .HasFilter("[SupplierId] IS NOT NULL");
        }
    }
}
