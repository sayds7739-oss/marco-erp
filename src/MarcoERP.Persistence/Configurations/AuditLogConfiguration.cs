using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarcoERP.Domain.Entities.Accounting;

namespace MarcoERP.Persistence.Configurations
{
    /// <summary>
    /// EF Core Fluent API configuration for AuditLog entity.
    /// Immutable audit trail — no updates or deletes allowed at domain level.
    /// Uses long PK to support high-volume audit records.
    /// </summary>
    public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            // ── Table ───────────────────────────────────────────
            builder.ToTable("AuditLogs");

            // ── Primary Key ─────────────────────────────────────
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id)
                .UseIdentityColumn();

            // ── Properties ──────────────────────────────────────
            builder.Property(a => a.EntityType)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(a => a.EntityId)
                .IsRequired();

            builder.Property(a => a.Action)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(a => a.PerformedBy)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(a => a.Details)
                .HasMaxLength(4000);

            builder.Property(a => a.Timestamp)
                .IsRequired();

            // AUD-02: OldValues/NewValues/ChangedColumns per DATABASE_POLICY
            builder.Property(a => a.OldValues)
                .HasColumnType(DbProviderHelper.MaxStringType());

            builder.Property(a => a.NewValues)
                .HasColumnType(DbProviderHelper.MaxStringType());

            builder.Property(a => a.ChangedColumns)
                .HasColumnType(DbProviderHelper.MaxStringType());

            // ── Indexes ─────────────────────────────────────────
            builder.HasIndex(a => new { a.EntityType, a.EntityId })
                .HasDatabaseName("IX_AuditLogs_Entity");

            builder.HasIndex(a => a.Timestamp)
                .HasDatabaseName("IX_AuditLogs_Timestamp");

            builder.HasIndex(a => a.PerformedBy)
                .HasDatabaseName("IX_AuditLogs_PerformedBy");

            builder.HasIndex(a => a.Action)
                .HasDatabaseName("IX_AuditLogs_Action");
        }
    }
}
