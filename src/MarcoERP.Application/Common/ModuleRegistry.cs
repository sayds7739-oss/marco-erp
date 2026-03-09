using System.Collections.Generic;
using System.Linq;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.Common
{
    /// <summary>
    /// Phase 8B: Static registry of all module dependency rules.
    /// Defines which modules are allowed to depend on which other modules.
    /// Common module (UnitOfWork, CurrentUser, DateTimeProvider, Validators) is implicitly allowed everywhere.
    /// </summary>
    public static class ModuleRegistry
    {
        /// <summary>Official module dependency rules.</summary>
        public static IReadOnlyList<ModuleDefinition> Definitions { get; } = new List<ModuleDefinition>
        {
            // ── Sales depends on Inventory, Accounting, Treasury ──
            new ModuleDefinition(SystemModule.Sales,
                SystemModule.Inventory,
                SystemModule.Accounting,
                SystemModule.Treasury),

            // ── Inventory depends on Accounting (for journal entries on adjustments) ──
            new ModuleDefinition(SystemModule.Inventory,
                SystemModule.Accounting),

            // ── Accounting has no module dependencies (foundation layer) ──
            new ModuleDefinition(SystemModule.Accounting),

            // ── Purchases depends on Inventory, Accounting, Treasury ──
            new ModuleDefinition(SystemModule.Purchases,
                SystemModule.Inventory,
                SystemModule.Accounting,
                SystemModule.Treasury),

            // ── Treasury depends on Accounting, Sales, Purchases ──
            new ModuleDefinition(SystemModule.Treasury,
                SystemModule.Accounting,
                SystemModule.Sales,
                SystemModule.Purchases),

            // ── Reporting is read-only — may query any business module ──
            new ModuleDefinition(SystemModule.Reporting,
                SystemModule.Sales,
                SystemModule.Purchases,
                SystemModule.Inventory,
                SystemModule.Accounting,
                SystemModule.Treasury),

            // ── Security has no business module dependencies ──
            new ModuleDefinition(SystemModule.Security),

            // ── Settings may query Security (roles, users) ──
            new ModuleDefinition(SystemModule.Settings,
                SystemModule.Security),

            // ── Governance may inspect everything ──
            new ModuleDefinition(SystemModule.Governance,
                SystemModule.Sales,
                SystemModule.Purchases,
                SystemModule.Inventory,
                SystemModule.Accounting,
                SystemModule.Treasury,
                SystemModule.Reporting,
                SystemModule.Security,
                SystemModule.Settings),

            // ── POS depends on Sales, Inventory, Accounting, Treasury ──
            new ModuleDefinition(SystemModule.POS,
                SystemModule.Sales,
                SystemModule.Inventory,
                SystemModule.Accounting,
                SystemModule.Treasury),

            // ── Common has no dependencies ──
            new ModuleDefinition(SystemModule.Common),
        };

        /// <summary>Find the definition for a given module.</summary>
        public static ModuleDefinition GetDefinition(SystemModule module)
            => Definitions.FirstOrDefault(d => d.Module == module);
    }
}
