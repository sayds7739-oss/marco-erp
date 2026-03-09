using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class SalesReturnConfiguration : IEntityTypeConfiguration<SalesReturn>
    {
        public void Configure(EntityTypeBuilder<SalesReturn> builder)
        {
            builder.ToTable("SalesReturns");

            builder.HasKey(sr => sr.Id);
            builder.Property(sr => sr.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(sr => sr.ReturnNumber).IsRequired().HasMaxLength(30).IsUnicode(false);
            builder.Property(sr => sr.ReturnDate).IsRequired().HasColumnType("date");
            builder.Property(sr => sr.Status).IsRequired().HasConversion<int>();
            builder.Property(sr => sr.Subtotal).IsRequired().HasPrecision(18, 4);
            builder.Property(sr => sr.DiscountTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(sr => sr.VatTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(sr => sr.NetTotal).IsRequired().HasPrecision(18, 4);
            builder.Property(sr => sr.DeliveryFee).IsRequired().HasPrecision(18, 4).HasDefaultValue(0m);
            builder.Property(sr => sr.Notes).HasMaxLength(1000);

            // Counterparty type
            builder.Property(sr => sr.CounterpartyType).IsRequired().HasConversion<int>()
                .HasDefaultValue(Domain.Enums.CounterpartyType.Customer);

            // Audit
            builder.Property(sr => sr.CreatedAt).IsRequired();
            builder.Property(sr => sr.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(sr => sr.ModifiedBy).HasMaxLength(100);

            // Soft Delete Fields
            builder.Property(sr => sr.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(sr => sr.DeletedAt);

            builder.Property(sr => sr.DeletedBy)
                .HasMaxLength(100);

            // Relationships
            builder.HasOne(sr => sr.Customer).WithMany()
                .HasForeignKey(sr => sr.CustomerId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne(sr => sr.CounterpartySupplier).WithMany()
                .HasForeignKey(sr => sr.SupplierId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne(sr => sr.Warehouse).WithMany()
                .HasForeignKey(sr => sr.WarehouseId).OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(sr => sr.OriginalInvoice).WithMany()
                .HasForeignKey(sr => sr.OriginalInvoiceId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne(sr => sr.SalesRepresentative).WithMany()
                .HasForeignKey(sr => sr.SalesRepresentativeId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Revenue reversal journal entry
            builder.HasOne<MarcoERP.Domain.Entities.Accounting.JournalEntry>().WithMany()
                .HasForeignKey(sr => sr.JournalEntryId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // COGS reversal journal entry
            builder.HasOne<MarcoERP.Domain.Entities.Accounting.JournalEntry>().WithMany()
                .HasForeignKey(sr => sr.CogsJournalEntryId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasMany(sr => sr.Lines).WithOne()
                .HasForeignKey(l => l.SalesReturnId).OnDelete(DeleteBehavior.Restrict);

            // Indexes
            builder.HasIndex(sr => new { sr.CompanyId, sr.ReturnNumber }).IsUnique()
                .HasDatabaseName("IX_SalesReturns_Company_ReturnNumber")
                .HasFilter(DbProviderHelper.SoftDeleteFilter());
            builder.HasIndex(sr => sr.ReturnDate)
                .HasDatabaseName("IX_SalesReturns_ReturnDate");
            builder.HasIndex(sr => sr.CustomerId)
                .HasDatabaseName("IX_SalesReturns_CustomerId");
            builder.HasIndex(sr => sr.SupplierId)
                .HasDatabaseName("IX_SalesReturns_SupplierId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("SupplierId"));
            builder.HasIndex(sr => sr.WarehouseId)
                .HasDatabaseName("IX_SalesReturns_WarehouseId");
            builder.HasIndex(sr => sr.OriginalInvoiceId)
                .HasDatabaseName("IX_SalesReturns_OriginalInvoiceId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("OriginalInvoiceId"));
            builder.HasIndex(sr => sr.Status)
                .HasDatabaseName("IX_SalesReturns_Status");
            builder.HasIndex(sr => sr.JournalEntryId)
                .HasDatabaseName("IX_SalesReturns_JournalEntryId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("JournalEntryId"));
            builder.HasIndex(sr => sr.CogsJournalEntryId)
                .HasDatabaseName("IX_SalesReturns_CogsJournalEntryId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("CogsJournalEntryId"));
            builder.HasIndex(sr => sr.SalesRepresentativeId)
                .HasDatabaseName("IX_SalesReturns_SalesRepresentativeId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("SalesRepresentativeId"));
        }
    }
}
