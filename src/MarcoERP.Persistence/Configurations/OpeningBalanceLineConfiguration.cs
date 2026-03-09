using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class OpeningBalanceLineConfiguration : IEntityTypeConfiguration<OpeningBalanceLine>
    {
        public void Configure(EntityTypeBuilder<OpeningBalanceLine> builder)
        {
            builder.ToTable("OpeningBalanceLines");

            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).UseIdentityColumn();
            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(l => l.LineType).IsRequired().HasConversion<int>();
            builder.Property(l => l.AccountId).IsRequired();
            builder.Property(l => l.DebitAmount).IsRequired().HasPrecision(18, 4);
            builder.Property(l => l.CreditAmount).IsRequired().HasPrecision(18, 4);
            builder.Property(l => l.Quantity).HasPrecision(18, 4);
            builder.Property(l => l.UnitCost).HasPrecision(18, 4);
            builder.Property(l => l.Notes).HasMaxLength(500);

            // Relationships (optional FKs — only populated based on LineType)
            builder.HasOne<MarcoERP.Domain.Entities.Accounting.Account>().WithMany()
                .HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<MarcoERP.Domain.Entities.Sales.Customer>().WithMany()
                .HasForeignKey(l => l.CustomerId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne<MarcoERP.Domain.Entities.Purchases.Supplier>().WithMany()
                .HasForeignKey(l => l.SupplierId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne<MarcoERP.Domain.Entities.Inventory.Product>().WithMany()
                .HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne<MarcoERP.Domain.Entities.Inventory.Warehouse>().WithMany()
                .HasForeignKey(l => l.WarehouseId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne<MarcoERP.Domain.Entities.Treasury.Cashbox>().WithMany()
                .HasForeignKey(l => l.CashboxId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne<MarcoERP.Domain.Entities.Treasury.BankAccount>().WithMany()
                .HasForeignKey(l => l.BankAccountId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Indexes
            builder.HasIndex(l => l.OpeningBalanceId)
                .HasDatabaseName("IX_OpeningBalanceLines_OpeningBalanceId");
            builder.HasIndex(l => l.LineType)
                .HasDatabaseName("IX_OpeningBalanceLines_LineType");
            builder.HasIndex(l => l.AccountId)
                .HasDatabaseName("IX_OpeningBalanceLines_AccountId");
        }
    }
}
