namespace MarcoERP.Domain.Entities.Common
{
    /// <summary>
    /// Marker interface for immutable financial records that must never be deleted
    /// (neither hard-deleted nor soft-deleted).
    /// 
    /// Applied to: journal entry lines, invoice lines, return lines,
    /// inventory movements, and warehouse stock balance records.
    /// 
    /// The HardDeleteProtectionInterceptor blocks any DbSet.Remove() call
    /// targeting entities implementing this interface.
    /// 
    /// RECORD_PROTECTION_POLICY: Financial transaction records are append-only.
    /// Corrections are made via reversal entries, not deletion.
    /// </summary>
    public interface IImmutableFinancialRecord
    {
    }
}
