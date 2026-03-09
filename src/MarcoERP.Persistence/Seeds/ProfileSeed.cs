using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Seeds
{
    /// <summary>
    /// Seeds complexity profiles and their feature mappings.
    /// Additive — only inserts profiles/mappings that do not already exist,
    /// so new profiles added in updates are automatically seeded.
    /// Phase 3: Progressive Complexity Layer.
    /// </summary>
    public static class ProfileSeed
    {
        public static async Task SeedAsync(MarcoDbContext context)
        {
            // ── Ensure Profiles Exist ─────────────────────────────
            var existingProfileNames = await context.SystemProfiles
                .Select(p => p.ProfileName)
                .ToListAsync();
            var existingProfileNameSet = new HashSet<string>(existingProfileNames);

            var simple = existingProfileNameSet.Contains("Simple")
                ? await context.SystemProfiles.FirstAsync(p => p.ProfileName == "Simple")
                : new SystemProfile("Simple", "بروفايل بسيط — المحاسبة والمبيعات والمخزون والخزينة وإدارة المستخدمين", false);

            var standard = existingProfileNameSet.Contains("Standard")
                ? await context.SystemProfiles.FirstAsync(p => p.ProfileName == "Standard")
                : new SystemProfile("Standard", "بروفايل قياسي — يشمل المشتريات والتقارير ونقاط البيع", true);

            var advanced = existingProfileNameSet.Contains("Advanced")
                ? await context.SystemProfiles.FirstAsync(p => p.ProfileName == "Advanced")
                : new SystemProfile("Advanced", "بروفايل متقدم — جميع الميزات مفعّلة بما يشمل المحاسبة المتقدمة", false);

            var newProfiles = new List<SystemProfile>();
            if (!existingProfileNameSet.Contains("Simple")) newProfiles.Add(simple);
            if (!existingProfileNameSet.Contains("Standard")) newProfiles.Add(standard);
            if (!existingProfileNameSet.Contains("Advanced")) newProfiles.Add(advanced);

            if (newProfiles.Any())
            {
                await context.SystemProfiles.AddRangeAsync(newProfiles);
                await context.SaveChangesAsync();
            }

            // ── Ensure Profile-Feature Mappings Exist ─────────────
            var existingMappings = await context.ProfileFeatures
                .Select(pf => new { pf.ProfileId, pf.FeatureKey })
                .ToListAsync();
            var existingMappingSet = new HashSet<string>(
                existingMappings.Select(m => $"{m.ProfileId}|{m.FeatureKey}"));

            var allMappings = new List<ProfileFeature>();

            // Simple: basic operations
            foreach (var key in new[] { "Accounting", "Inventory", "Sales", "Treasury", "UserManagement" })
            {
                if (!existingMappingSet.Contains($"{simple.Id}|{key}"))
                    allMappings.Add(new ProfileFeature(simple.Id, key));
            }

            // Standard: Simple + Purchases, POS, Reporting
            foreach (var key in new[] { "Accounting", "Inventory", "Sales", "Treasury", "UserManagement", "Purchases", "POS", "Reporting" })
            {
                if (!existingMappingSet.Contains($"{standard.Id}|{key}"))
                    allMappings.Add(new ProfileFeature(standard.Id, key));
            }

            // Advanced: all features
            foreach (var key in new[] { "Accounting", "Inventory", "Sales", "Treasury", "UserManagement", "Purchases", "POS", "Reporting", "FEATURE_ALLOW_NEGATIVE_STOCK", "FEATURE_ALLOW_NEGATIVE_CASH", "FEATURE_RECEIPT_PRINTING" })
            {
                if (!existingMappingSet.Contains($"{advanced.Id}|{key}"))
                    allMappings.Add(new ProfileFeature(advanced.Id, key));
            }

            if (allMappings.Any())
            {
                await context.ProfileFeatures.AddRangeAsync(allMappings);
                await context.SaveChangesAsync();
            }
        }
    }
}
