# Application Layer — Deep Audit Report

**Date:** 2026-02-14  
**Auditor:** Senior Software Engineer (Automated Deep Audit)  
**Scope:** All files under `src/MarcoERP.Application/` — Services, Common, Reporting, DTOs, Interfaces, Validators, Mappers  
**Files Audited:** 65+ files across Accounting, Inventory, Purchases, Sales, Treasury, Security, Settings, Common, Reporting  

---

## Executive Summary

The Application layer is **structurally sound** with consistent patterns: `ServiceResult<T>` wrapping, `FluentValidation`, `IUnitOfWork` transactions, `Serializable` isolation for posting operations, and centralized fiscal period validation via `FiscalPeriodValidator`. The `AuthorizationProxy` + `RequiresPermissionAttribute` pattern is well-designed. The `JournalEntryFactory` successfully centralizes journal creation across 13+ services.

However, the audit identified **5 Critical**, **8 High**, **10 Medium**, and **6 Low** severity issues that should be addressed before production deployment.

---

## CRITICAL Issues

### C-01: StockManager Exists but is Dead Code — All Services Inline Stock Logic

**Files:**  
- [Common/StockManager.cs](../src/MarcoERP.Application/Common/StockManager.cs)  
- [Services/Purchases/PurchaseInvoiceService.cs](../src/MarcoERP.Application/Services/Purchases/PurchaseInvoiceService.cs)  
- [Services/Purchases/PurchaseReturnService.cs](../src/MarcoERP.Application/Services/Purchases/PurchaseReturnService.cs)  
- [Services/Sales/SalesInvoiceService.cs](../src/MarcoERP.Application/Services/Sales/SalesInvoiceService.cs)  
- [Services/Sales/SalesReturnService.cs](../src/MarcoERP.Application/Services/Sales/SalesReturnService.cs)  
- [Services/Sales/PosService.cs](../src/MarcoERP.Application/Services/Sales/PosService.cs)  
- [Services/Inventory/InventoryAdjustmentService.cs](../src/MarcoERP.Application/Services/Inventory/InventoryAdjustmentService.cs)  

**Description:**  
`StockManager` was created to centralize `WarehouseProduct` increase/decrease + `InventoryMovement` recording (documented as "replacing duplication across 10+ services"). However, **zero services actually use it**. Every posting service (PurchaseInvoice, PurchaseReturn, SalesInvoice, SalesReturn, POS, InventoryAdjustment) manually inlines:
1. `_whProductRepo.GetOrCreateAsync()` / `GetAsync()`
2. `whProduct.IncreaseStock()` / `DecreaseStock()`
3. `new InventoryMovement(...)` + `movement.SetBalanceAfter()` + `_movementRepo.AddAsync()`

**Risk:** Any bug fix to stock logic must be applied in 6+ places. Inconsistent `AllowNegativeStock` handling between services. `SalesReturnService.ReverseStockAsync` checks `whProduct.Quantity < line.BaseQuantity` but `PurchaseReturnService` does not.

**Recommendation:** Migrate all inline stock operations to `StockManager`. This is the #1 architectural debt.

---

### C-02: POS Daily/Profit Reports Return Hardcoded Zeros

**File:** [Services/Sales/PosService.cs](../src/MarcoERP.Application/Services/Sales/PosService.cs)  
**Lines:** ~1050-1100 (GetDailyReportAsync), ~1100-1150 (GetProfitReportAsync)  

**Description:**  
Both reporting methods return `TotalCogs = 0` and `GrossProfit = 0` as hardcoded values. These are NOT calculated from actual inventory cost data. Any POS user relying on these reports sees zero for cost-of-goods and profit — a **silent data integrity failure**.

```csharp
// In GetDailyReportAsync:
TotalCogs = 0,       // ← Hardcoded
GrossProfit = 0,     // ← Hardcoded
```

**Risk:** Business decisions based on POS reports will be made with zero visibility into actual profitability. This is a financial reporting failure.

**Recommendation:** Calculate COGS from `InventoryMovement` records where `SourceType = POS` for the given date/session. GrossProfit = TotalSales - TotalCogs.

---

### C-03: InventoryAdjustmentService Silently Skips Journal Lines When Account is Null

**File:** [Services/Inventory/InventoryAdjustmentService.cs](../src/MarcoERP.Application/Services/Inventory/InventoryAdjustmentService.cs)  
**Lines:** ~345-370 (within ExecutePostAsync)

**Description:**  
When building journal lines for inventory adjustments, the code checks if each GL account (Inventory, AdjustmentIncome, AdjustmentExpense) exists. If an account is null, the line is **silently skipped** rather than throwing an error. This can result in a **one-sided journal entry** (only Debit, no Credit, or vice versa), which violates double-entry accounting.

```csharp
if (inventoryAccount != null)
    lines.Add(new JournalLineSpec(inventoryAccount.Id, surplusTotal, 0, ...));
if (adjustmentIncomeAccount != null)
    lines.Add(new JournalLineSpec(adjustmentIncomeAccount.Id, 0, surplusTotal, ...));
```

If `adjustmentIncomeAccount` is null but `inventoryAccount` exists, you get a debit-only journal.

**Risk:** Unbalanced journal entries violate accounting fundamentals. Trial balance will not balance.

**Recommendation:** Throw `InvalidOperationException` if any required GL account is missing (like `SalesReturnService.ResolveAccountsAsync` correctly does).

---

### C-04: JournalEntryFactory Has No Debit=Credit Balance Validation

**File:** [Common/JournalEntryFactory.cs](../src/MarcoERP.Application/Common/JournalEntryFactory.cs)  
**Lines:** 60-95

**Description:**  
`CreateAndPostAsync()` creates a journal, adds lines, generates a number, and posts — but **never validates that total debits equal total credits** before posting. It relies on the domain entity's `Post()` method to call `Validate()`, but if the domain entity's `Validate()` doesn't check balance (or has a bug), unbalanced journals will persist.

This is compounded by C-03 above, where callers may pass unbalanced line arrays.

**Recommendation:** Add explicit guard at the factory level:
```csharp
var totalDebit = lines.Sum(l => l.Debit);
var totalCredit = lines.Sum(l => l.Credit);
if (totalDebit != totalCredit)
    throw new InvalidOperationException($"Journal unbalanced: DR={totalDebit} CR={totalCredit}");
```

---

### C-05: CashReceiptService TOCTOU Vulnerability on Invoice Balance Validation

**File:** [Services/Treasury/CashReceiptService.cs](../src/MarcoERP.Application/Services/Treasury/CashReceiptService.cs)  
**Lines:** ~130-145 (CreateAsync invoice balance check) vs ~300-335 (ExecutePostAsync)

**Description:**  
`CreateAsync` validates that the receipt amount does not exceed the invoice `BalanceDue`:
```csharp
if (dto.Amount > linkedInvoice.BalanceDue)
    return Failure("...");
```
But `ExecutePostAsync` does **not** re-validate the invoice balance inside the Serializable transaction. Between creation and posting, another receipt/payment could have reduced the `BalanceDue`, making the receipt amount larger than the remaining balance. The `invoice.ApplyPayment(reloaded.Amount)` call at line ~325 could over-pay the invoice.

The same issue exists in `CashPaymentService.PostAsync` (line ~270) — payment amount vs purchase invoice balance not re-validated inside the transaction.

**Risk:** Overpayment on invoices. Invoice balance goes negative.

**Recommendation:** Re-validate `receipt.Amount <= invoice.BalanceDue` inside `ExecutePostAsync`, within the Serializable transaction.

---

## HIGH Issues

### H-01: BulkPriceUpdateService Has No Transaction Boundary

**File:** [Services/Inventory/BulkPriceUpdateService.cs](../src/MarcoERP.Application/Services/Inventory/BulkPriceUpdateService.cs)  
**Lines:** ApplyAsync method

**Description:**  
`ApplyAsync` updates product prices in a loop and calls `_unitOfWork.SaveChangesAsync()` once at the end. However, it does not wrap the entire operation in `ExecuteInTransactionAsync()`. If `SaveChangesAsync()` fails partway (e.g., from a constraint violation), some products may have been modified in the EF change tracker but not persisted, leading to an inconsistent in-memory state.

Additionally, audit log writes happen one-by-one inside the loop without transaction wrapping.

**Recommendation:** Wrap the entire apply operation in `_unitOfWork.ExecuteInTransactionAsync()`.

---

### H-02: CashTransferService.CancelAsync Duplicates Fiscal Validation Instead of Using FiscalPeriodValidator

**File:** [Services/Treasury/CashTransferService.cs](../src/MarcoERP.Application/Services/Treasury/CashTransferService.cs)  
**Lines:** ~440-465

**Description:**  
`CancelAsync` manually performs fiscal year/period validation inline:
```csharp
var fiscalYear = await _fiscalYearRepo.GetByYearAsync(reversalDate.Year, ct);
fiscalYear = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
if (fiscalYear.Status != FiscalYearStatus.Active) ...
var period = fiscalYear.GetPeriod(reversalDate.Month);
if (period == null || !period.IsOpen) ...
```
This duplicates exactly what `FiscalPeriodValidator.ValidateForCancelAsync()` does. `CashReceiptService.CancelAsync` and `CashPaymentService.CancelAsync` both correctly use `_fiscalValidator.ValidateForCancelAsync()`.

**Risk:** If fiscal validation rules change, `CashTransferService.CancelAsync` will diverge from the other services.

**Recommendation:** Replace inline validation with `_fiscalValidator.ValidateForCancelAsync(transfer.TransferDate, ct)`.

---

### H-03: SalesReturnService Creates COGS Reversal Journal Even When TotalCogs=0

**File:** [Services/Sales/SalesReturnService.cs](../src/MarcoERP.Application/Services/Sales/SalesReturnService.cs)  
**Lines:** ~530-570 (CreateCogsReversalJournalAsync)

**Description:**  
If all returned products have `WeightedAverageCost = 0`, the `totalCogs` will be 0. The code still calls `_journalFactory.CreateAndPostAsync()` which creates a journal entry with **zero lines** (since the `if (totalCogs > 0)` guard skips both lines). This creates a posted journal with no lines — a phantom entry in the GL.

The same issue exists in `SalesInvoiceService.CreateCogsJournalAsync`.

**Recommendation:** Skip COGS journal creation entirely when `totalCogs <= 0`:
```csharp
if (totalCogs <= 0) return (null, lineCosts);
```

---

### H-04: ProductImportService Loads ALL Products Into Memory

**File:** [Services/Inventory/ProductImportService.cs](../src/MarcoERP.Application/Services/Inventory/ProductImportService.cs)  
**Lines:** ~80-90

**Description:**  
```csharp
var existingProducts = await _productService.GetAllAsync(ct);
var existingCodes = new HashSet<string>(
    existingProducts.Data.Select(p => p.ProductCode), StringComparer.OrdinalIgnoreCase);
```
This loads the **entire product catalog** just to build a duplicate-check HashSet. For an ERP with 50K+ products, this is a significant memory allocation and query.

**Recommendation:** Add a `_productRepo.GetAllCodesAsync()` or `_productRepo.ExistsByCodeAsync(code)` method that performs a targeted query.

---

### H-05: FiscalPeriodValidator.ValidateForPosPostingAsync Skips Production-Mode Backdating Check

**File:** [Common/FiscalPeriodValidator.cs](../src/MarcoERP.Application/Common/FiscalPeriodValidator.cs)  
**Lines:** ~115-135

**Description:**  
`ValidateForPosPostingAsync` explicitly skips the production-mode backdating guard and date-containment check that `ValidateForPostingAsync` enforces. Comment says "Simplified POS posting validation." This means POS sales can be posted to ANY date in production mode without restriction.

**Risk:** In production, POS transactions could be backdated to closed periods if manual date override is possible.

**Recommendation:** Either enforce the same backdating check or document the explicit business decision to skip it for POS.

---

### H-06: Multiple Services Missing Audit Logging for Financial Mutations

**Files affected:**
- `ProductService.cs` — No audit log on Create/Update/Delete
- `BulkPriceUpdateService.cs` — Audit log per product but outside transaction
- `CategoryService.cs` — No audit log
- `UnitService.cs` — No audit log
- `AccountService.ActivateAsync` — Missing audit log (DeactivateAsync has it)
- `BankAccountService` — No audit log
- `CashboxService` — No audit log

**Description:** Several services perform mutations to financial-adjacent data (product prices, account status) without audit logging. The governance policy requires comprehensive audit trails.

**Recommendation:** Add `_auditLogger.LogAsync()` calls to all mutation operations, particularly in `ProductService` (price changes are financially significant).

---

### H-07: BankReconciliationService Uses Hard Delete

**File:** [Services/Treasury/BankReconciliationService.cs](../src/MarcoERP.Application/Services/Treasury/BankReconciliationService.cs)  
**Lines:** ~195-205 (DeleteAsync)

**Description:**  
`DeleteAsync` calls `_reconciliationRepo.Remove(entity)` — a **hard delete** that permanently removes the entity. Every other service in the system uses soft-delete (`entity.SoftDelete(username, now)`). This violates the `RECORD_PROTECTION_POLICY.md` governance document.

```csharp
_reconciliationRepo.Remove(entity);  // Hard delete!
```

**Recommendation:** Switch to soft-delete: `entity.SoftDelete(_currentUser.Username, _dateTime.UtcNow)`.

---

### H-08: No Negative Stock Check Before POS Sale Completes (Race Condition Window)

**File:** [Services/Sales/PosService.cs](../src/MarcoERP.Application/Services/Sales/PosService.cs)  
**Lines:** ~450-520 (CompleteSaleAsync, stock deduction section)

**Description:**  
POS `CompleteSaleAsync` runs in a Serializable transaction, but the stock availability check and stock deduction happen in sequence within the transaction. If `AllowNegativeStock` is disabled, the code calls `whProduct.DecreaseStock(baseQty)` which will throw if stock is insufficient. While the exception IS caught and the transaction rolls back, the error message is a generic domain exception — not a user-friendly "insufficient stock for product X" message.

More critically, the check relies on `WarehouseProduct.Quantity` which may be stale if the read was before the Serializable lock was acquired on the row.

**Recommendation:** Add explicit stock sufficiency validation with product name in message before the decrease call, inside the transaction.

---

## MEDIUM Issues

### M-01: FiscalYearService.FindPeriodByIdAsync is O(n) Over All Fiscal Years

**File:** [Services/Accounting/FiscalYearService.cs](../src/MarcoERP.Application/Services/Accounting/FiscalYearService.cs)  

**Description:**  
`FindPeriodByIdAsync` fetches ALL fiscal years with periods, then iterates through every period of every year to find a single period by Id. This is O(Y×12) where Y is the number of fiscal years.

**Recommendation:** Add `_fiscalYearRepo.GetPeriodByIdAsync(periodId)` direct query.

---

### M-02: ProductionHardening Defaults to Production Mode When Setting is Missing

**File:** [Common/ProductionHardening.cs](../src/MarcoERP.Application/Common/ProductionHardening.cs)  
**Lines:** 15-25

**Description:**  
`IsProductionModeAsync` returns `true` (production mode) if:
- The setting repository is null
- The setting key is not found
- The setting value is empty
- The value can't be parsed as bool

This fail-closed design is safe for production but can block development/testing if the seed data is missing or the database is empty. New developers may be confused when backdated posting is rejected.

---

### M-03: SalesReturnServices.SystemSettingRepo Is Optional (Default Null)

**File:** [Services/Sales/SalesReturnService.cs](../src/MarcoERP.Application/Services/Sales/SalesReturnService.cs)  
**Lines:** ~710 (SalesReturnServices constructor)

**Description:**  
```csharp
public SalesReturnServices(... ISystemSettingRepository systemSettingRepo = null)
```
`SystemSettingRepo` defaults to null. If any code path later tries to use it (e.g., checking `AllowNegativeStock`), it will throw NullReferenceException.

**Recommendation:** Make it required (remove default) or add null-check guards at usage sites.

---

### M-04: SystemSettingsService.UpdateBatchAsync Has No Transaction Boundary

**File:** [Services/Settings/SystemSettingsService.cs](../src/MarcoERP.Application/Services/Settings/SystemSettingsService.cs)  
**Lines:** ~85-100

**Description:**  
`UpdateBatchAsync` iterates over DTOs, updating settings one-by-one, then calls `SaveChangesAsync` once. If a setting key is not found mid-loop, it returns `Failure` — but the previously updated entities in the EF change tracker are **not rolled back**. Next `SaveChangesAsync` call from any service would persist the partial updates.

**Recommendation:** Wrap in `ExecuteInTransactionAsync` or detach/reload entities on failure.

---

### M-05: AuthorizationProxy Uses Uncached Reflection

**File:** [Common/AuthorizationProxy.cs](../src/MarcoERP.Application/Common/AuthorizationProxy.cs)  
**Lines:** 50-60

**Description:**  
Every method invocation calls `targetMethod.GetCustomAttribute<RequiresPermissionAttribute>()` via reflection. This is called on every single service method call (including queries). While the overhead is small per call, it adds up for high-frequency operations like POS.

**Recommendation:** Cache attribute lookups in a `ConcurrentDictionary<MethodInfo, RequiresPermissionAttribute>`.

---

### M-06: FeatureGuard Comment States "NOT wired into any existing service yet"

**File:** [Common/FeatureGuard.cs](../src/MarcoERP.Application/Common/FeatureGuard.cs)  
**Lines:** 10-12

**Description:**  
```csharp
/// Phase 2: Feature Governance Engine — NOT wired into any existing service yet.
```
The `FeatureGuard` helper exists but is never called. Only `CashTransferService.IsFeatureEnabledOrMissingAsync` performs feature checks inline. Feature governance is not enforced at the Application layer.

**Recommendation:** Either wire `FeatureGuard.CheckAsync()` into service entry points or remove the dead code.

---

### M-07: Inconsistent Cashbox Balance Update Order Between Post and Cancel

**File:** [Services/Treasury/CashReceiptService.cs](../src/MarcoERP.Application/Services/Treasury/CashReceiptService.cs)

**Description:**  
In `ExecutePostAsync`, cashbox balance is increased **after** the journal entry is created:
```
CreateJournalEntry → SaveChanges → receipt.Post → cashbox.IncreaseBalance → SaveChanges
```
In `ExecuteCancelAsync`, cashbox balance decrease happens **after** journal reversal:
```
ReverseJournal → receipt.Cancel → cashbox.DecreaseBalance → SaveChanges
```

While both are in Serializable transactions (correct), the `CashPaymentService.PostAsync` does the balance decrease **before** journal creation:
```
cashbox.DecreaseBalance → CreateJournalEntry → SaveChanges → payment.Post → SaveChanges
```

This inconsistency increases cognitive load and risk of future bugs.

**Recommendation:** Standardize the order: always create journal first, then update balances, then mark as posted.

---

### M-08: CashTransfer PostAsync Deadlock Prevention is Admirable but Source/Target Same-ID Edge Case

**File:** [Services/Treasury/CashTransferService.cs](../src/MarcoERP.Application/Services/Treasury/CashTransferService.cs)  
**Lines:** ~280-300

**Description:**  
The code sorts cashbox updates by `cashbox.Id` to prevent deadlocks:
```csharp
var cashboxOrder = new[] { (sourceCashbox, true), (targetCashbox, false) }
    .OrderBy(x => x.cashbox.Id);
```
This is excellent practice. However, the `CreateAsync` validation does NOT check that `SourceCashboxId != TargetCashboxId`. If they're the same, the loop updates the same cashbox twice (decrease then increase), which is a no-op but creates a misleading journal entry.

**Recommendation:** Add validation in `CreateAsync`: `if (dto.SourceCashboxId == dto.TargetCashboxId) return Failure(...)`.

---

### M-09: RoleService.DeleteAsync Checks Users Collection but May Not Be Loaded

**File:** [Services/Security/RoleService.cs](../src/MarcoERP.Application/Services/Security/RoleService.cs)  
**Lines:** ~95-100

**Description:**  
```csharp
if (entity.Users != null && entity.Users.Count > 0)
    return ServiceResult.Failure("لا يمكن حذف دور مرتبط بمستخدمين.");
```
`GetByIdWithPermissionsAsync` loads the role with permissions but may NOT include the `Users` navigation property. If `Users` is not loaded (null), the check passes and the role is deleted, orphaning user records.

**Recommendation:** Either use a dedicated `GetByIdWithUsersAsync` or add a `_userRepo.AnyWithRoleAsync(id)` check.

---

### M-10: LineCalculationService Naming Confusion: NetTotal vs TotalWithVat

**File:** [Services/Accounting/LineCalculationService.cs](../src/MarcoERP.Application/Services/Accounting/LineCalculationService.cs)

**Description:**  
`CalculateTotals` sums `line.TotalWithVat` into a variable/property called `NetTotal`. In accounting, "net" typically means "excluding tax." The naming suggests the opposite of what the value represents.

**Recommendation:** Rename to `GrandTotal` or `TotalInclVat` to avoid confusion.

---

## LOW Issues

### L-01: BankAccountService.CreateAsync Checks Default After Add

**File:** [Services/Treasury/BankAccountService.cs](../src/MarcoERP.Application/Services/Treasury/BankAccountService.cs)  
**Lines:** ~85-90

**Description:**  
After calling `AddAsync(entity)`, it calls `GetAllAsync()` to check if this is the first bank account. Since the entity was just added, `GetAllAsync()` may or may not include it depending on whether EF tracks it. Minor logic smell.

---

### L-02: FeatureKeys Has Only 3 Constants

**File:** [Common/FeatureKeys.cs](../src/MarcoERP.Application/Common/FeatureKeys.cs)

**Description:** Only `AllowNegativeStock`, `AllowNegativeCash`, and `ReceiptPrinting` are defined. The `FeatureService` and `FeatureRepository` support many more features. The remaining feature keys are string literals scattered across the codebase.

---

### L-03: CreateAsync Methods Generate Number Outside Transaction

**Files:** Multiple services (CashReceipt, CashPayment, CashTransfer, PurchaseInvoice)

**Description:** `GetNextNumberAsync()` is called before the entity is created and before any transaction. In high-concurrency scenarios, two concurrent `CreateAsync` calls could get the same number. While `DbUpdateException` handling catches this for posting operations, `CreateAsync` in CashReceipt/CashPayment only has a single try without retry logic (unlike `PurchaseInvoiceService.CreateAsync` which has a 3-attempt retry).

---

### L-04: SalesQuotationService and PurchaseQuotationService Don't Prevent Duplicate Conversions

**Files:**
- [Services/Sales/SalesQuotationService.cs](../src/MarcoERP.Application/Services/Sales/SalesQuotationService.cs)
- [Services/Purchases/PurchaseQuotationService.cs](../src/MarcoERP.Application/Services/Purchases/PurchaseQuotationService.cs)

**Description:** `ConvertToInvoiceAsync` marks quotation as converted, but if two users click "Convert" simultaneously, both could proceed (TOCTOU). The Quotation status check is outside any transaction.

---

### L-05: Missing CancellationToken Passthrough in Several Methods

**Description:** Some internal helper methods (e.g., `ResolveAccountsAsync`, `CreateJournalEntryAsync`) accept `CancellationToken` but don't always pass it through to all async calls within.

---

### L-06: Reporting Layer Contains Only Models and Interfaces — No Service Implementations

**Files:** `Reporting/Models/*.cs`, `Reporting/Interfaces/*.cs`

**Description:** The reporting infrastructure (KpiCard, PagedResult, FilterDefinition, DrillDown, etc.) is defined but no concrete report services exist in the Application layer. The WPF UI likely queries data directly, bypassing the Application layer for reports.

---

## Architecture Observations (Non-Issues — Informational)

### A-01: Well-Designed Centralization
- `JournalEntryFactory` successfully centralizes journal creation across 13+ services ✅
- `FiscalPeriodValidator` eliminates fiscal validation duplication across 7+ services ✅
- `AuthorizationProxy` + `RequiresPermissionAttribute` is a clean AOP pattern ✅
- `ModuleRegistry` + `ModuleAttribute` enables boundary validation ✅
- Parameter objects pattern (e.g., `CashReceiptRepositories`, `CashReceiptServices`, `CashReceiptValidators`) keeps constructors clean ✅

### A-02: Consistent Transaction Isolation
All posting operations use `IsolationLevel.Serializable` ✅
All CRUD operations use default (ReadCommitted) ✅
Double-check pattern (pre-check outside tx, re-check inside tx) is consistently applied in posting methods ✅

### A-03: Concurrency Handling
`DbUpdateConcurrencyException` is caught and surfaced as user-friendly Arabic messages ✅
Unique constraint violations are caught via `ProductionHardening.IsUniqueConstraintViolation` ✅
`PurchaseInvoiceService.CreateAsync` has retry logic (3 attempts) for number collisions ✅

### A-04: Invoice ↔ Treasury Integration
CashReceipt → SalesInvoice: receipt amount validated against `BalanceDue`, `invoice.ApplyPayment()` on post, `invoice.ReversePayment()` on cancel ✅
CashPayment → PurchaseInvoice: same pattern ✅
Math.Min on reversal prevents negative `PaidAmount` ✅

### A-05: Security
BCrypt via `IPasswordHasher` ✅
Account lockout after 5 failed attempts with 15-minute auto-unlock ✅
Audit logging for login success/failure ✅
Username normalization (trim + lowercase) ✅

---

## Priority Action Plan

| Priority | Issue | Effort | Impact |
|----------|-------|--------|--------|
| 1 | C-01: Migrate all stock operations to StockManager | High | Eliminates 6x duplication |
| 2 | C-03 + C-04: Add balance validation to JournalEntryFactory + fix silent skip | Low | Prevents unbalanced journals |
| 3 | C-02: Implement POS COGS/profit calculations | Medium | Enables POS financial reporting |
| 4 | C-05: Re-validate invoice balance inside PostAsync transactions | Low | Prevents overpayment |
| 5 | H-02: Use FiscalPeriodValidator in CashTransferService.CancelAsync | Low | Consistency |
| 6 | H-03: Skip COGS journal when totalCogs=0 | Low | Prevents empty journals |
| 7 | H-07: Hard delete → soft delete in BankReconciliationService | Low | Policy compliance |
| 8 | H-01: Add transaction boundary to BulkPriceUpdateService | Low | Data consistency |
| 9 | H-06: Add audit logging to Product/Category/Unit services | Medium | Governance compliance |
| 10 | M-08: Validate SourceCashboxId ≠ TargetCashboxId | Low | Data integrity |

---

*End of Audit Report*
