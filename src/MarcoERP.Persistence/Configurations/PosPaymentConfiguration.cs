using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Sales;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class PosPaymentConfiguration : IEntityTypeConfiguration<PosPayment>
    {
        public void Configure(EntityTypeBuilder<PosPayment> builder)
        {
            builder.ToTable("PosPayments");

            builder.HasKey(pp => pp.Id);
            builder.Property(pp => pp.Id).UseIdentityColumn();

            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(pp => pp.PaymentMethod).IsRequired().HasConversion<int>();
            builder.Property(pp => pp.Amount).IsRequired().HasPrecision(18, 4);
            builder.Property(pp => pp.ReferenceNumber).HasMaxLength(100);
            builder.Property(pp => pp.PaidAt).IsRequired();

            // Relationships
            builder.HasOne(pp => pp.SalesInvoice).WithMany()
                .HasForeignKey(pp => pp.SalesInvoiceId).OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(pp => pp.PosSession).WithMany(s => s.Payments)
                .HasForeignKey(pp => pp.PosSessionId).OnDelete(DeleteBehavior.Restrict);

            // Indexes
            builder.HasIndex(pp => pp.SalesInvoiceId)
                .HasDatabaseName("IX_PosPayments_SalesInvoiceId");
            builder.HasIndex(pp => pp.PosSessionId)
                .HasDatabaseName("IX_PosPayments_PosSessionId");
            builder.HasIndex(pp => pp.PaymentMethod)
                .HasDatabaseName("IX_PosPayments_PaymentMethod");
        }
    }
}
