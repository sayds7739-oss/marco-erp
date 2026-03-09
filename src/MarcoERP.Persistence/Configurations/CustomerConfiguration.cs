using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Sales;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.ToTable("Customers");

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            // ── Business Fields ─────────────────────────────────
            builder.Property(c => c.Code)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(c => c.NameAr)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(c => c.NameEn)
                .HasMaxLength(200);

            builder.Property(c => c.Phone)
                .HasMaxLength(30);

            builder.Property(c => c.Mobile)
                .HasMaxLength(30);

            builder.Property(c => c.Address)
                .HasMaxLength(500);

            builder.Property(c => c.City)
                .HasMaxLength(100);

            builder.Property(c => c.TaxNumber)
                .HasMaxLength(50);

            builder.Property(c => c.Email)
                .HasMaxLength(200);

            builder.Property(c => c.CommercialRegister)
                .HasMaxLength(50);

            builder.Property(c => c.Country)
                .HasMaxLength(100);

            builder.Property(c => c.PostalCode)
                .HasMaxLength(20);

            builder.Property(c => c.ContactPerson)
                .HasMaxLength(200);

            builder.Property(c => c.Website)
                .HasMaxLength(200);

            builder.Property(c => c.DefaultDiscountPercent)
                .HasPrecision(18, 4);

            builder.Property(c => c.PreviousBalance)
                .HasPrecision(18, 4)
                .HasDefaultValue(0m);

            builder.Property(c => c.CreditLimit)
                .HasPrecision(18, 4)
                .HasDefaultValue(0m);

            builder.Property(c => c.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(c => c.Notes)
                .HasMaxLength(1000);

            // ── Soft Delete Fields ──────────────────────────────
            builder.Property(c => c.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(c => c.DeletedBy)
                .HasMaxLength(100);

            // ── Audit Fields ────────────────────────────────────
            builder.Property(c => c.CreatedAt).IsRequired();
            builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(c => c.ModifiedBy).HasMaxLength(100);

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(c => c.Code)
                .IsUnique()
                .HasDatabaseName("IX_Customers_Code");

            builder.HasIndex(c => c.NameAr)
                .HasDatabaseName("IX_Customers_NameAr");

            builder.HasIndex(c => c.TaxNumber)
                .HasDatabaseName("IX_Customers_TaxNumber")
                .HasFilter(DbProviderHelper.IsNotNullFilter("TaxNumber"));

            builder.HasIndex(c => c.IsActive)
                .HasDatabaseName("IX_Customers_IsActive");

            // ── GL Account Relationship ─────────────────────────
            builder.Property(c => c.AccountId);

            builder.HasOne(c => c.Account)
                .WithMany()
                .HasForeignKey(c => c.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasIndex(c => c.AccountId)
                .HasDatabaseName("IX_Customers_AccountId");

            // ── Default Sales Representative Relationship ────────
            builder.HasOne(c => c.DefaultSalesRepresentative)
                .WithMany()
                .HasForeignKey(c => c.DefaultSalesRepresentativeId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasIndex(c => c.DefaultSalesRepresentativeId)
                .HasDatabaseName("IX_Customers_DefaultSalesRepresentativeId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("DefaultSalesRepresentativeId"));

            // ── Credit Control Fields ───────────────────────────
            builder.Property(c => c.DaysAllowed);

            builder.Property(c => c.BlockedOnOverdue)
                .IsRequired()
                .HasDefaultValue(false);

            // ── Price List Relationship ─────────────────────────
            builder.HasOne(c => c.PriceList)
                .WithMany()
                .HasForeignKey(c => c.PriceListId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasIndex(c => c.PriceListId)
                .HasDatabaseName("IX_Customers_PriceListId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("PriceListId"));
        }
    }
}
