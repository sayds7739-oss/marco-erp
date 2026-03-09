using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Treasury;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class CashboxConfiguration : IEntityTypeConfiguration<Cashbox>
    {
        public void Configure(EntityTypeBuilder<Cashbox> builder)
        {
            builder.ToTable("Cashboxes");

            // NOTE: CK_Cashboxes_Balance_NonNegative removed — domain-level protection
            // via Cashbox.DecreaseBalance() handles negative balance prevention,
            // while AllowNegativeCash feature toggle uses DecreaseBalanceAllowNegative().

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(c => c.Code)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(c => c.NameAr)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(c => c.NameEn)
                .HasMaxLength(100);

            builder.Property(c => c.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(c => c.IsDefault)
                .IsRequired()
                .HasDefaultValue(false);

            // ── Balance ────────────────────────────────────────────
            builder.Property(c => c.Balance)
                .IsRequired()
                .HasPrecision(18, 4)
                .HasDefaultValue(0m);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(c => c.CreatedAt).IsRequired();
            builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(c => c.ModifiedBy).HasMaxLength(100);

            // ── Soft Delete Fields ──────────────────────────────
            builder.Property(c => c.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(c => c.DeletedAt);

            builder.Property(c => c.DeletedBy)
                .HasMaxLength(100);

            // ── Optional relationship to GL Account ─────────────
            builder.HasOne(c => c.Account).WithMany()
                .HasForeignKey(c => c.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(c => c.Code)
                .IsUnique()
                .HasDatabaseName("IX_Cashboxes_Code");
        }
    }
}
