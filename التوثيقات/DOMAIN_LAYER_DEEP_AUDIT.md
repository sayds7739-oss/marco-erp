# MarcoERP — Domain Layer Deep Audit Report

**Date:** 2026-02-14  
**Scope:** `src/MarcoERP.Domain/` — all entities, enums, interfaces, exceptions, constants  
**Total Files Audited:** 86 files  
**Framework:** .NET 8.0 | `Nullable=disable` | `ImplicitUsings=disable` | Zero NuGet dependencies

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Entity Hierarchy Overview](#2-entity-hierarchy-overview)
3. [CRITICAL Findings](#3-critical-findings)
4. [HIGH Findings](#4-high-findings)
5. [MEDIUM Findings](#5-medium-findings)
6. [LOW Findings](#6-low-findings)
7. [File-by-File Summary](#7-file-by-file-summary)
8. [Recommendations Roadmap](#8-recommendations-roadmap)

---

## 1. Executive Summary

| Severity | Count |
|----------|-------|
| CRITICAL | 5 |
| HIGH     | 10 |
| MEDIUM   | 14 |
| LOW      | 8 |
| **Total** | **37** |

The Domain layer is well-structured overall. It follows DDD conventions with rich entities, factory methods, draft-based lifecycle management, and an `IImmutableFinancialRecord` marker for append-only financial data. However, the audit uncovered **5 CRITICAL issues** primarily around audit-field mutability, immutability contract violations, and missing multi-company isolation on key entities. These must be addressed before any production release.

---

## 2. Entity Hierarchy Overview

```
BaseEntity (Id, RowVersion)
  └─ AuditableEntity (CreatedAt/By, ModifiedAt/By)
       ├─ SoftDeletableEntity (IsDeleted, DeletedAt/By)
       │    ├─ CompanyAwareEntity (CompanyId)
       │    │    ├─ JournalEntry
       │    │    ├─ Product
       │    │    ├─ Warehouse
       │    │    ├─ InventoryAdjustment
       │    │    ├─ SalesInvoice / SalesReturn / SalesQuotation
       │    │    ├─ PurchaseInvoice / PurchaseReturn / PurchaseQuotation
       │    │    ├─ Customer / Supplier
       │    │    ├─ CashPayment / CashReceipt / CashTransfer
       │    │    └─ BankAccount / BankReconciliation
       │    ├─ Account ⚠️ (should be CompanyAwareEntity)
       │    ├─ PriceList
       │    └─ SalesRepresentative
       ├─ FiscalYear / FiscalPeriod
       ├─ InventoryMovement
       ├─ Unit
       ├─ Category ⚠️ (should be CompanyAwareEntity)
       ├─ User
       └─ Company (sealed)
  ├─ Role (no audit fields)
  ├─ JournalEntryLine
  ├─ All invoice/return/quotation lines
  ├─ ProductUnit
  ├─ PriceTier
  ├─ PosPayment
  ├─ WarehouseProduct
  ├─ RolePermission ⚠️ (not BaseEntity — standalone)
  └─ Cashbox

Standalone (own Id, no BaseEntity):
  ├─ AuditLog (long Id)
  ├─ CodeSequence (int Id + RowVersion)
  ├─ BackupHistory
  ├─ Feature / FeatureVersion / FeatureChangeLog
  ├─ MigrationExecution
  ├─ ProfileFeature
  ├─ SystemProfile
  ├─ SystemSetting
  └─ SystemVersion
```

---

## 3. CRITICAL Findings

### C-01: `AuditableEntity` — Audit Fields Have Public Setters

**File:** `Entities/Common/AuditableEntity.cs`  
**Severity:** CRITICAL  

```csharp
public DateTime CreatedAt { get; set; }   // ← should be private/internal set
public string CreatedBy { get; set; }     // ← should be private/internal set
public DateTime? ModifiedAt { get; set; } // ← should be private/internal set
public string ModifiedBy { get; set; }    // ← should be private/internal set
```

**Impact:** Any code can overwrite `CreatedAt`/`CreatedBy` post-creation, destroying the audit trail. In an ERP system handling financial records, this is an integrity risk. The infrastructure layer (e.g., `SaveChangesAsync` interceptor) should be the only code setting these values.

**Fix:** Change to `{ get; internal set; }` (visible to Persistence assembly via `InternalsVisibleTo`) or `{ get; private set; }` with a `SetAuditFields()` method.

---

### C-02: `BaseEntity.RowVersion` — Public Setter on Concurrency Token

**File:** `Entities/Common/BaseEntity.cs`  
**Severity:** CRITICAL  

```csharp
public byte[] RowVersion { get; set; } // ← should be private set
```

**Impact:** The SQL Server `rowversion` concurrency token can be tampered with externally, bypassing optimistic concurrency protection. If application code accidentally overwrites this, silent data corruption may follow.

**Fix:** Change to `{ get; private set; }` and let EF Core manage it via `[Timestamp]` or Fluent API.

---

### C-03: `IImmutableFinancialRecord` Entities Expose `UpdateDetails()` Methods

**File(s):**  
- `Entities/Sales/SalesInvoiceLine.cs` (line 63)  
- `Entities/Purchases/PurchaseInvoiceLine.cs` (line 63)  
- `Entities/Sales/SalesReturnLine.cs` (line 63)  
- `Entities/Purchases/PurchaseReturnLine.cs` (line 63)  
- `Entities/Inventory/InventoryAdjustmentLine.cs` (line 54)  

**Severity:** CRITICAL  

All five entities implement `IImmutableFinancialRecord` (which signals "append-only, never mutate"), yet each exposes a public `UpdateDetails(...)` method that completely replaces all financial values (quantity, unit price, discount, VAT, totals).

**Impact:** This directly violates the immutability contract. If the persistence layer trusts `IImmutableFinancialRecord` to block updates, but application code calls `UpdateDetails()` on a tracked entity, the change will be persisted — mutating a financial record that should be immutable.

**Fix:** Either:
1. Remove `UpdateDetails()` from all `IImmutableFinancialRecord` entities and use the `ReplaceLines()` pattern (delete + re-add) exclusively, or
2. Guard `UpdateDetails()` to throw if the parent document is already Posted (it currently runs unconditionally).

---

### C-04: `Account` Missing Multi-Company Isolation

**File:** `Entities/Accounting/Account.cs`  
**Severity:** CRITICAL  

`Account` extends `SoftDeletableEntity` instead of `CompanyAwareEntity`. In a multi-company ERP, the Chart of Accounts is per-company. Without `CompanyId`, a single chart is shared across all companies — making multi-company accounting impossible.

**Fix:** Change base class to `CompanyAwareEntity`.

---

### C-05: `Category` Missing Multi-Company Isolation

**File:** `Entities/Inventory/Category.cs`  
**Severity:** CRITICAL  

`Category` extends `AuditableEntity` (not even `SoftDeletableEntity`). Product categories should be company-scoped for multi-company setups, and should be soft-deletable to avoid orphaning products.

**Fix:** Change base class to `CompanyAwareEntity`.

---

## 4. HIGH Findings

### H-01: Massive Line Calculation Code Duplication

**Files:**  
- `Entities/Sales/SalesInvoiceLine.cs`  
- `Entities/Sales/SalesReturnLine.cs`  
- `Entities/Sales/SalesQuotationLine.cs`  
- `Entities/Purchases/PurchaseInvoiceLine.cs`  
- `Entities/Purchases/PurchaseReturnLine.cs`  
- `Entities/Purchases/PurchaseQuotationLine.cs`  

**Severity:** HIGH  

All six entities duplicate identical calculation logic:

```
BaseQuantity = Quantity × ConversionFactor
SubTotal     = Quantity × UnitPrice
DiscountAmt  = SubTotal × DiscountPercent / 100
NetTotal     = SubTotal − DiscountAmt
VatAmount    = NetTotal × VatPercent / 100
TotalWithVat = NetTotal + VatAmount
```

Any fix or formula change must be applied in 6 places (× 2 methods each — `Create` and `UpdateDetails` = 12 code locations).

**Fix:** Extract calculation into a shared static helper or a `DocumentLineCalculator` value object in `Entities/Common/`.

---

### H-02: `ReplaceLines` Pattern Duplicated Across 8+ Entities

**Files:** `SalesInvoice`, `SalesReturn`, `SalesQuotation`, `PurchaseInvoice`, `PurchaseReturn`, `PurchaseQuotation`, `InventoryAdjustment`, `BankReconciliation`

**Severity:** HIGH  

Each entity independently implements the same `ReplaceLines(...)` algorithm (check draft status, detect existing/added/removed, validate duplicates, update or add). ~40-60 lines repeated 8 times.

**Fix:** Extract into a generic base class (`DocumentEntity<TLine>`) or a helper method.

---

### H-03: `EnsureDraft` Pattern Duplicated Across 10+ Entities

**Files:** All document entities (invoices, returns, quotations, adjustments, treasury documents)

**Severity:** HIGH  

Each entity has its own `EnsureDraft()` private method that checks status and throws. The logic is identical but the exception type differs per module.

**Fix:** Consider a shared helper or base class with a virtual exception factory.

---

### H-04: `PosSession` and `PosPayment` Throw Wrong Exception Type

**File:** `Entities/Sales/PosSession.cs`, `Entities/Sales/PosPayment.cs`  
**Severity:** HIGH  

Both entities throw `SalesInvoiceDomainException` for ALL validation errors. POS sessions and payments are not invoices — they need their own exception (`PosDomainException`) or a general `SalesDomainException`.

**Examples from PosSession.cs:**
```csharp
throw new SalesInvoiceDomainException("رقم الجلسة مطلوب.");      // "Session number required"
throw new SalesInvoiceDomainException("الخزنة مطلوبة.");           // "Cashbox required"
throw new SalesInvoiceDomainException("رصيد الإغلاق لا يمكن..."); // "Closing balance..."
```

These are POS-specific errors, not invoice errors. Exception filtering in the application layer will misclassify them.

---

### H-05: `PriceList` and `PriceTier` Throw Wrong Exception Type

**File:** `Entities/Sales/PriceList.cs`, `Entities/Sales/PriceTier.cs`  
**Severity:** HIGH  

Both throw `SalesInvoiceDomainException` for price list validation. Price lists are independent of invoices. A `PriceListDomainException` is needed, or at minimum a generic `SalesDomainException`.

---

### H-06: `InventoryAdjustment` Reuses `InvoiceStatus` Enum

**File:** `Entities/Inventory/InventoryAdjustment.cs`  
**Severity:** HIGH  

```csharp
public InvoiceStatus Status { get; private set; }
```

An inventory adjustment is not an invoice. Reusing `InvoiceStatus` (Draft/Posted/Cancelled) creates semantic confusion and prevents future divergence (e.g., `PartiallyAdjusted` status). Should use a dedicated `AdjustmentStatus` or a more generic `DocumentStatus` enum.

---

### H-07: `SecurityDomainException` and `ConcurrencyConflictException` Not Sealed

**Files:**  
- `Exceptions/SecurityDomainException.cs`  
- `Exceptions/ConcurrencyConflictException.cs`  

**Severity:** HIGH  

All 15 other domain exceptions are `sealed class`, but these two are plain `class`. This breaks the pattern and allows unintended inheritance, which can defeat `catch` block specificity.

**Fix:** Add `sealed` modifier to both.

---

### H-08: `ConcurrencyConflictException` Has Extra Property Not Following Pattern

**File:** `Exceptions/ConcurrencyConflictException.cs`  
**Severity:** HIGH  

This exception carries `public string EntityName { get; }` — the only domain exception with a custom property. While useful, this breaks the uniform exception pattern and suggests it should be part of a richer base exception class, or all domain exceptions should carry entity context.

---

### H-09: `AccountDomainException` Missing Inner Exception Constructor

**File:** `Exceptions/Accounting/AccountDomainException.cs`  
**Severity:** HIGH  

```csharp
public sealed class AccountDomainException : Exception
{
    public AccountDomainException(string message) : base(message) { }
    // Missing: (string message, Exception inner) overload
}
```

Also applies to: `JournalEntryDomainException`, `SalesReturnDomainException`, `PurchaseReturnDomainException`. Without the inner-exception constructor, wrapped exceptions lose their original stack trace.

---

### H-10: Missing Navigation Properties on Line Entities

**Files:** All invoice/return/quotation line entities  
**Severity:** HIGH  

Line entities store `ProductId`, `UnitId`, `AccountId` etc. as FK integers but do not expose navigation properties (`Product`, `Unit`, `Account`). While this is sometimes a deliberate DDD choice to avoid lazy-loading, it forces the repository layer to always project or eagerly load, and makes the domain model less expressive.

**Recommendation:** Add read-only navigation properties where needed (at least `Product` and `Unit` on line entities) with `private set` for EF Core.

---

## 5. MEDIUM Findings

### M-01: `AuditLog` and `CodeSequence` Don't Extend `BaseEntity`

**Files:**  
- `Entities/Accounting/AuditLog.cs` — has `long Id` (not `int`)  
- `Entities/Accounting/CodeSequence.cs` — has `int Id` + `byte[] RowVersion`  

**Severity:** MEDIUM  

These entities define their own identity fields instead of inheriting from `BaseEntity`. `AuditLog` uses `long` which is justified (high volume), but `CodeSequence` duplicates `BaseEntity` exactly. Consider making `BaseEntity` generic (`BaseEntity<TId>`) or documenting the exemption.

---

### M-02: `Company` Not Soft-Deletable

**File:** `Entities/Common/Company.cs`  
**Severity:** MEDIUM  

`Company` extends `AuditableEntity` directly, skipping `SoftDeletableEntity`. A company cannot be soft-deleted — only deactivated via `IsActive`. This works but is inconsistent with the pattern used by other master-data entities. If a company is deactivated, all its data remains fully accessible with no deletion timestamp trail.

---

### M-03: `IAccountingPolicies.cs` Filename Doesn't Match Content

**File:** `Entities/Accounting/Policies/IAccountingPolicies.cs`  
**Severity:** MEDIUM  

The file is named `IAccountingPolicies.cs` but contains:
```csharp
public interface IJournalNumberGenerator
{
    Task<string> NextNumberAsync(int fiscalYearId);
}
```

The filename implies a broader policy interface. Either rename to `IJournalNumberGenerator.cs` or expand to match the name.

---

### M-04: `JournalEntryLine.CreatedBy` Never Set in Factory Method

**File:** `Entities/Accounting/JournalEntryLine.cs`  
**Severity:** MEDIUM  

The `Create()` factory method sets `CreatedAt = DateTime.UtcNow` but never sets `CreatedBy`. The property exists but will always be `null` unless set externally. Since this entity doesn't extend `AuditableEntity`, there's no interceptor to auto-fill it.

---

### M-05: `InventoryMovement.SetBalanceAfter` Rejects Negative Values

**File:** `Entities/Inventory/InventoryMovement.cs`  
**Severity:** MEDIUM  

```csharp
public void SetBalanceAfter(decimal balance)
{
    if (balance < 0) throw ...
}
```

But `WarehouseProduct` has `DecreaseStockAllowNegative()` which explicitly permits negative stock. If negative stock is allowed, the movement's `BalanceAfter` should also allow negative values. This creates an inconsistency when processing adjustments or special overrides.

---

### M-06: Inner Draft/Update Classes — Inconsistent Placement

**Files:**  
- `Customer.cs` — `CustomerDraft` and `CustomerUpdate` are **inner classes** inside `Customer`  
- `Supplier.cs` — `SupplierDraft` and `SupplierUpdate` are **top-level classes** in the same file  
- `SalesRepresentative.cs` — inner classes  
- `CashPayment.cs` / `CashReceipt.cs` — top-level in same file  
- `PriceList.cs` — inner classes  

**Severity:** MEDIUM  

No consistent pattern. Some use inner classes, others use file-scoped top-level classes. Should standardize on one approach.

---

### M-07: `PurchaseReturn` and `SalesReturn` Don't Track Payment Status

**Files:** `Entities/Purchases/PurchaseReturn.cs`, `Entities/Sales/SalesReturn.cs`  
**Severity:** MEDIUM  

Unlike `PurchaseInvoice` and `SalesInvoice` which track `PaidAmount`, `BalanceDue`, `PaymentStatus`, the return entities have no payment tracking fields. Returns that generate refunds need payment/refund tracking for reconciliation.

---

### M-08: `DomainConstants` Is Sparse

**File:** `DomainConstants.cs`  
**Severity:** MEDIUM  

Only 4 constants defined:
```csharp
SystemUser = "System"
AdminUsername = "admin"
DraftCodePrefix = "DRAFT-"
DefaultPriority = "Medium"
```

Missing: standard account code patterns, document number formats, default VAT rates, maximum decimal precision constants, role names. Many magic strings and numbers are scattered in entity files instead.

---

### M-09: `QuotationStatus` Has Unused Values

**File:** `Enums/QuotationStatus.cs`  
**Severity:** MEDIUM  

`QuotationStatus.Expired = 3` exists in the enum but is never set by any domain entity. The `SalesQuotation` and `PurchaseQuotation` entities check `ValidUntil < DateTime.UtcNow` in their `Accept()` method but don't transition the status to `Expired`. Expiry detection is purely runtime, not persisted.

---

### M-10: `FiscalPeriod` — `FiscalYearId` Not Set in Constructor

**File:** `Entities/Accounting/FiscalPeriod.cs`  
**Severity:** MEDIUM  

The `internal` constructor takes `(int periodNumber, int year, int month, DateTime start, DateTime end)` but never receives or sets `FiscalYearId`. It relies on EF Core relationship fixup when added to `FiscalYear._periods`. If the period is ever detached from the context, `FiscalYearId` will be 0.

---

### M-11: `BackupHistory` Has Own Audit Fields Instead of Extending `AuditableEntity`

**File:** `Entities/Settings/BackupHistory.cs`  
**Severity:** MEDIUM  

Defines its own `CreatedAt` property but doesn't extend `AuditableEntity`. If the persistence interceptor only targets `AuditableEntity` subtypes, `BackupHistory.CreatedAt` won't be auto-populated.

---

### M-12: Missing `ICompanyRepository` Interface

**File:** N/A — does not exist  
**Severity:** MEDIUM  

`Company` entity exists but has no repository interface. CRUD operations on `Company` must go through the generic `IRepository<Company>`, which may lack company-specific queries (e.g., `GetByCodeAsync`, `GetActiveCompaniesAsync`).

---

### M-13: `CounterpartyType` Enum Has Unused `None = 0` Value

**File:** `Enums/CounterpartyType.cs`  
**Severity:** MEDIUM  

```csharp
public enum CounterpartyType { None = 0, Supplier = 1, Customer = 2 }
```

`None` is only used as a default. No entity validates or handles `CounterpartyType.None` explicitly. If a document is created with `None`, validation won't catch it (most checks only ensure the FK matches the type).

---

### M-14: `Role` Entity Missing Audit Fields

**File:** `Entities/Security/Role.cs`  
**Severity:** MEDIUM  

`Role` extends `BaseEntity` directly — no `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`. Security entities should have full audit trails for compliance.

---

## 6. LOW Findings

### L-01: `SystemModule` Enum Missing Explicit Integer Values

**File:** `Enums/SystemModule.cs`  
**Severity:** LOW  

```csharp
public enum SystemModule
{
    Accounting,    // implicitly 0
    Inventory,     // implicitly 1
    Sales,         // implicitly 2
    ...
}
```

All other enums have explicit values. If a value is inserted mid-list, existing database records will break.

---

### L-02: Unnecessary Null Assignments in Entity Constructors

**Files:** `Supplier.cs`, `Customer.cs`, and others  
**Severity:** LOW  

```csharp
Account = null;       // Reference type default is already null
Supplier = null;      // Unnecessary
```

These are noise — reference types default to `null` in C#.

---

### L-03: `Company` Uses `InvalidOperationException` Instead of Domain Exception

**File:** `Entities/Common/Company.cs`  
**Severity:** LOW  

```csharp
throw new InvalidOperationException("Company code is required.");
```

All other entities throw module-specific domain exceptions. `Company` is the only entity using `InvalidOperationException`.

---

### L-04: `PosPayment` Extends `BaseEntity` — No Audit Fields

**File:** `Entities/Sales/PosPayment.cs`  
**Severity:** LOW  

POS payments are financial records but have no `CreatedAt`/`CreatedBy`. They do have `PaidAt` but no modifier tracking.

---

### L-05: `Cashbox` Extends `BaseEntity` — Inconsistent Hierarchy Depth

**File:** `Entities/Treasury/Cashbox.cs`  
**Severity:** LOW  

`Cashbox` extends `BaseEntity` directly, skipping `AuditableEntity`, `SoftDeletableEntity`, and `CompanyAwareEntity`. It has `IsActive` and `IsDefault` but can't be soft-deleted, has no audit trail, and has no company isolation.

---

### L-06: Domain Events Only on `JournalEntry`

**File:** `Entities/Accounting/JournalEntry.cs`  
**Severity:** LOW  

Only `JournalEntry` implements the domain events pattern (`_domainEvents`, `DomainEvents`, `ClearDomainEvents()`). Other state-changing operations (invoice posting, payment completion, session closing) don't raise events. While this may be intentional for now, it limits event-driven extensibility.

---

### L-07: `SalesInvoiceDomainException` Doc Comment Claims Coverage of `SalesReturn`

**File:** `Exceptions/Sales/SalesInvoiceDomainException.cs`  
**Severity:** LOW  

```csharp
/// <summary>
/// Domain exception for SalesInvoice / SalesReturn invariant violations.
/// </summary>
```

But `SalesReturn` actually uses `SalesReturnDomainException` (a separate class). The doc comment is outdated/misleading.

---

### L-08: `InventoryDomainException` Imports Unused Namespace

**File:** `Exceptions/Inventory/InventoryDomainException.cs`  
**Severity:** LOW  

```csharp
using MarcoERP.Domain.Entities.Common; // ← not used
```

---

## 7. File-by-File Summary

### Entities/Common

| File | Status | Issues |
|------|--------|--------|
| `BaseEntity.cs` | ⚠️ | C-02: `RowVersion` public setter |
| `AuditableEntity.cs` | ⚠️ | C-01: All 4 audit fields have public setters |
| `SoftDeletableEntity.cs` | ✅ | Clean — private setters, full validation |
| `CompanyAwareEntity.cs` | ✅ | Clean — protected setter, default CompanyId=1 |
| `Company.cs` | ⚠️ | M-02: No soft-delete; L-03: Uses `InvalidOperationException` |
| `IImmutableFinancialRecord.cs` | ✅ | Clean empty marker interface |

### Entities/Accounting

| File | Status | Issues |
|------|--------|--------|
| `Account.cs` | ⚠️ | C-04: Missing `CompanyAwareEntity` base |
| `AuditLog.cs` | ⚠️ | M-01: Standalone entity (long Id) — justified but undocumented |
| `CodeSequence.cs` | ⚠️ | M-01: Standalone entity duplicating BaseEntity pattern |
| `FiscalYear.cs` | ✅ | Clean — good lifecycle, auto-creates 12 periods |
| `FiscalPeriod.cs` | ⚠️ | M-10: `FiscalYearId` not set in constructor |
| `JournalEntry.cs` | ✅ | Well-designed — factory methods, events, validation, reversal |
| `JournalEntryLine.cs` | ⚠️ | M-04: `CreatedBy` never set; C-03 partial (but lines are immutable in practice since parent checks) |
| `Events/IDomainEvent.cs` | ✅ | Clean |
| `Events/JournalEntryPostedEvent.cs` | ✅ | Clean |
| `Policies/IAccountingPolicies.cs` | ⚠️ | M-03: Filename mismatch with content (`IJournalNumberGenerator`) |

### Entities/Inventory

| File | Status | Issues |
|------|--------|--------|
| `Category.cs` | ⚠️ | C-05: Missing `CompanyAwareEntity`; not `SoftDeletableEntity` |
| `Product.cs` | ✅ | Good WAC calculation, proper status management |
| `InventoryAdjustment.cs` | ⚠️ | H-06: Reuses `InvoiceStatus` enum |
| `InventoryAdjustmentLine.cs` | ⚠️ | C-03: Has `UpdateDetails()` while implementing `IImmutableFinancialRecord` |
| `InventoryMovement.cs` | ⚠️ | M-05: `SetBalanceAfter` rejects negatives vs `DecreaseStockAllowNegative` |
| `Unit.cs` | ✅ | Clean |
| `ProductUnit.cs` | ✅ | Clean — conversion factor, pricing |
| `Warehouse.cs` | ✅ | Clean — default protection |
| `WarehouseProduct.cs` | ✅ | Clean — proper stock increase/decrease with negative handling |

### Entities/Sales

| File | Status | Issues |
|------|--------|--------|
| `Customer.cs` | ✅ | Clean — inner draft classes |
| `PosPayment.cs` | ⚠️ | H-04: Wrong exception type; L-04: No audit fields |
| `PosSession.cs` | ⚠️ | H-04: Wrong exception type |
| `PriceList.cs` | ⚠️ | H-05: Wrong exception type |
| `PriceTier.cs` | ⚠️ | H-05: Wrong exception type |
| `SalesInvoice.cs` | ✅ | Clean — full lifecycle, dual JE support |
| `SalesInvoiceLine.cs` | ⚠️ | C-03: `UpdateDetails()` + `IImmutableFinancialRecord`; H-01: Duplicated calc; H-10: No nav props |
| `SalesQuotation.cs` | ✅ | Clean — expiry check on Accept |
| `SalesQuotationLine.cs` | ⚠️ | H-01: Duplicated calc |
| `SalesRepresentative.cs` | ✅ | Clean |
| `SalesReturn.cs` | ✅ | Clean — uses own `SalesReturnDomainException` |
| `SalesReturnLine.cs` | ⚠️ | C-03: `UpdateDetails()` + `IImmutableFinancialRecord`; H-01: Duplicated calc |

### Entities/Purchases

| File | Status | Issues |
|------|--------|--------|
| `Supplier.cs` | ✅ | Clean — file-level draft classes |
| `PurchaseInvoice.cs` | ✅ | Clean — full lifecycle, payment tracking |
| `PurchaseInvoiceLine.cs` | ⚠️ | C-03: `UpdateDetails()` + `IImmutableFinancialRecord`; H-01: Duplicated calc |
| `PurchaseQuotation.cs` | ✅ | Clean |
| `PurchaseQuotationLine.cs` | ⚠️ | H-01: Duplicated calc |
| `PurchaseReturn.cs` | ⚠️ | M-07: No payment/refund tracking |
| `PurchaseReturnLine.cs` | ⚠️ | C-03: `UpdateDetails()` + `IImmutableFinancialRecord`; H-01: Duplicated calc |

### Entities/Security

| File | Status | Issues |
|------|--------|--------|
| `Role.cs` | ⚠️ | M-14: No audit fields |
| `RolePermission.cs` | ⚠️ | M-01 variant: Standalone with own Id, not BaseEntity |
| `User.cs` | ✅ | Clean — good lockout logic, login tracking |

### Entities/Treasury

| File | Status | Issues |
|------|--------|--------|
| `BankAccount.cs` | ✅ | Clean |
| `BankReconciliation.cs` | ⚠️ | H-02: Duplicated `ReplaceLines` pattern |
| `BankReconciliationItem.cs` | ✅ | Clean |
| `Cashbox.cs` | ⚠️ | L-05: Extends `BaseEntity` — no audit/company/soft-delete |
| `CashPayment.cs` | ✅ | Clean — draft pattern |
| `CashReceipt.cs` | ✅ | Clean — draft pattern |
| `CashTransfer.cs` | ✅ | Clean |

### Entities/Settings

| File | Status | Issues |
|------|--------|--------|
| `BackupHistory.cs` | ⚠️ | M-11: Own audit fields |
| `Feature.cs` | ✅ | Clean — governance engine |
| `FeatureChangeLog.cs` | ✅ | Clean |
| `FeatureVersion.cs` | ✅ | Clean |
| `MigrationExecution.cs` | ✅ | Clean |
| `ProfileFeature.cs` | ✅ | Clean |
| `SystemProfile.cs` | ✅ | Clean |
| `SystemSetting.cs` | ✅ | Clean |
| `SystemVersion.cs` | ✅ | Clean |

### Enums

| File | Status | Issues |
|------|--------|--------|
| `AccountType.cs` | ✅ | Explicit values 0-7 |
| `CommissionBasis.cs` | ✅ | Explicit values 0-1 |
| `CounterpartyType.cs` | ⚠️ | M-13: `None=0` unused/unvalidated |
| `FiscalYearStatus.cs` | ✅ | Explicit values 0-2 |
| `InvoiceStatus.cs` | ⚠️ | H-06: Reused for non-invoice entities |
| `JournalEntryStatus.cs` | ✅ | Explicit values 0-2 |
| `MovementType.cs` | ✅ | Explicit values 0-8 |
| `NormalBalance.cs` | ✅ | Explicit values 0-1 |
| `PaymentMethod.cs` | ✅ | Explicit values 0-2 |
| `PaymentStatus.cs` | ✅ | Explicit values 0-2 |
| `PeriodStatus.cs` | ✅ | Explicit values 0-1 |
| `PosSessionStatus.cs` | ✅ | Explicit values 0-1 |
| `ProductStatus.cs` | ✅ | Explicit values 0-2 |
| `QuotationStatus.cs` | ⚠️ | M-09: `Expired` never set by domain logic |
| `SourceType.cs` | ✅ | Explicit values 0-13 |
| `SystemModule.cs` | ⚠️ | L-01: No explicit integer values |

### Interfaces

| File | Status | Issues |
|------|--------|--------|
| `IRepository.cs` | ✅ | Clean generic contract |
| `IUnitOfWork.cs` | ✅ | Clean — includes `ExecuteInTransactionAsync` |
| `IAccountRepository.cs` | ✅ | Clean — hierarchy queries |
| `IAuditLogRepository.cs` | ✅ | Clean — append-only (AddAsync only) |
| `IFiscalYearRepository.cs` | ✅ | Clean |
| `IJournalEntryRepository.cs` | ✅ | Clean — rich query set |
| `Inventory/*Repository.cs` (7) | ✅ | Clean — good domain-specific queries |
| `Sales/*Repository.cs` (7) | ✅ | Clean |
| `Purchases/*Repository.cs` (4) | ✅ | Clean |
| `Treasury/*Repository.cs` (5) | ✅ | Clean — includes `GetGLBalanceAsync` |
| `Security/*Repository.cs` (2) | ✅ | Clean |
| `Settings/*Repository.cs` (3) | ✅ | Clean |
| N/A | ⚠️ | M-12: Missing `ICompanyRepository` |

### Exceptions

| File | Status | Issues |
|------|--------|--------|
| `ConcurrencyConflictException.cs` | ⚠️ | H-07: Not sealed; H-08: Extra `EntityName` property |
| `SecurityDomainException.cs` | ⚠️ | H-07: Not sealed |
| `AccountDomainException.cs` | ⚠️ | H-09: Missing inner-exception constructor |
| `JournalEntryDomainException.cs` | ⚠️ | H-09: Missing inner-exception constructor |
| `SalesReturnDomainException.cs` | ⚠️ | H-09: Missing inner-exception constructor |
| `PurchaseReturnDomainException.cs` | ⚠️ | H-09: Missing inner-exception constructor |
| All other exceptions (11) | ✅ | Clean — sealed, proper constructors |

### Other

| File | Status | Issues |
|------|--------|--------|
| `DomainConstants.cs` | ⚠️ | M-08: Sparse — only 4 constants |
| `MarcoERP.Domain.csproj` | ✅ | Clean — zero dependencies, net8.0 |

---

## 8. Recommendations Roadmap

### Phase 1 — CRITICAL Fixes (Immediate)

| # | Action | Files | Effort |
|---|--------|-------|--------|
| 1 | Change `AuditableEntity` setters to `internal set` + add `InternalsVisibleTo` for Persistence | `AuditableEntity.cs`, `.csproj` | Small |
| 2 | Change `BaseEntity.RowVersion` to `private set` | `BaseEntity.cs` | Small |
| 3 | Remove `UpdateDetails()` from all `IImmutableFinancialRecord` entities OR guard it behind draft-status check | 5 line entities | Medium |
| 4 | Change `Account` base to `CompanyAwareEntity` | `Account.cs` + migrations | Medium |
| 5 | Change `Category` base to `CompanyAwareEntity` (via `SoftDeletableEntity`) | `Category.cs` + migrations | Medium |

### Phase 2 — HIGH Fixes (This Sprint)

| # | Action | Files | Effort |
|---|--------|-------|--------|
| 6 | Extract line calculation into `DocumentLineCalculator` | New file + 6 line entities | Medium |
| 7 | Extract `ReplaceLines` into generic base or helper | 8 document entities | Medium |
| 8 | Create `PosDomainException` + `PriceListDomainException` | 4 entity files + 2 exception files | Small |
| 9 | Create `AdjustmentStatus` or `DocumentStatus` enum | `InventoryAdjustment.cs` + enum | Small |
| 10 | Seal `SecurityDomainException` + `ConcurrencyConflictException` | 2 exception files | Trivial |
| 11 | Add inner-exception constructors to 4 exception classes | 4 files | Trivial |

### Phase 3 — MEDIUM Fixes (Next Sprint)

| # | Action | Files | Effort |
|---|--------|-------|--------|
| 12 | Rename `IAccountingPolicies.cs` → `IJournalNumberGenerator.cs` | 1 file | Trivial |
| 13 | Set `JournalEntryLine.CreatedBy` in factory method | 1 file | Trivial |
| 14 | Allow negative `BalanceAfter` in `InventoryMovement.SetBalanceAfter` | 1 file | Small |
| 15 | Standardize draft/update class placement (choose inner or file-level) | 5 entity files | Small |
| 16 | Add `ICompanyRepository` interface | 1 new file | Small |
| 17 | Extend `DomainConstants` with standard values | 1 file | Small |
| 18 | Add payment tracking to return entities | 2 files | Medium |
| 19 | Add audit fields to `Role` | 1 file + migration | Small |

### Phase 4 — LOW Fixes (Backlog)

| # | Action | Files | Effort |
|---|--------|-------|--------|
| 20 | Add explicit values to `SystemModule` enum | 1 file | Trivial |
| 21 | Remove unnecessary null assignments | Multiple | Trivial |
| 22 | Replace `InvalidOperationException` in `Company` | 1 file | Trivial |
| 23 | Add `PosDomainException` for `PosPayment` audit fields | 1 file | Small |
| 24 | Consider `CompanyAwareEntity` for `Cashbox` | 1 file + migration | Medium |

---

*End of Domain Layer Audit Report*
