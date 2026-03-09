using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Inventory;

namespace MarcoERP.Persistence.Configurations
{
    public sealed class InventoryAdjustmentConfiguration : IEntityTypeConfiguration<InventoryAdjustment>
    {
        public void Configure(EntityTypeBuilder<InventoryAdjustment> builder)
        {
            builder.ToTable("InventoryAdjustments");

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).UseIdentityColumn();
            DbProviderHelper.ConfigureRowVersion(builder);

            builder.Property(a => a.AdjustmentNumber).IsRequired().HasMaxLength(30).IsUnicode(false);
            builder.Property(a => a.AdjustmentDate).IsRequired().HasColumnType("date");
            builder.Property(a => a.Reason).IsRequired().HasMaxLength(500);
            builder.Property(a => a.Notes).HasMaxLength(1000);
            builder.Property(a => a.Status).IsRequired().HasConversion<int>();
            builder.Property(a => a.TotalCostDifference).IsRequired().HasPrecision(18, 4);

            // Soft Delete
            builder.Property(a => a.IsDeleted).IsRequired().HasDefaultValue(false);
            builder.Property(a => a.DeletedBy).HasMaxLength(100);

            // Audit
            builder.Property(a => a.CreatedAt).IsRequired();
            builder.Property(a => a.CreatedBy).IsRequired().HasMaxLength(100);
            builder.Property(a => a.ModifiedBy).HasMaxLength(100);

            // Relationships
            builder.HasOne(a => a.Warehouse).WithMany()
                .HasForeignKey(a => a.WarehouseId).OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<MarcoERP.Domain.Entities.Accounting.JournalEntry>().WithMany()
                .HasForeignKey(a => a.JournalEntryId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasMany(a => a.Lines).WithOne()
                .HasForeignKey(l => l.InventoryAdjustmentId).OnDelete(DeleteBehavior.Restrict);

            // Indexes
            builder.HasIndex(a => a.AdjustmentNumber).IsUnique()
                .HasDatabaseName("IX_InventoryAdjustments_Number");
            builder.HasIndex(a => a.AdjustmentDate)
                .HasDatabaseName("IX_InventoryAdjustments_Date");
            builder.HasIndex(a => a.WarehouseId)
                .HasDatabaseName("IX_InventoryAdjustments_WarehouseId");
            builder.HasIndex(a => a.Status)
                .HasDatabaseName("IX_InventoryAdjustments_Status");
            builder.HasIndex(a => a.JournalEntryId)
                .HasDatabaseName("IX_InventoryAdjustments_JournalEntryId")
                .HasFilter(DbProviderHelper.IsNotNullFilter("JournalEntryId"));
        }
    }
}
