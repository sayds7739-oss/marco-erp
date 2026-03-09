using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Accounting;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for FiscalPeriod entity.
    /// Maps all columns, indexes, and constraints per design doc Section 4.2.
    /// </summary>
    public sealed class FiscalPeriodConfiguration : IEntityTypeConfiguration<FiscalPeriod>
    {
        public void Configure(EntityTypeBuilder<FiscalPeriod> builder)
        {
            // ── Table ───────────────────────────────────────────
            builder.ToTable("FiscalPeriods");

            // ── Primary Key ─────────────────────────────────────
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id)
                .UseIdentityColumn();

            // ── Concurrency Token ───────────────────────────────
            DbProviderHelper.ConfigureRowVersion(builder);

            // ── Properties ──────────────────────────────────────
            builder.Property(p => p.FiscalYearId)
                .IsRequired();

            builder.Property(p => p.PeriodNumber)
                .IsRequired();

            builder.Property(p => p.Year)
                .IsRequired();

            builder.Property(p => p.Month)
                .IsRequired();

            builder.Property(p => p.StartDate)
                .IsRequired()
                .HasColumnType("date");

            builder.Property(p => p.EndDate)
                .IsRequired()
                .HasColumnType("date");

            builder.Property(p => p.Status)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(p => p.LockedAt);

            builder.Property(p => p.LockedBy)
                .HasMaxLength(100);

            builder.Property(p => p.UnlockReason)
                .HasMaxLength(500);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(p => p.CreatedAt)
                .IsRequired();

            builder.Property(p => p.CreatedBy)
                .HasMaxLength(100);

            builder.Property(p => p.ModifiedAt);

            builder.Property(p => p.ModifiedBy)
                .HasMaxLength(100);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(p => new { p.FiscalYearId, p.PeriodNumber })
                .IsUnique()
                .HasDatabaseName("IX_FiscalPeriods_Year_PeriodNumber");

            builder.HasIndex(p => new { p.FiscalYearId, p.Month })
                .IsUnique()
                .HasDatabaseName("IX_FiscalPeriods_Year_Month");

            builder.HasIndex(p => p.Status)
                .HasDatabaseName("IX_FiscalPeriods_Status");

            builder.HasIndex(p => new { p.StartDate, p.EndDate })
                .HasDatabaseName("IX_FiscalPeriods_DateRange");
        }
    }
}
