using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Treasury;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class BankReconciliationConfiguration : IEntityTypeConfiguration<BankReconciliation>
    {
        public void Configure(EntityTypeBuilder<BankReconciliation> builder)
        {
            builder.ToTable("BankReconciliations");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(r => r.ReconciliationDate).IsRequired();
            builder.Property(r => r.StatementBalance).IsRequired().HasPrecision(18, 4);
            builder.Property(r => r.SystemBalance).IsRequired().HasPrecision(18, 4);
            builder.Property(r => r.Difference).IsRequired().HasPrecision(18, 4);
            builder.Property(r => r.IsCompleted).IsRequired().HasDefaultValue(false);
            builder.Property(r => r.Notes).HasMaxLength(500);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(r => r.CreatedAt).IsRequired();
            builder.Property(r => r.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(r => r.ModifiedBy).HasMaxLength(100);

            // ── Relationships ───────────────────────────────────
            builder.HasOne(r => r.BankAccount)
                .WithMany()
                .HasForeignKey(r => r.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(r => r.Items)
                .WithOne(i => i.BankReconciliation)
                .HasForeignKey(i => i.BankReconciliationId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(r => r.BankAccountId)
                .HasDatabaseName("IX_BankReconciliations_BankAccountId");
        }
    }

    public sealed class BankReconciliationItemConfiguration : IEntityTypeConfiguration<BankReconciliationItem>
    {
        public void Configure(EntityTypeBuilder<BankReconciliationItem> builder)
        {
            builder.ToTable("BankReconciliationItems");

            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(i => i.TransactionDate).IsRequired();
            builder.Property(i => i.Description).IsRequired().HasMaxLength(300);
            builder.Property(i => i.Amount).IsRequired().HasPrecision(18, 4);
            builder.Property(i => i.Reference).HasMaxLength(100);
            builder.Property(i => i.IsMatched).IsRequired().HasDefaultValue(false);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(i => i.CreatedAt).IsRequired();
            builder.Property(i => i.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(i => i.ModifiedBy).HasMaxLength(100);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(i => i.BankReconciliationId)
                .HasDatabaseName("IX_BankReconciliationItems_ReconciliationId");
        }
    }
}
