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
            builder.ToTable("SalesInvoices", t =>
            {
                // FIN-CK-03: Paid amount cannot exceed net total
                t.HasCheckConstraint("CK_SalesInvoices_PaidAmount",
                    DbProviderHelper.CheckExpr("{0} >= 0 AND {0} <= {1}", "PaidAmount", "NetTotal"));
            });

            builder.HasKey(si => si.Id);
            builder.Property(si => si.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(si => si.InvoiceNumber).IsRequired().HasMaxLength(30).IsUnicode(false);
            builder.Property(si => si.InvoiceDate).IsRequired().HasColumnType("date");
            builder.Property(si => si.Status).IsRequired().HasConversion<int>();
            builder.Property(si => si.InvoiceType).IsRequired().HasConversion<int>().HasDefaultValue(Domain.Enums.InvoiceType.Cash);
            builder.Property(si => si.PaymentMethod).IsRequired().HasConversion<int>().HasDefaultValue(Domain.Enums.PaymentMethod.Cash);
            builder.Property(si => si.DueDate).HasColumnType("date");
            builder.Property(si => si.Subtotal).IsRequired().HasPrecision(18, 4);
            builder.Property(si => si.DiscountTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(si => si.VatTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(si => si.NetTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(si => si.HeaderDiscountPercent).IsRequired().HasPrecision(18, 4).HasDefaultValue(0m);
            builder.Property(si => si.HeaderDiscountAmount).IsRequired().HasPrecision(18, 4).HasDefaultValue(0m);
            builder.Property(si => si.DeliveryFee).IsRequired().HasPrecision(18, 4).HasDefaultValue(0m);
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
                .HasForeignKey(si => si.CustomerId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne(si => si.Warehouse).WithMany()
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

            // Commission journal entry
            builder.HasOne<MarcoERP.Domain.Entities.Accounting.JournalEntry>().WithMany()
                .HasForeignKey(si => si.CommissionJournalEntryId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasMany(si => si.Lines).WithOne()
                .HasForeignKey(l => l.SalesInvoiceId).OnDelete(DeleteBehavior.Restrict);

            // Sales Representative (optional)
            builder.HasOne(si => si.SalesRepresentative).WithMany()
                .HasForeignKey(si => si.SalesRepresentativeId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Indexes
            // Unique index with filter: allows reusing invoice numbers for soft-deleted records
            builder.HasIndex(si => new { si.CompanyId, si.InvoiceNumber }).IsUnique()
                .HasDatabaseName("IX_SalesInvoices_Company_InvoiceNumber")
                .HasFilter(DbProviderHelper.SoftDeleteFilter());
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
                .HasFilter(DbProviderHelper.IsNotNullFilter("JournalEntryId"));
            builder.HasIndex(si => si.CogsJournalEntryId)
                .HasDatabaseName("IX_SalesInvoices_CogsJournalEntryId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("CogsJournalEntryId"));
            builder.HasIndex(si => si.CommissionJournalEntryId)
                .HasDatabaseName("IX_SalesInvoices_CommissionJournalEntryId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("CommissionJournalEntryId"));
            builder.HasIndex(si => si.SalesRepresentativeId)
                .HasDatabaseName("IX_SalesInvoices_SalesRepresentativeId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("SalesRepresentativeId"));
            builder.HasIndex(si => si.SupplierId)
                .HasDatabaseName("IX_SalesInvoices_SupplierId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("SupplierId"));
        }
    }
}
