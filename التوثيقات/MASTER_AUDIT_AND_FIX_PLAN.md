# MarcoERP — التقرير الشامل للفحص وخطة الإصلاح

**التاريخ:** 2026-02-14  
**النطاق:** فحص تشريحي كامل — كل ملف، كل خدمة، كل طبقة، كل سطر كود  
**الإصدار الحالي:** v1.1.0  
**المنصة:** .NET 8 / WPF / EF Core / SQL Server

---

## الفهرس

1. [ملخص تنفيذي](#1-ملخص-تنفيذي)
2. [طبقة Domain — المشاكل](#2-طبقة-domain)
3. [طبقة Application — المشاكل](#3-طبقة-application)
4. [طبقة Persistence — المشاكل](#4-طبقة-persistence)
5. [طبقة Infrastructure — المشاكل](#5-طبقة-infrastructure)
6. [طبقة WPF UI — المشاكل](#6-طبقة-wpf-ui)
7. [نظام الحوكمة — المشاكل](#7-نظام-الحوكمة)
8. [التقارير — تغطية وفجوات](#8-التقارير)
9. [الخزينة السريعة مقابل الخزينة الكاملة](#9-الخزينة-السريعة-vs-الكاملة)
10. [نوافذ الحوار — فحص التصميم](#10-نوافذ-الحوار)
11. [التصميم والألوان والتخطيط](#11-التصميم-والألوان)
12. [الكود المكرر](#12-الكود-المكرر)
13. [الكود الميت](#13-الكود-الميت)
14. [جاهزية الإنتاج](#14-جاهزية-الإنتاج)
15. [الميزات الناقصة لبرنامج محاسبة إنتاجي](#15-الميزات-الناقصة)
16. [خطة الإصلاح المرحلية](#16-خطة-الإصلاح)
17. [أسئلة الاتجاه — تحتاج قرارك](#17-أسئلة-الاتجاه)

---

## 1. ملخص تنفيذي

### نقاط القوة ✅
- بنية Clean Architecture ممتازة (5 طبقات منفصلة)
- DDD مع Rich Domain Entities
- تشفير كلمات المرور BCrypt WF=12
- AuthorizationProxy مركزي مع RequiresPermission
- Audit Trail على مستوى الحقل (field-level change tracking)
- حماية الحذف متعددة الطبقات (IImmutableFinancialRecord)
- نسخ احتياطي كامل مع استعادة وتاريخ
- إدارة المعاملات Serializable + RowVersion
- 12 تقرير مع تصدير PDF/Excel
- نظام حوكمة Feature Flags مع Impact Analyzer

### نقاط الضعف الحرجة ❌
- **صفر تسجيل أحداث (Logging)** — Debug sink فقط
- **لا يوجد Global Company Filter** — تسريب بيانات multi-tenant
- **WAC لا يتحدث عند المرتجعات** — COGS خاطئ
- **نظام الحوكمة يعمل بنسبة ~30% فقط** — UI فقط بدون Backend
- **171+ لون ثابت في XAML** — يمنع تغيير الثيم
- **~3,650 سطر كود مكرر** — قابل للتخفيض لـ ~1,055
- **حسابات نظام 5112/4112 مفقودة** — InventoryAdjustment يرمي NullReferenceException
- **لا يوجد قائمة تدفقات نقدية** — قائمة مالية أساسية مفقودة

---

## 2. طبقة Domain

### 2.1 مشاكل حرجة (CRITICAL)

| # | المشكلة | الملف | الحل |
|---|---------|-------|------|
| D1 | `SalesInvoice.CustomerId` هو `int` وليس `int?` — عند CounterpartyType=Supplier يكون FK=0 | `Entities/Sales/SalesInvoice.cs` | غيّره لـ `int?` مع nullable FK configuration |
| D2 | `CostCenter` entity مشار إليه في JournalEntry/JournalEntryLine لكن **لا يوجد كـ Entity** | Domain/Entities/ | أنشئ CostCenter entity أو احذف الـ FK |

### 2.2 مشاكل عالية (HIGH)

| # | المشكلة | الملف | الحل |
|---|---------|-------|------|
| D3 | `JournalEntryLine.CreatedBy` لا يُملأ أبداً (دائماً null) | `Entities/Accounting/JournalEntryLine.cs` | مرره من الخدمة عند الإنشاء |
| D4 | `UpdateDetails()` في line entities يتخطى validation على discount/VAT (0-100) | `SalesInvoiceLine.cs`, `PurchaseInvoiceLine.cs` | أضف Guard clauses |
| D5 | `InventoryMovement.SetBalanceAfter` يرفض السالب لكن `DecreaseStockAllowNegative` يسمح به — تناقض | `Entities/Inventory/InventoryMovement.cs` | وحّد المنطق |

### 2.3 مشاكل متوسطة (MEDIUM)

| # | المشكلة | الملف | الحل |
|---|---------|-------|------|
| D6 | `UserStatus` enum غير مستخدم تماماً — User يستخدم IsActive/IsLocked | `Enums/UserStatus.cs` | احذفه |
| D7 | `InvoiceStatus` مُعاد استخدامه لـ CashReceipt, CashPayment, CashTransfer, InventoryAdjustment | `Enums/InvoiceStatus.cs` | أنشئ `DocumentStatus` عام أو enum منفصل لكل نوع |
| D8 | `CompanyAwareEntity` بدون Navigation Property لـ Company | `Common/CompanyAwareEntity.cs` | أضف `Company` navigation property |
| D9 | 40+ FK properties بدون navigation properties | متعدد | أضف navigation properties تدريجياً |
| D10 | Customer/Supplier SoftDelete overrides هي no-ops مع تعليقات مضللة | `Customer.cs`, `Supplier.cs` | أصلح أو احذف |

---

## 3. طبقة Application

### 3.1 مشاكل حرجة (CRITICAL)

| # | المشكلة | الملف | الحل |
|---|---------|-------|------|
| A1 | **WAC لا يتحدث في PurchaseReturn** — `DeductStockAsync` ينقص الكمية لكن لا يحدّث WAC | `Services/PurchaseReturnService.cs` | أضف WAC recalculation بعد خصم المخزون |
| A2 | **WAC لا يتحدث في SalesReturn** — stock يزيد لكن WAC يبقى كما هو | `Services/SalesReturnService.cs` | WAC = (oldQty×oldWAC + returnQty×returnCost) / newQty |
| A3 | **VAT-inclusive mode** (`IsVatInclusive`) لا يُمرر لـ domain entity constructors | `SalesInvoiceService.cs`, `PurchaseInvoiceService.cs` | مرر الإعداد واستخدمه في حساب الضريبة |
| A4 | **حسابات 5112/4112 مفقودة** من `SystemAccountSeed` — InventoryAdjustment posting يرمي NullReferenceException | `Persistence/Seeds/SystemAccountSeed.cs` | أضف seed entries لـ 5112 و 4112 |

### 3.2 مشاكل عالية (HIGH)

| # | المشكلة | الملف | الحل |
|---|---------|-------|------|
| A5 | "First entity = default" dead branch — count check بعد AddAsync دائماً ≥1 | `WarehouseService.cs`, `CashboxService.cs`, `BankAccountService.cs` | احذف الفرع الميت |
| A6 | POS `GetDailyReportAsync`/`GetProfitReportAsync` هم stubs يرجعون COGS=0, GrossProfit=0 | `Services/PosService.cs` | طبّق الحسابات الفعلية |
| A7 | `ConvertToInvoiceAsync` في SalesQuotation/PurchaseQuotation بـ saves بدون transaction | `SalesQuotationService.cs`, `PurchaseQuotationService.cs` | لُفّهم في transaction |
| A8 | 8 FluentValidation validators غير مسجلين في DI | متعدد | سجّلهم في `ConfigureServices` |
| A9 | `InventoryAdjustmentMapper` و `PriceListMapper` غير موجودين رغم وجود DTOs | Application/Mappers/ | أنشئهم |
| A10 | `InventoryAdjustmentService.CancelAsync` يستخدم `UtcNow.Month` بدل `AdjustmentDate.Month` | `InventoryAdjustmentService.cs` | استخدم التاريخ الصحيح |
| A11 | SystemSettings لحسابات default معرّفة لكن **غير مستخدمة تماماً** — كل الخدمات تثبّت أكواد الحسابات | متعدد | استخدم SystemSettings أو احذف التعريفات |

### 3.3 مشاكل متوسطة (MEDIUM)

| # | المشكلة | الملف | الحل |
|---|---------|-------|------|
| A12 | `NetTotal` يعني "قبل الضريبة" على مستوى السطر و"بعد الضريبة" على مستوى الرأس — مربك | متعدد | إعادة تسمية لوضوح |
| A13 | نمط إنشاء Journal Entry متكرر 11 مرة | متعدد | أنشئ `JournalEntryFactory` |
| A14 | نمط إدارة Stock متكرر 10 مرات | متعدد | أنشئ `StockManager` helper |

---

## 4. طبقة Persistence

### 4.1 مشاكل حرجة (CRITICAL)

| # | المشكلة | الملف | الحل |
|---|---------|-------|------|
| P1 | **لا يوجد Global Company Query Filter** — بيانات كل الشركات مكشوفة | `AppDbContext.cs` | أضف `.HasQueryFilter(e => e.CompanyId == companyId)` |
| P2 | **FK constraints مفقودة** لـ AccountId على Cashbox, BankAccount, Warehouse | Configuration files | أضف FK configuration |
| P3 | **Cascade Delete على IImmutableFinancialRecord lines** — حذف فاتورة يحذف بنودها | Configuration files | غيّر لـ Restrict |

### 4.2 مشاكل عالية (HIGH)

| # | المشكلة | الملف | الحل |
|---|---------|-------|------|
| P4 | `RolePermission` و `AuditLog` بدون concurrency token (RowVersion) | Entity configurations | أضف RowVersion |
| P5 | Duplicate audit field configuration في BankAccountConfiguration (copy-paste) | `BankAccountConfiguration.cs` | احذف التكرار |

### 4.3 مشاكل متوسطة (MEDIUM)

| # | المشكلة | الملف | الحل |
|---|---------|-------|------|
| P6 | لا pagination في `GetAllAsync` — يحمّل الجداول كاملة | Repository files | أضف `GetPagedAsync` |
| P7 | Hardcoded connection string في `DesignTimeDbContextFactory` | Factory file | اقرأ من configuration |
| P8 | Persistence يشير لـ Application project (circular-ish dependency) | `.csproj` | استخدم interface-based DI |
| P9 | Post يستخدم `AsNoTracking` ثم `Update` — marks all properties modified | Repository files | استخدم tracking أو selective update |

---

## 5. طبقة Infrastructure

| # | المشكلة | الأولوية | الملف | الحل |
|---|---------|----------|-------|------|
| I1 | `WindowsEscPosPrinterService` هو stub كامل (IsAvailable=>false, كل شيء TODO) | HIGH | `WindowsEscPosPrinterService.cs` | طبّق أو احذف |
| I2 | `AuditLogger` يكتب في نفس DB transaction كالعملية — audit يضيع لو فشل tx | HIGH | `AuditLogger.cs` | استخدم transaction منفصل أو SaveChanges منفصل |
| I3 | `CurrentUserService` يستخدم `RoleId==1` magic number للتجاوز الإداري | MEDIUM | `CurrentUserService.cs` | استخدم constant أو configuration |

---

## 6. طبقة WPF UI

### 6.1 مشاكل عالية (HIGH)

| # | المشكلة | الحل |
|---|---------|------|
| U1 | **171+ لون hex ثابت** في XAML views — يمنع تغيير الثيم | استبدل بـ `StaticResource` brushes |
| U2 | **30+ `Background="White"`** ثابت | استخدم `{StaticResource CardBrush}` |
| U3 | Status bar version `"v0.2.0"` ثابت بينما App version `1.1.0` | اربطه بـ assembly version |
| U4 | **لا يوجد XAML-level input validation** (INotifyDataErrorInfo) | طبّقه في ViewModels |
| U5 | `ProductImportView`/`ViewModel` مسجل في ViewRegistry لكن مفقود في DI → runtime crash | سجّل `AddTransient` |
| U6 | `.Wait()` / `.Result` blocking في LoginViewModel L176-177 → deadlock risk | استخدم async/await |
| U7 | **14 من 16 نافذة حوار لا تُغلق بـ Escape** | أضف `IsCancel="True"` أو key handler |

### 6.2 مشاكل متوسطة (MEDIUM)

| # | المشكلة | الحل |
|---|---------|------|
| U8 | `StatusBadgeDraft`/`StatusBadgePosted` styles معرّفة لكن غير مستخدمة | استخدمها أو احذفها |
| U9 | `ShortcutConfigDialog` و `ChangePasswordDialog` غير مسجلين في DI | سجّلهم |
| U10 | 11 `Debug.WriteLine` في production code | احذفها أو استبدلها بـ logger |
| U11 | 50+ DataGrid بدون `VirtualizingStackPanel.VirtualizationMode="Recycling"` | أضفه |

---

## 7. نظام الحوكمة

### 7.1 ملخص الحالة

| القياس | القيمة |
|--------|--------|
| إجمالي الميزات/الوحدات في التطبيق | 41 |
| ميزات لها Feature Key في DB (module-level) | 8 |
| ميزات لها Feature Key في DB (behavioral) | 3 |
| ميزات في `_sectionFeatureMap` (إخفاء Sidebar) | 6 أقسام (~28 عنصر) |
| ميزات لها تطبيق Backend | **0** module-level, **3** behavioral فقط |
| ميزات بدون أي حوكمة | 7 |
| ميزات بحوكمة UI فقط (قابلة للتجاوز) | 28 |
| كود ميت (`FeatureGuard`) | ملف واحد، لا يُستدعى أبداً |

### 7.2 الميزات بدون أي حوكمة

| الميزة | التأثير |
|--------|---------|
| Dashboard | منخفض — طبيعي أن يكون دائماً |
| Fiscal Year (الإعدادات) | منخفض — إعداد أساسي |
| System Settings | منخفض — إعداد أساسي |
| Role Management | **متوسط** — يجب ربطه بـ `UserManagement` flag |
| Audit Log | منخفض — قراءة فقط |
| Backup/Restore | **متوسط** — إمكانية تدميرية بدون حوكمة |
| Integrity Check | منخفض — قراءة فقط |

### 7.3 مشكلة التجاوز الحرجة

عندما يعطّل المدير ميزة "المبيعات" في الحوكمة:
- ✅ Sidebar يخفي عناصر المبيعات
- ❌ لكن `SalesInvoiceService.CreateAsync()` يعمل بشكل طبيعي
- ❌ البحث العام يعرض كيانات المبيعات
- ❌ TabNavigationService.NavigateTo("SalesInvoices") يفتح بدون فحص
- ❌ `FeatureGuard.CheckAsync()` موجود لكن **كود ميت** — لا يُستدعى أبداً

### 7.4 الإصلاح المطلوب

1. **اربط `FeatureGuard.CheckAsync()`** في نقاط دخول الخدمات (P0)
2. **أضف فحص Feature Flag في `TabNavigationService`** (P0)
3. **أضف أقسام الإعدادات لـ `_sectionFeatureMap`** (P1)
4. **أضف فحص لـ WindowService.OpenPosWindow()** (P2)
5. **أضف فحص لـ البحث العام** (P2)

---

## 8. التقارير — تغطية وفجوات

### 8.1 التقارير الموجودة (12 تقرير)

| # | التقرير | فلتر تاريخ | PDF/Excel | فترة محاسبية | الحسابات |
|---|---------|-----------|-----------|-------------|----------|
| 1 | ميزان المراجعة | ✅ | ✅ | ❌ | ✅ |
| 2 | الميزانية العمومية | ✅ | ✅ | ❌ | ✅ |
| 3 | قائمة الدخل | ✅ | ✅ | ❌ | ✅ |
| 4 | كشف حساب | ✅ | ✅ | ❌ | ✅ |
| 5 | تقرير المبيعات | ✅ | ✅ | ❌ | ✅ |
| 6 | تقرير المشتريات | ✅ | ✅ | ❌ | ✅ |
| 7 | تقرير الأرباح | ✅ | ✅ | ❌ | ✅ |
| 8 | تقرير المخزون | فلتر مخزن | ✅ | — | ✅ |
| 9 | بطاقة صنف | ✅ | ✅ | ❌ | ✅ |
| 10 | حركة الصندوق | ✅ | ✅ | ❌ | ✅ |
| 11 | أعمار الديون | بدون تاريخ | ✅ | — | ✅ |
| 12 | تقرير الضريبة | ✅ | ✅ | ❌ | ✅ |

### 8.2 مشاكل التقارير

| # | المشكلة | الأولوية |
|---|---------|----------|
| R1 | **قائمة التدفقات النقدية غير موجودة تماماً** | CRITICAL |
| R2 | **دفتر الأستاذ العام غير موجود** | CRITICAL |
| R3 | **لا تحترم حدود السنة المالية** — فلتر تاريخ حر | MEDIUM |
| R4 | **لا يوجد طباعة مباشرة** — تصدير فقط ثم فتح الملف | MEDIUM |
| R5 | **بنية التقارير المتقدمة مبنية لكن غير مستخدمة** (ReportViewModelBase, SmartFilterEngine, VirtualizingReportCollection) | MEDIUM |
| R6 | **لا PrintCommand في أي ViewModel تقرير** رغم وجود shortcut handler | LOW |

### 8.3 تقارير ناقصة (مطلوبة)

| التقرير | الأولوية | الجهد |
|---------|----------|-------|
| قائمة التدفقات النقدية | CRITICAL | L |
| دفتر الأستاذ العام | CRITICAL | M |
| تقرير ربح/خسارة حسب مركز التكلفة | HIGH | M |
| كشف حساب عميل تفصيلي | HIGH | M |
| كشف حساب مورد تفصيلي | HIGH | M |
| تقرير المبيعات حسب الصنف | MEDIUM | S |
| تقرير المبيعات حسب المندوب | MEDIUM | S |
| تقرير المشتريات حسب الصنف | MEDIUM | S |
| تقرير سجل الشيكات | HIGH | S |
| تقرير أرصدة البنوك | MEDIUM | S |
| تقرير الأصناف بطيئة الحركة | MEDIUM | M |
| تقرير تقييم المخزون | HIGH | M |
| القوائم المالية المقارنة | MEDIUM | M |
| تقرير اليومية | MEDIUM | S |
| طباعة فاتورة ضريبية مطابقة | HIGH | M |

---

## 9. الخزينة السريعة vs الكاملة

### 9.1 الخدمة نفسها ✅

كلا الإصدارين يستدعيان **نفس** `ICashReceiptService` / `ICashPaymentService`.

### 9.2 جدول المقارنة

| الميزة | السريع | الكامل |
|--------|--------|--------|
| خدمة مشتركة | ✅ نعم | ✅ نعم |
| اختيار العميل/المورد إلزامي | ✅ نعم | ❌ اختياري |
| اختيار الحساب | تلقائي من العميل | يدوي |
| حفظ كمسودة | ✅ | ✅ |
| حفظ وترحيل | ✅ زر واحد | ❌ خطوتين منفصلتين |
| تأكيد قبل الترحيل | ❌ يرحّل فوراً | ✅ نافذة تأكيد |
| ربط بفاتورة | ❌ **لا يضبط SalesInvoiceId** 🐛 | ✅ عبر NavigationParams |
| عرض رصيد العميل | ✅ مباشر | ❌ |
| رصيد بعد العملية | ✅ | ❌ |
| قائمة السندات | ❌ | ✅ DataGrid كامل |
| تعديل سند موجود | ❌ | ✅ |
| إلغاء سند مرحّل | ❌ | ✅ |
| حذف مسودة | ❌ | ✅ |
| بحث F1 | ❌ | ✅ |
| Concurrency handling | ❌ | ✅ |
| Navigation integration | ❌ — نافذة مستقلة | ✅ INavigationAware |

### 9.3 باج يحتاج إصلاح

- **Quick Receipt `BuildDto()` لا يضبط `SalesInvoiceId`** — لا يمكن ربط السند بالفاتورة
- **Quick Payment `BuildDto()` لا يضبط `PurchaseInvoiceId`** — نفس المشكلة
- **Quick Receipt يرحّل بدون تأكيد** — عكس النسخة الكاملة

---

## 10. نوافذ الحوار — فحص التصميم

### 10.1 قائمة كاملة (16 نافذة)

| # | النافذة | النوع | Escape Close | Error Display |
|---|---------|-------|:---:|:---:|
| 1 | MainWindow | Shell | — | ✅ |
| 2 | LoginWindow | Login | — | ✅ |
| 3 | ShortcutConfigDialog | حوار | ❌ | ❌ |
| 4 | SuperAdminAuthDialog | مصادقة | ❌ | ✅ |
| 5 | PosWindow | نافذة كاملة | ✅ | ✅ |
| 6 | PosOpenSessionDialog | حوار | ❌ | ❌ |
| 7 | PosCloseSessionDialog | حوار | ❌ | ❌ |
| 8 | SearchLookupWindow | بحث | ✅ | ❌ |
| 9 | QuickAddProductDialog | حوار | ❌ | ✅ |
| 10 | InvoicePdfPreviewDialog | معاينة | ❌ | ✅ |
| 11 | QuickTreasuryDialog | حوار | ❌ | ❌ |
| 12 | PriceHistoryDialog | حوار | ❌ | ❌ |
| 13 | InvoiceAddLineWindow | حوار | ❌ | ✅ |
| 14 | ChangePasswordDialog | حوار | ❌ | ✅ |
| 15 | QuickCashReceiptWindow | حوار | ❌ | ✅ |
| 16 | QuickCashPaymentWindow | حوار | ❌ | ✅ |

**النتيجة:** 14 من 16 نافذة لا تُغلق بـ Escape — SearchLookupWindow فقط يعمل بشكل صحيح.

---

## 11. التصميم والألوان والتخطيط

### 11.1 درجة التوافق لكل View

| View | الألوان | الهوامش | الخطوط | الأزرار | البطاقات | العناوين | الدرجة |
|------|---------|---------|--------|---------|----------|---------|--------|
| SalesInvoiceView | 2 ثابت | ✅ | ✅ | ✅ | ✅ | ✅ | **92%** |
| SalesReturnView | 2 | ✅ | ✅ | ✅ | ✅ | ✅ | **92%** |
| PurchaseInvoiceView | 2 | ✅ | ✅ | ✅ | ✅ | ✅ | **92%** |
| CashReceiptView | 1 | ✅ | ✅ | ✅ | ✅ | ✅ | **95%** |
| CashPaymentView | 1 | ✅ | ✅ | ✅ | ✅ | ✅ | **95%** |
| CashTransferView | 0 | ❌ | مختلط | ❌ | ✅ | ❌ | **55%** |
| ChartOfAccountsView | 3 | ❌ | ✅ | ✅ | ❌ | ❌ | **65%** |
| JournalEntryView | 0 | طفيف | مختلط | ✅ | ✅ | ✅ | **85%** |
| ProductView | 4 | ❌ | ✅ | ✅ | ❌ | ❌ | **60%** |
| TrialBalanceView | 1 | ❌ | مختلط | ❌ | ✅ | ✅ | **60%** |
| UserManagementView | 1 | ❌ | ❌ | ❌ | جزئي | ❌ | **50%** |
| MainWindow | 12+ | ✅ | مختلط | ✅ | — | — | **70%** |
| PosWindow | 8+ | ✅ | مختلط | ✅ | ✅ | ✅ | **75%** |

### 11.2 الألوان الثابتة — ملخص

| نوع اللون | أمثلة | العدد التقريبي |
|-----------|-------|:-----------:|
| خلفيات رمادية فاتحة | `#ECEFF1`, `#F0F2F5`, `#FAFAFA` | 15+ |
| حدود | `#E0E0E0`, `#E8EAF0`, `#DEE2E6` | 25+ |
| خلفيات داكنة (sidebar) | `#263238`, `#1a252f`, `#37474F` | 7 |
| ألوان وظيفية (تحذير/نجاح/خطأ) | `#FFA000`, `#2E7D32`, `#D32F2F` | 10+ |
| ألوان أيقونات | `#0277BD`, `#6A1B9A`, `#90CAF9` | 8+ |
| `Background="White"` | متعدد | 50+ |
| `Foreground="Gray"` | متعدد | 15+ |
| **الإجمالي** | — | **171+** |

### 11.3 مشاكل التصميم الحرجة

1. **CashTransferView و JournalEntryView** يستخدمان نمط `Label + TextBox` بدل `MaterialDesign:HintAssist.Hint` — غير متسق
2. **ChartOfAccountsView و ProductView** يستخدمان `Background="White"` بدل `{StaticResource CardPanel}` style
3. **UserManagementView** ودالات أخرى من الإعدادات تستخدم `FontSize="22"` بينما `PageTitle` style يستخدم `FontSize="18"`
4. **أعمدة DataGrid** — عرض عمود "الرقم" يختلف: `110` (فواتير) vs `100` (خزينة) vs `120` (تحويلات)
5. **Root margin** غير موحّد: `8` (أغلب), `6` (حسابات/منتجات), `16` (تقارير)
6. **Sidebar item height** `36px` بينما الحوكمة تقول `44px`

---

## 12. الكود المكرر (~3,650 سطر)

### 12.1 الخدمات (~1,890 سطر مكرر)

| النمط المكرر | عدد التكرارات | الأسطر | الحل |
|-------------|:------------:|:------:|------|
| Journal Entry creation | 11 | ~770 | أنشئ `JournalEntryFactory` |
| Stock management (increase/decrease + WAC + movement) | 10 | ~600 | أنشئ `StockManager` helper |
| Fiscal period validation | 8 | ~240 | أنشئ `FiscalPeriodValidator` |
| Post validation (Draft check + period check) | 8 | ~160 | أنشئ `PostingGuard` |
| Cancel posted document | 6 | ~120 | أنشئ `DocumentCancellation` helper |

### 12.2 الـ ViewModels (~1,761 سطر مكرر)

| النمط المكرر | عدد التكرارات | الأسطر | الحل |
|-------------|:------------:|:------:|------|
| Error/Success bar with auto-dismiss | 55+ | ~550 | `MessageBarMixin` أو base class |
| Loading state management | 40+ | ~400 | `LoadingStateMixin` |
| Confirm dialog pattern | 20+ | ~300 | `ConfirmationHelper` |
| Date initialization (Today/fiscal) | 15+ | ~150 | `DateHelper` |
| Permission check binding | 15+ | ~225 | `PermissionHelper` |
| CanSave computation | 12+ | ~136 | Abstract في base class |

### 12.3 بعد التخفيض

المجموع الحالي: **~3,651 سطر مكرر**  
بعد إنشاء helpers/factories: **~1,055 سطر** (تخفيض 71%)

---

## 13. الكود الميت

| # | الملف | السبب | الإجراء |
|---|-------|-------|---------|
| 1 | `verify_password.cs` (الجذر) | **خطر أمني** — كلمات مرور مكشوفة | **احذف فوراً** |
| 2 | `_tmp_RoleManagementView.xaml` (الجذر) | ملف مؤقت متروك | احذف |
| 3 | `AuthorizationGuard.cs` | `[Obsolete]`, صفر مستدعين | احذف |
| 4 | `FeatureGuard.cs` | مبني لكن لم يُربط أبداً | **اربطه (P0) أو احذفه** |
| 5 | `UserStatus.cs` | Enum غير مستخدم تماماً | احذف |
| 6 | 5 واجهات policy في `IAccountingPolicies.cs` | معرّفة لكن لم تُطبّق أبداً | طبّق أو احذف |
| 7 | `CounterpartyType.SalesRepresentative` | قيمة enum لا تُعالج في أي switch | احذفها |
| 8 | `StatusBadgeDraft`/`StatusBadgePosted` styles | معرّفة لكن غير مستخدمة | استخدمها أو احذفها |

---

## 14. جاهزية الإنتاج — بطاقات الأداء

| المجال | الدرجة | التفاصيل |
|--------|--------|----------|
| تشفير كلمات المرور | **A** | BCrypt WF=12 |
| التفويض (Authorization) | **A** | AuthorizationProxy مركزي |
| سجل المراجعة (Audit Trail) | **A+** | تتبع على مستوى الحقل |
| حماية الحذف | **A+** | متعددة الطبقات |
| النسخ الاحتياطي | **A** | كامل + استعادة + تاريخ |
| إدارة المعاملات | **A** | Serializable + rollback + RowVersion |
| تخزين الاعتمادات | **B-** | AES-256 لكن يخزن كلمة المرور الفعلية |
| إدارة الأسرار | **D** | `Admin@123456` في appsettings.json |
| DataGrid Virtualization | **C+** | 10/60+ مفعّل |
| Blocking Async | **C** | `.Wait()` في LoginViewModel |
| **التسجيل (Logging)** | **F** | **Debug sink فقط — لا تسجيل مستدام إطلاقاً** |

---

## 15. الميزات الناقصة لبرنامج محاسبة إنتاجي

### 15.2 ميزات تشغيلية

| # | الميزة | العربي | الأولوية | الجهد |
|---|--------|--------|----------|-------|
| O1 | Purchase Orders | أوامر الشراء | HIGH | L |
| O2 | Inventory Transfer | تحويل مخزون | HIGH | M |
| O3 | Stock Alerts (push) | تنبيهات المخزون | MEDIUM | S |
| O4 | Serial Number Tracking | الأرقام التسلسلية | LOW | XL |
| O5 | Batch/Lot Tracking | تتبع الدفعات | LOW | XL |
| O6 | Multi-Unit Conversion | تحويل الوحدات | MEDIUM | M |
| O7 | Bill of Materials | قوائم المواد | LOW | XL |
| O8 | Credit Limit Enforcement | تطبيق حد الائتمان | HIGH | S |
| O9 | Supplier Credit Limit | حد ائتمان المورد | MEDIUM | S |
| O10 | Delivery Notes | إذن تسليم | HIGH | L |
| O11 | Goods Receipt Notes | إذن استلام | HIGH | L |
| O12 | Customer/Supplier Aging Detail | أعمار الديون التفصيلي | HIGH | M |
| O13 | Customer/Supplier Statement | كشف حساب عميل/مورد | MEDIUM | M |
| O14 | Product Price History | سجل تاريخ الأسعار | LOW | S |
| O15 | Barcode Printing | طباعة الباركود | MEDIUM | M |

### 15.3 ميزات UX / البنية التحتية

| # | الميزة | العربي | الأولوية | الجهد |
|---|--------|--------|----------|-------|

| U6 | Data Import (all entities) | استيراد بيانات شامل | HIGH | L |
| U7 | Full Data Export | تصدير كامل | MEDIUM | M |
| U8 | Undo/Redo | تراجع/إعادة | LOW | L |
| U9 | Activity Log per Entity | سجل نشاط لكل سجل | MEDIUM | M |
| U10 | Print Templates | قوالب طباعة مخصصة | HIGH | L |
| U11 | Keyboard Shortcuts | اختصارات لوحة المفاتيح | MEDIUM | S |



## 16. خطة الإصلاح المرحلية

### المرحلة 0 — إصلاحات أمنية فورية (يوم واحد)

| # | المهمة | الجهد |
|---|--------|-------|
| 1 | احذف `verify_password.cs` | 5 دقائق |
| 2 | احذف `_tmp_RoleManagementView.xaml` | 5 دقائق |
| 3 | أزل `Admin@123456` من appsettings.json واستخدم User Secrets | 30 دقيقة |
| 4 | أصلح `Encrypt=False` في connection string | 15 دقيقة |
| 5 | أضف `Escape` close لكل الـ 14 نافذة حوار | ساعة |

### المرحلة 1 — إصلاحات حرجة (أسبوع 1-2)

| # | المهمة | الجهد |
|---|--------|-------|
| 1 | أصلح WAC في PurchaseReturnService + SalesReturnService | يوم |
| 2 | أضف Global Company Query Filter | يوم |
| 3 | أضف حسابات 5112/4112 في SystemAccountSeed | ساعة |
| 4 | أصلح SalesInvoice.CustomerId → int? | ساعتين |
| 5 | أصلح .Wait()/.Result في LoginViewModel | ساعتين |
| 6 | سجّل ProductImportView/ViewModel في DI | 30 دقيقة |
| 7 | سجّل الـ 8 validators في DI | ساعة |
| 8 | أصلح Cascade Delete على IImmutableFinancialRecord | ساعتين |
| 9 | أصلح Quick Treasury BuildDto — أضف SalesInvoiceId/PurchaseInvoiceId | ساعتين |
| 10 | طبّق Logging framework (Serilog → File/DB) | يوم |

### المرحلة 2 — جودة الكود (أسبوع 3-4)

| # | المهمة | الجهد |
|---|--------|-------|
| 1 | أنشئ JournalEntryFactory (يخفض 770 سطر) | يوم |
| 2 | أنشئ StockManager helper (يخفض 600 سطر) | يوم |
| 3 | أنشئ FiscalPeriodValidator + PostingGuard | نصف يوم |
| 4 | أنشئ MessageBarMixin في base ViewModel | نصف يوم |
| 5 | احذف Dead Code (AuthorizationGuard, UserStatus, 5 policy interfaces) | ساعتين |
| 6 | أنشئ InventoryAdjustmentMapper و PriceListMapper | ساعتين |
| 7 | أصلح InventoryAdjustmentService.CancelAsync — صحّح الشهر | 30 دقيقة |
| 8 | أصلح "first entity = default" dead branches | ساعة |

### المرحلة 3 — UI/UX (أسبوع 5-6)

| # | المهمة | الجهد |
|---|--------|-------|
| 1 | أنشئ semantic brushes مفقودة في AppStyles.xaml | نصف يوم |
| 2 | استبدل 171+ لون ثابت بـ StaticResource | يومين |
| 3 | وحّد CashTransferView + JournalEntryView لنمط HintAssist | يوم |
| 4 | طبّق CardPanel style على ChartOfAccountsView + ProductView | نصف يوم |
| 5 | وحّد أعمدة DataGrid (Number=110, Date=90, Status=70) | نصف يوم |
| 6 | أصلح version display في status bar | 30 دقيقة |
| 7 | أضف VirtualizingStackPanel.Recycling لـ DataGrids | نصف يوم |
| 8 | أضف INotifyDataErrorInfo validation | يومين |

### المرحلة 4 — الحوكمة (أسبوع 7)

| # | المهمة | الجهد |
|---|--------|-------|
| 1 | اربط FeatureGuard.CheckAsync في نقاط دخول الخدمات | يوم |
| 2 | أضف feature check في TabNavigationService | نصف يوم |
| 3 | أضف الإعدادات لـ _sectionFeatureMap | ساعتين |
| 4 | أضف feature check في WindowService.OpenPosWindow | ساعة |
| 5 | أضف feature check في Global Search | ساعتين |
| 6 | أضف feature check في Quick Treasury dialogs | ساعة |

### المرحلة 5 — تقارير ناقصة (أسبوع 8-10)

| # | المهمة | الجهد |
|---|--------|-------|
| 1 | قائمة التدفقات النقدية | أسبوع |
| 2 | دفتر الأستاذ العام | 3-4 أيام |
| 3 | كشف حساب عميل/مورد تفصيلي | 3-4 أيام |
| 4 | تقرير تقييم المخزون | 3 أيام |
| 5 | طباعة فاتورة ضريبية مطابقة | 3 أيام |

### المرحلة 6 — ميزات جديدة حرجة (أسبوع 11-18)

| # | المهمة | الجهد |
|---|--------|-------|

| 7 | تحويل المخزون بين المخازن | أسبوع |

## 17. أسئلة الاتجاه — تحتاج قرارك

> هذه أسئلة تحتاج منك قرار قبل البدء في الإصلاح. كل سؤال له خيارات — اختر ما يناسبك.

---

###
---


---

###

### السؤال 5: نمط النموذج (Form Pattern)

**أي نمط نموذج يجب اعتماده كـ Standard عبر كل الشاشات؟** حالياً هناك نمطين مختلفين:

- **أ) MaterialDesign HintAssist (Outlined)** → النمط المستخدم في الفواتير والخزينة (الأكثر حداثة)
- **ب) Label + TextBox (Stacked)** → النمط المستخدم في CashTransfer و JournalEntry (تقليدي أكثر)

---الاجابه أ

#
---

### السؤال 7: الكود المكرر — استراتيجية التخفيض

**كيف تريد التعامل مع ~3,650 سطر مكرر؟**

- **أ) إنشاء Factories + Helpers** → `JournalEntryFactory`, `StockManager`, `FiscalPeriodValidator`, `PostingGuard` → تخفيض 71%
- **ب) إنشاء Generic Base Services** → `TransactionalDocumentService<T>` → تخفيض أكبر لكن أصعب في الصيانة
- **ج) تركه كما هو** → أسهل للفهم لكن عرضة لأخطاء التحديث

---
الاجابه أ
### السؤال 8: FeatureGuard — أين يُطبّق؟

**ما مستوى تطبيق حوكمة الميزات؟**

- **أ) UI فقط (sidebar + TabNavigation)** → أسرع، يغطي 90% من الحالات
- **ب) UI + Service layer** → حماية كاملة، لكن يحتاج تعديل ~30 خدمة
- **ج) UI + Service + API** → للمستقبل لو أضفت REST API

---الاجابه ب

### السؤال 9: التسجيل (Logging)

**ما مزوّد التسجيل المطلوب؟**

- **أ) Serilog → File** → ملفات .log يومية، الأبسط
الاجابه أ
#
-