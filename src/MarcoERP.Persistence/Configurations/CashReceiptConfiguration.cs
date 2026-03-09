using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Entities.Sales;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class CashReceiptConfiguration : IEntityTypeConfiguration<CashReceipt>
    {
        public void Configure(EntityTypeBuilder<CashReceipt> builder)
        {
            builder.ToTable("CashReceipts");

            builder.HasKey(cr => cr.Id);
            builder.Property(cr => cr.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(cr => cr.ReceiptNumber)
                .IsRequired()
                .HasMaxLength(30)
                .IsUnicode(false);

            builder.Property(cr => cr.ReceiptDate)
                .IsRequired()
                .HasColumnType("date");

            builder.Property(cr => cr.Status)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(cr => cr.Amount)
                .IsRequired()
                .HasPrecision(18, 4);

            builder.Property(cr => cr.Description)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(cr => cr.Notes)
                .HasMaxLength(1000);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(cr => cr.CreatedAt).IsRequired();
            builder.Property(cr => cr.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(cr => cr.ModifiedBy).HasMaxLength(100);

            // ── Soft Delete Fields ─────────────────────────────
            builder.Property(cr => cr.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(cr => cr.DeletedAt);

            builder.Property(cr => cr.DeletedBy)
                .HasMaxLength(100);

            // ── Relationships ───────────────────────────────────

            // Cashbox FK (required)
            builder.HasOne(cr => cr.Cashbox).WithMany()
                .HasForeignKey(cr => cr.CashboxId)
                .OnDelete(DeleteBehavior.Restrict);

            // Contra Account FK (required)
            builder.HasOne(cr => cr.Account).WithMany()
                .HasForeignKey(cr => cr.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // Optional Customer FK
            builder.HasOne(cr => cr.Customer).WithMany()
                .HasForeignKey(cr => cr.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Optional Sales Invoice FK
            builder.HasOne<SalesInvoice>().WithMany()
                .HasForeignKey(cr => cr.SalesInvoiceId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Optional Journal Entry FK
            builder.HasOne<MarcoERP.Domain.Entities.Accounting.JournalEntry>().WithMany()
                .HasForeignKey(cr => cr.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(cr => new { cr.CompanyId, cr.ReceiptNumber })
                .IsUnique()
                .HasFilter(DbProviderHelper.SoftDeleteFilter())
                .HasDatabaseName("IX_CashReceipts_CompanyId_ReceiptNumber");

            builder.HasIndex(cr => cr.ReceiptDate)
                .HasDatabaseName("IX_CashReceipts_ReceiptDate");

            builder.HasIndex(cr => cr.CashboxId)
                .HasDatabaseName("IX_CashReceipts_CashboxId");

            builder.HasIndex(cr => cr.Status)
                .HasDatabaseName("IX_CashReceipts_Status");

            builder.HasIndex(cr => cr.CustomerId)
                .HasDatabaseName("IX_CashReceipts_CustomerId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("CustomerId"));

            builder.HasIndex(cr => cr.SalesInvoiceId)
                .HasDatabaseName("IX_CashReceipts_SalesInvoiceId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("SalesInvoiceId"));

            builder.HasIndex(cr => cr.JournalEntryId)
                .HasDatabaseName("IX_CashReceipts_JournalEntryId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("JournalEntryId"));
        }
    }
}
