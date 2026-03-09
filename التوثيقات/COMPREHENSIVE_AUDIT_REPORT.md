# MarcoERP — Comprehensive Audit Report

**Date:** 2026-02-14  
**Scope:** Governance, Validators, Mappers, DTOs, Application Common, Infrastructure  
**Severity Scale:** 🔴 Critical | 🟠 High | 🟡 Medium | 🟢 Low | ℹ️ Info

---

## PART 1 — Governance Files Audit

### 1.1 Inventory of Governance Files (16 total)

| # | File | Lines | Purpose |
|---|------|-------|---------|
| 1 | `governance/ARCHITECTURE.md` | ~320 | Layer contracts, forbidden actions, data flow |
| 2 | `governance/PROJECT_RULES.md` | ~200 | Supreme governance doc, 15+ rule categories |
| 3 | `governance/SECURITY_POLICY.md` | ~210 | RBAC model, 5 roles, AUTH/AUTHZ rules |
| 4 | `governance/DATABASE_POLICY.md` | ~213 | DB design rules DB-01 to DB-12 |
| 5 | `governance/ACCOUNTING_PRINCIPLES.md` | ~286 | Double-entry, COA, VAT |
| 6 | `governance/AGENT_POLICY.md` | ~200 | AI agent EXECUTE/PROPOSE/FORBIDDEN |
| 7 | `governance/AGENT_CONTROL_SYSTEM.md` | ~399 | Agent operational manual |
| 8 | `governance/ALM_POLICY.md` | ~243 | CI/CD, static analysis thresholds |
| 9 | `governance/FINANCIAL_ENGINE_RULES.md` | ~300 | Posting workflow, journal integrity |
| 10 | `governance/SOLUTION_STRUCTURE.md` | ~342 | Solution layout, dependency rules |
| 11 | `governance/RECORD_PROTECTION_POLICY.md` | ~283 | Immutability, reversals, adjustments |
| 12 | `governance/RISK_PREVENTION_FRAMEWORK.md` | ~337 | 7 structural failure modes |
| 13 | `governance/VERSIONING.md` | ~200 | SemVer, current v0.1.0-P1 |
| 14 | `governance/UI_GUIDELINES.md` | ~339 | WPF standards, window architecture |
| 15 | `governance/UI Guidelines v2.md` | ~340 | **DUPLICATE** of #14 with changes |
| 16 | `governance/Accounting Principles v1.1.md` | ~350 | **DUPLICATE** of #5 with extensions |

---

### 1.2 Duplicate / Versioned Governance Files

🟠 **GOV-01: Duplicate governance files create contradiction risk**

Two pairs of duplicate files exist side-by-side:

| Original | Versioned Copy | Key Differences |
|----------|---------------|-----------------|
| `UI_GUIDELINES.md` | `UI Guidelines v2.md` | Sidebar width: **210px** vs **300px**; v2 adds module-based navigation with sidebar icons |
| `ACCOUNTING_PRINCIPLES.md` | `Accounting Principles v1.1.md` | v1.1 adds credit control, quotation governance, profit analysis, representative management |

**Risk:** Developers or agents may reference the wrong version, leading to inconsistent implementations. No governance file specifies which version is canonical.

**Recommendation:** Archive old versions to a `governance/archive/` folder and keep only the latest.

---

### 1.3 Cross-Document Contradictions

🟡 **GOV-02: Sidebar width conflict**
- `UI_GUIDELINES.md` (line ~55): Sidebar width = **210px**
- `UI Guidelines v2.md` (line ~48): Sidebar width = **300px**
- **Impact:** UI implementations may use either value.

🟡 **GOV-03: Scope gap between accounting versions**
- `ACCOUNTING_PRINCIPLES.md` covers: Chart of Accounts, Journals, VAT, Fiscal Years.
- `Accounting Principles v1.1.md` adds: Credit Control (CRD-01..04), Quotation Governance (QOT-01..05), Profit Analysis, Representative Management.
- **Impact:** If original is referenced, credit control and quotation rules are missed entirely.

ℹ️ **GOV-04: No contradiction found between other governance files**
- `ARCHITECTURE.md`, `PROJECT_RULES.md`, `DATABASE_POLICY.md`, `SECURITY_POLICY.md`, `FINANCIAL_ENGINE_RULES.md`, `RECORD_PROTECTION_POLICY.md` are internally consistent.
- Rule references (ARC-xx, DB-xx, AUTH-xx, FIN-xx, REC-xx) do not overlap or conflict.

---

### 1.4 Phantom References (Rules Referenced but Not Enforced)

🟡 **GOV-05: ALM_POLICY static analysis thresholds not enforced**
- `ALM_POLICY.md` specifies: Code coverage ≥ 80%, cyclomatic complexity ≤ 10, Roslyn analyzers mandatory.
- **No CI/CD pipeline found** in the repository (no `.github/workflows/`, no `azure-pipelines.yml`, no `.gitlab-ci.yml`).
- Static analysis tools not configured in any `.csproj`.

🟡 **GOV-06: VERSIONING.md changelog automation not implemented**
- `VERSIONING.md` specifies: Automated changelog generation, version bump scripts.
- No version bump tooling found. Version is hardcoded.

🟢 **GOV-07: RISK_PREVENTION_FRAMEWORK integration testing mandate**
- Framework requires integration tests for all 7 failure modes.
- Integration test project exists (`tests/MarcoERP.Integration.Tests/`) but coverage unknown.

---

### 1.5 Governance Enforcement in Code

| Rule | Governance Source | Enforced? | Evidence |
|------|------------------|-----------|----------|
| No AutoMapper | ARCHITECTURE §7 | ✅ Yes | All 28 mappers are manual static. Comments reference governance. |
| No lazy loading | DATABASE_POLICY DB-06 | ✅ Yes | Zero `virtual ICollection` in entities. |
| No Data Annotations on entities | ARCHITECTURE §4 | ✅ Yes | Zero `[Required]`, `[MaxLength]` etc. in Domain/Entities. |
| Fluent API only for EF config | DATABASE_POLICY DB-03 | ✅ Yes | Fluent configs in Persistence layer. |
| BCrypt for passwords | SECURITY_POLICY AUTH-03 | ✅ Yes | `PasswordHasher.cs` uses BCrypt WorkFactor=12. |
| RBAC with permissions | SECURITY_POLICY AUTHZ-01 | ✅ Yes | `AuthorizationProxy` + `[RequiresPermission]` attribute. |
| Audit logging | DATABASE_POLICY DB-11 | ✅ Yes | `AuditLogger.cs` writes `AuditLog` entity. |
| ServiceResult pattern | ARCHITECTURE §8 | ✅ Yes | All services return `ServiceResult<T>`. |
| Manual DI registration | PROJECT_RULES SYS-04 | ✅ Yes | All 62 validators + services registered manually in `App.xaml.cs`. |

---

### 1.6 Part 1 Summary

| Severity | Count | IDs |
|----------|-------|-----|
| 🟠 High | 1 | GOV-01 |
| 🟡 Medium | 4 | GOV-02, GOV-03, GOV-05, GOV-06 |
| 🟢 Low | 1 | GOV-07 |
| ℹ️ Info | 1 | GOV-04 |

---

## PART 2 — Validators (FluentValidation) Audit

### 2.1 Validator Inventory

**Total validator files:** ~65 validators across 7 module folders  
**DI registrations in `App.xaml.cs`:** 62 `IValidator<T>` registrations (lines 472–535) + 8 composite validator classes (lines 557–597)

#### Module Breakdown

| Module | Validator Files | DI Registrations | Gap? |
|--------|----------------|------------------|------|
| Accounting | 5 validators | 5 | ✅ Match |
| Inventory | 13 validators | 8 | 🔴 **5 missing** |
| Purchases | 11 validators | 6 | ✅ (5 are line validators used via SetValidator) |
| Sales | 21 validators | 12 | 🔴 **3 missing** + 6 line validators via SetValidator |
| Security | 7 validators | 7 | ✅ Match |
| Settings | 1 validator | 1 | ✅ Match |
| Treasury | 12 validators | 12 | ✅ Match |

---

### 2.2 Missing DI Registrations

🔴 **VAL-01: 8 validators exist but are NOT registered in DI**

These validator classes exist in the codebase but have no `services.AddScoped<IValidator<T>>` registration in `App.xaml.cs`:

| # | Validator | File Location | Severity |
|---|-----------|---------------|----------|
| 1 | `BulkPriceUpdateRequestDtoValidator` | `Validators/Inventory/` | 🔴 Critical |
| 2 | `CreateInventoryAdjustmentDtoValidator` | `Validators/Inventory/` | 🔴 Critical |
| 3 | `UpdateInventoryAdjustmentDtoValidator` | `Validators/Inventory/` | 🔴 Critical |
| 4 | `CreateInventoryAdjustmentLineDtoValidator` | `Validators/Inventory/` | 🟡 Medium (child) |
| 5 | `CreateProductUnitDtoValidator` | `Validators/Inventory/` | 🔴 Critical |
| 6 | `CreatePriceListDtoValidator` | `Validators/Sales/` | 🔴 Critical |
| 7 | `UpdatePriceListDtoValidator` | `Validators/Sales/` | 🔴 Critical |
| 8 | `CreatePriceTierDtoValidator` | `Validators/Sales/` | 🟡 Medium (child) |

**Impact:** If services resolve `IValidator<CreateInventoryAdjustmentDto>` from DI, they will get a runtime DI exception. If services skip validation for unregistered validators, invalid data enters the system unchecked.

**Fix location:** `src/MarcoERP.WpfUI/App.xaml.cs` lines 472–535 — add missing registrations.

---

### 2.3 Magic Numbers in Validators

🟡 **VAL-02: Hard-coded constants instead of domain constants**

| Value | Usage | File(s) |
|-------|-------|---------|
| `2020` | Minimum fiscal year | `CreateFiscalYearDtoValidator` |
| `2099` | Maximum fiscal year | `CreateFiscalYearDtoValidator` |
| `30` | Max days in future for dates | Multiple invoice/return validators |
| `99_999_999_999m` | Max monetary amount | Multiple financial validators |
| `100` | Max discount percent | Line validators |
| `50` | Max VAT rate | Product validators |
| `500` | Max string length (name fields) | Multiple validators |
| `1000` | Max string length (description) | Multiple validators |
| `9999` | Max conversion factor | `CreateProductUnitDtoValidator` |

**Recommendation:** Extract to `DomainConstants.cs` or a new `ValidationConstants.cs` in Application layer. The file `DomainConstants.cs` already exists at `src/MarcoERP.Domain/DomainConstants.cs` but these values are not defined there.

---

### 2.4 Duplicate Validation Rules

🟢 **VAL-03: Minor duplicate rule in ProductValidators**
- `CreateProductDtoValidator` and `UpdateProductDtoValidator` both contain `CostPrice >= 0` rule.
- This is expected (Create/Update validators are separate by design), not a defect.

---

### 2.5 DTOs Without Validators

🟡 **VAL-04: Create/Update DTOs that have no corresponding validator**

| DTO | Module | Risk |
|-----|--------|------|
| `ToggleFeatureDto` | Settings | 🟡 Feature toggle with no validation |
| `ProductImportDtos` | Inventory | 🟡 Bulk import with no validation |

---

### 2.6 Part 2 Summary

| Severity | Count | IDs |
|----------|-------|-----|
| 🔴 Critical | 1 | VAL-01 (6 critical validators) |
| 🟡 Medium | 3 | VAL-01 (2 child validators), VAL-02, VAL-04 |
| 🟢 Low | 1 | VAL-03 |

---

## PART 3 — Mappers Audit

### 3.1 Mapper Inventory

**Total mapper files:** 28 static mapper classes across 7 module folders  
**Pattern:** All mappers are `public static class` with `ToDto()` methods — manual, explicit, no AutoMapper.

| Module | Mapper Files |
|--------|-------------|
| Accounting | `AccountMapper`, `FiscalYearMapper`, `JournalEntryMapper` |
| Inventory | `CategoryMapper`, `ProductMapper`, `UnitMapper`, `WarehouseMapper` |
| Purchases | `PurchaseInvoiceMapper`, `PurchaseQuotationMapper`, `PurchaseReturnMapper`, `SupplierMapper` |
| Sales | `CustomerMapper`, `PosMapper`, `SalesInvoiceMapper`, `SalesQuotationMapper`, `SalesRepresentativeMapper`, `SalesReturnMapper` |
| Security | `RoleMapper`, `UserMapper` |
| Settings | `FeatureMapper`, `ProfileMapper`, `SystemSettingMapper` |
| Treasury | `BankAccountMapper`, `BankReconciliationMapper`, `CashboxMapper`, `CashPaymentMapper`, `CashReceiptMapper`, `CashTransferMapper` |

---

### 3.2 Missing Mappers

🔴 **MAP-01: Entities with DTOs but NO mapper class**

| Entity | DTO File | Missing Mapper |
|--------|----------|---------------|
| `InventoryAdjustment` | `InventoryAdjustmentDtos.cs` (6 DTO classes) | `InventoryAdjustmentMapper` ❌ |
| `PriceList` / `PriceTier` | `PriceListDtos.cs` (7 DTO classes) | `PriceListMapper` ❌ |

**Impact:** Services for Inventory Adjustments and Price Lists must be doing inline mapping or not mapping at all, violating the governance pattern of centralized mappers.

---

### 3.3 Unmapped Fields in Existing Mappers

🟠 **MAP-02: PosMapper missing navigation property fields**

`PosMapper.ToSessionDto()` does NOT map these fields present in `PosSessionDto`:

| DTO Field | Entity Source | Mapped? |
|-----------|-------------|---------|
| `UserName` | `entity.User?.Username` | ❌ Missing |
| `CashboxNameAr` | `entity.Cashbox?.NameAr` | ❌ Missing |
| `WarehouseNameAr` | `entity.Warehouse?.NameAr` | ❌ Missing |

`PosMapper.ToSessionListDto()` does NOT map:

| DTO Field | Entity Source | Mapped? |
|-----------|-------------|---------|
| `UserName` | `entity.User?.Username` | ❌ Missing |

**Impact:** UI displays for POS sessions will show null/empty for user name, cashbox name, and warehouse name.

---

### 3.4 Mapper Consistency Check

✅ **MAP-03: Consistent null-guard pattern**
- All mappers return `null` for null input — consistent guard clause.

✅ **MAP-04: Consistent static class pattern**
- All 28 mappers follow `public static class XyzMapper` with `public static XyzDto ToDto(Entity)`.

✅ **MAP-05: Governance compliance**
- Three mappers explicitly reference governance: `AccountMapper` ("explicit, auditable mapping"), `SupplierMapper` ("per governance ARCHITECTURE §7"), `CustomerMapper` ("per governance ARCHITECTURE §7").

🟡 **MAP-06: No reverse mapping (DTO → Entity)**
- All mappers are one-directional: Entity → DTO only.
- Create/Update DTOs are consumed directly by services without a mapper back to entity.
- This is intentional by design but means entity construction is scattered across service methods.

---

### 3.5 Entities Without DTOs or Mappers (Acceptable)

These entities intentionally have no DTOs/mappers (internal infrastructure):

| Entity | Reason |
|--------|--------|
| `WarehouseProduct` | Join entity, managed internally |
| `CodeSequence` | Auto-numbering engine, internal only |
| `Company` | Single-tenant (CompanyId=1 always) |
| `RolePermission` | Mapped as `List<string>` inside `RoleMapper` |
| `FeatureChangeLog` | Audit trail, no UI exposure |
| `FeatureVersion` | Internal versioning |
| `SystemVersion` | Internal versioning |
| `ProfileFeature` | Join entity |
| `PosPayment` | Mapped as `PosPaymentDto` inside `PosDtos.cs`, handled inline |

---

### 3.6 Part 3 Summary

| Severity | Count | IDs |
|----------|-------|-----|
| 🔴 Critical | 1 | MAP-01 (2 missing mappers) |
| 🟠 High | 1 | MAP-02 (3 unmapped fields) |
| 🟡 Medium | 1 | MAP-06 |

---

## PART 4 — DTOs Audit

### 4.1 DTO Inventory

**Total DTO files:** 67 files across 9 directories  
**Total DTO classes:** ~150+ (many files contain multiple DTOs)

| Directory | Files | Key DTOs |
|-----------|-------|----------|
| Accounting | 12 | AccountDto, JournalEntryDto, FiscalYearDto, CreateAccountDto… |
| Common | 2 | LineCalculationDtos, PriceHistoryDtos |
| Inventory | 6 | ProductDtos, CategoryDtos, UnitDtos, WarehouseDtos, BulkPriceUpdateDtos, InventoryAdjustmentDtos |
| Purchases | 4 | PurchaseInvoiceDtos, PurchaseQuotationDtos, PurchaseReturnDtos, SupplierDtos |
| Reports | 14 | TrialBalance, BalanceSheet, IncomeStatement, VatReport, DashboardSummary… |
| Sales | 7 | SalesInvoiceDtos, CustomerDtos, PosDtos, PriceListDtos, SalesQuotationDtos, SalesReturnDtos, ReceiptDtos, SalesRepresentativeDtos |
| Search | 1 | GlobalSearchDtos |
| Security | 3 | UserDtos, RoleDtos, AuthDtos |
| Settings | 9 | FeatureDto, SystemSettingDtos, SystemProfileDto, BackupDtos, AuditLogDto, IntegrityDtos, MigrationExecutionDto, FeatureImpactReport, ToggleFeatureDto |
| Treasury | 6 | CashboxDtos, BankAccountDtos, CashReceiptDtos, CashPaymentDtos, CashTransferDtos, BankReconciliationDtos |

---

### 4.2 DTO Structure Issues

🟡 **DTO-01: All DTOs are mutable classes — no records or init-only properties**

Every DTO in the project uses `public T PropertyName { get; set; }` pattern. None use:
- C# `record` types (available since C# 9 / .NET 5)
- `init` accessors (available since C# 9)
- Immutable read-only DTOs

**Example** from `src/MarcoERP.Application/DTOs/Accounting/CreateAccountDto.cs`:
```csharp
public class CreateAccountDto
{
    public string Code { get; set; }
    public string NameAr { get; set; }
    // ... all mutable
}
```

**Impact:** Read DTOs (like `AccountDto`, `JournalEntryDto`) can be accidentally mutated after creation. This doesn't violate current governance explicitly but conflicts with the immutability philosophy in `RECORD_PROTECTION_POLICY.md`.

**Recommendation:** Convert read-only DTOs to `record` types. Keep Create/Update DTOs as mutable classes (needed for binding).

---

### 4.3 Sealed vs Non-Sealed DTOs

🟢 **DTO-02: Inconsistent use of `sealed` keyword**

Most newer DTOs are `sealed class` (e.g., all of `PosDtos.cs`, `InventoryAdjustmentDtos.cs`, `PriceListDtos.cs`, `RoleDtos.cs`, `CustomerDtos.cs`).

Some older DTOs are plain `class` without `sealed`:
- `CreateAccountDto` — not sealed
- `JournalEntryDto` — not sealed
- `UserDto`, `CreateUserDto` — not sealed

**Impact:** Minor inconsistency. No functional issue since DTOs should never be inherited.

---

### 4.4 DTO-Entity Field Coverage

🟡 **DTO-03: PosSessionDto has fields not populated by mapper**

As detailed in MAP-02, `PosSessionDto` declares `UserName`, `CashboxNameAr`, `WarehouseNameAr` but `PosMapper` doesn't populate them.

🟡 **DTO-04: Report DTOs have no corresponding entities**

14 report DTO files exist (`TrialBalanceRowDto`, `BalanceSheetRowDto`, `IncomeStatementRowDto`, etc.) that are projection DTOs without backing entities. This is correct by design — they represent computed query results.

---

### 4.5 Naming Conventions

✅ **DTO-05: Consistent naming pattern**
- Read DTOs: `XyzDto` / `XyzListDto`
- Create DTOs: `CreateXyzDto`
- Update DTOs: `UpdateXyzDto`
- All follow this pattern consistently across all modules.

🟢 **DTO-06: POS typo in DTO name**
- `CompletePoseSaleDto` (note: "Pose" vs "Pos") in `src/MarcoERP.Application/DTOs/Sales/PosDtos.cs`.
- DI registration at `App.xaml.cs` line 511 also uses `CompletePoseSaleDto`.
- Minor naming inconsistency but consistently misspelled.

---

### 4.6 Part 4 Summary

| Severity | Count | IDs |
|----------|-------|-----|
| 🟡 Medium | 3 | DTO-01, DTO-03, DTO-04 |
| 🟢 Low | 2 | DTO-02, DTO-06 |

---

## PART 5 — Application Common Audit

### 5.1 Common Files Inventory (13 files)

| File | Purpose | Lines |
|------|---------|-------|
| `ServiceResult.cs` | Result pattern (Success/Failure) | ~60 |
| `AuthorizationGuard.cs` | **DEPRECATED** — manual permission check | ~50 |
| `AuthorizationProxy.cs` | DispatchProxy auto-permission enforcement | ~143 |
| `FeatureGuard.cs` | Feature toggle check | ~30 |
| `FeatureKeys.cs` | 3 feature key constants | ~15 |
| `ModuleAttribute.cs` | Module declaration attribute | ~15 |
| `ModuleDefinition.cs` | Module + allowed dependencies | ~15 |
| `ModuleRegistry.cs` | 10 module dependency rules | ~50 |
| `PermissionKeys.cs` | 33 permission constants | ~60 |
| `ProductionHardening.cs` | Production checks, backdating detection | ~80 |
| `RequiresPermissionAttribute.cs` | Method-level permission attribute | ~15 |
| `SystemSettingHelpers.cs` | Read bool settings from ISystemSettingService | ~20 |
| `SystemSettingKeys.cs` | 3 setting key constants | ~15 |

---

### 5.2 Issues Found

🟠 **COM-01: AuthorizationGuard is [Obsolete] but still in codebase**

`src/MarcoERP.Application/Common/AuthorizationGuard.cs` is marked `[Obsolete]` with a message pointing to `AuthorizationProxy`. However:
- The file still exists and compiles.
- Any code referencing it will get compiler warnings.
- No evidence it's been fully replaced everywhere.

**Recommendation:** Verify no references remain, then delete the file.

---

🟡 **COM-02: FeatureKeys and SystemSettingKeys overlap**

| FeatureKeys | SystemSettingKeys | Concern |
|-------------|-------------------|---------|
| `AllowNegativeStock` | `AllowNegativeStock` | Same concept, two key stores |
| `AllowNegativeCash` | `AllowNegativeCashboxBalance` | Similar concept, different naming |
| `ReceiptPrinting` | `EnableReceiptPrinting` | Same concept, different naming |

**Impact:** Developers may use the wrong key set. `FeatureGuard` checks `Feature` entity, while `SystemSettingHelpers` checks `SystemSetting` entity. If both exist in the database, behavior depends on which one is queried.

**Recommendation:** Consolidate into a single source of truth. Either Features subsume Settings or vice versa.

---

🟡 **COM-03: ModuleRegistry has static hard-coded dependency rules**

`ModuleRegistry.cs` defines 10 module dependency rules statically:
```
Core → (no deps)
Accounting → Core
Inventory → Core
Sales → Core, Inventory, Accounting
Purchases → Core, Inventory, Accounting
Treasury → Core, Accounting
POS → Core, Sales, Inventory, Treasury
Reports → Core, Accounting, Inventory, Sales, Purchases, Treasury
Settings → Core
Security → Core
```

These are not enforced at compile-time (no Roslyn analyzer). They are only checkable at runtime or by manual inspection.

**Impact:** A developer could add a reference from Treasury to Sales without triggering any build error.

---

🟢 **COM-04: PermissionKeys has 33 constants — complete coverage**

All major CRUD operations across all modules have permission keys defined:
- Accounts (3), JournalEntries (3), FiscalYears (2)
- Products (3), Categories (3), Units (2), Warehouses (2)
- Customers (3), SalesInvoices (3), SalesReturns (2), SalesQuotations (2)
- Suppliers (3), PurchaseInvoices (3)
- Users (3), Roles (2)
- Settings (1), etc.

---

🟢 **COM-05: ProductionHardening provides good security checks**

- `AssertProductionSafe()`: Guards against debug-mode operations in production.
- `IsBackdated()`: Detects entries dated more than configurable days in the past.
- `ExtractUniqueConstraintField()`: Parses SQL unique constraint violations into user-friendly messages.

---

### 5.3 Part 5 Summary

| Severity | Count | IDs |
|----------|-------|-----|
| 🟠 High | 1 | COM-01 |
| 🟡 Medium | 2 | COM-02, COM-03 |
| 🟢 Low | 2 | COM-04, COM-05 |

---

## PART 6 — Infrastructure Services Audit

### 6.1 Infrastructure Files Inventory

| File | Interface Implemented | Singleton/Scoped |
|------|----------------------|-----------------|
| `PasswordHasher.cs` | `IPasswordHasher` | Scoped |
| `ActivityTracker.cs` | `IActivityTracker` | Singleton |
| `AlertService.cs` | `IAlertService` | Singleton |
| `AuditLogger.cs` | `IAuditLogger` | Scoped |
| `BackgroundJobService.cs` | `IBackgroundJobService` | Singleton |
| `CurrentUserService.cs` | `ICurrentUserService` | Singleton |
| `DateTimeProvider.cs` | `IDateTimeProvider` | Scoped |
| `DefaultCompanyContext.cs` | `ICompanyContext` | Scoped |
| `WindowsEscPosPrinterService.cs` | `IReceiptPrinterService` | Scoped |

**Project References (Infrastructure.csproj):**
- `MarcoERP.Domain` ✅
- `MarcoERP.Application` ✅ (marked as technical debt TD-1 but architecturally necessary)
- `BCrypt.Net-Next` v4.0.3

---

### 6.2 Issues Found

🔴 **INF-01: WindowsEscPosPrinterService is a complete stub**

`src/MarcoERP.Infrastructure/Services/WindowsEscPosPrinterService.cs`:
```csharp
public bool IsAvailable => false;

public Task PrintReceiptAsync(ReceiptDto receipt)
{
    // TODO: Integrate ESC/POS library and implement print output.
    return Task.CompletedTask;
}

public Task OpenCashDrawerAsync()
{
    // TODO: Integrate drawer open command via ESC/POS.
    return Task.CompletedTask;
}
```

- `IsAvailable` always returns `false`.
- Both methods are no-ops with `TODO` comments.
- Feature flag `EnableReceiptPrinting` exists in `SystemSettingKeys`, but the implementation does nothing.

**Impact:** POS receipt printing silently fails. Users may believe receipts are printing when they are not.

---

🟠 **INF-02: AuditLogger writes in the same database transaction**

`src/MarcoERP.Infrastructure/Services/AuditLogger.cs`:
```csharp
public async Task LogAsync(string action, string entity, int? entityId, ...)
{
    var log = new AuditLog { ... };
    _auditRepo.Add(log);
    await _unitOfWork.SaveChangesAsync();
}
```

The audit log is written to the same database using the same `IUnitOfWork`. If the main transaction fails, the audit log of the failure is also lost. If the audit write fails, it could roll back the main operation.

**Governance conflict:** `DATABASE_POLICY.md` DB-11 states audit logs should be permanent and tamper-resistant.

**Recommendation:** Consider a separate DbContext or out-of-band logging for audit entries.

---

🟠 **INF-03: CurrentUserService has mutable singleton state without thread safety**

`src/MarcoERP.Infrastructure/Services/CurrentUserService.cs` is registered as **Singleton** but stores mutable user identity:
```csharp
public int UserId { get; private set; }
public string Username { get; private set; }
public int RoleId { get; private set; }
public List<string> Permissions { get; private set; }
```

- `HasPermission()` checks `RoleId == 1` (admin bypass) — magic number.
- `Permissions` is a `List<string>` (mutable) — could be modified after setting.
- As a singleton in a WPF app (single user), this works but is fragile.

**Risk:** If the app ever supports multiple simultaneous users or is ported to a web context, this will be a critical security flaw.

---

🟡 **INF-04: DefaultCompanyContext always returns CompanyId = 1**

```csharp
public int CompanyId => 1;
```

Hard-coded single-tenant. Governance (`ARCHITECTURE.md`) mentions multi-company as a future consideration. This implementation would need replacement.

---

🟡 **INF-05: BackgroundJobService uses Timer-based scheduling**

`src/MarcoERP.Infrastructure/Services/BackgroundJobService.cs` (212 lines):
- Uses `System.Threading.Timer` for: auto-backup, session watchdog, low-stock alerts.
- No retry logic on failure.
- No dead-letter / failure notification mechanism.
- Timer intervals are hard-coded.

**Risk:** If a background job throws, the timer continues but the failed job is lost silently.

---

🟢 **INF-06: PasswordHasher implementation is sound**

- Uses BCrypt with WorkFactor=12 (industry standard).
- Implements `IPasswordHasher` with `Hash()` and `Verify()`.
- No plain-text fallback.
- Sealed class prevents subclassing.

---

🟢 **INF-07: AlertService is thread-safe**

Uses `ConcurrentDictionary` and `ConcurrentBag` for in-memory alert storage. Appropriate for singleton lifetime.

---

### 6.3 Interface Completeness

All 9 infrastructure implementations implement exactly one application-layer interface. No interface is left unimplemented.

| Application Interface | Infrastructure Implementation | Complete? |
|----------------------|------------------------------|-----------|
| `IPasswordHasher` | `PasswordHasher` | ✅ |
| `IActivityTracker` | `ActivityTracker` | ✅ |
| `IAlertService` | `AlertService` | ✅ |
| `IAuditLogger` | `AuditLogger` | ✅ (see INF-02) |
| `IBackgroundJobService` | `BackgroundJobService` | ✅ |
| `ICurrentUserService` | `CurrentUserService` | ✅ (see INF-03) |
| `IDateTimeProvider` | `DateTimeProvider` | ✅ |
| `ICompanyContext` | `DefaultCompanyContext` | ✅ |
| `IReceiptPrinterService` | `WindowsEscPosPrinterService` | ❌ Stub (INF-01) |

---

### 6.4 Part 6 Summary

| Severity | Count | IDs |
|----------|-------|-----|
| 🔴 Critical | 1 | INF-01 |
| 🟠 High | 2 | INF-02, INF-03 |
| 🟡 Medium | 2 | INF-04, INF-05 |
| 🟢 Low | 2 | INF-06, INF-07 |

---

## EXECUTIVE SUMMARY

### All Findings by Severity

| Severity | Count | Finding IDs |
|----------|-------|-------------|
| 🔴 Critical | 3 | VAL-01 (8 unregistered validators), MAP-01 (2 missing mappers), INF-01 (printer stub) |
| 🟠 High | 4 | GOV-01 (duplicate governance), MAP-02 (unmapped POS fields), COM-01 (obsolete AuthorizationGuard), INF-02 (audit in same tx), INF-03 (mutable singleton) |
| 🟡 Medium | 12 | GOV-02, GOV-03, GOV-05, GOV-06, VAL-02, VAL-04, MAP-06, DTO-01, DTO-03, DTO-04, COM-02, COM-03, INF-04, INF-05 |
| 🟢 Low | 6 | GOV-07, VAL-03, DTO-02, DTO-06, COM-04, COM-05, INF-06, INF-07 |

### Top Priority Actions

1. **Register 8 missing validators in DI** (VAL-01) — Runtime failures for InventoryAdjustment, PriceList, BulkPriceUpdate, ProductUnit
2. **Create InventoryAdjustmentMapper and PriceListMapper** (MAP-01) — Required for consistent mapping pattern
3. **Fix PosMapper unmapped fields** (MAP-02) — UI shows null for UserName, CashboxNameAr, WarehouseNameAr
4. **Resolve duplicate governance files** (GOV-01) — Archive old versions
5. **Implement or explicitly disable receipt printing** (INF-01) — Currently silently fails
6. **Remove obsolete AuthorizationGuard** (COM-01) — Dead code with compiler warnings
7. **Consolidate FeatureKeys / SystemSettingKeys** (COM-02) — Single source of truth for feature toggles
