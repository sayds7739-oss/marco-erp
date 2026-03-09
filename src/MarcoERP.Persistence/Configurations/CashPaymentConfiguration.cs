using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Entities.Purchases;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class CashPaymentConfiguration : IEntityTypeConfiguration<CashPayment>
    {
        public void Configure(EntityTypeBuilder<CashPayment> builder)
        {
            builder.ToTable("CashPayments");

            builder.HasKey(cp => cp.Id);
            builder.Property(cp => cp.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(cp => cp.PaymentNumber)
                .IsRequired()
                .HasMaxLength(30)
                .IsUnicode(false);

            builder.Property(cp => cp.PaymentDate)
                .IsRequired()
                .HasColumnType("date");

            builder.Property(cp => cp.Status)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(cp => cp.Amount)
                .IsRequired()
                .HasPrecision(18, 4);

            builder.Property(cp => cp.Description)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(cp => cp.Notes)
                .HasMaxLength(1000);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(cp => cp.CreatedAt).IsRequired();
            builder.Property(cp => cp.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(cp => cp.ModifiedBy).HasMaxLength(100);

            // ── Soft Delete Fields ─────────────────────────────
            builder.Property(cp => cp.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(cp => cp.DeletedAt);

            builder.Property(cp => cp.DeletedBy)
                .HasMaxLength(100);

            // ── Relationships ───────────────────────────────────

            // Cashbox FK (required)
            builder.HasOne(cp => cp.Cashbox).WithMany()
                .HasForeignKey(cp => cp.CashboxId)
                .OnDelete(DeleteBehavior.Restrict);

            // Contra Account FK (required)
            builder.HasOne(cp => cp.Account).WithMany()
                .HasForeignKey(cp => cp.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // Optional Supplier FK
            builder.HasOne(cp => cp.Supplier).WithMany()
                .HasForeignKey(cp => cp.SupplierId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Optional Purchase Invoice FK
            builder.HasOne<PurchaseInvoice>().WithMany()
                .HasForeignKey(cp => cp.PurchaseInvoiceId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Optional Journal Entry FK
            builder.HasOne<MarcoERP.Domain.Entities.Accounting.JournalEntry>().WithMany()
                .HasForeignKey(cp => cp.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(cp => new { cp.CompanyId, cp.PaymentNumber })
                .IsUnique()
                .HasFilter(DbProviderHelper.SoftDeleteFilter())
                .HasDatabaseName("IX_CashPayments_CompanyId_PaymentNumber");

            builder.HasIndex(cp => cp.PaymentDate)
                .HasDatabaseName("IX_CashPayments_PaymentDate");

            builder.HasIndex(cp => cp.CashboxId)
                .HasDatabaseName("IX_CashPayments_CashboxId");

            builder.HasIndex(cp => cp.Status)
                .HasDatabaseName("IX_CashPayments_Status");

            builder.HasIndex(cp => cp.SupplierId)
                .HasDatabaseName("IX_CashPayments_SupplierId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("SupplierId"));

            builder.HasIndex(cp => cp.PurchaseInvoiceId)
                .HasDatabaseName("IX_CashPayments_PurchaseInvoiceId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("PurchaseInvoiceId"));

            builder.HasIndex(cp => cp.JournalEntryId)
                .HasDatabaseName("IX_CashPayments_JournalEntryId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("JournalEntryId"));
        }
    }
}
