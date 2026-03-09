using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Accounting;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for JournalEntry entity.
    /// Maps all columns, indexes, relationships per design doc Section 3.1.
    /// </summary>
    public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
    {
        public void Configure(EntityTypeBuilder<JournalEntry> builder)
        {
            // ── Table ───────────────────────────────────────────
            builder.ToTable("JournalEntries", t =>
            {
                // Ensure posted journal entries are always balanced (Debit = Credit)
                t.HasCheckConstraint("CK_JournalEntries_Balance",
                    DbProviderHelper.CheckExpr("{0} <> 1 OR {1} = {2}", "Status", "TotalDebit", "TotalCredit"));
            });

            // ── Primary Key ─────────────────────────────────────
            builder.HasKey(j => j.Id);
            builder.Property(j => j.Id)
                .UseIdentityColumn();

            // ── Concurrency Token ───────────────────────────────
            DbProviderHelper.ConfigureRowVersion(builder);

            // ── Properties ──────────────────────────────────────
            builder.Property(j => j.JournalNumber)
                .HasMaxLength(20)
                .IsUnicode(false);

            builder.Property(j => j.DraftCode)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false);

            builder.Property(j => j.JournalDate)
                .IsRequired()
                .HasColumnType("date");

            builder.Property(j => j.PostingDate);

            builder.Property(j => j.Description)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(j => j.ReferenceNumber)
                .HasMaxLength(100);

            builder.Property(j => j.Status)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(j => j.SourceType)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(j => j.ReversalReason)
                .HasMaxLength(500);

            builder.Property(j => j.PostedBy)
                .HasMaxLength(100);

            builder.Property(j => j.TotalDebit)
                .IsRequired()
                .HasPrecision(18, 4);

            builder.Property(j => j.TotalCredit)
                .IsRequired()
                .HasPrecision(18, 4);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(j => j.CreatedAt)
                .IsRequired();

            builder.Property(j => j.CreatedBy)
                .HasMaxLength(100);

            builder.Property(j => j.ModifiedAt);

            builder.Property(j => j.ModifiedBy)
                .HasMaxLength(100);

            // ── Soft Delete Fields ──────────────────────────────
            builder.Property(j => j.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            // CostCenterId: reserved for future Cost Center module — no FK constraint
            builder.Property(j => j.CostCenterId)
                .IsRequired(false)
                .HasComment("Reserved for future Cost Center module");

            builder.Property(j => j.DeletedAt);

            builder.Property(j => j.DeletedBy)
                .HasMaxLength(100);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(j => j.JournalNumber)
                .IsUnique()
                .HasDatabaseName("IX_JournalEntries_JournalNumber")
                .HasFilter(DbProviderHelper.NotNullAndSoftDeleteFilter("JournalNumber"));

            builder.HasIndex(j => j.DraftCode)
                .IsUnique()
                .HasFilter(DbProviderHelper.NotNullAndSoftDeleteFilter("DraftCode"))
                .HasDatabaseName("IX_JournalEntries_DraftCode");

            builder.HasIndex(j => j.JournalDate)
                .HasDatabaseName("IX_JournalEntries_JournalDate");

            builder.HasIndex(j => j.Status)
                .HasDatabaseName("IX_JournalEntries_Status");

            builder.HasIndex(j => j.FiscalYearId)
                .HasDatabaseName("IX_JournalEntries_FiscalYearId");

            builder.HasIndex(j => j.FiscalPeriodId)
                .HasDatabaseName("IX_JournalEntries_FiscalPeriodId");

            builder.HasIndex(j => new { j.FiscalYearId, j.Status })
                .HasDatabaseName("IX_JournalEntries_Year_Status")
                .HasFilter(DbProviderHelper.SoftDeleteFilter());

            builder.HasIndex(j => j.SourceType)
                .HasDatabaseName("IX_JournalEntries_SourceType");

            builder.HasIndex(j => new { j.SourceType, j.SourceId })
                .HasDatabaseName("IX_JournalEntries_Source")
                .HasFilter(DbProviderHelper.IsNotNullFilter("SourceId"));

            // ── Relationships ───────────────────────────────────
            // JournalEntry → FiscalYear
            builder.HasOne<FiscalYear>()
                .WithMany()
                .HasForeignKey(j => j.FiscalYearId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            // JournalEntry → FiscalPeriod
            builder.HasOne<FiscalPeriod>()
                .WithMany()
                .HasForeignKey(j => j.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            // JournalEntry → Lines (one-to-many, no cascade delete)
            builder.HasMany(j => j.Lines)
                .WithOne()
                .HasForeignKey(l => l.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            // Self-referencing: ReversedEntry, ReversalEntry, AdjustedEntry
            builder.HasOne<JournalEntry>()
                .WithOne()
                .HasForeignKey<JournalEntry>(j => j.ReversedEntryId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Ignore navigation-less DomainEvents collection
            builder.Ignore(j => j.DomainEvents);
        }
    }
}
