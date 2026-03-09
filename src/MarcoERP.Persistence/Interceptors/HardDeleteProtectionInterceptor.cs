using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Interceptors
{
    /// <summary>
    /// EF Core interceptor that prevents hard deletion of:
    ///   1. Any entity inheriting SoftDeletableEntity (must use SoftDelete() instead).
    ///   2. Any entity implementing IImmutableFinancialRecord (never deletable at all).
    /// Enforces RECORD_PROTECTION_POLICY: financial and business records must never be removed.
    /// </summary>
    public sealed class HardDeleteProtectionInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            ThrowIfHardDelete(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfHardDelete(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void ThrowIfHardDelete(DbContext context)
        {
            if (context == null) return;

            var deletedEntries = context.ChangeTracker
                .Entries()
                .Where(e => e.State == EntityState.Deleted)
                .ToList();

            if (deletedEntries.Count == 0)
                return;

            if (IsProductionModeEnabled(context))
            {
                var prohibitedInProduction = deletedEntries
                    .Where(e => !IsDraftEditableAggregateLine(e.Entity))
                    .ToList();

                if (prohibitedInProduction.Count == 0)
                    return;

                var entityTypes = string.Join(", ", prohibitedInProduction
                    .Select(e => e.Entity.GetType().Name)
                    .Distinct());

                throw new InvalidOperationException(
                    "الحذف النهائي ممنوع أثناء تفعيل وضع الإنتاج. " +
                    $"الأنواع المتأثرة: {entityTypes}");
            }

            // ── Guard 1: SoftDeletableEntity descendants ────────────────
            var softDeletableEntries = context.ChangeTracker
                .Entries<SoftDeletableEntity>()
                .Where(e => e.State == EntityState.Deleted)
                .ToList();

            if (softDeletableEntries.Count > 0)
            {
                var entityTypes = string.Join(", ",
                    softDeletableEntries.Select(e => e.Entity.GetType().Name).Distinct());

                throw new InvalidOperationException(
                    $"الحذف النهائي للسجلات القابلة للحذف الناعم ممنوع (سياسة حماية السجلات). " +
                    $"استخدم SoftDelete() بدلاً من ذلك. الأنواع المتأثرة: {entityTypes}");
            }

            // ── Guard 2: IImmutableFinancialRecord entities ─────────────
            var immutableEntries = context.ChangeTracker
                .Entries()
                .Where(e => e.State == EntityState.Deleted
                         && e.Entity is IImmutableFinancialRecord
                         && !IsDraftEditableAggregateLine(e.Entity))
                .ToList();

            if (immutableEntries.Count > 0)
            {
                var entityTypes = string.Join(", ",
                    immutableEntries.Select(e => e.Entity.GetType().Name).Distinct());

                throw new InvalidOperationException(
                    $"حذف السجلات المالية الثابتة ممنوع منعًا باتًا (سياسة حماية السجلات). " +
                    $"هذه السجلات للإضافة فقط ولا يمكن إزالتها. الأنواع المتأثرة: {entityTypes}");
            }
        }

        private static bool IsDraftEditableAggregateLine(object entity)
        {
            return entity is SalesInvoiceLine
                or PurchaseInvoiceLine
                or SalesReturnLine
                or PurchaseReturnLine
                or InventoryAdjustmentLine;
        }

        private static bool IsProductionModeEnabled(DbContext context)
        {
            try
            {
                var tracked = context.ChangeTracker
                    .Entries<SystemSetting>()
                    .FirstOrDefault(e => string.Equals(e.Entity.SettingKey, "IsProductionMode", StringComparison.OrdinalIgnoreCase));

                var rawValue = tracked?.Entity?.SettingValue;

                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    rawValue = context.Set<SystemSetting>()
                        .AsNoTracking()
                        .Where(s => s.SettingKey == "IsProductionMode")
                        .Select(s => s.SettingValue)
                        .FirstOrDefault();
                }

                if (string.IsNullOrWhiteSpace(rawValue))
                    return true;

                return bool.TryParse(rawValue, out var parsed) ? parsed : true;
            }
            catch
            {
                return true;
            }
        }
    }
}
