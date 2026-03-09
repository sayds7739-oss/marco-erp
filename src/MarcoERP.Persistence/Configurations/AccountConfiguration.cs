using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Accounting;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for Account entity.
    /// Maps all columns, indexes, relationships, and constraints per design doc Section 1.2.
    /// </summary>
    public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            // ── Table ───────────────────────────────────────────
            builder.ToTable("Accounts");

            // ── Primary Key ─────────────────────────────────────
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id)
                .UseIdentityColumn();

            // ── Concurrency Token ───────────────────────────────
            DbProviderHelper.ConfigureRowVersion(builder);

            // ── Properties ──────────────────────────────────────
            builder.Property(a => a.AccountCode)
                .IsRequired()
                .HasMaxLength(4)
                .IsUnicode(false); // numeric digits only

            builder.Property(a => a.AccountNameAr)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(a => a.AccountNameEn)
                .HasMaxLength(200);

            builder.Property(a => a.AccountType)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(a => a.NormalBalance)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(a => a.Level)
                .IsRequired();

            builder.Property(a => a.IsLeaf)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(a => a.AllowPosting)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(a => a.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(a => a.IsSystemAccount)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(a => a.CurrencyCode)
                .IsRequired()
                .HasMaxLength(3)
                .IsUnicode(false);

            builder.Property(a => a.Description)
                .HasMaxLength(500);

            builder.Property(a => a.HasPostings)
                .IsRequired()
                .HasDefaultValue(false);

            // ── Audit Fields (inherited from AuditableEntity via SoftDeletableEntity) ──
            builder.Property(a => a.CreatedAt)
                .IsRequired();

            builder.Property(a => a.CreatedBy)
                .HasMaxLength(100);

            builder.Property(a => a.ModifiedAt);

            builder.Property(a => a.ModifiedBy)
                .HasMaxLength(100);

            // ── Soft Delete Fields ──────────────────────────────
            builder.Property(a => a.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(a => a.DeletedAt);

            builder.Property(a => a.DeletedBy)
                .HasMaxLength(100);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(a => a.AccountCode)
                .IsUnique()
                .HasDatabaseName("IX_Accounts_AccountCode");

            builder.HasIndex(a => a.AccountType)
                .HasDatabaseName("IX_Accounts_AccountType");

            builder.HasIndex(a => a.ParentAccountId)
                .HasDatabaseName("IX_Accounts_ParentAccountId");

            builder.HasIndex(a => a.IsActive)
                .HasDatabaseName("IX_Accounts_IsActive");

            builder.HasIndex(a => new { a.IsLeaf, a.AllowPosting, a.IsActive })
                .HasDatabaseName("IX_Accounts_Postable")
                .HasFilter(DbProviderHelper.SoftDeleteFilter());

            // ── Relationships ───────────────────────────────────
            // Self-referencing hierarchy: Account → ParentAccount
            builder.HasOne(a => a.ParentAccount)
                .WithMany()
                .HasForeignKey(a => a.ParentAccountId)
                .OnDelete(DeleteBehavior.Restrict) // Cannot cascade-delete hierarchy
                .IsRequired(false);
        }
    }
}
