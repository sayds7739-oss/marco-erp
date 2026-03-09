using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Treasury;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class CashTransferConfiguration : IEntityTypeConfiguration<CashTransfer>
    {
        public void Configure(EntityTypeBuilder<CashTransfer> builder)
        {
            builder.ToTable("CashTransfers");

            builder.HasKey(ct => ct.Id);
            builder.Property(ct => ct.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(ct => ct.TransferNumber)
                .IsRequired()
                .HasMaxLength(30)
                .IsUnicode(false);

            builder.Property(ct => ct.TransferDate)
                .IsRequired()
                .HasColumnType("date");

            builder.Property(ct => ct.Status)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(ct => ct.Amount)
                .IsRequired()
                .HasPrecision(18, 4);

            builder.Property(ct => ct.Description)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(ct => ct.Notes)
                .HasMaxLength(1000);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(ct => ct.CreatedAt).IsRequired();
            builder.Property(ct => ct.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(ct => ct.ModifiedBy).HasMaxLength(100);

            // ── Soft Delete Fields ─────────────────────────────
            builder.Property(ct => ct.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(ct => ct.DeletedAt);

            builder.Property(ct => ct.DeletedBy)
                .HasMaxLength(100);

            // ── Relationships ───────────────────────────────────

            // Source Cashbox FK (required)
            builder.HasOne(ct => ct.SourceCashbox).WithMany()
                .HasForeignKey(ct => ct.SourceCashboxId)
                .OnDelete(DeleteBehavior.Restrict);

            // Target Cashbox FK (required)
            builder.HasOne(ct => ct.TargetCashbox).WithMany()
                .HasForeignKey(ct => ct.TargetCashboxId)
                .OnDelete(DeleteBehavior.Restrict);

            // Optional Journal Entry FK
            builder.HasOne<MarcoERP.Domain.Entities.Accounting.JournalEntry>().WithMany()
                .HasForeignKey(ct => ct.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(ct => new { ct.CompanyId, ct.TransferNumber })
                .IsUnique()
                .HasFilter(DbProviderHelper.SoftDeleteFilter())
                .HasDatabaseName("IX_CashTransfers_CompanyId_TransferNumber");

            builder.HasIndex(ct => ct.TransferDate)
                .HasDatabaseName("IX_CashTransfers_TransferDate");

            builder.HasIndex(ct => ct.SourceCashboxId)
                .HasDatabaseName("IX_CashTransfers_SourceCashboxId");

            builder.HasIndex(ct => ct.TargetCashboxId)
                .HasDatabaseName("IX_CashTransfers_TargetCashboxId");

            builder.HasIndex(ct => ct.Status)
                .HasDatabaseName("IX_CashTransfers_Status");

            builder.HasIndex(ct => ct.JournalEntryId)
                .HasDatabaseName("IX_CashTransfers_JournalEntryId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("JournalEntryId"));
        }
    }
}
