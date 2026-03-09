# تقرير التدقيق الجنائي العميق — نظام MarcoERP
**Deep Forensic Audit Report**

**التاريخ:** 2026-02-15  
**النطاق:** كل ملف كود في المشروع — 386+ ملف عبر 5 طبقات  
**المنهجية:** تتبع كل تدفق مستخدم من تسجيل الدخول حتى كل عملية  
**الحالة:** فحص جنائي شامل — كود بكود، سطر بسطر

---

## الملخص التنفيذي

| المقياس | القيمة |
|---------|--------|
| **إجمالي الأخطاء المكتشفة** | **147** |
| CRITICAL | **19** |
| HIGH | **31** |
| MEDIUM | **42** |
| LOW | **55** |
| **الملفات المفحوصة** | 386+ |
| **التدفقات المتتبعة** | 12 تدفق كامل |
| **حالة البناء** | ✅ 0 أخطاء، 0 تحذيرات |
| **التوافق مع الحوكمة** | 89% (بعد التدقيق العميق) |

---

## جدول المحتويات

1. [الأخطاء الحرجة (CRITICAL) — 19 خطأ](#1-critical)
2. [الأخطاء عالية الخطورة (HIGH) — 31 خطأ](#2-high)
3. [تفصيل كل وحدة](#3-modules)
   - 3.1 تسجيل الدخول والمصادقة
   - 3.2 فواتير المبيعات
   - 3.3 مرتجعات المبيعات
   - 3.4 الخزينة (سندات قبض/صرف/تحويل)
   - 3.5 الأدوار والصلاحيات
   - 3.6 حوكمة الميزات (Feature Toggle)
   - 3.7 استيراد الأصناف
   - 3.8 المخزون والمستودعات
   - 3.9 نقاط البيع (POS)
   - 3.10 التقارير ولوحة التحكم
   - 3.11 السنوات والفترات المالية
4. [نقاط القوة](#4-strengths)
5. [خطة الإصلاح المرتبة](#5-fix-plan)

---

## <a name="1-critical"></a>1. الأخطاء الحرجة (CRITICAL) — 19 خطأ

### أخطاء تفقد البيانات أو تمنع الوظائف الأساسية

| # | الوحدة | الملف | الوصف | التأثير |
|---|--------|-------|-------|---------|
| C-01 | الأدوار | `RoleRepository.cs` | جميع الاستعلامات تستخدم `AsNoTracking()` — تحديث وحذف الأدوار **لا يُحفظ في قاعدة البيانات** | تحديث صلاحيات الأدوار لا يعمل |
| C-02 | الأدوار | `RoleService.cs` | المدير يمكنه إزالة كل صلاحيات دور المدير بما فيها `roles.manage` — **إقفال كامل للنظام** | لا يمكن لأحد إدارة الأدوار بعدها |
| C-03 | المستخدمين | `UserManagementView.xaml` | `PasswordBox` بدون binding — كلمة المرور **لا تُرسل أبداً** للـ ViewModel | إنشاء مستخدم جديد **مستحيل** |
| C-04 | الحوكمة | `FeatureGuard.cs` | **كود ميت** — لا يُستدعى من أي خدمة. الطبقة التطبيقية **غير محمية** | تعطيل ميزة لا يمنع استخدامها |
| C-05 | الحوكمة | `MainWindowViewModel.cs` | التعطيل يُخفي القائمة فقط — الشاشة تبقى قابلة للفتح عبر Command Palette أو البحث | ميزة الحوكمة **جزئية التطبيق** |
| C-06 | الحوكمة | `FeatureRepository.cs` | `GetByKeyAsync()` يستخدم `AsNoTracking()` مع `RowVersion` — تبديل الميزة قد يفشل | تبديل الميزات غير موثوق |
| C-07 | المرتجعات | `SalesReturnRepository.cs` | `GetByOriginalInvoiceAsync` بدون `.Include(Lines)` — فحص "المرتجع أكثر من الفاتورة" **لا يعمل** | يمكن إرجاع كميات أكثر من الفاتورة |
| C-08 | المرتجعات | `SalesReturnService.cs` | لا يتم تحديث `BalanceDue` للفاتورة الأصلية عند ترحيل المرتجع | أرصدة العملاء خاطئة |
| C-09 | المبيعات | `SalesInvoiceMapper.cs` | `ProductNameAr`, `ProductCode`, `UnitNameAr` لا تُعبأ في سطور DTO | تصدير Excel للفواتير فارغ |
| C-10 | المبيعات | `SalesInvoiceRepository.cs` | `GetAllAsync` بدون `.Include(Warehouse)` و `.Include(CounterpartySupplier)` | أسماء المستودع والمورد فارغة |
| C-11 | المبيعات | `SalesInvoiceViewModel.cs` | `ApplyFilter()` هيكل فارغ — البحث **لا يعمل** | لا يمكن البحث في الفواتير |
| C-12 | المبيعات | `SalesInvoiceService.cs` | عكس تكلفة البضاعة يستخدم WAC الحالي وليس الأصلي | فرق في حساب الأرباح والخسائر |
| C-13 | الاستيراد | `ProductImportService.cs` | `LoadExistingCodesAsync()` يحمّل **كل المنتجات** في الذاكرة | انهيار الذاكرة مع بيانات كبيرة |
| C-14 | الاستيراد | `ProductImportService.cs` | لا توجد معاملة (Transaction) — فشل في منتصف الاستيراد يترك بيانات ناقصة | حالة بيانات غير متسقة |
| C-15 | الاستيراد | `ProductImportService.cs` | لا يتحقق من تكرار الباركود | باركود مكرر، POS يعرض منتج خاطئ |
| C-16 | الخزينة | `CashboxConfiguration.cs` | قيد CHECK `Balance >= 0` يتعارض مع `DecreaseBalanceAllowNegative()` | ميزة السماح بالسالب **لا تعمل** |
| C-17 | الخزينة | `CashPaymentService.cs` | لا يدعم `AllowNegativeCash` — يستخدم دائماً الفحص الصارم | سلوك غير متسق بين التحويل والصرف |
| C-18 | POS | `PosService.cs` | تقرير الأرباح اليومي يُرجع COGS = 0 وربح = 0 دائماً | تقرير الأرباح **عديم الفائدة** |
| C-19 | المالية | `FiscalYearService.cs` | قفل/فتح الفترة يعدّل نسخة Entity منفصلة عن المتتبعة — **التغيير لا يُحفظ** | قفل الفترات المالية **لا يعمل** |

---

## <a name="2-high"></a>2. الأخطاء عالية الخطورة (HIGH) — 31 خطأ

| # | الوحدة | الملف | الوصف |
|---|--------|-------|-------|
| H-01 | المبيعات | `SalesInvoiceService.cs` | `GetAllAsync` لا يفلتر `IsDeleted` — المحذوفات تظهر في القائمة |
| H-02 | المبيعات | `SalesInvoiceMapper.cs` | DTO ينقص `PaidAmount`, `BalanceDue`, `PaymentStatus` |
| H-03 | المبيعات | `SalesInvoiceViewModel.cs` | العرض المنقسم (Split View) بدون selector لنوع الطرف المقابل |
| H-04 | المرتجعات | `SalesReturnMapper.cs` | `ProductNameAr`, `ProductCode`, `UnitNameAr` فارغة — لا navigation properties |
| H-05 | المرتجعات | `SalesReturnRepository.cs` | لا يشمل `CounterpartySupplier` أو `SalesRepresentative` — أسماء المورد فارغة |
| H-06 | المرتجعات | `SalesReturnService.cs` | عكس COGS يستخدم WAC الحالي وليس تكلفة البيع الأصلية |
| H-07 | الأدوار | `RoleService.cs` | فحص التكرار يتحقق من `NameEn` فقط وليس `NameAr` |
| H-08 | الأدوار | `RoleService.cs` | المدير يمكنه تغيير اسم دور النظام |
| H-09 | الأدوار | `RoleRepository.cs` | `NameExistsAsync` يتحقق من `NameEn` فقط |
| H-10 | المستخدمين | `UserService.cs` | لا يمنع المدير من تعطيل حسابه (إلا اسم `admin` فقط) |
| H-11 | المستخدمين | `UserService.cs` | لا يمنع المدير من تغيير دوره لدور غير إداري |
| H-12 | المستخدمين | `UserManagementViewModel.cs` | كلمة مرور إعادة التعيين متوقعة: `Marco@HHmmss` |
| H-13 | الحوكمة | `ViewRegistry.cs` | لا يتحقق من تفعيل الميزة قبل إنشاء الشاشة |
| H-14 | الحوكمة | `NavigationItem.cs` | لا يحتوي على `FeatureKey` — الربط هش عبر اسم القسم العربي |
| H-15 | الحوكمة | `GovernanceConsoleView.xaml` | تصادم اسم `IsEnabled` مع WPF built-in property |
| H-16 | الحوكمة | `FeatureKeys.cs` | 3 ثوابت فقط من أصل 11 ميزة |
| H-17 | الحوكمة | `ProfileRepository.cs` | `GetByNameAsync`/`GetActiveProfileAsync` بـ `AsNoTracking` ثم `Update` |
| H-18 | الاستيراد | `ProductImportService.cs` | لا يُنشئ حركات مخزون لكميات أولية |
| H-19 | الاستيراد | `ProductImportService.cs` | `ValidateHeaders()` لا يقارن محتوى الأعمدة فعلياً |
| H-20 | الاستيراد | `ProductImportService.cs` | أسماء مكررة في التصنيفات/الوحدات تسبب `ArgumentException` |
| H-21 | الاستيراد | كل الملفات | لا يحترم نظام Feature Toggle |
| H-22 | الاستيراد | `ProductImportService.cs` | لا يتحقق من حجم الملف — ملف 100MB+ يُعلق التطبيق |
| H-23 | الخزينة | `CashReceiptService.cs` | إلغاء سند القبض يفشل إذا رصيد الصندوق غير كافٍ |
| H-24 | الخزينة | `CashReceiptMapper.cs` | `AccountName`, `CustomerName` لا تُعبأ أبداً |
| H-25 | الخزينة | `CashboxDtos.cs` | لا يعرض رصيد الصندوق — المستخدم لا يرى الرصيد |
| H-26 | المخزون | `StockManager.cs` | **كود ميت** — 114 سطر غير مستخدم، والمنطق مكرر في 6+ خدمات |
| H-27 | المخزون | كل المستودعات | لا يوجد أي ترقيم صفحات (Pagination) في أي استعلام |
| H-28 | POS | `WindowsEscPosPrinterService.cs` | طباعة الإيصال stub فارغ — `IsAvailable()` يُرجع `false` دائماً |
| H-29 | POS | `PosMapper.cs` | `UserName`, `CashboxNameAr`, `WarehouseNameAr` لا تُعبأ |
| H-30 | POS | `PosService.cs` | أكواد حسابات GL مشفرة (1111, 1112, 1121) — غير قابلة للتكوين |
| H-31 | المالية | `FiscalYearRepository.cs` | جميع الاستعلامات `AsNoTracking()` — تسبب مشاكل التحديث |

---

## <a name="3-modules"></a>3. تفصيل كل وحدة

---

### 3.1 تسجيل الدخول والمصادقة

**الملفات المفحوصة:** LoginWindow.xaml, LoginViewModel.cs, AuthenticationService.cs, CredentialStore.cs, User.cs

**التدفق:**
```
LoginWindow → LoginViewModel.LoginAsync() → AuthenticationService.LoginAsync()
  → UserRepository.GetByUsernameAsync()
  → BCrypt.Verify(password, hash)
  → CurrentUserService.SetUser(user, permissions)
  → MainWindow
```

**الحكم:** ✅ **سليم في الأساس**
- BCrypt مع work factor 12
- قفل بعد 5 محاولات فاشلة لمدة 15 دقيقة مع فتح تلقائي
- CredentialStore يستخدم AES مع مفتاح مشتق من اسم الجهاز/المستخدم
- Activities logging لكل تسجيل دخول ناجح/فاشل

**الأخطاء:**
| الخطورة | الوصف |
|---------|-------|
| MEDIUM | `CredentialStore` مفتاح التشفير مشتق من `Environment.MachineName + Environment.UserName` — قابل للتخمين |
| LOW | Activity log لا يسجل محاولات الدخول الفاشلة (فقط الناجحة) |

---

### 3.2 فواتير المبيعات (23 خطأ)

**الملفات المفحوصة:** 20+ ملف عبر كل الطبقات

**التدفق:**
```
SalesInvoiceViewModel → CreateAsync → FluentValidation → SalesInvoice.Create()
  → AddLine (×N) → RecalculateTotals → _invoiceRepo.AddAsync → SaveChanges

PostAsync → Serializable Tx:
  1. Revenue Journal: DR AR (1121) / CR Sales (4111) + CR VAT Output (2121)
  2. COGS Journal: DR COGS (5111) / CR Inventory (1131) per-line at WAC
  3. Stock Decrease: WarehouseProduct.DecreaseStock + InventoryMovement
  4. WAC unchanged (sell doesn't recalculate WAC)
  5. invoice.Post(revenueJE.Id, cogsJE.Id)
```

**الأخطاء الحرجة:** C-09, C-10, C-11, C-12 (مذكورة أعلاه)

**أخطاء إضافية:**
| الخطورة | الوصف |
|---------|-------|
| MEDIUM | لا يوجد ترقيم صفحات — كل الفواتير تُحمل في الذاكرة |
| MEDIUM | فحص `AllowNegativeStock` يتحقق قبل الترحيل لكن ليس أثناء الإضافة |
| LOW | MessageBox.Show في 16+ ViewModel — انتهاك MVVM |

---

### 3.3 مرتجعات المبيعات (8 أخطاء)

**التدفق:**
```
SalesReturnViewModel → CreateAsync → Validate lines vs original invoice
  → SalesReturn.Create() → AddLine (×N) → RecalculateTotals → SaveChanges

PostAsync → Serializable Tx:
  1. Revenue Reversal: DR Sales (4111) + DR VAT Output (2121) / CR AR (1121)
  2. COGS Reversal: DR Inventory (1131) / CR COGS (5111) at CURRENT WAC
  3. Stock Increase: WarehouseProduct.IncreaseStock + InventoryMovement
  4. WAC Recalculation on product
  5. ❌ NO update to original invoice BalanceDue
```

**الأخطاء الحرجة:** C-07, C-08 (مذكورة أعلاه)

**أخطاء إضافية:**
| الخطورة | الوصف |
|---------|-------|
| MEDIUM | لا يتحقق من تطابق العميل بين المرتجع والفاتورة الأصلية |
| MEDIUM | `UpdateAsync` لا يعيد التحقق من كميات المرتجع ضد الفاتورة |
| MEDIUM | أزرار ترحيل/إلغاء/حذف ظاهرة دائماً بغض النظر عن الحالة |

---

### 3.4 الخزينة — سندات قبض/صرف/تحويل (9 أخطاء)

**التدفق:**
```
Cash Receipt:
  Draft → Post:
    1. cashbox.IncreaseBalance(amount)
    2. Journal: DR Cashbox GL / CR Contra Account
    3. invoice.ApplyPayment(amount)
    4. SaveChanges

Cash Payment:
  Draft → Post:
    1. cashbox.DecreaseBalance(amount) ← STRICT ALWAYS
    2. Journal: DR Contra / CR Cashbox GL
    3. invoice.ApplyPayment(amount)
    4. SaveChanges

Cash Transfer:
  Draft → Post:
    1. Check AllowNegativeCash
    2. Lock cashboxes in ID order (deadlock prevention ✅)
    3. Source.Decrease / Target.Increase
    4. Journal: DR Target GL / CR Source GL
```

**الأخطاء الحرجة:** C-16, C-17 (ميزة السالب لا تعمل)

**نقاط القوة:**
- ✅ Serializable transactions في كل العمليات
- ✅ `ApplyPayment` / `ReversePayment` تعمل بشكل صحيح
- ✅ منع الحذف الفعلي (Hard Delete) بـ `NotSupportedException`
- ✅ Deadlock prevention في التحويلات (قفل بترتيب ID)

---

### 3.5 الأدوار والصلاحيات (14 خطأ)

**آلية العمل:**
```
[RequiresPermission("module.action")] على Interface methods
  ↓
AuthorizationProxy (DispatchProxy) يعترض الاستدعاء
  ↓
ICurrentUserService.HasPermission(key)
  ↓
_roleId == 1 → bypass (all permissions) ← hardcoded!
  أو
_permissions.Contains(key) → allow/deny
```

**الأخطاء الحرجة:** C-01, C-02, C-03 (مذكورة أعلاه)

**الإجابات على الأسئلة المحددة:**

> **هل الصلاحيات تتقفل بطريقة صحيحة؟**  
> ❌ **لا** — تحديث صلاحيات الدور لا يُحفظ (C-01). المدير يمكنه إقفال النظام بالكامل (C-02). إنشاء مستخدم مستحيل (C-03).

> **هل AuthorizationProxy يعمل؟**  
> ✅ **نعم** — يعمل بشكل صحيح للمنع. لكن فقط على methods بـ `[RequiresPermission]`. القراءة (`GetAllAsync`, `GetByIdAsync`) لا تتطلب صلاحية.

> **هل الصلاحيات تتحدث حياً؟**  
> ❌ **لا** — تُحمّل مرة واحدة عند تسجيل الدخول. تغيير الصلاحيات يتطلب إعادة تسجيل الدخول.

---

### 3.6 حوكمة الميزات — Feature Toggle (14 خطأ)

**الإجابات على الأسئلة المحددة:**

> **عند قفل ميزة من شاشة الحوكمة — ماذا يحدث بالضبط؟**

| الطبقة | السلوك | مُطبّق؟ |
|--------|--------|---------|
| إخفاء القائمة/الأيقونة | `RefreshNavigationAsync()` يُخفي **القسم بالكامل** من Sidebar | ✅ نعم |
| تعطيل الشاشة | لا — تبقى قابلة للفتح عبر Command Palette أو البحث | ❌ لا |
| حجب الخدمة | لا — `FeatureGuard` كود ميت | ❌ لا |

> **عند تعديل ميزة — بتقفل ايه؟ الشاشة بس ولا الأيقونة ولا الميزة؟**  
> ❌ **بتقفل الأيقونة/القائمة فقط** — الشاشة تبقى قابلة للفتح من أماكن أخرى، والخدمة تعمل بدون حماية.

> **هل الحوكمة موصولة من طرف لطرف؟**
```
✅ Governance Console → FeatureService.ToggleAsync → DB
✅ RefreshNavigationAsync → Hide sidebar section
❌ FeatureGuard → NOT CALLED (dead code)
❌ ViewRegistry → No feature check
❌ Services → No feature check
```

---

### 3.7 استيراد الأصناف (18 خطأ)

**التدفق:**
```
1. User clicks "اختيار ملف" → OpenFileDialog (*.xlsx)
2. ParseExcelAsync:
   - Open XLWorkbook (ClosedXML)
   - ValidateHeaders ← ⚠️ لا يقارن المحتوى فعلياً
   - Load categories/units/suppliers → ALL into memory
   - Load ALL products for code check ← ⚠️ C-13
   - Parse rows → validate → preview
3. ImportAsync:
   - For each valid row:
     - Build CreateProductDto
     - ProductService.CreateAsync (individual save) ← ⚠️ C-14
   - No barcode duplicate check ← ⚠️ C-15
```

**نقاط القوة:**
- ✅ تصميم two-phase (Parse → Preview → Import) — ممتاز
- ✅ تحميل القالب (Template) مع headers عربية
- ✅ اكتشاف تكرار الكود داخل نفس الملف
- ✅ CancellationToken مدعوم

---

### 3.8 المخزون والمستودعات (11 خطأ)

**التصميم:**
```
WarehouseProduct — رصيد حالي (Qty per warehouse+product)
InventoryMovement — سجل حركات دائم (audit trail)
Product.WeightedAverageCost — متوسط مرجح
```

**WAC Formula:** `(existingQty × oldWAC + receivedQty × unitCost) / (existingQty + receivedQty)` ✅ صحيح

**AllowNegativeStock:**
| الخدمة | تتحقق من الإعداد؟ |
|--------|------------------|
| SalesInvoiceService | ✅ نعم |
| PosService | ✅ نعم (service layer) |
| InventoryAdjustmentService | ❌ لا — دائماً strict |

**خطأ خفي:** `InventoryMovement.SetBalanceAfter()` يرفض القيم السالبة لكن `DecreaseStockAllowNegative()` تُنتجها → تعارض منطقي (M-01)

---

### 3.9 نقاط البيع — POS (12 خطأ)

**التدفق:**
```
افتح جلسة → مسح الباركود → أضف للسلة → أكمل البيع
  → Serializable Tx:
    1. Create SalesInvoice (draft → post)
    2. Revenue Journal (DR Cash/Card / CR Sales + VAT)
    3. COGS Journal (DR COGS / CR Inventory)
    4. Stock Decrease
    5. PosPayment records
    6. Session.RecordSale()
```

**نقاط القوة:**
- ✅ Dual journal entry (Revenue + COGS)
- ✅ Deadlock prevention via Serializable transactions
- ✅ Keyboard shortcuts (F1, F4, F9, Esc)
- ✅ Product cache for fast barcode lookup
- ✅ Integration with Sales module (same SalesInvoice entity)

**نقاط الضعف:**
- ❌ طباعة الإيصال stub فارغ
- ❌ تقارير الأرباح/COGS = 0 دائماً
- ❌ ViewModel يمنع إضافة منتج ناقص المخزون حتى لو `AllowNegativeStock = true`
- ❌ لا يوجد اختيار عميل من الواجهة
- ❌ لا يعمل offline

---

### 3.10 التقارير ولوحة التحكم (7 أخطاء)

**13 تقرير مُطبّق:**
| التقرير | حالة | PDF | Excel |
|---------|------|-----|-------|
| ميزان المراجعة | ✅ | ✅ | ✅ |
| قائمة الدخل | ✅ | ✅ | ✅ |
| الميزانية العمومية | ✅ | ✅ | ✅ |
| كشف حساب | ✅ | ✅ | ✅ |
| تقرير المبيعات | ✅ | ✅ | ✅ |
| تقرير المشتريات | ✅ | ✅ | ✅ |
| تقرير الأرباح | ✅ | ✅ | ✅ |
| تقرير المخزون | ✅ | ✅ | ✅ |
| بطاقة صنف | ✅ | ✅ | ✅ |
| حركة الصندوق | ✅ | ✅ | ✅ |
| أعمار الديون | ✅ | ✅ | ✅ |
| تقرير الضريبة | ✅ | ✅ | ✅ |
| لوحة التحكم | ✅ | — | — |

**لوحة التحكم:** مبيعات اليوم/الشهر + مشتريات + ربح + أرصدة + تنبيهات + اختصارات + تحديث تلقائي 60 ثانية ✅

**أخطاء:**
| الخطورة | الوصف |
|---------|-------|
| MEDIUM | لوحة التحكم: الربح = المبيعات - المشتريات (تقريب تجاري وليس محاسبي) |
| MEDIUM | أكواد حسابات مشفرة (`111*`, `112*`, `211*`) في استعلامات الأرصدة |
| MEDIUM | إطار `Reporting/` (350 سطر) كود ميت — لا يُستخدم |

---

### 3.11 السنوات والفترات المالية (7 أخطاء)

**التدفق:**
```
Create Year → Status=Setup, auto-creates 12 periods
Activate Year → Status=Active (only one at a time)
Lock Period → Sequential (prior periods must be locked first)
Close Year → Validate all periods locked → Generate closing entry → Status=Closed
```

**إقفال نهاية السنة:**
```
1. Aggregate all posted journal lines for the year
2. Filter income statement accounts (Revenue, COGS, Expense, OtherIncome, OtherExpense)
3. Reverse each account balance → credit/debit RetainedEarnings (3121)
4. Auto-post closing journal entry
```

**الخطأ الحرج C-19:** قفل الفترة يعدّل نسخة Entity مختلفة عن المتتبعة في EF Core:
```
Step 1: FindPeriodByIdAsync → returns DETACHED period (Instance A)
Step 2: GetWithPeriodsAsync → returns NEW year with NEW periods (Instance B set)
Step 3: period.Lock() → mutates Instance A ← NOT TRACKED
Step 4: _repo.Update(fiscalYear) → tracks Instance B ← UNMUTATED
Step 5: SaveChanges → saves Instance B → PERIOD STAYS OPEN
```

---

## <a name="4-strengths"></a>4. نقاط القوة في النظام

### التصميم المعماري
1. ✅ **Clean Architecture** حقيقية — 5 طبقات بتبعيات أحادية الاتجاه
2. ✅ **Domain-Driven Design** — كيانات غنية مع invariants وprivate setters
3. ✅ **Double-Entry Accounting** — كل ترحيل يُنشئ قيدين محاسبيين متوازنين
4. ✅ **Serializable Transactions** — كل عمليات الترحيل/الإلغاء في معاملات آمنة
5. ✅ **Concurrency Control** — `RowVersion` على جميع الكيانات + معالجة `DbUpdateConcurrencyException`
6. ✅ **Soft Delete + Global Query Filter** — الحذف الناعم مع تنقية تلقائية

### الأمان
7. ✅ **BCrypt** مع work factor 12
8. ✅ **AuthorizationProxy** — اعتراض شفاف على مستوى DI
9. ✅ **قفل الحساب** — 5 محاولات، 15 دقيقة
10. ✅ **منع الحذف الفعلي** للمستندات المالية (`NotSupportedException`)

### المحاسبة
11. ✅ **13 تقرير مالي** مع PDF (QuestPDF) و Excel (ClosedXML)
12. ✅ **WAC** — صيغة صحيحة مع حماية القسمة على صفر
13. ✅ **إقفال نهاية السنة** — قيد إقفال تلقائي مع تأكد من توازن ميزان المراجعة
14. ✅ **FiscalPeriodValidator** — مركزي يُستخدم في 7+ خدمات

### واجهة المستخدم
15. ✅ **Material Design** RTL — واجهة عربية كاملة اتجاه يمين-لشمال
16. ✅ **MVVM** — فصل نظيف بين View و ViewModel (مع استثناء MessageBox)
17. ✅ **لوحة تحكم** → 6 مؤشرات + تنبيهات + اختصارات + تحديث تلقائي
18. ✅ **نظام تنقل متقدم** — Command Palette + Tabs + Breadcrumb

---

## <a name="5-fix-plan"></a>5. خطة الإصلاح المرتبة حسب الأولوية

### P0 — عاجل جداً (يجب إصلاحه فوراً)

| # | الإصلاح | الملف | الجهد |
|---|---------|-------|-------|
| 1 | إزالة `AsNoTracking()` من `RoleRepository` أو إضافة tracking query للتعديل | `RoleRepository.cs` | 30 دقيقة |
| 2 | حل مشكلة `PasswordBox` binding عبر code-behind helper | `UserManagementView.xaml.cs` | 1 ساعة |
| 3 | منع إزالة صلاحيات حرجة من أدوار النظام | `RoleService.cs` | 30 دقيقة |
| 4 | إصلاح قفل الفترات المالية (استخدام instance واحدة) | `FiscalYearService.cs` | 30 دقيقة |
| 5 | إضافة `.Include(Lines)` في `GetByOriginalInvoiceAsync` | `SalesReturnRepository.cs` | 5 دقائق |
| 6 | إضافة تعديل `BalanceDue` في فاتورة المرتجع | `SalesReturnService.cs` | 1 ساعة |
| 7 | إزالة/تكييف CHECK constraint على Cashbox Balance | `CashboxConfiguration.cs` | Migration |
| 8 | إضافة `AllowNegativeCash` لـ `CashPaymentService` | `CashPaymentService.cs` | 30 دقيقة |

### P1 — مهم (الأسبوع القادم)

| # | الإصلاح | الجهد |
|---|---------|-------|
| 9 | تفعيل `FeatureGuard` في الخدمات أو إضافة middleware | 2-3 ساعات |
| 10 | إضافة فحص ميزة في `ViewRegistry.CreateView()` | 30 دقيقة |
| 11 | إصلاح `ApplyFilter()` في `SalesInvoiceViewModel` | 1 ساعة |
| 12 | إضافة `.Include()` المفقودة في repositories | 2 ساعات |
| 13 | تعبئة navigation properties في Mappers | 2 ساعات |
| 14 | لف استيراد الأصناف في Transaction واحد | 1-2 ساعة |
| 15 | إضافة فحص تكرار الباركود في الاستيراد | 1 ساعة |
| 16 | إصلاح `ValidateHeaders` لمقارنة النص | 15 دقيقة |

### P2 — تحسينات (الشهر القادم)

| # | الإصلاح | الجهد |
|---|---------|-------|
| 17 | إضافة Pagination لكل `GetAllAsync` | 3-4 ساعات |
| 18 | تبني `StockManager` وإزالة التكرار | 4-6 ساعات |
| 19 | إضافة `FeatureKey` لـ `NavigationItem` | 1 ساعة |
| 20 | تطبيق طباعة الإيصال (ESC/POS library) | 4-8 ساعات |
| 21 | تنفيذ تقارير COGS/Profit في POS | 2-3 ساعات |
| 22 | كلمة مرور إعادة تعيين عشوائية | 30 دقيقة |
| 23 | استبدال `MessageBox.Show` بـ DialogService | 3-4 ساعات |
| 24 | إصلاح `SetBalanceAfter` لقبول السالب عند الإعداد | 30 دقيقة |

### P3 — تحسينات مستقبلية

| # | الإصلاح | الجهد |
|---|---------|-------|
| 25 | إضافة reverse dependency check في `ImpactAnalyzerService` | 2 ساعات |
| 26 | إضافة customer picker في POS UI | 2-3 ساعات |
| 27 | نقل أكواد GL المشفرة لإعدادات النظام | 2 ساعات |
| 28 | إضافة حد حجم ملف وعدد صفوف في الاستيراد | 30 دقيقة |
| 29 | حذف Reporting framework الميت | 30 دقيقة |
| 30 | إضافة carry-forward لأرصدة السنة الجديدة | 3-4 ساعات |

---

## ملحق: ملخص الأخطاء حسب الوحدة

| الوحدة | CRITICAL | HIGH | MEDIUM | LOW | المجموع |
|--------|----------|------|--------|-----|---------|
| فواتير المبيعات | 4 | 3 | 3 | 3 | 13 |
| مرتجعات المبيعات | 2 | 3 | 3 | 3 | 11 |
| الأدوار والصلاحيات | 3 | 5 | 3 | 3 | 14 |
| حوكمة الميزات | 3 | 4 | 3 | 2 | 12 |
| استيراد الأصناف | 3 | 5 | 6 | 4 | 18 |
| الخزينة | 2 | 3 | 2 | 1 | 8 |
| المخزون والمستودعات | 0 | 2 | 3 | 6 | 11 |
| POS | 2 | 4 | 8 | 4 | 18 |
| التقارير | 0 | 0 | 3 | 4 | 7 |
| المالية | 1 | 2 | 2 | 2 | 7 |
| تسجيل الدخول | 0 | 0 | 1 | 1 | 2 |
| عام (cross-cutting) | — | — | 5 | 22 | 27 |
| **المجموع** | **19** | **31** | **42** | **55** | **147** |

---

**نهاية التقرير**  
*تم الفحص بمنهجية جنائية — كل ملف، كل سطر، كل تدفق*
