using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Settings;

namespace MarcoERP.Persistence.Seeds
{
    /// <summary>
    /// Seeds default system settings per v1.1 Phase 5D.
    /// Additive — only inserts settings whose keys do not already exist,
    /// so new settings added in updates are automatically seeded.
    /// </summary>
    public static class SystemSettingSeed
    {
        public static async Task SeedAsync(MarcoDbContext context)
        {
            var allDefinitions = GetDefinitions();

            // Get existing keys in one query
            var existingKeys = await context.SystemSettings
                .Select(s => s.SettingKey)
                .ToListAsync();

            var existingKeySet = new HashSet<string>(existingKeys);

            var newSettings = allDefinitions
                .Where(s => !existingKeySet.Contains(s.SettingKey))
                .ToList();

            if (newSettings.Count == 0)
                return;

            await context.SystemSettings.AddRangeAsync(newSettings);
            await context.SaveChangesAsync();
        }

        private static List<SystemSetting> GetDefinitions() => new()
        {
            // ── حسابات افتراضية ─────────────────────────────
            new("DefaultInventoryAccountId", "", "حساب المخزون الافتراضي", "حسابات افتراضية", "int"),
            new("DefaultRevenueAccountId", "", "حساب الإيرادات الافتراضي", "حسابات افتراضية", "int"),
            new("DefaultCOGSAccountId", "", "حساب تكلفة المبيعات الافتراضي", "حسابات افتراضية", "int"),
            new("DefaultVatInputAccountId", "", "حساب ضريبة المدخلات", "حسابات افتراضية", "int"),
            new("DefaultVatOutputAccountId", "", "حساب ضريبة المخرجات", "حسابات افتراضية", "int"),
            new("DefaultCashboxId", "", "الصندوق الافتراضي", "حسابات افتراضية", "int"),
            new("DefaultCustomerAccountId", "", "حساب العملاء الافتراضي", "حسابات افتراضية", "int"),
            new("DefaultSupplierAccountId", "", "حساب الموردين الافتراضي", "حسابات افتراضية", "int"),

            // ── تنسيقات الترقيم ─────────────────────────────
            new("InvoiceNumberFormat", "INV-{0:yyyyMM}-{1:D4}", "تنسيق رقم الفاتورة", "تنسيقات الترقيم", "string"),
            new("JournalNumberFormat", "JV-{0:D6}", "تنسيق رقم القيد", "تنسيقات الترقيم", "string"),

            // ── معلومات الشركة ─────────────────────────────
            new("CompanyName", "شركة ماركو", "اسم الشركة", "معلومات الشركة", "string"),
            new("CompanyNameEn", "Marco Company", "اسم الشركة بالإنجليزية", "معلومات الشركة", "string"),
            new("CompanyAddress", "", "عنوان الشركة", "معلومات الشركة", "string"),
            new("CompanyPhone", "", "هاتف الشركة", "معلومات الشركة", "string"),
            new("CompanyEmail", "", "البريد الإلكتروني للشركة", "معلومات الشركة", "string"),
            new("CompanyTaxNumber", "", "الرقم الضريبي", "معلومات الشركة", "string"),
            new("CompanyLogo", "", "مسار شعار الشركة", "معلومات الشركة", "string"),

            // ── إعدادات مالية ────────────────────────────────
            new("CurrencySymbol", "ج.م", "رمز العملة", "إعدادات مالية", "string"),
            new("CurrencyCode", "EGP", "كود العملة", "إعدادات مالية", "string"),
            new("FinancialPrecision", "2", "دقة المبالغ المالية (عدد الكسور)", "إعدادات مالية", "int"),
            new("CostPrecision", "4", "دقة التكلفة (عدد الكسور)", "إعدادات مالية", "int"),
            new("VatRate", "14", "نسبة الضريبة الافتراضية %", "إعدادات مالية", "decimal"),

            // ── إعدادات النظام ────────────────────────────────
            new("MaxLoginAttempts", "5", "عدد محاولات الدخول الفاشلة قبل القفل", "إعدادات النظام", "int"),
            new("SessionTimeoutMinutes", "30", "مدة الجلسة بالدقائق", "إعدادات النظام", "int"),
            new("DefaultPageSize", "50", "عدد السجلات الافتراضي بالصفحة", "إعدادات النظام", "int"),
            new("IsProductionMode", "true", "تفعيل وضع الإنتاج الصارم", "إعدادات النظام", "bool"),
            // DEPRECATED: Duplicated in Feature Governance Engine — prefer FeatureKeys.AllowNegativeStock
            new("AllowNegativeStock", "false", "السماح بالبيع بالسالب للمخزون", "إعدادات النظام", "bool"),
            // DEPRECATED: Duplicated in Feature Governance Engine — prefer FeatureKeys.AllowNegativeCash
            new("AllowNegativeCashboxBalance", "false", "السماح برصيد خزنة سالب", "إعدادات النظام", "bool"),
            // DEPRECATED: Duplicated in Feature Governance Engine — prefer FeatureKeys.ReceiptPrinting
            new("EnableReceiptPrinting", "false", "تفعيل طباعة إيصالات نقطة البيع", "إعدادات النظام", "bool"),

            // ── إعدادات البريد الإلكتروني ────────────────────
            new("SMTP_Host", "", "خادم SMTP", "إعدادات البريد الإلكتروني", "string"),
            new("SMTP_Port", "587", "منفذ SMTP", "إعدادات البريد الإلكتروني", "string"),
            new("SMTP_Username", "", "اسم المستخدم SMTP", "إعدادات البريد الإلكتروني", "string"),
            new("SMTP_Password", "", "كلمة المرور SMTP", "إعدادات البريد الإلكتروني", "string"),
            new("SMTP_FromAddress", "", "عنوان المرسل", "إعدادات البريد الإلكتروني", "string"),
            new("SMTP_UseSsl", "True", "استخدام SSL", "إعدادات البريد الإلكتروني", "bool"),
        };
    }
}
