using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Accounting;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for CodeSequence entity.
    /// Ensures one row per document type per fiscal year with concurrency control.
    /// </summary>
    public sealed class CodeSequenceConfiguration : IEntityTypeConfiguration<CodeSequence>
    {
        public void Configure(EntityTypeBuilder<CodeSequence> builder)
        {
            // ── Table ───────────────────────────────────────────
            builder.ToTable("CodeSequences");

            // ── Primary Key ─────────────────────────────────────
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id)
                .UseIdentityColumn();

            // ── Concurrency Token ───────────────────────────────
            DbProviderHelper.ConfigureRowVersion(builder);

            // ── Properties ──────────────────────────────────────
            builder.Property(c => c.DocumentType)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false);

            builder.Property(c => c.FiscalYearId)
                .IsRequired();

            builder.Property(c => c.Prefix)
                .IsRequired()
                .HasMaxLength(30)
                .IsUnicode(false);

            builder.Property(c => c.CurrentSequence)
                .IsRequired()
                .HasDefaultValue(0);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(c => new { c.DocumentType, c.FiscalYearId })
                .IsUnique()
                .HasDatabaseName("IX_CodeSequences_DocType_FiscalYear");

            // ── Relationships ───────────────────────────────────
            // CodeSequence → FiscalYear (restrict delete)
            builder.HasOne<FiscalYear>()
                .WithMany()
                .HasForeignKey(c => c.FiscalYearId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        }
    }
}
