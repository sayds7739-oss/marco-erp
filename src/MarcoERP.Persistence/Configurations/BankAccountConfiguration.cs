using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Treasury;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
    {
        public void Configure(EntityTypeBuilder<BankAccount> builder)
        {
            builder.ToTable("BankAccounts");

            builder.HasKey(b => b.Id);
            builder.Property(b => b.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(b => b.Code)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(b => b.NameAr)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(b => b.NameEn)
                .HasMaxLength(100);

            builder.Property(b => b.BankName)
                .HasMaxLength(200);

            builder.Property(b => b.AccountNumber)
                .HasMaxLength(50);

            builder.Property(b => b.IBAN)
                .HasMaxLength(34);

            builder.Property(b => b.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(b => b.IsDefault)
                .IsRequired()
                .HasDefaultValue(false);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(b => b.CreatedAt).IsRequired();
            builder.Property(b => b.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(b => b.ModifiedBy).HasMaxLength(100);
            builder.Property(b => b.CreatedAt).IsRequired();
            builder.Property(b => b.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(b => b.ModifiedBy).HasMaxLength(100);

            // ── Soft Delete Fields ──────────────────────────────
            builder.Property(b => b.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(b => b.DeletedAt);

            builder.Property(b => b.DeletedBy)
                .HasMaxLength(100);

            // ── Optional relationship to GL Account ─────────────
            builder.Property(b => b.AccountId);

            builder.HasOne(b => b.Account)
                .WithMany()
                .HasForeignKey(b => b.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(b => b.Code)
                .IsUnique()
                .HasDatabaseName("IX_BankAccounts_Code");

            builder.HasIndex(b => b.IBAN)
                .HasDatabaseName("IX_BankAccounts_IBAN");
        }
    }
}
