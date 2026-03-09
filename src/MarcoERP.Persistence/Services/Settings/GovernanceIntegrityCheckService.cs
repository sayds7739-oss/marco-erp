using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Entities.Common;

namespace MarcoERP.Persistence.Services.Settings
{
    /// <summary>
    /// Runs system governance integrity checks.
    /// Phase 5: Version &amp; Integrity Engine — read-only validation only.
    /// 
    /// Checks:
    /// 1. Feature registry vs FeatureVersion completeness
    /// 2. Enabled features with disabled dependencies
    /// 3. CompanyId = null in CompanyAware tables
    /// 4. Database version vs Code version
    /// 5. High Risk features enabled without FeatureChangeLog
    /// 6. Module dependency boundary violations (Phase 8E)
    /// </summary>
    public sealed class GovernanceIntegrityCheckService : IIntegrityCheckService
    {
        private readonly MarcoDbContext _context;
        private readonly Func<string> _codeVersionProvider;
        private readonly IModuleDependencyInspector _dependencyInspector;

        public GovernanceIntegrityCheckService(
            MarcoDbContext context,
            Func<string> codeVersionProvider,
            IModuleDependencyInspector dependencyInspector = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _codeVersionProvider = codeVersionProvider ?? throw new ArgumentNullException(nameof(codeVersionProvider));
            _dependencyInspector = dependencyInspector;
        }

        public async Task<List<IntegrityCheckResult>> RunChecksAsync(CancellationToken ct = default)
        {
            var results = new List<IntegrityCheckResult>();

            results.Add(await CheckFeatureVersionCoverage(ct));
            results.Add(await CheckDependencyConsistency(ct));
            results.Add(await CheckCompanyIdNulls(ct));
            results.Add(await CheckVersionAlignment(ct));
            results.Add(await CheckHighRiskWithoutLog(ct));

            // Phase 8E: Module dependency boundary check
            results.Add(CheckModuleDependencies());

            return results;
        }

        // ── Check 1: Feature registry vs FeatureVersion ──────────

        private async Task<IntegrityCheckResult> CheckFeatureVersionCoverage(CancellationToken ct)
        {
            var featureKeys = await _context.Features
                .Select(f => f.FeatureKey)
                .ToListAsync(ct);

            var versionKeys = await _context.FeatureVersions
                .Select(fv => fv.FeatureKey)
                .ToListAsync(ct);

            var missing = featureKeys.Except(versionKeys).ToList();

            if (missing.Count == 0)
            {
                return new IntegrityCheckResult
                {
                    CheckName = "تغطية إصدارات الميزات",
                    Status = "OK",
                    Message = $"جميع الميزات ({featureKeys.Count}) لها إصدار مسجل."
                };
            }

            return new IntegrityCheckResult
            {
                CheckName = "تغطية إصدارات الميزات",
                Status = "Warning",
                Message = $"ميزات بدون إصدار مسجل: {string.Join("، ", missing)}"
            };
        }

        // ── Check 2: Enabled features with disabled dependencies ─

        private async Task<IntegrityCheckResult> CheckDependencyConsistency(CancellationToken ct)
        {
            var features = await _context.Features.ToListAsync(ct);
            var featureMap = features.ToDictionary(f => f.FeatureKey, f => f.IsEnabled);

            var issues = new List<string>();
            foreach (var f in features.Where(f => f.IsEnabled && !string.IsNullOrWhiteSpace(f.DependsOn)))
            {
                var deps = f.DependsOn.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var dep in deps)
                {
                    if (featureMap.TryGetValue(dep, out bool enabled) && !enabled)
                    {
                        issues.Add($"{f.FeatureKey} ← {dep} (معطّل)");
                    }
                }
            }

            if (issues.Count == 0)
            {
                return new IntegrityCheckResult
                {
                    CheckName = "اتساق التبعيات",
                    Status = "OK",
                    Message = "جميع الميزات المفعلة تبعياتها مفعلة."
                };
            }

            return new IntegrityCheckResult
            {
                CheckName = "اتساق التبعيات",
                Status = "Critical",
                Message = $"ميزات مفعلة بتبعيات معطلة: {string.Join(" | ", issues)}"
            };
        }

        // ── Check 3: CompanyId = null in CompanyAware tables ─────

        private async Task<IntegrityCheckResult> CheckCompanyIdNulls(CancellationToken ct)
        {
            // Get all entity types that inherit from CompanyAwareEntity
            var companyAwareTypes = _context.Model.GetEntityTypes()
                .Where(et => typeof(CompanyAwareEntity).IsAssignableFrom(et.ClrType) && !et.ClrType.IsAbstract)
                .ToList();

            var tablesWithNulls = new List<string>();

            foreach (var entityType in companyAwareTypes)
            {
                var tableName = entityType.GetTableName();
                var schemaName = entityType.GetSchema() ?? "dbo";

                try
                {
                    // Use parameterized SQL identifier quoting (safe because names come from EF Core model, not user input)
                    var quotedTable = $"[{schemaName.Replace("]", "]]")}].[{tableName.Replace("]", "]]")}]";
                    var sql = $"SELECT COUNT(*) FROM {quotedTable} WHERE CompanyId IS NULL";
                    using var cmd = _context.Database.GetDbConnection().CreateCommand();
                    cmd.CommandText = sql;

                    var connectionWasOpened = false;
                    if (cmd.Connection.State != System.Data.ConnectionState.Open)
                    {
                        await cmd.Connection.OpenAsync(ct);
                        connectionWasOpened = true;
                    }

                    try
                    {
                        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
                        if (count > 0)
                        {
                            tablesWithNulls.Add($"{tableName} ({count} سجل)");
                        }
                    }
                    finally
                    {
                        if (connectionWasOpened)
                            await cmd.Connection.CloseAsync();
                    }
                }
                catch (Exception ex)
                {
                    // Log and skip tables that can't be queried (e.g., not yet migrated)
                    System.Diagnostics.Debug.WriteLine($"[GovernanceIntegrity] فشل فحص جدول {tableName}: {ex.Message}");
                }
            }

            if (tablesWithNulls.Count == 0)
            {
                return new IntegrityCheckResult
                {
                    CheckName = "سلامة CompanyId",
                    Status = "OK",
                    Message = $"لا توجد سجلات بدون CompanyId في {companyAwareTypes.Count} جدول."
                };
            }

            return new IntegrityCheckResult
            {
                CheckName = "سلامة CompanyId",
                Status = "Critical",
                Message = $"جداول بها سجلات بدون CompanyId: {string.Join("، ", tablesWithNulls)}"
            };
        }

        // ── Check 4: Database version vs Code version ────────────

        private async Task<IntegrityCheckResult> CheckVersionAlignment(CancellationToken ct)
        {
            var latestDb = await _context.SystemVersions
                .OrderByDescending(v => v.AppliedAt)
                .FirstOrDefaultAsync(ct);

            var dbVersion = latestDb?.VersionNumber ?? "0.0.0";
            var codeVersion = _codeVersionProvider();

            if (dbVersion == codeVersion)
            {
                return new IntegrityCheckResult
                {
                    CheckName = "توافق الإصدارات",
                    Status = "OK",
                    Message = $"إصدار الكود ({codeVersion}) = إصدار قاعدة البيانات ({dbVersion})"
                };
            }

            // Compare versions
            var dbParts = ParseVersion(dbVersion);
            var codeParts = ParseVersion(codeVersion);

            bool dbBehind = CompareVersionParts(dbParts, codeParts) < 0;

            if (dbBehind)
            {
                return new IntegrityCheckResult
                {
                    CheckName = "توافق الإصدارات",
                    Status = "Warning",
                    Message = $"إصدار قاعدة البيانات ({dbVersion}) أقل من إصدار الكود ({codeVersion}) — قد يتطلب تسجيل إصدار جديد."
                };
            }

            return new IntegrityCheckResult
            {
                CheckName = "توافق الإصدارات",
                Status = "Warning",
                Message = $"إصدار قاعدة البيانات ({dbVersion}) ≠ إصدار الكود ({codeVersion})"
            };
        }

        // ── Check 5: High Risk features without FeatureChangeLog ─

        private async Task<IntegrityCheckResult> CheckHighRiskWithoutLog(CancellationToken ct)
        {
            var highRiskEnabled = await _context.Features
                .Where(f => f.IsEnabled && f.RiskLevel == "High")
                .Select(f => new { f.Id, f.FeatureKey })
                .ToListAsync(ct);

            if (highRiskEnabled.Count == 0)
            {
                return new IntegrityCheckResult
                {
                    CheckName = "سجل الميزات عالية الخطورة",
                    Status = "OK",
                    Message = "لا توجد ميزات عالية الخطورة مفعلة حالياً."
                };
            }

            var featureIds = highRiskEnabled.Select(f => f.Id).ToList();
            var loggedFeatureIds = await _context.FeatureChangeLogs
                .Where(cl => featureIds.Contains(cl.FeatureId))
                .Select(cl => cl.FeatureId)
                .Distinct()
                .ToListAsync(ct);

            var noLogFeatures = highRiskEnabled
                .Where(f => !loggedFeatureIds.Contains(f.Id))
                .Select(f => f.FeatureKey)
                .ToList();

            if (noLogFeatures.Count == 0)
            {
                return new IntegrityCheckResult
                {
                    CheckName = "سجل الميزات عالية الخطورة",
                    Status = "OK",
                    Message = $"جميع الميزات عالية الخطورة ({highRiskEnabled.Count}) لها سجل تغيير."
                };
            }

            return new IntegrityCheckResult
            {
                CheckName = "سجل الميزات عالية الخطورة",
                Status = "Warning",
                Message = $"ميزات عالية الخطورة مفعلة بدون سجل تغيير: {string.Join("، ", noLogFeatures)}"
            };
        }

        // ── Helpers ──────────────────────────────────────────────

        private static int[] ParseVersion(string version)
        {
            var parts = version.Split('.');
            var result = new int[3];
            for (int i = 0; i < Math.Min(parts.Length, 3); i++)
            {
                int.TryParse(parts[i], out result[i]);
            }
            return result;
        }

        private static int CompareVersionParts(int[] a, int[] b)
        {
            for (int i = 0; i < 3; i++)
            {
                if (a[i] < b[i]) return -1;
                if (a[i] > b[i]) return 1;
            }
            return 0;
        }

        // ── Check 6: Module dependency boundary violations (Phase 8E) ──

        private IntegrityCheckResult CheckModuleDependencies()
        {
            if (_dependencyInspector == null)
            {
                return new IntegrityCheckResult
                {
                    CheckName = "فحص حدود الوحدات",
                    Status = "Warning",
                    Message = "لم يتم تسجيل مفتش التبعيات — تم تخطي الفحص"
                };
            }

            try
            {
                var violations = _dependencyInspector.ValidateDependencies();

                if (violations == null || violations.Count == 0)
                {
                    return new IntegrityCheckResult
                    {
                        CheckName = "فحص حدود الوحدات",
                        Status = "OK",
                        Message = "جميع التبعيات ضمن الحدود المسموحة"
                    };
                }

                var details = string.Join(" | ",
                    violations.Select(v => v.Message));

                return new IntegrityCheckResult
                {
                    CheckName = "فحص حدود الوحدات",
                    Status = "Warning",
                    Message = $"{violations.Count} تبعية غير مصرح بها: {details}"
                };
            }
            catch (Exception ex)
            {
                return new IntegrityCheckResult
                {
                    CheckName = "فحص حدود الوحدات",
                    Status = "Warning",
                    Message = $"خطأ أثناء فحص التبعيات: {ex.Message}"
                };
            }
        }
    }
}
