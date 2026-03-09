using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class OpeningBalanceConfiguration : IEntityTypeConfiguration<OpeningBalance>
    {
        public void Configure(EntityTypeBuilder<OpeningBalance> builder)
        {
            builder.ToTable("OpeningBalances");

            builder.HasKey(ob => ob.Id);
            builder.Property(ob => ob.Id).UseIdentityColumn();
            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(ob => ob.BalanceDate).IsRequired().HasColumnType("date");
            builder.Property(ob => ob.Status).IsRequired().HasConversion<int>();
            builder.Property(ob => ob.Notes).HasMaxLength(1000);
            builder.Property(ob => ob.PostedBy).HasMaxLength(100);
            builder.Property(ob => ob.TotalDebit).IsRequired().HasPrecision(18, 4);
            builder.Property(ob => ob.TotalCredit).IsRequired().HasPrecision(18, 4);

            // Audit
            builder.Property(ob => ob.CreatedAt).IsRequired();
            builder.Property(ob => ob.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(ob => ob.ModifiedBy).HasMaxLength(100);

            // Relationships
            builder.HasOne(ob => ob.FiscalYear).WithMany()
                .HasForeignKey(ob => ob.FiscalYearId).OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<JournalEntry>().WithMany()
                .HasForeignKey(ob => ob.JournalEntryId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasMany(ob => ob.Lines).WithOne()
                .HasForeignKey(l => l.OpeningBalanceId).OnDelete(DeleteBehavior.Restrict);

            // Indexes
            builder.HasIndex(ob => ob.FiscalYearId).IsUnique()
                .HasDatabaseName("IX_OpeningBalances_FiscalYearId");
            builder.HasIndex(ob => ob.BalanceDate)
                .HasDatabaseName("IX_OpeningBalances_BalanceDate");
            builder.HasIndex(ob => ob.Status)
                .HasDatabaseName("IX_OpeningBalances_Status");

            // Check: Posted entries must be balanced
            builder.ToTable(t => t.HasCheckConstraint(
                "CK_OpeningBalances_PostedBalanced",
                DbProviderHelper.CheckExpr("{0} <> 1 OR {1} = {2}", "Status", "TotalDebit", "TotalCredit")));
        }
    }
}
