using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MarcoERP.Persistence;

#nullable disable

namespace MarcoERP.Persistence.Migrations
{
    [DbContext(typeof(MarcoDbContext))]
    [Migration("20260214170000_Phase1_NegativeStockAndReceiptSettings")]
    public partial class Phase1_NegativeStockAndReceiptSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM SystemSettings WHERE SettingKey = 'AllowNegativeStock')
BEGIN
    INSERT INTO SystemSettings (SettingKey, SettingValue, Description, GroupName, DataType)
    VALUES ('AllowNegativeStock', 'false', N'السماح بالبيع بالسالب للمخزون', N'إعدادات النظام', 'bool');
END

IF NOT EXISTS (SELECT 1 FROM SystemSettings WHERE SettingKey = 'AllowNegativeCashboxBalance')
BEGIN
    INSERT INTO SystemSettings (SettingKey, SettingValue, Description, GroupName, DataType)
    VALUES ('AllowNegativeCashboxBalance', 'false', N'السماح برصيد خزنة سالب', N'إعدادات النظام', 'bool');
END

IF NOT EXISTS (SELECT 1 FROM SystemSettings WHERE SettingKey = 'EnableReceiptPrinting')
BEGIN
    INSERT INTO SystemSettings (SettingKey, SettingValue, Description, GroupName, DataType)
    VALUES ('EnableReceiptPrinting', 'false', N'تفعيل طباعة إيصالات نقطة البيع', N'إعدادات النظام', 'bool');
END

IF NOT EXISTS (SELECT 1 FROM Features WHERE FeatureKey = 'FEATURE_ALLOW_NEGATIVE_STOCK')
BEGIN
    INSERT INTO Features
        (FeatureKey, NameAr, NameEn, Description, IsEnabled, RiskLevel, DependsOn,
         AffectsData, RequiresMigration, AffectsAccounting, AffectsInventory, AffectsReporting, ImpactDescription,
         CreatedAt, CreatedBy)
    VALUES
        ('FEATURE_ALLOW_NEGATIVE_STOCK', N'السماح بالمخزون السالب', 'Allow Negative Stock', N'السماح ببيع المخزون بالسالب عند تفعيله', 0, 'High', 'Inventory,Sales',
         1, 0, 1, 1, 1, N'السماح بالمخزون السالب قد يؤدي لفروقات مخزون وتكلفة', GETUTCDATE(), 'System');
END

IF NOT EXISTS (SELECT 1 FROM Features WHERE FeatureKey = 'FEATURE_ALLOW_NEGATIVE_CASH')
BEGIN
    INSERT INTO Features
        (FeatureKey, NameAr, NameEn, Description, IsEnabled, RiskLevel, DependsOn,
         AffectsData, RequiresMigration, AffectsAccounting, AffectsInventory, AffectsReporting, ImpactDescription,
         CreatedAt, CreatedBy)
    VALUES
        ('FEATURE_ALLOW_NEGATIVE_CASH', N'السماح برصيد خزنة سالب', 'Allow Negative Cash', N'السماح بتحويل يؤدي إلى رصيد خزنة سالب عند تفعيله', 0, 'High', 'Treasury',
         1, 0, 1, 0, 1, N'السماح برصيد خزنة سالب يؤثر على سلامة النقدية', GETUTCDATE(), 'System');
END

IF NOT EXISTS (SELECT 1 FROM Features WHERE FeatureKey = 'FEATURE_RECEIPT_PRINTING')
BEGIN
    INSERT INTO Features
        (FeatureKey, NameAr, NameEn, Description, IsEnabled, RiskLevel, DependsOn,
         AffectsData, RequiresMigration, AffectsAccounting, AffectsInventory, AffectsReporting, ImpactDescription,
         CreatedAt, CreatedBy)
    VALUES
        ('FEATURE_RECEIPT_PRINTING', N'طباعة إيصالات نقطة البيع', 'Receipt Printing', N'تفعيل طباعة إيصالات نقطة البيع', 0, 'Medium', 'POS',
         0, 0, 0, 0, 0, N'تعطيل طباعة الإيصالات لا يؤثر على البيانات', GETUTCDATE(), 'System');
END

DECLARE @advancedProfileId INT = (SELECT TOP 1 Id FROM SystemProfiles WHERE ProfileName = 'Advanced');
IF @advancedProfileId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM ProfileFeatures WHERE ProfileId = @advancedProfileId AND FeatureKey = 'FEATURE_ALLOW_NEGATIVE_STOCK')
        INSERT INTO ProfileFeatures (ProfileId, FeatureKey) VALUES (@advancedProfileId, 'FEATURE_ALLOW_NEGATIVE_STOCK');

    IF NOT EXISTS (SELECT 1 FROM ProfileFeatures WHERE ProfileId = @advancedProfileId AND FeatureKey = 'FEATURE_ALLOW_NEGATIVE_CASH')
        INSERT INTO ProfileFeatures (ProfileId, FeatureKey) VALUES (@advancedProfileId, 'FEATURE_ALLOW_NEGATIVE_CASH');

    IF NOT EXISTS (SELECT 1 FROM ProfileFeatures WHERE ProfileId = @advancedProfileId AND FeatureKey = 'FEATURE_RECEIPT_PRINTING')
        INSERT INTO ProfileFeatures (ProfileId, FeatureKey) VALUES (@advancedProfileId, 'FEATURE_RECEIPT_PRINTING');
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM ProfileFeatures WHERE FeatureKey IN (
    'FEATURE_ALLOW_NEGATIVE_STOCK',
    'FEATURE_ALLOW_NEGATIVE_CASH',
    'FEATURE_RECEIPT_PRINTING'
);

DELETE FROM Features WHERE FeatureKey IN (
    'FEATURE_ALLOW_NEGATIVE_STOCK',
    'FEATURE_ALLOW_NEGATIVE_CASH',
    'FEATURE_RECEIPT_PRINTING'
);

DELETE FROM SystemSettings WHERE SettingKey IN (
    'AllowNegativeStock',
    'AllowNegativeCashboxBalance',
    'EnableReceiptPrinting'
);
");
        }
    }
}
