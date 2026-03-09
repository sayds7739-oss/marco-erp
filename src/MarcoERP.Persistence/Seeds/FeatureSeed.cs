using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Seeds
{
    /// <summary>
    /// Seeds default features for the Feature Governance Engine.
    /// Additive — only inserts features whose keys do not already exist,
    /// so new features added in updates are automatically seeded.
    /// Phase 2: Feature Governance Engine.
    /// </summary>
    public static class FeatureSeed
    {
        public static async Task SeedAsync(MarcoDbContext context)
        {
            var allFeatures = new Feature[]
            {
                new("Accounting",       "المحاسبة",         "Accounting",         "النظام المحاسبي الأساسي (قيود - حسابات - سنوات مالية)", true,  "High"),
                new("Inventory",        "المخزون",          "Inventory",          "إدارة المخازن والأصناف والوحدات",                       true,  "Medium"),
                new("Sales",            "المبيعات",         "Sales",              "فواتير البيع والمرتجعات وعروض الأسعار",                 true,  "Medium", "Accounting,Inventory"),
                new("Purchases",        "المشتريات",        "Purchases",          "فواتير الشراء والمرتجعات وعروض الأسعار",                true,  "Medium", "Accounting,Inventory"),
                new("Treasury",         "الخزينة",          "Treasury",           "الصناديق والبنوك وسندات القبض والصرف",                  true,  "Medium", "Accounting"),
                new("POS",              "نقاط البيع",       "Point of Sale",      "شاشة نقاط البيع السريعة",                               true,  "Low",    "Sales,Treasury"),
                new("Reporting",        "التقارير",         "Reporting",          "التقارير المالية والإدارية",                             true,  "Low",    "Accounting"),
                new("UserManagement",   "إدارة المستخدمين", "User Management",    "إدارة المستخدمين والأدوار والصلاحيات",                  true,  "High"),
                new("FEATURE_ALLOW_NEGATIVE_STOCK", "السماح بالمخزون السالب", "Allow Negative Stock", "السماح ببيع المخزون بالسالب عند تفعيله", false, "High", "Inventory,Sales"),
                new("FEATURE_ALLOW_NEGATIVE_CASH", "السماح برصيد خزنة سالب", "Allow Negative Cash", "السماح بتحويل يؤدي إلى رصيد خزنة سالب عند تفعيله", false, "High", "Treasury"),
                new("FEATURE_RECEIPT_PRINTING", "طباعة إيصالات نقطة البيع", "Receipt Printing", "تفعيل طباعة إيصالات نقطة البيع", false, "Medium", "POS"),
            };

            // Phase 4: Set impact analysis metadata
            allFeatures[0].SetImpactMetadata(affectsData: true,  requiresMigration: false, affectsAccounting: true,  affectsInventory: false, affectsReporting: true,  "تعطيل المحاسبة يؤثر على كل القيود والتقارير المالية");
            allFeatures[1].SetImpactMetadata(affectsData: true,  requiresMigration: false, affectsAccounting: false, affectsInventory: true,  affectsReporting: true,  "تعطيل المخزون يؤثر على أرصدة الأصناف وحركات المستودعات");
            allFeatures[2].SetImpactMetadata(affectsData: true,  requiresMigration: false, affectsAccounting: true,  affectsInventory: true,  affectsReporting: true,  "تعطيل المبيعات يؤثر على القيود المحاسبية وحركات المخزون");
            allFeatures[3].SetImpactMetadata(affectsData: true,  requiresMigration: false, affectsAccounting: true,  affectsInventory: true,  affectsReporting: true,  "تعطيل المشتريات يؤثر على القيود المحاسبية وحركات المخزون");
            allFeatures[4].SetImpactMetadata(affectsData: true,  requiresMigration: false, affectsAccounting: true,  affectsInventory: false, affectsReporting: true,  "تعطيل الخزينة يؤثر على سندات القبض والصرف والحسابات البنكية");
            allFeatures[5].SetImpactMetadata(affectsData: false, requiresMigration: false, affectsAccounting: true,  affectsInventory: true,  affectsReporting: false, "نقاط البيع واجهة فقط — التعطيل آمن");
            allFeatures[6].SetImpactMetadata(affectsData: false, requiresMigration: false, affectsAccounting: false, affectsInventory: false, affectsReporting: true,  "تعطيل التقارير يخفي الشاشات فقط — البيانات لا تتأثر");
            allFeatures[7].SetImpactMetadata(affectsData: true,  requiresMigration: false, affectsAccounting: false, affectsInventory: false, affectsReporting: false, "تعطيل إدارة المستخدمين يمنع تعديل الصلاحيات والأدوار");
            allFeatures[8].SetImpactMetadata(affectsData: true,  requiresMigration: false, affectsAccounting: true,  affectsInventory: true,  affectsReporting: true,  "السماح بالمخزون السالب قد يؤدي لفروقات مخزون وتكلفة");
            allFeatures[9].SetImpactMetadata(affectsData: true,  requiresMigration: false, affectsAccounting: true,  affectsInventory: false, affectsReporting: true,  "السماح برصيد خزنة سالب يؤثر على سلامة النقدية");
            allFeatures[10].SetImpactMetadata(affectsData: false, requiresMigration: false, affectsAccounting: false, affectsInventory: false, affectsReporting: false, "تعطيل طباعة الإيصالات لا يؤثر على البيانات");

            // Additive: only add features whose keys don't already exist
            var existingKeys = await context.Features
                .Select(f => f.FeatureKey)
                .ToListAsync();
            var existingKeySet = new System.Collections.Generic.HashSet<string>(existingKeys);

            var featuresToAdd = allFeatures
                .Where(f => !existingKeySet.Contains(f.FeatureKey))
                .ToList();

            if (featuresToAdd.Any())
            {
                await context.Features.AddRangeAsync(featuresToAdd);
                await context.SaveChangesAsync();
            }
        }
    }
}
