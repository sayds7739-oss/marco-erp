using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Persistence.Seeds
{
    /// <summary>
    /// Seeds the Chart of Accounts with system accounts defined in PHASE2_ACCOUNTING_CORE_DESIGN §1.3.
    /// SEED-01: Generated on first application run only.
    /// SEED-02: Idempotent — checks existence before inserting.
    /// SEED-03: All seeded accounts carry IsSystemAccount = true.
    /// SEED-04: All seeded accounts carry IsActive = true.
    /// SEED-05: Runs inside a transaction — all or nothing.
    /// SEED-06: Implemented in Persistence layer (Seeds/ folder).
    /// </summary>
    public static class SystemAccountSeed
    {
        /// <summary>
        /// Seeds all system accounts if they don't already exist.
        /// Inserts level by level (1 → 2 → 3 → 4) so that parent IDs are resolved from the DB.
        /// Must be called after database migration.
        /// </summary>
        public static async Task SeedAsync(MarcoDbContext context)
        {
            // SEED-02: If any system accounts exist, skip seeding
            var hasSystemAccounts = await context.Accounts
                .IgnoreQueryFilters()
                .AnyAsync(a => a.IsSystemAccount);

            if (hasSystemAccounts)
                return;

            var definitions = GetAccountDefinitions();

            // SEED-05: Transaction — all or nothing
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                // Insert level by level so FK references resolve correctly
                for (int level = 1; level <= 4; level++)
                {
                    var levelDefs = definitions.Where(d => d.Level == level).ToList();

                    foreach (var def in levelDefs)
                    {
                        int? parentId = null;

                        if (def.ParentCode != null)
                        {
                            // Look up parent by code from the DB (already saved in prior level)
                            var parent = await context.Accounts
                                .IgnoreQueryFilters()
                                .FirstAsync(a => a.AccountCode == def.ParentCode);
                            parentId = parent.Id;
                        }

                        var account = new Account(
                            def.Code,
                            def.NameAr,
                            def.NameEn,
                            def.Type,
                            parentId,
                            def.Level,
                            isSystemAccount: true,
                            "EGP");

                        // Mark non-leaf accounts (accounts that have children in the chart)
                        if (def.IsParent)
                        {
                            account.MarkAsParent();
                        }

                        await context.Accounts.AddAsync(account);
                    }

                    await context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ─── Account Definition Record ─────────────────────────

        private sealed record AccountDef(
            string Code,
            string NameAr,
            string NameEn,
            AccountType Type,
            string ParentCode,
            int Level,
            bool IsParent);

        /// <summary>
        /// Returns the flat list of all system account definitions per design doc §1.3.
        /// IsParent = true means the account has children and AllowPosting should be false.
        /// </summary>
        private static List<AccountDef> GetAccountDefinitions()
        {
            return new List<AccountDef>
            {
                // ═══════════════ ASSETS (1xxx) ═══════════════
                // Level 1
                new("1000", "الأصول", "Assets", AccountType.Asset, null, 1, IsParent: true),

                // Level 2
                new("1100", "الأصول المتداولة", "Current Assets", AccountType.Asset, "1000", 2, IsParent: true),
                new("1200", "الأصول غير المتداولة", "Non-Current Assets", AccountType.Asset, "1000", 2, IsParent: false),

                // Level 3
                new("1110", "النقدية والبنوك", "Cash and Banks", AccountType.Asset, "1100", 3, IsParent: true),
                new("1120", "المدينون", "Accounts Receivable", AccountType.Asset, "1100", 3, IsParent: true),
                new("1130", "المخزون", "Inventory", AccountType.Asset, "1100", 3, IsParent: true),
                new("1140", "ضريبة القيمة المضافة — مدخلات", "VAT Input", AccountType.Asset, "1100", 3, IsParent: true),

                // Level 4 (Leaf — AllowPosting = true)
                new("1111", "الصندوق الرئيسي", "Main Cash", AccountType.Asset, "1110", 4, IsParent: false),
                new("1112", "البنك / بطاقات الدفع", "Bank / Card Payments", AccountType.Asset, "1110", 4, IsParent: false),
                new("1121", "المدينون — ذمم تجارية", "AR — Trade Receivables", AccountType.Asset, "1120", 4, IsParent: false),
                new("1131", "المخزون — المستودع الرئيسي", "Inventory — Main Warehouse", AccountType.Asset, "1130", 4, IsParent: false),
                new("1141", "ضريبة مدخلات مستحقة", "VAT Input Receivable", AccountType.Asset, "1140", 4, IsParent: false),

                // ═══════════════ LIABILITIES (2xxx) ═══════════════
                new("2000", "الخصوم", "Liabilities", AccountType.Liability, null, 1, IsParent: true),

                new("2100", "الخصوم المتداولة", "Current Liabilities", AccountType.Liability, "2000", 2, IsParent: true),

                new("2110", "الدائنون", "Accounts Payable", AccountType.Liability, "2100", 3, IsParent: true),
                new("2120", "ضريبة القيمة المضافة — مخرجات", "VAT Output", AccountType.Liability, "2100", 3, IsParent: true),
                new("2130", "مصروفات مستحقة", "Accrued Expenses", AccountType.Liability, "2100", 3, IsParent: true),

                new("2111", "الدائنون — ذمم تجارية", "AP — Trade Payables", AccountType.Liability, "2110", 4, IsParent: false),
                new("2121", "ضريبة مخرجات مستحقة", "VAT Output Payable", AccountType.Liability, "2120", 4, IsParent: false),
                new("2131", "مصروفات مستحقة — عام", "Accrued Expenses — General", AccountType.Liability, "2130", 4, IsParent: false),

                // Commission Payable (under Current Liabilities)
                new("2300", "العمولات المستحقة", "Commissions Payable", AccountType.Liability, "2000", 2, IsParent: true),
                new("2301", "العمولات المستحقة — مندوبين", "Commissions Payable — Sales Rep", AccountType.Liability, "2300", 3, IsParent: false),

                // ═══════════════ EQUITY (3xxx) ═══════════════
                new("3000", "حقوق الملكية", "Equity", AccountType.Equity, null, 1, IsParent: true),

                new("3100", "رأس مال المالك", "Owner's Equity", AccountType.Equity, "3000", 2, IsParent: true),

                new("3110", "رأس المال المدفوع", "Paid-in Capital", AccountType.Equity, "3100", 3, IsParent: true),
                new("3120", "الأرباح المحتجزة", "Retained Earnings", AccountType.Equity, "3100", 3, IsParent: true),

                new("3111", "رأس المال", "Capital", AccountType.Equity, "3110", 4, IsParent: false),
                new("3121", "الأرباح المحتجزة — الحالية", "Retained Earnings — Current", AccountType.Equity, "3120", 4, IsParent: false),

                // ═══════════════ REVENUE (4xxx) ═══════════════
                new("4000", "الإيرادات", "Revenue", AccountType.Revenue, null, 1, IsParent: true),

                new("4100", "إيرادات المبيعات", "Sales Revenue", AccountType.Revenue, "4000", 2, IsParent: true),

                new("4110", "مبيعات المنتجات", "Product Sales", AccountType.Revenue, "4100", 3, IsParent: true),
                new("4120", "مردودات المبيعات", "Sales Returns", AccountType.Revenue, "4100", 3, IsParent: true),
                new("4130", "خصومات المبيعات", "Sales Discounts", AccountType.Revenue, "4100", 3, IsParent: true),

                new("4111", "المبيعات — عام", "Sales — General", AccountType.Revenue, "4110", 4, IsParent: false),
                new("4112", "فائض مخزني", "Inventory Surplus", AccountType.Revenue, "4110", 4, IsParent: false),
                new("4121", "مردودات المبيعات — عام", "Sales Returns — General", AccountType.Revenue, "4120", 4, IsParent: false),
                new("4131", "خصومات المبيعات — عام", "Sales Discounts — General", AccountType.Revenue, "4130", 4, IsParent: false),

                // ═══════════════ COGS (5xxx) ═══════════════
                new("5000", "تكلفة البضاعة المباعة", "Cost of Goods Sold", AccountType.COGS, null, 1, IsParent: true),

                new("5100", "تكلفة المنتجات", "COGS — Products", AccountType.COGS, "5000", 2, IsParent: true),

                new("5110", "تكلفة مباشرة", "COGS — Direct", AccountType.COGS, "5100", 3, IsParent: true),

                new("5111", "تكلفة البضاعة المباعة — عام", "COGS — General", AccountType.COGS, "5110", 4, IsParent: false),
                new("5112", "عجز وتالف مخزني", "Inventory Shortage & Damage", AccountType.COGS, "5110", 4, IsParent: false),

                // ═══════════════ EXPENSES (6xxx) ═══════════════
                new("6000", "المصروفات", "Expenses", AccountType.Expense, null, 1, IsParent: true),

                new("6100", "مصروفات التشغيل", "Operating Expenses", AccountType.Expense, "6000", 2, IsParent: true),

                new("6110", "الرواتب والأجور", "Salaries & Wages", AccountType.Expense, "6100", 3, IsParent: true),
                new("6120", "الإيجار", "Rent", AccountType.Expense, "6100", 3, IsParent: true),
                new("6130", "المرافق", "Utilities", AccountType.Expense, "6100", 3, IsParent: true),
                new("6140", "فروق التقريب", "Rounding Differences", AccountType.Expense, "6100", 3, IsParent: true),
                new("6150", "تسويات المخزون", "Inventory Adjustments", AccountType.Expense, "6100", 3, IsParent: true),

                new("6111", "الرواتب", "Salaries", AccountType.Expense, "6110", 4, IsParent: false),
                new("6121", "إيجار المكتب", "Office Rent", AccountType.Expense, "6120", 4, IsParent: false),
                new("6131", "المرافق — عام", "Utilities — General", AccountType.Expense, "6130", 4, IsParent: false),
                new("6141", "حساب التقريب", "Rounding Account", AccountType.Expense, "6140", 4, IsParent: false),
                new("6151", "حساب تسوية المخزون", "Inventory Adjustment Account", AccountType.Expense, "6150", 4, IsParent: false),

                // Commission Expense (under Expenses)
                new("6200", "مصروفات العمولات", "Commission Expenses", AccountType.Expense, "6000", 2, IsParent: true),
                new("6201", "مصروف العمولات — مندوبين", "Commission Expense — Sales Rep", AccountType.Expense, "6200", 3, IsParent: false),

                // ═══════════════ OTHER INCOME (7xxx) ═══════════════
                new("7000", "إيرادات أخرى", "Other Income", AccountType.OtherIncome, null, 1, IsParent: true),

                new("7100", "إيرادات متنوعة", "Miscellaneous Income", AccountType.OtherIncome, "7000", 2, IsParent: true),

                new("7110", "إيرادات أخرى — عام", "Other Income — General", AccountType.OtherIncome, "7100", 3, IsParent: true),

                new("7111", "إيرادات أخرى — متنوع", "Other Income — Misc", AccountType.OtherIncome, "7110", 4, IsParent: false),

                // Cash Over/Short (POS session close variance)
                new("7200", "فروقات الصندوق", "Cash Over/Short", AccountType.OtherIncome, "7000", 2, IsParent: true),
                new("7201", "فروقات الصندوق", "Cash Over/Short — POS", AccountType.OtherIncome, "7200", 3, IsParent: false),

                // ═══════════════ OTHER EXPENSES (8xxx) ═══════════════
                new("8000", "مصروفات أخرى", "Other Expenses", AccountType.OtherExpense, null, 1, IsParent: true),

                new("8100", "مصروفات غير تشغيلية", "Non-Operating Expenses", AccountType.OtherExpense, "8000", 2, IsParent: true),

                new("8110", "مصروفات أخرى — عام", "Other Expenses — General", AccountType.OtherExpense, "8100", 3, IsParent: true),

                new("8111", "مصروفات أخرى — متنوع", "Other Expenses — Misc", AccountType.OtherExpense, "8110", 4, IsParent: false),
            };
        }
    }
}
