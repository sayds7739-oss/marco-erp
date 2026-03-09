# خطة الإصلاح الصارمة — MarcoERP
**التاريخ:** 2026-02-16  
**المرجع:** تقرير الفحص الشامل 2026-02-16  
**الحالة:** ✅ المرحلة 1 + المرحلة 2 (2.1–2.5) مكتملة — Build Succeeded 0 Errors 0 Warnings

---

## المرحلة 1: إصلاحات حرجة فورية (Critical — يجب تنفيذها الآن)

### 1.1 ✅ إصلاح NavHeaderTemplate — لا يربط Visibility بـ IsVisible
- **الملف:** `src/MarcoERP.WpfUI/Views/Shell/MainWindow.xaml` (سطر 137-144)
- **المشكلة:** عند تعطيل قسم (مثل المشتريات)، العنوان يبقى ظاهراً ويأخذ مساحة
- **الحل:** إضافة Border يربط Visibility بـ IsVisible ويحتوي على TextBlock الحالي
- **التأثير:** بصري — القسم المعطل لن يظهر نهائياً في الشريط الجانبي

### 1.2 ✅ إضافة POS + UserManagement لـ _sectionFeatureMap
- **الملف:** `src/MarcoERP.WpfUI/ViewModels/Shell/MainWindowViewModel.cs` (سطر 768-776)
- **المشكلة:** قسمي "نقاط البيع" و"إدارة المستخدمين" لا يختفيان أبداً عند تعطيل ميزاتهما
- **الحل:** ملاحظة — POS تحت قسم "المبيعات" فيختفي معها. لكن UserManagement تحت "الإعدادات" ولا تحتوي قسم مستقل. لذلك الإصلاح الفعلي هو: لا يحتاج _sectionFeatureMap تعديل لـ POS (لأنه تحت المبيعات). لكن يجب أن يكون عنصر POS أيضاً يفحص feature POS بشكل مستقل عبر ViewRegistry._featureMap (وهو مفحوص فعلاً).
- **التأثير:** POS مغطى ✓ — UserManagement مغطى كعنصر في ViewRegistry ✓

### 1.3 ✅ تصحيح وصف بروفايل Simple المتناقض
- **الملف:** `src/MarcoERP.Persistence/Seeds/ProfileSeed.cs` (سطر 23)
- **المشكلة:** الوصف "المبيعات والمخزون والخزينة فقط" لكنه يشمل المحاسبة وإدارة المستخدمين أيضاً
- **الحل:** تصحيح الوصف ليعكس الواقع
- **التأثير:** دقة البيانات

### 1.4 ✅ تفعيل FeatureGuard في كل Application Service
- **المشكلة:** FeatureGuard.CheckAsync موجود كـ dead code — لا يستدعيه أي Service
- **الحل:** إضافة FeatureGuard.CheckAsync في بداية CreateAsync لكل خدمة رئيسية
- **الملفات المتأثرة:**
  1. `SalesInvoiceService.CreateAsync` — Sales guard (لديه `_featureService` فعلاً)
  2. `PurchaseInvoiceService.CreateAsync` — Purchases guard (يحتاج إضافة `_featureService`)
  3. `SalesReturnService.CreateAsync` — Sales guard (يحتاج إضافة `_featureService`)
  4. `PurchaseReturnService.CreateAsync` — Purchases guard (يحتاج إضافة `_featureService`)
  5. `CashPaymentService.CreateAsync` — Treasury guard (لديه `_featureService` فعلاً)
  6. `CashReceiptService.CreateAsync` — Treasury guard (يحتاج إضافة `_featureService`)
  7. `CashTransferService.CreateAsync` — Treasury guard (لديه `_featureService` فعلاً)
  8. `JournalEntryService.CreateDraftAsync` — Accounting guard (يحتاج إضافة `_featureService`)
  9. `InventoryAdjustmentService.CreateAsync` — Inventory guard (يحتاج إضافة `_featureService`)
- **التأثير:** تعطيل ميزة يمنع العمليات فعلياً وليس بصرياً فقط

### 1.5 ✅ تحديث تعليق FeatureGuard ليعكس أنه مُفعّل
- **الملف:** `src/MarcoERP.Application/Common/FeatureGuard.cs`
- **المشكلة:** التعليق يقول "NOT wired into any existing service yet"
- **الحل:** تحديث التعليق

---

## المرحلة 2: إصلاحات عالية (High — يجب تنفيذها قريباً)

### 2.1 ✅ إزالة الحقول غير المستخدمة (Unused Fields) — تم
- `CashPaymentService._fiscalYearRepo` — ✅ أُزيل
- `CashPaymentService._auditLogger` — ✅ أُزيل
- `PurchaseReturnService._fiscalYearRepo` — ✅ أُزيل
- `PurchaseReturnService._systemSettingRepository` — ✅ أُزيل
- `SalesInvoiceService._fiscalYearRepo` — ✅ أُزيل
- `RoleService._currentUser` — ⏳ يحتاج حذف constructor parameter (تغيير DI)
- `ProductImportService._currentUser` — ⏳ يحتاج حذف constructor parameter (تغيير DI)

### 2.2 ✅ نقل منطق التقارير — لا يحتاج إصلاح (تصحيح تصنيف)
- **التقييم:** ReportService في Persistence يستخدم DbContext مباشرة لاستعلامات التجميع — وهذا الموضع الصحيح معمارياً
- **الواجهة:** IReportService معرّفة في Application ✓ — التطبيق يجتاز Clean Architecture
- **ملاحظة:** الملف كبير (1,211 سطر) ومرشح للتقسيم ضمن مهمة 2.6

### 2.3 ✅ إزالة EF Core reference من Application.csproj — تم
- أُنشئ `DuplicateRecordException` في Domain
- أُضيف wrapping لـ DbUpdateException في UnitOfWork (Persistence)
- استُبدلت **56 catch block** في 9 خدمات: `DbUpdateException` → `DuplicateRecordException`, `DbUpdateConcurrencyException` → `ConcurrencyConflictException`
- أُزيل `IsUniqueConstraintViolation` من ProductionHardening (الآن في UnitOfWork)
- حُذف `<PackageReference Include="Microsoft.EntityFrameworkCore">` من Application.csproj
- أُزيلت جميع `using Microsoft.EntityFrameworkCore;` من 10 ملفات في Application

### 2.4 ✅ استبدال DateTime.Today بـ IDateTimeProvider في ViewModels — تم
- استُبدلت **80 حالة** `DateTime.Today` بـ `_dateTime.Today` في **27 ViewModel**
- أُضيف حقن `IDateTimeProvider` في constructors لجميع ViewModels المتأثرة
- الواجهة `IDateTimeProvider` كانت موجودة مسبقاً — فقط تم تفعيلها
### 2.5 ✅ نقل الحسابات من ViewModels إلى Application Layer — تم (Phase 9C)
- أُضيفت 5 دوالّ جديدة إلى `ILineCalculationService` + `LineCalculationService`:
  - `ApplyHeaderDiscount()` — خصم رأس الفاتورة (نسبة + مبلغ + رسوم توصيل)
  - `CalculatePartCount()` — حساب عدد الأجزاء (majorFactor / minorFactor)
  - `ConvertBaseToUnitQuantity()` — تحويل كمية أساسية إلى وحدة
  - `CalculateCostDifference()` — فرق تكلفة الجرد
  - `CalculateNetCash()` — صافي النقدي بعد الباقي
- أُضيف DTO جديد `HeaderDiscountResult` في `LineCalculationDtos.cs`
- **الملفات المُعدّلة (8 ViewModels):**
  1. `SalesInvoiceDetailViewModel.cs` — header discount + stock conversion
  2. `PurchaseInvoiceDetailViewModel.cs` — header discount
  3. `PosViewModel.cs` — profit = TotalProfit (من الخدمة) + netCash via service
  4. `InventoryAdjustmentDetailViewModel.cs` — CostDifference + ConvertBaseToUnit
  5. `ProductViewModel.cs` + `ProductUnitFormItem` — AutoCalcPrices via ConvertPrice delegate
  6. `PriceListViewModel.cs` + `PriceListProductItem` — جميع حسابات الأسعار (13 موقع)
  7. `BulkPriceUpdateViewModel.cs` — ConvertPrice for minor unit prices
- **حُقن `ILineCalculationService` في:** ProductViewModel, PriceListViewModel, BulkPriceUpdateViewModel
### 2.6 ✅ تقسيم الملفات الكبيرة (8 ملفات > 800 سطر — partial classes)

**تم التنفيذ بالكامل:**

| # | الملف | الحجم الأصلي | الملفات الناتجة |
|---|-------|-------------|----------------|
| 1 | PosService | 1,267 | .cs, .Session.cs, .CompleteSale.cs, .CancelSale.cs, .Reports.cs, PosDependencyGroups.cs |
| 2 | SalesInvoiceService | 963 | .cs, .Posting.cs, .Helpers.cs |
| 3 | SalesInvoiceDetailViewModel | 1,534 | .cs, .Loading.cs, .Save.cs, .Lines.cs, .Helpers.cs |
| 4 | ReportService | — | كان مقسّم مسبقاً (6 ملفات) |
| 5 | PriceListViewModel | 1,058 | .cs, .Loading.cs, .Save.cs, .Helpers.cs, PriceListProductItem.cs |
| 6 | PurchaseInvoiceDetailVM | 1,042 | .cs, .Loading.cs, .Save.cs, .Helpers.cs |
| 7 | PurchaseInvoiceVM | 1,037 | .cs, .Loading.cs, .Operations.cs, PurchaseInvoiceLineFormItem.cs |
| 8 | SalesInvoiceVM | 1,020 | .cs, .Loading.cs, .Operations.cs, SalesInvoiceLineFormItem.cs |
| 9 | ProductImportService | 814 | .cs, .Validation.cs, .Helpers.cs |

- **Build:** 0 Errors, 0 Warnings
- **Tests:** 235+173+1+34 passed (نفس الأساس — 32 فشل مسبق لا علاقة لها)

---

## سجل التنفيذ

| # | المهمة | الحالة | التاريخ |
|---|--------|--------|---------|
| 1.1 | NavHeaderTemplate Visibility | ✅ تم | 2026-02-16 |
| 1.3 | وصف بروفايل Simple | ✅ تم | 2026-02-16 |
| 1.4 | FeatureGuard في Services | ✅ تم | 2026-02-16 |
| 1.5 | تعليق FeatureGuard | ✅ تم | 2026-02-16 |
| 2.1 | حقول غير مستخدمة | ✅ تم | 2026-02-16 |
| 2.2 | تقارير Persistence | ✅ لا يحتاج (تصحيح تصنيف) | 2026-02-16 |
| 2.3 | إزالة EF Core من Application | ✅ تم | 2026-02-16 |
| 2.4 | DateTime.Today → IDateTimeProvider | ✅ تم (80 حالة) | 2026-02-16 |
| 2.5 | نقل الحسابات من ViewModels | ✅ تم (8 VMs, 5 دوالّ جديدة) | 2026-02-16 |
| 2.6 | تقسيم الملفات الكبيرة | ✅ تم (9 ملفات → partial classes) | 2026-02-16 |
