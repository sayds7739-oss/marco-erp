using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Accounting;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for JournalEntryLine entity.
    /// Maps all columns, indexes, and constraints per design doc Section 3.2.
    /// </summary>
    public sealed class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
    {
        public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
        {
            // ── Table ───────────────────────────────────────────
            builder.ToTable("JournalEntryLines", t =>
            {
                // FIN-CK-01: Journal amounts must be non-negative
                t.HasCheckConstraint("CK_JournalEntryLines_NonNegative",
                    DbProviderHelper.CheckExpr("{0} >= 0 AND {1} >= 0", "DebitAmount", "CreditAmount"));

                // FIN-CK-02: A line cannot have both debit and credit
                t.HasCheckConstraint("CK_JournalEntryLines_SingleSide",
                    DbProviderHelper.CheckExpr("NOT ({0} > 0 AND {1} > 0)", "DebitAmount", "CreditAmount"));
            });

            // ── Primary Key ─────────────────────────────────────
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id)
                .UseIdentityColumn();

            // ── Concurrency Token ───────────────────────────────
            DbProviderHelper.ConfigureRowVersion(builder);

            // ── Properties ──────────────────────────────────────
            builder.Property(l => l.JournalEntryId)
                .IsRequired();

            builder.Property(l => l.LineNumber)
                .IsRequired();

            builder.Property(l => l.AccountId)
                .IsRequired();

            builder.Property(l => l.DebitAmount)
                .IsRequired()
                .HasPrecision(18, 4);

            builder.Property(l => l.CreditAmount)
                .IsRequired()
                .HasPrecision(18, 4);

            builder.Property(l => l.Description)
                .HasMaxLength(500);

            // CostCenterId: reserved for future Cost Center module — no FK constraint
            builder.Property(l => l.CostCenterId)
                .IsRequired(false)
                .HasComment("Reserved for future Cost Center module");

            builder.Property(l => l.CreatedAt)
                .IsRequired();

            builder.Property(l => l.CreatedBy)
                .HasMaxLength(100);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(l => l.JournalEntryId)
                .HasDatabaseName("IX_JournalEntryLines_JournalEntryId");

            builder.HasIndex(l => l.AccountId)
                .HasDatabaseName("IX_JournalEntryLines_AccountId");

            builder.HasIndex(l => new { l.JournalEntryId, l.LineNumber })
                .IsUnique()
                .HasDatabaseName("IX_JournalEntryLines_Entry_LineNumber");

            // ── Relationships ───────────────────────────────────
            // Line → Account (many-to-one, restrict delete)
            builder.HasOne<Account>()
                .WithMany()
                .HasForeignKey(l => l.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        }
    }
}
