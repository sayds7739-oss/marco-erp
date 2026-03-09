using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Purchases;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
    {
        public void Configure(EntityTypeBuilder<Supplier> builder)
        {
            builder.ToTable("Suppliers");

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            // ── Business Fields ─────────────────────────────────
            builder.Property(s => s.Code)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(s => s.NameAr)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(s => s.NameEn)
                .HasMaxLength(200);

            builder.Property(s => s.Phone)
                .HasMaxLength(30);

            builder.Property(s => s.Mobile)
                .HasMaxLength(30);

            builder.Property(s => s.Address)
                .HasMaxLength(500);

            builder.Property(s => s.City)
                .HasMaxLength(100);

            builder.Property(s => s.TaxNumber)
                .HasMaxLength(50);

            builder.Property(s => s.Email)
                .HasMaxLength(200);

            builder.Property(s => s.CommercialRegister)
                .HasMaxLength(50);

            builder.Property(s => s.Country)
                .HasMaxLength(100);

            builder.Property(s => s.PostalCode)
                .HasMaxLength(20);

            builder.Property(s => s.ContactPerson)
                .HasMaxLength(200);

            builder.Property(s => s.Website)
                .HasMaxLength(200);

            builder.Property(s => s.CreditLimit)
                .HasPrecision(18, 4);

            builder.Property(s => s.BankName)
                .HasMaxLength(200);

            builder.Property(s => s.BankAccountName)
                .HasMaxLength(200);

            builder.Property(s => s.BankAccountNumber)
                .HasMaxLength(50);

            builder.Property(s => s.IBAN)
                .HasMaxLength(34);

            builder.Property(s => s.PreviousBalance)
                .HasPrecision(18, 4)
                .HasDefaultValue(0m);

            builder.Property(s => s.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(s => s.Notes)
                .HasMaxLength(1000);

            // ── Soft Delete Fields ──────────────────────────────
            builder.Property(s => s.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(s => s.DeletedBy)
                .HasMaxLength(100);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(s => s.CreatedAt).IsRequired();
            builder.Property(s => s.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(s => s.ModifiedBy).HasMaxLength(100);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(s => s.Code)
                .IsUnique()
                .HasDatabaseName("IX_Suppliers_Code");

            builder.HasIndex(s => s.NameAr)
                .HasDatabaseName("IX_Suppliers_NameAr");

            builder.HasIndex(s => s.TaxNumber)
                .HasDatabaseName("IX_Suppliers_TaxNumber")
                .HasFilter(DbProviderHelper.IsNotNullFilter("TaxNumber"));

            builder.HasIndex(s => s.IsActive)
                .HasDatabaseName("IX_Suppliers_IsActive");

            // ── GL Account Relationship ─────────────────────────
            builder.Property(s => s.AccountId);

            builder.HasOne(s => s.Account)
                .WithMany()
                .HasForeignKey(s => s.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasIndex(s => s.AccountId)
                .HasDatabaseName("IX_Suppliers_AccountId");
        }
    }
}
