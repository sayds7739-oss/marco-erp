namespace MarcoERP.Domain.Enums
{
    /// <summary>
    /// Phase 8A: Formal module definitions for dependency graph analysis.
    /// Each value represents a logical module boundary in the ERP system.
    /// </summary>
    public enum SystemModule
    {
        Sales,
        Inventory,
        Accounting,
        Purchases,
        Treasury,
        Reporting,
        Security,
        Settings,
        Governance,
        POS,
        /// <summary>Cross-cutting concerns (UnitOfWork, CurrentUser, DateTimeProvider, Validators).</summary>
        Common
    }
}
