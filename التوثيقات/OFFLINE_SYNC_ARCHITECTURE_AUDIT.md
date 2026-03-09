# MarcoERP – OFFLINE-FIRST SYNC ARCHITECTURE AUDIT & DESIGN
## Full Architecture Audit Report

**Audit Date:** March 6, 2026  
**Scope:** Backend API + Mobile App + Database  
**Audited Layers:** Domain → Application → Persistence → API → Mobile Flutter  
**Lines Analyzed:** 30,000+ LOC across 250+ files  

---

## TABLE OF CONTENTS

1. [Executive Summary](#1-executive-summary)
2. [Current System Architecture](#2-current-system-architecture)
3. [Layer-by-Layer Audit Results](#3-layer-by-layer-audit-results)
4. [Gap Analysis Matrix](#4-gap-analysis-matrix)
5. [Offline-First Sync Architecture Design](#5-offline-first-sync-architecture-design)
6. [Database Sync Tables Design](#6-database-sync-tables-design)
7. [API Sync Endpoints Design](#7-api-sync-endpoints-design)
8. [Mobile Sync Engine Design](#8-mobile-sync-engine-design)
9. [Conflict Resolution Strategy](#9-conflict-resolution-strategy)
10. [Idempotent Operations Design](#10-idempotent-operations-design)
11. [Network Detection & Resilience](#11-network-detection--resilience)
12. [Transaction Safety Design](#12-transaction-safety-design)
13. [Implementation Roadmap](#13-implementation-roadmap)
14. [Architecture Diagrams](#14-architecture-diagrams)

---

## 1. EXECUTIVE SUMMARY

### Current State: Online-Only Architecture (Score: 3/10 for Offline Readiness)

The MarcoERP system is currently a **fully online-centric** architecture with:
- ✅ **Solid foundation**: Clean Architecture, optimistic concurrency (RowVersion), comprehensive audit trail, soft delete
- ❌ **Zero offline capabilities**: No local database, no sync metadata, no queue, no conflict resolution
- ❌ **No resilience**: No retry logic, no circuit breaker, no network detection
- ❌ **No idempotency**: POST requests create duplicates on retry

### Required Transformation

```
CURRENT STATE                    TARGET STATE
┌──────────────┐                ┌──────────────────────────┐
│  Online-Only │   ──────►     │  Offline-First + Sync     │
│  Mobile App  │                │  Local DB + Queue + Sync  │
│  (Dio → API) │                │  (SQLite → Queue → API)   │
└──────────────┘                └──────────────────────────┘
```

---

## 2. CURRENT SYSTEM ARCHITECTURE

### 2.1 Architecture Layers

```
┌─────────────────────────────────────────────────────────────────┐
│                     MOBILE (Flutter/Dart)                        │
│  27 screens │ 5 models │ Dio HTTP client │ Provider state mgmt  │
│  ❌ No local DB │ ❌ No cache │ ❌ No sync │ ❌ No offline queue │
├─────────────────────────────────────────────────────────────────┤
│                     API LAYER (ASP.NET Core)                     │
│  20+ Controllers │ JWT Auth │ CORS │ ExceptionMiddleware        │
│  ❌ No idempotency │ ❌ No sync endpoints │ ❌ No rate limiting  │
├─────────────────────────────────────────────────────────────────┤
│                   APPLICATION LAYER (Services)                   │
│  45+ Services │ ServiceResult pattern │ FluentValidation         │
│  AuthorizationProxy │ FeatureGuard │ UnitOfWork transactions    │
│  ❌ No RequestId │ ❌ No conflict detection │ ❌ No compensation  │
├─────────────────────────────────────────────────────────────────┤
│                   PERSISTENCE LAYER (EF Core)                    │
│  170+ DbSets │ 50+ Repositories │ AuditInterceptor             │
│  HardDeleteProtectionInterceptor │ Global Query Filters         │
│  ❌ No SyncVersion │ ❌ No change tracking │ ❌ No delta queries  │
├─────────────────────────────────────────────────────────────────┤
│                      DOMAIN LAYER (Entities)                     │
│  50+ Entities │ RowVersion (optimistic concurrency)              │
│  Soft Delete │ Audit Trail │ Domain Events │ IImmutableRecord   │
│  ❌ No SyncId │ ❌ No SyncStatus │ ❌ No DeviceId               │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Entity Hierarchy

```
BaseEntity (Id: int, RowVersion: byte[])
  └── AuditableEntity (+CreatedAt/By, ModifiedAt/By)
        └── SoftDeletableEntity (+IsDeleted, DeletedAt/By)
              └── CompanyAwareEntity (+CompanyId)
                    └── All business entities (Product, SalesInvoice, Customer, etc.)
```

### 2.3 Financial Entity Protection

| Pattern | Applied To | Protection Level |
|---------|-----------|-----------------|
| IImmutableFinancialRecord | JournalEntryLine, SalesInvoiceLine, WarehouseProduct, AuditLog | Append-only, no update/delete |
| SoftDeletableEntity | All transactional entities | No hard delete |
| HardDeleteProtectionInterceptor | Database level | Blocks EntityState.Deleted |
| RowVersion | ALL entities | Optimistic concurrency |

---

## 3. LAYER-BY-LAYER AUDIT RESULTS

### 3.1 Domain Layer Audit

| Feature | Status | Details |
|---------|--------|---------|
| Optimistic Concurrency | ✅ Present | `byte[] RowVersion` on ALL entities via BaseEntity |
| Soft Delete | ✅ Present | IsDeleted, DeletedAt, DeletedBy on 40+ entities |
| Audit Trail | ✅ Present | CreatedAt/By, ModifiedAt/By on all AuditableEntity |
| Domain Events | ✅ Present | JournalEntryPostedEvent pattern |
| Immutable Records | ✅ Present | IImmutableFinancialRecord on financial lines |
| **SyncId (UUID)** | ❌ **Missing** | No globally unique sync identifier |
| **SyncVersion** | ❌ **Missing** | No incremental change version number |
| **SyncStatus** | ❌ **Missing** | No Pending/Synced/Conflict state |
| **DeviceId** | ❌ **Missing** | No device origin tracking |
| **IdempotencyKey** | ❌ **Missing** | No request deduplication key |

### 3.2 Application Layer Audit

| Feature | Status | Details |
|---------|--------|---------|
| ServiceResult pattern | ✅ Present | Consistent success/failure with error messages |
| Transaction handling | ✅ Present | ExecuteInTransactionAsync with ReadCommitted |
| FluentValidation | ✅ Present | DTO-level + Business-level + Domain-level |
| Authorization | ✅ Present | DispatchProxy with 41 permission keys |
| **Idempotency detection** | ⚠️ Minimal | Only in YearEndClosingService |
| **Error classification** | ❌ **Missing** | No Transient vs Permanent distinction |
| **RequestId tracking** | ❌ **Missing** | No correlation of client requests |
| **Compensation logic** | ❌ **Missing** | No rollback/undo mechanism |
| **Offline validation** | ❌ **Missing** | All validations require DB |

### 3.3 Persistence Layer Audit

| Feature | Status | Details |
|---------|--------|---------|
| DbContext | ✅ Solid | 170+ DbSets, global filters |
| UnitOfWork | ✅ Solid | Transaction + exception translation |
| AuditInterceptor | ✅ Solid | Full before/after JSON, changed columns |
| HardDeleteProtection | ✅ Solid | 3-guard system (Production, SoftDelete, Immutable) |
| Global Query Filters | ✅ Solid | CompanyId + IsDeleted automatic filtering |
| Compiled Queries | ✅ Solid | 30+ compiled queries for hot paths |
| **SyncVersion columns** | ❌ **Missing** | No change tracking version |
| **Delta query support** | ❌ **Missing** | No GetChangesSince() methods |
| **Sync metadata tables** | ❌ **Missing** | No SyncQueue, SyncLog, SyncConflict |

### 3.4 Mobile App Audit

| Feature | Status | Details |
|---------|--------|---------|
| HTTP client | ✅ Present | Dio with 15s timeouts |
| JWT auth | ✅ Present | FlutterSecureStorage |
| Arabic UI | ✅ Present | Full RTL support |
| 27 screens | ✅ Present | CRUD for all modules |
| **Local database** | ❌ **Missing** | No SQLite, Hive, or drift |
| **Network detection** | ❌ **Missing** | No connectivity_plus |
| **Retry logic** | ❌ **Missing** | Single attempt only |
| **Data caching** | ❌ **Missing** | Full refetch on every load |
| **Offline queue** | ❌ **Missing** | Operations lost if offline |
| **Token refresh** | ❌ **Missing** | Logout on token expiry |
| **Background sync** | ❌ **Missing** | No scheduled sync |

---

## 4. GAP ANALYSIS MATRIX

### Priority Classification

| Gap | Severity | Layer | Impact |
|-----|----------|-------|--------|
| No local database on mobile | 🔴 CRITICAL | Mobile | Cannot work offline at all |
| No network detection | 🔴 CRITICAL | Mobile | App crashes/hangs without network |
| No sync metadata tables | 🔴 CRITICAL | Database | Cannot track what's synced |
| No SyncVersion on entities | 🔴 CRITICAL | Domain | Cannot detect changes |
| No idempotency keys | 🔴 CRITICAL | API | Duplicate transactions on retry |
| No offline operation queue | 🔴 CRITICAL | Mobile | Pending work lost |
| No retry logic | 🟡 HIGH | Mobile | Single failure = manual retry |
| No conflict resolution | 🟡 HIGH | API+Mobile | Data corruption risk |
| No error classification | 🟡 HIGH | Application | Can't distinguish retry-worthy errors |
| No token refresh | 🟡 HIGH | Mobile | User logged out frequently |
| No delta sync queries | 🟠 MEDIUM | Persistence | Full table sync = slow |
| No compensation/undo | 🟠 MEDIUM | Application | Failed multi-step ops leave state |
| No circuit breaker | 🟠 MEDIUM | Mobile | Server floods on outage |
| No pagination | 🟢 LOW | API+Mobile | Large datasets = poor UX |

---

## 5. OFFLINE-FIRST SYNC ARCHITECTURE DESIGN

### 5.1 Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        MOBILE DEVICE (Flutter)                          │
│                                                                         │
│  ┌─────────────┐  ┌──────────────┐  ┌──────────────┐  ┌─────────────┐ │
│  │   UI Layer   │  │ Sync Engine  │  │ Network      │  │ Conflict    │ │
│  │  27 Screens  │  │  (Queue +    │  │ Monitor      │  │ Resolver    │ │
│  │  + Providers │  │   Scheduler) │  │ (Online/     │  │ (UI +       │ │
│  │              │  │              │  │  Offline)     │  │  Strategy)  │ │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘  └──────┬──────┘ │
│         │                  │                  │                  │       │
│  ┌──────▼──────────────────▼──────────────────▼──────────────────▼──────┐│
│  │                    LOCAL REPOSITORY LAYER                            ││
│  │  ┌─────────────┐  ┌──────────────┐  ┌──────────────────────────┐   ││
│  │  │ SQLite DB   │  │ Sync Queue   │  │ Pending Operations Table │   ││
│  │  │ (drift/     │  │ (FIFO with   │  │ (Create/Update/Delete    │   ││
│  │  │  sqflite)   │  │  priority)   │  │  before sync)            │   ││
│  │  └─────────────┘  └──────────────┘  └──────────────────────────┘   ││
│  └─────────────────────────────────────────────────────────────────────┘│
│         │                  │                                            │
│         │ READ (local)     │ SYNC (when online)                         │
└─────────┼──────────────────┼────────────────────────────────────────────┘
          │                  │
          │                  ▼
┌─────────┼──────────────────────────────────────────────────────────────┐
│         │            BACKEND API (ASP.NET Core)                        │
│         │                                                              │
│  ┌──────▼──────────┐  ┌──────────────┐  ┌───────────────────────────┐ │
│  │ SyncController   │  │ Idempotency  │  │ Conflict Detection       │ │
│  │ POST /sync/push  │  │ Middleware   │  │ Service                   │ │
│  │ GET  /sync/pull  │  │ (X-Idemkey)  │  │ (RowVersion + SyncVer)   │ │
│  │ GET  /sync/status│  │              │  │                           │ │
│  └──────┬───────────┘  └──────┬───────┘  └───────────┬───────────────┘ │
│         │                     │                       │                 │
│  ┌──────▼─────────────────────▼───────────────────────▼───────────────┐ │
│  │                    APPLICATION SERVICES                            │ │
│  │  SyncOrchestrator → Existing Services + IdempotencyGuard          │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│         │                                                              │
│  ┌──────▼────────────────────────────────────────────────────────────┐ │
│  │                    PERSISTENCE (SQL Server)                        │ │
│  │  ┌─────────────┐  ┌──────────────┐  ┌──────────────┐             │ │
│  │  │ Business    │  │ SyncMetadata │  │ Idempotency  │             │ │
│  │  │ Tables      │  │ SyncConflict │  │ Store        │             │ │
│  │  │ (+SyncVer)  │  │ SyncLog      │  │ (RequestId)  │             │ │
│  │  └─────────────┘  └──────────────┘  └──────────────┘             │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────┘
```

### 5.2 Sync Strategy: Delta Sync with Server-Wins Default

**Why Delta Sync (not full sync)?**
- MarcoERP has 170+ tables with potentially millions of records
- Mobile devices have limited storage and bandwidth
- Only changed records since last sync need to transfer

**Why Server-Wins Default?**
- Financial data integrity is paramount (ACCOUNTING_PRINCIPLES)
- Server has validated transactions through 15-step posting workflows
- Conflict exceptions flagged for manual review
- Critical: Posted financial records are immutable — never overwritten

### 5.3 Sync Data Classification

| Category | Tables | Sync Direction | Conflict Strategy |
|----------|--------|---------------|-------------------|
| **Reference Data** | Product, Category, Unit, Warehouse, Account, Customer, Supplier | Server → Mobile (Read-only) | Server always wins |
| **Transaction Drafts** | SalesInvoice(Draft), CashReceipt(Draft), CashPayment(Draft) | Bidirectional | Last-writer-wins for drafts |
| **Posted Transactions** | SalesInvoice(Posted), JournalEntry(Posted) | Server → Mobile (Read-only) | Immutable — no conflict possible |
| **User Settings** | UserPreferences, LastUsedWarehouse | Mobile → Server | Mobile wins |
| **Lookup Data** | FiscalYear, FiscalPeriod, SystemSettings, PriceList | Server → Mobile (Read-only) | Server always wins |
| **Inventory Levels** | WarehouseProduct (stock balances) | Server → Mobile (Read-only) | Server always wins |

---

## 6. DATABASE SYNC TABLES DESIGN

### 6.1 New Tables Required on Server (SQL Server)

#### Table: `SyncDevices` — Device Registration

```sql
CREATE TABLE SyncDevices (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    DeviceId        NVARCHAR(64) NOT NULL UNIQUE,     -- UUID generated on first app launch
    DeviceName      NVARCHAR(200),                     -- "Samsung Galaxy S24", etc.
    UserId          INT NOT NULL,                      -- FK → Users
    CompanyId       INT NOT NULL DEFAULT 1,            -- FK → Companies
    Platform        NVARCHAR(20) NOT NULL,             -- 'Android', 'iOS'
    AppVersion      NVARCHAR(20),                      -- '1.0.0'
    LastSyncUtc     DATETIME2 NULL,                    -- Last successful sync
    LastSyncVersion BIGINT NOT NULL DEFAULT 0,         -- Last sync watermark
    IsActive        BIT NOT NULL DEFAULT 1,
    RegisteredAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    RowVersion      ROWVERSION NOT NULL,

    CONSTRAINT FK_SyncDevices_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT FK_SyncDevices_Companies FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
);
CREATE INDEX IX_SyncDevices_UserId ON SyncDevices(UserId);
CREATE INDEX IX_SyncDevices_LastSync ON SyncDevices(LastSyncUtc);
```

#### Table: `SyncLog` — Sync Session History

```sql
CREATE TABLE SyncLog (
    Id                  BIGINT IDENTITY(1,1) PRIMARY KEY,
    DeviceId            NVARCHAR(64) NOT NULL,              -- FK → SyncDevices.DeviceId
    CompanyId           INT NOT NULL DEFAULT 1,
    SyncDirection       NVARCHAR(10) NOT NULL,              -- 'Push', 'Pull', 'Full'
    StartedAtUtc        DATETIME2 NOT NULL,
    CompletedAtUtc      DATETIME2 NULL,
    Status              NVARCHAR(20) NOT NULL DEFAULT 'InProgress',  -- InProgress, Completed, Failed, PartiallyCompleted
    TotalOperations     INT NOT NULL DEFAULT 0,
    SuccessfulOps       INT NOT NULL DEFAULT 0,
    FailedOps           INT NOT NULL DEFAULT 0,
    ConflictOps         INT NOT NULL DEFAULT 0,
    FromSyncVersion     BIGINT NOT NULL,                    -- Starting watermark
    ToSyncVersion       BIGINT NULL,                        -- Ending watermark (set on completion)
    ErrorMessage        NVARCHAR(MAX) NULL,
    DurationMs          INT NULL,

    CONSTRAINT FK_SyncLog_Devices FOREIGN KEY (DeviceId) REFERENCES SyncDevices(DeviceId)
);
CREATE INDEX IX_SyncLog_DeviceId ON SyncLog(DeviceId);
CREATE INDEX IX_SyncLog_StartedAt ON SyncLog(StartedAtUtc DESC);
```

#### Table: `SyncConflicts` — Conflict Audit Trail

```sql
CREATE TABLE SyncConflicts (
    Id                  BIGINT IDENTITY(1,1) PRIMARY KEY,
    SyncLogId           BIGINT NOT NULL,                    -- FK → SyncLog
    DeviceId            NVARCHAR(64) NOT NULL,
    CompanyId           INT NOT NULL DEFAULT 1,
    EntityType          NVARCHAR(100) NOT NULL,             -- 'SalesInvoice', 'Product', etc.
    EntityId            INT NOT NULL,
    ConflictType        NVARCHAR(30) NOT NULL,              -- 'UpdateUpdate', 'DeleteUpdate', 'CreateCreate'
    ClientData          NVARCHAR(MAX) NOT NULL,             -- JSON of client version
    ServerData          NVARCHAR(MAX) NOT NULL,             -- JSON of server version
    ClientSyncVersion   BIGINT NOT NULL,
    ServerSyncVersion   BIGINT NOT NULL,
    ResolutionStrategy  NVARCHAR(20) NOT NULL,              -- 'ServerWins', 'ClientWins', 'Merged', 'ManualRequired'
    ResolvedData        NVARCHAR(MAX) NULL,                 -- JSON of final resolved data
    DetectedAtUtc       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ResolvedAtUtc       DATETIME2 NULL,
    ResolvedBy          NVARCHAR(100) NULL,

    CONSTRAINT FK_SyncConflicts_SyncLog FOREIGN KEY (SyncLogId) REFERENCES SyncLog(Id)
);
CREATE INDEX IX_SyncConflicts_Entity ON SyncConflicts(EntityType, EntityId);
CREATE INDEX IX_SyncConflicts_Unresolved ON SyncConflicts(ResolvedAtUtc) WHERE ResolvedAtUtc IS NULL;
```

#### Table: `IdempotencyKeys` — Prevent Duplicate Operations

```sql
CREATE TABLE IdempotencyKeys (
    Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
    IdempotencyKey  NVARCHAR(64) NOT NULL UNIQUE,           -- UUID from client
    CompanyId       INT NOT NULL DEFAULT 1,
    DeviceId        NVARCHAR(64) NOT NULL,
    EntityType      NVARCHAR(100) NOT NULL,
    OperationType   NVARCHAR(20) NOT NULL,                  -- 'Create', 'Update', 'Delete', 'Post'
    RequestPayload  NVARCHAR(MAX) NULL,                     -- Original request JSON (for audit)
    ResponsePayload NVARCHAR(MAX) NULL,                     -- Cached response JSON
    HttpStatusCode  INT NULL,
    CreatedAtUtc    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAtUtc    DATETIME2 NOT NULL,                     -- Auto-cleanup after 7 days

    CONSTRAINT FK_IdempotencyKeys_Devices FOREIGN KEY (DeviceId) REFERENCES SyncDevices(DeviceId)
);
CREATE INDEX IX_IdempotencyKeys_Key ON IdempotencyKeys(IdempotencyKey);
CREATE INDEX IX_IdempotencyKeys_Expires ON IdempotencyKeys(ExpiresAtUtc);
```

### 6.2 Column Additions to Existing Tables

**Add to `SoftDeletableEntity` base class (affects 40+ tables):**

```sql
-- Add SyncVersion to ALL CompanyAwareEntity tables
ALTER TABLE Products ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
ALTER TABLE Customers ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
ALTER TABLE Suppliers ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
ALTER TABLE SalesInvoices ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
ALTER TABLE PurchaseInvoices ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
ALTER TABLE CashReceipts ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
ALTER TABLE CashPayments ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
ALTER TABLE CashTransfers ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
ALTER TABLE Warehouses ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
ALTER TABLE Categories ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
ALTER TABLE Units ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
ALTER TABLE Cashboxes ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
ALTER TABLE BankAccounts ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
ALTER TABLE Accounts ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
ALTER TABLE JournalEntries ADD SyncVersion BIGINT NOT NULL DEFAULT 0;
-- ... ALL CompanyAwareEntity tables

-- Add indexes for delta sync queries
CREATE INDEX IX_Products_SyncVersion ON Products(SyncVersion) WHERE IsDeleted = 0;
CREATE INDEX IX_Customers_SyncVersion ON Customers(SyncVersion) WHERE IsDeleted = 0;
CREATE INDEX IX_SalesInvoices_SyncVersion ON SalesInvoices(SyncVersion) WHERE IsDeleted = 0;
-- ... ALL syncable tables
```

**Global Version Counter (Sequence):**

```sql
-- Server-wide monotonic sync version counter
CREATE SEQUENCE SyncVersionSequence
    AS BIGINT
    START WITH 1
    INCREMENT BY 1
    NO CYCLE;
```

---

## 7. API SYNC ENDPOINTS DESIGN

### 7.1 Sync Controller Endpoints

```
POST   /api/sync/register-device          Register mobile device for sync
GET    /api/sync/pull?since={version}      Pull all changes since version (delta)
POST   /api/sync/push                      Push local changes to server (batch)
GET    /api/sync/status                    Get current sync watermark
GET    /api/sync/conflicts                 Get unresolved conflicts for device
POST   /api/sync/conflicts/{id}/resolve    Resolve a specific conflict
POST   /api/health/ping                    Quick connectivity check (already exists)
```

### 7.2 Pull Endpoint — Delta Sync (Server → Mobile)

**Request:**
```http
GET /api/sync/pull?since=1500&entities=Product,Customer,SalesInvoice&limit=500
Authorization: Bearer {jwt}
X-Device-Id: {device-uuid}
```

**Response:**
```json
{
  "currentSyncVersion": 1847,
  "hasMore": false,
  "changes": {
    "products": {
      "upserted": [
        { "id": 42, "code": "P001", "nameAr": "لابتوب", "syncVersion": 1510, ... },
        { "id": 43, "code": "P002", "nameAr": "طابعة", "syncVersion": 1612, ... }
      ],
      "deleted": [
        { "id": 99, "syncVersion": 1520 }
      ]
    },
    "customers": {
      "upserted": [
        { "id": 10, "code": "C001", "nameAr": "شركة الأمل", "syncVersion": 1600, ... }
      ],
      "deleted": []
    },
    "salesInvoices": {
      "upserted": [ ... ],
      "deleted": []
    }
  },
  "syncMetadata": {
    "serverTimeUtc": "2026-03-06T14:30:00Z",
    "totalChanges": 5,
    "fromVersion": 1500,
    "toVersion": 1847
  }
}
```

### 7.3 Push Endpoint — Batch Upload (Mobile → Server)

**Request:**
```http
POST /api/sync/push
Authorization: Bearer {jwt}
X-Device-Id: {device-uuid}
Content-Type: application/json
```

```json
{
  "deviceId": "abc-123-def",
  "operations": [
    {
      "idempotencyKey": "op-uuid-001",
      "entityType": "SalesInvoice",
      "operationType": "Create",
      "clientTimestamp": "2026-03-06T10:00:00Z",
      "data": {
        "customerId": 10,
        "warehouseId": 1,
        "invoiceType": "Cash",
        "lines": [
          { "productId": 42, "unitId": 1, "quantity": 2, "unitPrice": 500.00 }
        ]
      }
    },
    {
      "idempotencyKey": "op-uuid-002",
      "entityType": "CashReceipt",
      "operationType": "Create",
      "clientTimestamp": "2026-03-06T10:05:00Z",
      "data": {
        "cashboxId": 1,
        "customerId": 10,
        "amount": 1000.00,
        "description": "تحصيل فاتورة"
      }
    }
  ]
}
```

**Response:**
```json
{
  "syncLogId": 4567,
  "results": [
    {
      "idempotencyKey": "op-uuid-001",
      "status": "Success",
      "entityType": "SalesInvoice",
      "serverId": 156,
      "serverSyncVersion": 1848
    },
    {
      "idempotencyKey": "op-uuid-002",
      "status": "Conflict",
      "entityType": "CashReceipt",
      "conflictId": 89,
      "conflictType": "ValidationError",
      "errorMessage": "رصيد الصندوق غير كافي."
    }
  ],
  "newSyncVersion": 1848,
  "serverTimeUtc": "2026-03-06T14:30:05Z"
}
```

### 7.4 Idempotency Middleware

```
For every POST/PUT/DELETE request:
1. Check X-Idempotency-Key header
2. If key exists in IdempotencyKeys table AND not expired:
   → Return cached response (same status code + body)
3. If key is new:
   → Execute operation
   → Store response in IdempotencyKeys with 7-day TTL
   → Return response
4. If no header:
   → Execute normally (backward compatible)
```

---

## 8. MOBILE SYNC ENGINE DESIGN

### 8.1 Local Database Schema (SQLite via drift)

```dart
// Mobile SQLite tables mirror server tables with sync metadata

// == Product Table (Local) ==
class Products extends Table {
  IntColumn get id => integer()();           // Server ID (0 if not synced)
  TextColumn get localId => text()();        // Local UUID (always present)
  TextColumn get code => text()();
  TextColumn get nameAr => text()();
  TextColumn get nameEn => text().nullable()();
  IntColumn get categoryId => integer()();
  RealColumn get defaultSalePrice => real()();
  RealColumn get wholesalePrice => real()();
  RealColumn get retailPrice => real()();
  RealColumn get weightedAverageCost => real()();
  TextColumn get barcode => text().nullable()();
  BoolColumn get isActive => boolean().withDefault(const Constant(true))();
  
  // Sync metadata
  IntColumn get syncVersion => integer().withDefault(const Constant(0))();
  TextColumn get syncStatus => text().withDefault(const Constant('synced'))();
  // Values: 'synced', 'pending_create', 'pending_update', 'pending_delete', 'conflict'
  DateTimeColumn get localModifiedAt => dateTime()();
  DateTimeColumn get lastSyncedAt => dateTime().nullable()();
  
  @override
  Set<Column> get primaryKey => {localId};
}

// == Sync Queue Table (FIFO operations queue) ==
class SyncQueue extends Table {
  IntColumn get id => integer().autoIncrement()();
  TextColumn get idempotencyKey => text()();      // UUID, unique per operation
  TextColumn get entityType => text()();          // 'SalesInvoice', 'CashReceipt'
  TextColumn get operationType => text()();       // 'Create', 'Update', 'Delete'
  TextColumn get entityLocalId => text()();       // Local UUID of affected entity
  TextColumn get payload => text()();             // JSON of the operation data
  IntColumn get priority => integer().withDefault(const Constant(0))();  // Higher = first
  IntColumn get attemptCount => integer().withDefault(const Constant(0))();
  TextColumn get lastError => text().nullable()();
  DateTimeColumn get createdAt => dateTime()();
  DateTimeColumn get nextRetryAt => dateTime().nullable()();
  TextColumn get status => text().withDefault(const Constant('pending'))();
  // Values: 'pending', 'in_progress', 'completed', 'failed', 'conflict'
}

// == Sync Metadata Table ==
class SyncMetadata extends Table {
  TextColumn get key => text()();               // 'last_sync_version', 'device_id', etc.
  TextColumn get value => text()();
  DateTimeColumn get updatedAt => dateTime()();
  
  @override
  Set<Column> get primaryKey => {key};
}
```

### 8.2 Sync Engine State Machine

```
                    ┌──────────┐
                    │   IDLE   │ ◄──────────────────────────┐
                    └────┬─────┘                             │
                         │                                   │
                   [Timer tick or                        [All done]
                    Network restored]                        │
                         │                                   │
                    ┌────▼─────┐                             │
                    │ CHECKING │                              │
                    │ NETWORK  │                              │
                    └────┬─────┘                              │
                         │                                   │
                    [Online?]                                 │
                    YES │  NO → back to IDLE                 │
                         │                                   │
                    ┌────▼─────┐                              │
                    │ PULLING  │  GET /sync/pull?since=N      │
                    │ CHANGES  │                              │
                    └────┬─────┘                              │
                         │                                   │
                   [Apply to local DB]                       │
                         │                                   │
                    ┌────▼─────┐                              │
                    │ PUSHING  │  POST /sync/push (batch)    │
                    │ CHANGES  │                              │
                    └────┬─────┘                              │
                         │                                   │
                   [Process results]                         │
                   (Success/Conflict/Error)                  │
                         │                                   │
                    ┌────▼─────┐                              │
                    │ RESOLVING│  Handle conflicts            │
                    │ CONFLICTS│                              │
                    └────┬─────┘                              │
                         │                                   │
                         └───────────────────────────────────┘
```

### 8.3 Sync Scheduler

```dart
class SyncScheduler {
  Timer? _periodicTimer;
  StreamSubscription? _connectivitySubscription;
  
  // Configuration
  static const syncIntervalMinutes = 5;     // Auto-sync every 5 minutes
  static const maxRetryAttempts = 5;
  static const baseRetryDelaySeconds = 10;  // Exponential backoff: 10, 20, 40, 80, 160
  
  void start() {
    // 1. Periodic sync
    _periodicTimer = Timer.periodic(
      Duration(minutes: syncIntervalMinutes),
      (_) => _triggerSync(),
    );
    
    // 2. Connectivity-driven sync
    _connectivitySubscription = Connectivity().onConnectivityChanged.listen((status) {
      if (status != ConnectivityResult.none) {
        _triggerSync(); // Sync immediately when network restored
      }
    });
  }
  
  Future<void> _triggerSync() async {
    // Pull first, then push (server state is authoritative)
    await _pullChanges();
    await _pushPendingOperations();
  }
}
```

### 8.4 Offline Operation Flow

```
USER ACTION (e.g., Create Sales Invoice)
    │
    ▼
[Check Network Status]
    │
    ├── ONLINE ──────────────────────────────┐
    │   │                                     │
    │   ▼                                     │
    │   POST /api/sales-invoices              │
    │   │                                     │
    │   ├── 200 OK → Save to local DB         │
    │   │             (syncStatus = 'synced')  │
    │   │                                     │
    │   └── Error → Save to SyncQueue         │
    │                (syncStatus = 'pending')  │
    │                                         │
    └── OFFLINE ─────────────────────────────┐│
        │                                    ││
        ▼                                    ││
        Save to local DB                     ││
        (syncStatus = 'pending_create')      ││
        │                                    ││
        ▼                                    ││
        Add to SyncQueue                     ││
        (idempotencyKey = new UUID)          ││
        │                                    ││
        ▼                                    ││
        Show success to user ✅              ││
        (with "pending sync" indicator)      ││
        │                                    ││
        ▼                                    ▼▼
        [Later: SyncEngine picks up          
         when network available]
```

---

## 9. CONFLICT RESOLUTION STRATEGY

### 9.1 Conflict Types & Resolution Rules

| Conflict Type | Scenario | Resolution | Rationale |
|--------------|----------|------------|-----------|
| **Create-Create** | Same product code created on 2 devices | Server record wins, client gets new auto-code | Code uniqueness must be server-enforced |
| **Update-Update (Draft)** | Same draft invoice edited on 2 devices | **Last-writer-wins** (by timestamp) | Drafts are work-in-progress, not financial records |
| **Update-Update (Posted)** | Impossible — posted records are immutable | N/A | IImmutableFinancialRecord prevents this |
| **Delete-Update** | Device A deletes customer, Device B edits it | **Delete wins** | Safety: deleted records stay deleted |
| **Update-Delete** | Device A edits customer, Device B deletes it | **Delete wins** | Same as above |
| **Stale Read** | Mobile shows old stock balance | **Server always wins** for reads | WarehouseProduct is read-only on mobile |

### 9.2 Conflict Detection Algorithm

```
FOR EACH pushed operation:
  1. Lookup entity by serverId
  2. Compare client's syncVersion with server's current syncVersion
  3. IF client.syncVersion == server.syncVersion:
       → No conflict, apply change
  4. IF client.syncVersion < server.syncVersion:
       → CONFLICT detected
       → Apply resolution strategy based on entity type
       → Log to SyncConflicts table
  5. IF entity not found and operation is Update:
       → Entity was deleted on server
       → Log as Delete-Update conflict
```

### 9.3 Entity-Specific Rules

```
REFERENCE DATA (Product, Customer, Supplier, Category, Unit, Warehouse):
  → Mobile can create/update drafts
  → Conflicts: Server-wins for updates, client retries with server version
  → Deletes: Server-only

FINANCIAL TRANSACTIONS (SalesInvoice, CashReceipt, CashPayment):
  → Mobile creates as DRAFT only
  → Server handles posting workflow
  → Conflicts on draft: Last-writer-wins
  → Posted records: Immutable, no conflict possible

INVENTORY (WarehouseProduct stock levels):
  → READ-ONLY on mobile
  → Server computes definitive stock
  → No write conflicts possible

ACCOUNTING (JournalEntry, Account balances):
  → READ-ONLY on mobile
  → Server-only posting
  → No write conflicts possible
```

---

## 10. IDEMPOTENT OPERATIONS DESIGN

### 10.1 Server-Side Idempotency

**Every mutating API call must support `X-Idempotency-Key` header:**

```csharp
// New: IdempotencyMiddleware.cs
public class IdempotencyMiddleware
{
    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store)
    {
        // Only for mutating methods
        if (context.Request.Method is not ("POST" or "PUT" or "DELETE"))
        {
            await _next(context);
            return;
        }
        
        var idempotencyKey = context.Request.Headers["X-Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            await _next(context); // Backward compatible
            return;
        }
        
        // Check if we already processed this key
        var cached = await store.GetAsync(idempotencyKey);
        if (cached != null)
        {
            // Return cached response
            context.Response.StatusCode = cached.HttpStatusCode;
            context.Response.Headers["X-Idempotency-Replayed"] = "true";
            await context.Response.WriteAsync(cached.ResponsePayload);
            return;
        }
        
        // Execute and cache
        // ... capture response, store in IdempotencyKeys table with 7-day TTL
    }
}
```

### 10.2 Client-Side Idempotency

```dart
class SyncQueue {
  /// Every operation gets a UUID idempotency key at creation time
  /// This key is sent with every retry attempt
  /// Server deduplicates using this key
  
  Future<String> enqueue({
    required String entityType,
    required String operationType,
    required Map<String, dynamic> data,
  }) {
    final idempotencyKey = const Uuid().v4();
    // Store in local SQLite queue
    // Same key used for ALL retries of this operation
    return idempotencyKey;
  }
}
```

### 10.3 Natural Idempotency Guards (Already Existing)

| Entity | Natural Guard | How It Works |
|--------|--------------|-------------|
| SalesInvoice | `InvoiceNumber` unique constraint | Duplicate number rejected |
| Product | `Code` unique index | Duplicate code rejected |
| Customer | `Code` unique index | Duplicate code rejected |
| JournalEntry | `ReferenceNumber` + `FiscalPeriodId` | Duplicate ref in period rejected |
| CashReceipt | `ReceiptNumber` unique constraint | Duplicate number rejected |

---

## 11. NETWORK DETECTION & RESILIENCE

### 11.1 Mobile Network Monitor

```dart
// New: network_monitor.dart
class NetworkMonitor extends ChangeNotifier {
  ConnectivityResult _status = ConnectivityResult.none;
  bool _isApiReachable = false;  // Separate from connectivity
  
  bool get isOnline => _status != ConnectivityResult.none && _isApiReachable;
  bool get isOffline => !isOnline;
  
  void start() {
    // Layer 1: OS-level connectivity
    Connectivity().onConnectivityChanged.listen((result) {
      _status = result;
      if (result != ConnectivityResult.none) {
        _checkApiReachability();
      } else {
        _isApiReachable = false;
        notifyListeners();
      }
    });
    
    // Layer 2: Periodic API health check (every 30 seconds when "online")
    Timer.periodic(Duration(seconds: 30), (_) {
      if (_status != ConnectivityResult.none) {
        _checkApiReachability();
      }
    });
  }
  
  Future<void> _checkApiReachability() async {
    try {
      final response = await _dio.get('/health/ping',
        options: Options(sendTimeout: Duration(seconds: 3),
                         receiveTimeout: Duration(seconds: 3)));
      _isApiReachable = response.statusCode == 200;
    } catch (_) {
      _isApiReachable = false;
    }
    notifyListeners();
  }
}
```

### 11.2 Retry Strategy with Exponential Backoff

```dart
class RetryPolicy {
  static const maxRetries = 5;
  static const baseDelayMs = 1000;  // 1 second
  
  // Delays: 1s, 2s, 4s, 8s, 16s (with jitter)
  static Duration getDelay(int attempt) {
    final exponential = baseDelayMs * pow(2, attempt);
    final jitter = Random().nextInt(500); // 0-500ms jitter
    return Duration(milliseconds: exponential + jitter);
  }
  
  // Only retry on transient errors
  static bool shouldRetry(DioException error) {
    return error.type == DioExceptionType.connectionTimeout
        || error.type == DioExceptionType.sendTimeout
        || error.type == DioExceptionType.receiveTimeout
        || error.type == DioExceptionType.connectionError
        || (error.response?.statusCode != null && 
            error.response!.statusCode! >= 500);
  }
  
  // Never retry on these (permanent failures)
  static bool isPermanentError(DioException error) {
    final status = error.response?.statusCode;
    return status == 400  // Bad Request (validation)
        || status == 401  // Unauthorized
        || status == 403  // Forbidden
        || status == 404  // Not Found
        || status == 409; // Conflict (handle separately)
  }
}
```

### 11.3 Circuit Breaker

```dart
class CircuitBreaker {
  int _failureCount = 0;
  DateTime? _openedAt;
  
  static const failureThreshold = 5;       // Open after 5 consecutive failures
  static const resetTimeoutSeconds = 60;   // Half-open after 60 seconds
  
  CircuitState get state {
    if (_failureCount < failureThreshold) return CircuitState.closed;
    if (_openedAt != null && 
        DateTime.now().difference(_openedAt!).inSeconds > resetTimeoutSeconds) {
      return CircuitState.halfOpen;
    }
    return CircuitState.open;
  }
  
  bool get allowRequest => state != CircuitState.open;
  
  void recordSuccess() {
    _failureCount = 0;
    _openedAt = null;
  }
  
  void recordFailure() {
    _failureCount++;
    if (_failureCount >= failureThreshold) {
      _openedAt = DateTime.now();
    }
  }
}
```

---

## 12. TRANSACTION SAFETY DESIGN

### 12.1 Server-Side Transaction Safety (Already Strong)

| Mechanism | Status | Coverage |
|-----------|--------|----------|
| IUnitOfWork.ExecuteInTransactionAsync | ✅ Active | All multi-step operations |
| IsolationLevel.ReadCommitted | ✅ Active | Default for all transactions |
| RowVersion concurrency | ✅ Active | All entities |
| ConcurrencyConflictException handling | ✅ Active | Caught in all services |
| DuplicateRecordException handling | ✅ Active | Caught in all services |
| AuditSaveChangesInterceptor | ✅ Active | Audit in same transaction |

### 12.2 Sync-Specific Transaction Patterns

**Push Transaction Atomicity:**
```
For each batch push:
  BEGIN TRANSACTION
    FOR EACH operation in batch:
      1. Check IdempotencyKey → skip if already processed
      2. Check SyncVersion conflict → log conflict if detected
      3. Execute operation via existing service
      4. Update SyncVersion on affected entity
      5. Store IdempotencyKey response
    END FOR
    
    Update SyncDevice.LastSyncVersion
    Update SyncDevice.LastSyncUtc
    Create SyncLog record
  COMMIT TRANSACTION
```

**Important: Each operation in a push batch is independent.**  
If operation 1 succeeds and operation 2 fails, operation 1 stays committed.  
The sync response tells the client which operations succeeded/failed.

### 12.3 Financial Record Safety

```
RULE: Posted financial records are NEVER modified by sync

SalesInvoice(Posted) → READ-ONLY on mobile, no push allowed
JournalEntry(Posted) → READ-ONLY on mobile, no push allowed  
CashReceipt(Posted)  → READ-ONLY on mobile, no push allowed

ONLY DRAFTS can be created/updated via sync push:
SalesInvoice(Draft)  → Create/Update via sync ✅
CashReceipt(Draft)   → Create via sync ✅
CashPayment(Draft)   → Create via sync ✅

Posting workflow MUST happen on server:
Mobile creates draft → syncs to server → user posts on desktop/web
```

---

## 13. IMPLEMENTATION ROADMAP

### Phase 1: Foundation (Server-Side Sync Infrastructure)
**Estimated Complexity: HIGH**

| Task | Layer | Files |
|------|-------|-------|
| Add `SyncVersion` to SoftDeletableEntity | Domain | BaseEntity.cs or SoftDeletableEntity.cs |
| Create SyncDevice, SyncLog, SyncConflict, IdempotencyKeys entities | Domain | New entity files |
| Create EF migration for SyncVersion + new tables | Persistence | New migration |
| Create SyncVersionInterceptor (auto-increment on save) | Persistence | New interceptor |
| Add delta query methods to repositories | Persistence | Existing repos |
| Create ISyncService interface | Application | New interface |
| Create SyncOrchestrator service | Application | New service |
| Create IdempotencyMiddleware | API | New middleware |
| Create SyncController | API | New controller |

### Phase 2: Mobile Offline Foundation
**Estimated Complexity: HIGH**

| Task | Layer | Files |
|------|-------|-------|
| Add drift/sqflite dependency | Mobile | pubspec.yaml |
| Create local SQLite schema (mirror server tables) | Mobile | New database/ folder |
| Create SyncQueue table | Mobile | New table |
| Implement NetworkMonitor | Mobile | New provider |
| Implement LocalRepository pattern | Mobile | New repository layer |
| Refactor screens to use local data first | Mobile | All 27 screens |

### Phase 3: Sync Engine
**Estimated Complexity: VERY HIGH**

| Task | Layer | Files |
|------|-------|-------|
| Implement SyncEngine state machine | Mobile | New sync/ folder |
| Implement pull logic (delta download) | Mobile | SyncEngine |
| Implement push logic (queue upload) | Mobile | SyncEngine |
| Implement conflict detection UI | Mobile | New conflict screen |
| Implement retry with exponential backoff | Mobile | RetryPolicy |
| Implement circuit breaker | Mobile | CircuitBreaker |

### Phase 4: Token Refresh & Error Classification
**Estimated Complexity: MEDIUM**

| Task | Layer | Files |
|------|-------|-------|
| Implement JWT token refresh endpoint | API | AuthController |
| Implement token refresh interceptor | Mobile | api_client.dart |
| Add ServiceErrorType classification | Application | ServiceResult.cs |
| Classify all service errors | Application | All 45+ services |

### Phase 5: Testing & Hardening
**Estimated Complexity: MEDIUM**

| Task | Layer | Files |
|------|-------|-------|
| Integration tests for sync endpoints | Tests | New test files |
| Offline scenario tests (mobile) | Mobile | Test folder |
| Conflict resolution tests | Tests | New test files |
| Load testing for concurrent sync sessions | Tests | Performance tests |
| Idempotency key cleanup job | API | Background service |

---

## 14. ARCHITECTURE DIAGRAMS

### 14.1 Complete System Architecture (Target State)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                            MOBILE DEVICE                                     │
│                                                                              │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │                      PRESENTATION LAYER                              │   │
│  │  ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐       │   │
│  │  │ Dashboard  │ │  Products  │ │   Sales    │ │  Treasury  │ ...   │   │
│  │  └──────┬─────┘ └──────┬─────┘ └──────┬─────┘ └──────┬─────┘       │   │
│  │         │               │               │               │            │   │
│  │         └───────────────┴───────┬───────┴───────────────┘            │   │
│  │                                 │                                    │   │
│  │         ┌───────────────────────▼───────────────────────┐            │   │
│  │         │            STATE MANAGEMENT                    │            │   │
│  │         │  ┌─────────────┐  ┌──────────┐  ┌──────────┐ │            │   │
│  │         │  │AuthProvider │  │SyncState │  │NetworkMon│ │            │   │
│  │         │  └─────────────┘  └──────────┘  └──────────┘ │            │   │
│  │         └───────────────────────┬───────────────────────┘            │   │
│  └─────────────────────────────────┼────────────────────────────────────┘   │
│                                    │                                         │
│  ┌─────────────────────────────────▼────────────────────────────────────┐   │
│  │                     LOCAL REPOSITORY LAYER                           │   │
│  │                                                                      │   │
│  │  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐    │   │
│  │  │ ProductLocalRepo │ │CustomerLocalRepo │ │InvoiceLocalRepo │    │   │
│  │  └─────────┬────────┘ └─────────┬────────┘ └─────────┬────────┘    │   │
│  │            │                     │                     │             │   │
│  │  ┌─────────▼─────────────────────▼─────────────────────▼──────────┐ │   │
│  │  │                    SQLite DATABASE (drift)                      │ │   │
│  │  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────────┐ │ │   │
│  │  │  │ Products │ │Customers │ │ Invoices │ │   SyncQueue      │ │ │   │
│  │  │  │ (cached) │ │ (cached) │ │ (local)  │ │ (pending ops)    │ │ │   │
│  │  │  └──────────┘ └──────────┘ └──────────┘ └──────────────────┘ │ │   │
│  │  └────────────────────────────────────────────────────────────────┘ │   │
│  └─────────────────────────────────┬────────────────────────────────────┘   │
│                                    │                                         │
│  ┌─────────────────────────────────▼────────────────────────────────────┐   │
│  │                        SYNC ENGINE                                   │   │
│  │                                                                      │   │
│  │  ┌──────────┐  ┌───────────┐  ┌──────────┐  ┌──────────────────┐  │   │
│  │  │ Puller   │  │ Pusher    │  │ Conflict │  │ Retry +          │  │   │
│  │  │ (Delta)  │  │ (Queue)   │  │ Resolver │  │ CircuitBreaker   │  │   │
│  │  └──────────┘  └───────────┘  └──────────┘  └──────────────────┘  │   │
│  └─────────────────────────────────┬────────────────────────────────────┘   │
│                                    │                                         │
│                              HTTP/HTTPS                                      │
└────────────────────────────────────┼─────────────────────────────────────────┘
                                     │
                                     │  Internet / LAN
                                     │
┌────────────────────────────────────▼─────────────────────────────────────────┐
│                            SERVER (ASP.NET Core)                             │
│                                                                              │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │                     MIDDLEWARE PIPELINE                               │   │
│  │  ExceptionHandler → IdempotencyMiddleware → CORS → JWT Auth → Route │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                    │                                         │
│  ┌─────────────────────────────────▼────────────────────────────────────┐   │
│  │                      API CONTROLLERS                                 │   │
│  │  ┌────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ │   │
│  │  │  Sync      │ │Products     │ │SalesInvoices │ │CashReceipts  │ │   │
│  │  │ Controller │ │Controller   │ │Controller    │ │Controller    │ │   │
│  │  └────────────┘ └──────────────┘ └──────────────┘ └──────────────┘ │   │
│  └─────────────────────────────────┬────────────────────────────────────┘   │
│                                    │                                         │
│  ┌─────────────────────────────────▼────────────────────────────────────┐   │
│  │                    APPLICATION SERVICES                              │   │
│  │  ┌────────────────┐ ┌──────────────────┐ ┌──────────────────────┐  │   │
│  │  │SyncOrchestrator│ │Existing 45+      │ │IdempotencyGuard     │  │   │
│  │  │(Pull/Push/     │ │Services          │ │(Dedup requests)     │  │   │
│  │  │ Conflict)      │ │(Unchanged)       │ │                     │  │   │
│  │  └────────────────┘ └──────────────────┘ └──────────────────────┘  │   │
│  └─────────────────────────────────┬────────────────────────────────────┘   │
│                                    │                                         │
│  ┌─────────────────────────────────▼────────────────────────────────────┐   │
│  │                      PERSISTENCE (EF Core)                           │   │
│  │  ┌────────────────────────────────────────────────────────────────┐  │   │
│  │  │ MarcoDbContext (170+ DbSets)                                   │  │   │
│  │  │ + SyncVersionInterceptor (auto-increment SyncVersion)          │  │   │
│  │  │ + AuditSaveChangesInterceptor                                  │  │   │
│  │  │ + HardDeleteProtectionInterceptor                              │  │   │
│  │  └────────────────────────────────────────────────────────────────┘  │   │
│  └─────────────────────────────────┬────────────────────────────────────┘   │
│                                    │                                         │
│  ┌─────────────────────────────────▼────────────────────────────────────┐   │
│  │                     SQL SERVER DATABASE                              │   │
│  │  ┌───────────────┐ ┌──────────────┐ ┌──────────────────────────┐   │   │
│  │  │ Business      │ │ Sync Tables  │ │ Idempotency Store        │   │   │
│  │  │ Tables (50+)  │ │ SyncDevices  │ │ IdempotencyKeys          │   │   │
│  │  │ +SyncVersion  │ │ SyncLog      │ │                          │   │   │
│  │  │               │ │ SyncConflicts│ │                          │   │   │
│  │  └───────────────┘ └──────────────┘ └──────────────────────────┘   │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 14.2 Sync Data Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                     SYNC PULL FLOW                               │
│                                                                  │
│  Mobile                          Server                          │
│    │                               │                             │
│    │── GET /sync/pull?since=N ────►│                             │
│    │                               │── Query WHERE SyncVer > N   │
│    │                               │── Combine all entity changes│
│    │◄── JSON { changes, version } ─│                             │
│    │                               │                             │
│    │── Apply to local SQLite       │                             │
│    │── Update lastSyncVersion = M  │                             │
│    │                               │                             │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                     SYNC PUSH FLOW                               │
│                                                                  │
│  Mobile                          Server                          │
│    │                               │                             │
│    │── Read SyncQueue (pending) ──►│                             │
│    │                               │                             │
│    │── POST /sync/push ──────────►│                             │
│    │   { operations: [...] }       │                             │
│    │                               │── For each operation:       │
│    │                               │   ├─ Check IdempotencyKey   │
│    │                               │   ├─ Check SyncVersion      │
│    │                               │   ├─ Execute via service    │
│    │                               │   └─ Update SyncVersion     │
│    │                               │                             │
│    │◄── { results: [...] } ────────│                             │
│    │                               │                             │
│    │── For each result:            │                             │
│    │   ├─ Success: mark synced     │                             │
│    │   ├─ Conflict: show UI        │                             │
│    │   └─ Error: retry later       │                             │
│    │                               │                             │
└─────────────────────────────────────────────────────────────────┘
```

### 14.3 Conflict Resolution Flow

```
┌──────────────────────────────────────────────────────────────────┐
│                   CONFLICT RESOLUTION FLOW                        │
│                                                                   │
│  DETECT                     CLASSIFY                RESOLVE       │
│    │                           │                       │          │
│    ▼                           ▼                       ▼          │
│  Client.SyncVer             ┌──────────┐           ┌──────────┐  │
│  < Server.SyncVer  ──►     │Reference │ ──Auto──► │Server    │  │
│                              │Data      │           │Wins      │  │
│                              └──────────┘           └──────────┘  │
│                                                                   │
│                              ┌──────────┐           ┌──────────┐  │
│                        ──►  │Draft     │ ──Auto──► │Last-Write│  │
│                              │Document  │           │Wins      │  │
│                              └──────────┘           └──────────┘  │
│                                                                   │
│                              ┌──────────┐           ┌──────────┐  │
│                        ──►  │Financial │ ──Block─► │Reject    │  │
│                              │Posted    │           │Client    │  │
│                              └──────────┘           └──────────┘  │
│                                                                   │
│                              ┌──────────┐           ┌──────────┐  │
│                        ──►  │Unknown   │ ──Log───► │Manual    │  │
│                              │          │           │Review    │  │
│                              └──────────┘           └──────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

---

## APPENDIX A: CURRENT TECHNOLOGY STACK

| Component | Technology | Version |
|-----------|-----------|---------|
| Backend | ASP.NET Core (Minimal Hosting) | .NET 6+ |
| Database | SQL Server | 2022 |
| ORM | Entity Framework Core | 7+ |
| Mobile | Flutter/Dart | 3.x |
| HTTP Client | Dio | 5.4.1 |
| State Management | Provider | 6.1.1 |
| Auth | JWT Bearer Token | Custom |
| Validation | FluentValidation | 11+ |
| Secure Storage | flutter_secure_storage | 9.0.0 |

## APPENDIX B: NEW PACKAGES REQUIRED

### Server (NuGet)
- No new packages needed (pure implementation using existing EF Core + ASP.NET Core)

### Mobile (pub.dev)
| Package | Purpose | Version |
|---------|---------|---------|
| `drift` | SQLite ORM for local database | latest |
| `sqlite3_flutter_libs` | SQLite native bindings | latest |
| `connectivity_plus` | Network status monitoring | latest |
| `uuid` | Generate idempotency keys | latest |
| `workmanager` | Background sync scheduling | latest |

## APPENDIX C: AFFECTED FILES SUMMARY

### Server Files to CREATE
- `src/MarcoERP.Domain/Entities/Sync/SyncDevice.cs`
- `src/MarcoERP.Domain/Entities/Sync/SyncLog.cs`
- `src/MarcoERP.Domain/Entities/Sync/SyncConflict.cs`
- `src/MarcoERP.Domain/Entities/Sync/IdempotencyRecord.cs`
- `src/MarcoERP.Domain/Interfaces/ISyncDeviceRepository.cs`
- `src/MarcoERP.Domain/Interfaces/ISyncLogRepository.cs`
- `src/MarcoERP.Domain/Interfaces/IIdempotencyStore.cs`
- `src/MarcoERP.Application/Interfaces/ISyncService.cs`
- `src/MarcoERP.Application/Services/Sync/SyncOrchestrator.cs`
- `src/MarcoERP.Application/DTOs/Sync/SyncPullResponseDto.cs`
- `src/MarcoERP.Application/DTOs/Sync/SyncPushRequestDto.cs`
- `src/MarcoERP.Application/DTOs/Sync/SyncPushResultDto.cs`
- `src/MarcoERP.Persistence/Repositories/Sync/SyncDeviceRepository.cs`
- `src/MarcoERP.Persistence/Repositories/Sync/SyncLogRepository.cs`
- `src/MarcoERP.Persistence/Repositories/Sync/IdempotencyStore.cs`
- `src/MarcoERP.Persistence/Interceptors/SyncVersionInterceptor.cs`
- `src/MarcoERP.Persistence/Configurations/Sync/SyncDeviceConfiguration.cs`
- `src/MarcoERP.Persistence/Configurations/Sync/SyncLogConfiguration.cs`
- `src/MarcoERP.Persistence/Configurations/Sync/SyncConflictConfiguration.cs`
- `src/MarcoERP.Persistence/Configurations/Sync/IdempotencyRecordConfiguration.cs`
- `src/MarcoERP.API/Controllers/SyncController.cs`
- `src/MarcoERP.API/Middleware/IdempotencyMiddleware.cs`

### Server Files to MODIFY
- `src/MarcoERP.Domain/Entities/Common/SoftDeletableEntity.cs` — Add `SyncVersion`
- `src/MarcoERP.Persistence/MarcoDbContext.cs` — Add new DbSets, register interceptor
- `src/MarcoERP.API/Program.cs` — Register IdempotencyMiddleware, SyncService
- All 50+ repositories — Add `GetChangesSinceSyncVersionAsync()` method

### Mobile Files to CREATE
- `lib/core/database/app_database.dart` — drift database definition
- `lib/core/database/tables/*.dart` — Local table definitions
- `lib/core/database/daos/*.dart` — Data access objects
- `lib/core/sync/sync_engine.dart` — Main sync orchestrator
- `lib/core/sync/sync_puller.dart` — Delta download logic
- `lib/core/sync/sync_pusher.dart` — Queue upload logic
- `lib/core/sync/sync_queue.dart` — FIFO operation queue
- `lib/core/sync/conflict_resolver.dart` — Conflict handling
- `lib/core/network/network_monitor.dart` — Connectivity provider
- `lib/core/network/retry_policy.dart` — Exponential backoff
- `lib/core/network/circuit_breaker.dart` — Fault tolerance
- `lib/core/repositories/*.dart` — Local-first repository pattern
- `lib/widgets/sync_status_indicator.dart` — UI sync badge
- `lib/widgets/offline_banner.dart` — Offline mode banner
- `lib/features/sync/screens/sync_status_screen.dart` — Sync dashboard
- `lib/features/sync/screens/conflict_resolution_screen.dart` — Conflict UI

### Mobile Files to MODIFY
- `pubspec.yaml` — Add drift, connectivity_plus, uuid, workmanager
- `lib/main.dart` — Initialize database, add NetworkMonitor + SyncEngine providers
- `lib/core/api/api_client.dart` — Add retry interceptor, idempotency header
- `lib/core/auth/auth_provider.dart` — Add token refresh
- All 27 screens — Use local repository instead of direct API calls

---

**Report Generated:** March 6, 2026  
**Total Analysis Scope:** 250+ source files, 30,000+ LOC  
**Architecture Rating (Current → Target):** 3/10 → 9/10  
