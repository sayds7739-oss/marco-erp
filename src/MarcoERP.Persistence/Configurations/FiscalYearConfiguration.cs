using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Accounting;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for FiscalYear entity.
    /// Maps all columns, indexes, relationships per design doc Section 4.
    /// </summary>
    public sealed class FiscalYearConfiguration : IEntityTypeConfiguration<FiscalYear>
    {
        public void Configure(EntityTypeBuilder<FiscalYear> builder)
        {
            // ── Table ───────────────────────────────────────────
            builder.ToTable("FiscalYears");

            // ── Primary Key ─────────────────────────────────────
            builder.HasKey(f => f.Id);
            builder.Property(f => f.Id)
                .UseIdentityColumn();

            // ── Concurrency Token ───────────────────────────────
            DbProviderHelper.ConfigureRowVersion(builder);

            // ── Properties ──────────────────────────────────────
            builder.Property(f => f.Year)
                .IsRequired();

            builder.Property(f => f.StartDate)
                .IsRequired()
                .HasColumnType("date");

            builder.Property(f => f.EndDate)
                .IsRequired()
                .HasColumnType("date");

            builder.Property(f => f.Status)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(f => f.ClosedAt);

            builder.Property(f => f.ClosedBy)
                .HasMaxLength(100);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(f => f.CreatedAt)
                .IsRequired();

            builder.Property(f => f.CreatedBy)
                .HasMaxLength(100);

            builder.Property(f => f.ModifiedAt);

            builder.Property(f => f.ModifiedBy)
                .HasMaxLength(100);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(f => f.Year)
                .IsUnique()
                .HasDatabaseName("IX_FiscalYears_Year");

            builder.HasIndex(f => f.Status)
                .HasDatabaseName("IX_FiscalYears_Status");

            // ── Relationships ───────────────────────────────────
            // FiscalYear → Periods (one-to-many, no cascade delete)
            builder.HasMany(f => f.Periods)
                .WithOne()
                .HasForeignKey(p => p.FiscalYearId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            // EF Core: access private backing field for Periods
            builder.Navigation(f => f.Periods)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
