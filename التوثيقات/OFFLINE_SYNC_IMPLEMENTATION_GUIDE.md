# MarcoERP — Offline-First Sync Implementation Guide

## Overview

MarcoERP now supports **offline-first** operation across mobile (Flutter) and desktop (WPF) clients. Data is stored locally in SQLite on mobile devices, synced bidirectionally with the SQL Server backend when connectivity is available.

**Architecture:** Server-wins conflict resolution, delta-sync via monotonic `SyncVersion`, negative temp IDs for offline-created records.

---

## 1. Database Migration

### Prerequisites
- SQL Server instance: `.\SQL2022`
- Database: `MarcoERP`
- **Take a backup first:**
```sql
BACKUP DATABASE [MarcoERP] TO DISK = 'C:\Backups\MarcoERP_PreSync.bak' WITH FORMAT;
```

### Run the Migration
Execute the migration script against the database:
```powershell
sqlcmd -S .\SQL2022 -d MarcoERP -i migration_sync_infrastructure.sql -E
```

### What the Migration Does
| Part | Description | Tables Affected |
|------|-------------|----------------|
| 1 | Adds `SyncVersion BIGINT NOT NULL DEFAULT 0` column | 20 SoftDeletableEntity tables |
| 2 | Creates `IX_{Table}_SyncVersion` indexes | 20 tables |
| 3 | Creates `SyncDevices` table | New |
| 4 | Creates `SyncConflicts` table | New |
| 5 | Creates `IdempotencyRecords` table | New |

**Tables receiving SyncVersion:**
Products, Warehouses, InventoryAdjustments, Customers, SalesInvoices, SalesReturns, SalesQuotations, SalesRepresentatives, PriceLists, Suppliers, PurchaseInvoices, PurchaseReturns, PurchaseQuotations, Cashboxes, CashReceipts, CashPayments, CashTransfers, BankAccounts, Accounts, JournalEntries

---

## 2. Server-Side Components

### New Files Created

| File | Purpose |
|------|---------|
| `Domain/Entities/Common/SoftDeletableEntity.cs` | Added `long SyncVersion` property |
| `Domain/Entities/Sync/SyncDevice.cs` | Device registration entity |
| `Domain/Entities/Sync/SyncConflict.cs` | Conflict audit log entity |
| `Domain/Entities/Sync/IdempotencyRecord.cs` | Duplicate operation prevention |
| `Persistence/Configurations/SyncDeviceConfiguration.cs` | EF config |
| `Persistence/Configurations/SyncConflictConfiguration.cs` | EF config |
| `Persistence/Configurations/IdempotencyRecordConfiguration.cs` | EF config |
| `Persistence/Interceptors/SyncVersionInterceptor.cs` | Auto-increments SyncVersion on save |
| `Persistence/Services/Sync/SyncService.cs` | Pull/push sync logic (~350 lines) |
| `Application/Interfaces/Sync/ISyncService.cs` | Service interface |
| `Application/DTOs/Sync/SyncDtos.cs` | Request/response DTOs |
| `API/Controllers/SyncController.cs` | 4 REST endpoints |
| `API/Middleware/IdempotencyMiddleware.cs` | Replay cached responses for retry requests |

### API Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/api/sync/pull` | Pull changes since last SyncVersion |
| `POST` | `/api/sync/push` | Push offline changes to server |
| `POST` | `/api/sync/register-device` | Register a new sync device |
| `GET`  | `/api/sync/status` | Get sync status for current device |

All endpoints require JWT `Authorization: Bearer <token>`.

### How SyncVersion Works
1. `SyncVersionInterceptor` intercepts every `SaveChanges()` call
2. For each modified `SoftDeletableEntity`, it reads the current max SyncVersion from the database and increments by 1
3. Clients send their last known SyncVersion to `/api/sync/pull`
4. Server returns only entities where `SyncVersion > clientVersion`

### Sync Cycle (Server Perspective)
1. **Pull**: Client sends `{ lastSyncVersion, deviceId, entityTypes[] }` → Server returns changed entities as JSON
2. **Push**: Client sends `{ deviceId, changes: [{ entityType, tempId, data, operation }] }` → Server applies changes, returns ID mappings for newly created entities

### Conflict Resolution
- **Strategy**: Server-wins (last-write-wins on the server side)
- Conflicts are logged to `SyncConflicts` table for audit
- Client data is preserved in `ClientData` column, server state in `ServerData`

---

## 3. Mobile (Flutter) Components

### New Dependencies (`pubspec.yaml`)
```yaml
sqflite: ^2.3.2        # Local SQLite database
path: ^1.9.0           # Database file path resolution
connectivity_plus: ^6.0.3  # Network connectivity monitoring
uuid: ^4.3.3           # Device ID and idempotency key generation
```

### New Files Created

| File | Purpose |
|------|---------|
| `lib/core/database/local_database.dart` | SQLite schema (15 tables), CRUD helpers (~320 lines) |
| `lib/core/network/connectivity_service.dart` | Online/offline detection via connectivity_plus |
| `lib/core/sync/sync_engine.dart` | Sync orchestrator: pull/push, ID mapping, periodic sync (~350 lines) |
| `lib/core/providers/offline_data_provider.dart` | Generic offline CRUD with pending changes queue (~150 lines) |
| `lib/widgets/sync_status_widget.dart` | Cloud icon + pending count badge, offline banner |

### Local SQLite Tables (15)

**Entity tables (13):** products, categories, units, warehouses, customers, suppliers, sales_invoices, sales_invoice_lines, purchase_invoices, cashboxes, cash_receipts, cash_payments, bank_accounts

**System tables (2):** sync_metadata (key-value), pending_changes (offline change queue)

### How Offline Mode Works
1. All screens read from **local SQLite** via `OfflineDataProvider.getAll(table)`
2. Create/update/delete go to **local SQLite + pending_changes queue**
3. When online, `SyncEngine` pushes pending changes to server, then pulls new data
4. Offline-created records use **negative temp IDs** (decrementing from -1)
5. Server responds with real IDs → SyncEngine updates local DB ID mappings

### main.dart Initialization Flow
```
main() → LocalDatabase.open() → ConnectivityService.initialize()
       → MultiProvider (ApiClient, LocalDB, Connectivity, SyncEngine, OfflineDataProvider, AuthProvider)
       → After auth success: SyncEngine.initialize() → registerDevice → fullSync
```

### Screens Converted to Offline-First

| Screen | Read Source | Write Destination |
|--------|-------------|-------------------|
| Products list | `offlineData.getAll('products')` | — |
| Product detail/create | Local lookups | `offlineData.create/update('products')` |
| Customers list | `offlineData.getAll('customers')` | — |
| Customer detail/create | — | `offlineData.create/update('customers')` |
| Suppliers list | `offlineData.getAll('suppliers')` | — |
| Categories list | `offlineData.getAll('categories')` | — |
| Units list | `offlineData.getAll('units')` | — |
| Warehouses list | `offlineData.getAll('warehouses')` | — |
| Cashboxes list | `offlineData.getAll('cashboxes')` | — |
| Cash Receipts list | `offlineData.getAll('cash_receipts')` | — |
| Cash Receipt create | Customers + Cashboxes from local | `offlineData.create('cash_receipts')` |
| Cash Payments list | `offlineData.getAll('cash_payments')` | — |
| Cash Payment create | Suppliers + Cashboxes from local | `offlineData.create('cash_payments')` |
| Bank Accounts list | `offlineData.getAll('bank_accounts')` | — |
| Sales Invoices list | `offlineData.getAll('sales_invoices')` | — |
| Sales Invoice create | Customers + Warehouses + Products local | `offlineData.create('sales_invoices')` |
| Purchase Invoices list | `offlineData.getAll('purchase_invoices')` | — |
| Dashboard | SyncStatusWidget added | — |

### Screens Remaining on Direct API (No Local Table)
- Cash Transfers
- Sales Returns
- Sales Quotations
- Reports
- Settings (server URL config)
- Login

### Automatic Sync Triggers
1. **On connectivity restore**: Immediate full sync
2. **Periodic**: Every 5 minutes when online
3. **After offline write**: If online, immediate sync push

---

## 4. Testing Checklist

### Database
- [ ] Run `migration_sync_infrastructure.sql` on a test database first
- [ ] Verify all 20 tables have SyncVersion column: `SELECT * FROM sys.columns WHERE name = 'SyncVersion'`
- [ ] Verify 3 new tables exist: SyncDevices, SyncConflicts, IdempotencyRecords
- [ ] Verify all 20 SyncVersion indexes exist

### Server API
- [ ] Build: `dotnet build src/MarcoERP.API/MarcoERP.API.csproj`
- [ ] Start server and test: `POST /api/sync/register-device` with `{ deviceId, deviceName, deviceType }`
- [ ] Test pull: `POST /api/sync/pull` with `{ lastSyncVersion: 0, deviceId, entityTypes: ["Product"] }`
- [ ] Verify returned products include SyncVersion field
- [ ] Test push: `POST /api/sync/push` with a sample create change
- [ ] Verify idempotency: Repeat same push with same Idempotency-Key header → should get cached response

### Mobile App
- [ ] Run `flutter pub get` in `mobile/marco_erp/`
- [ ] Launch app, login → verify SyncEngine initializes (cloud icon appears)
- [ ] Navigate to Products → verify data loads from local DB
- [ ] Turn off WiFi → verify OfflineBanner appears ("وضع عدم الاتصال")
- [ ] Create a product offline → verify it appears in list with negative temp ID
- [ ] Restore WiFi → verify sync runs and temp ID is replaced with real server ID
- [ ] Create a cash receipt offline → verify pending changes badge shows count
- [ ] After sync → verify receipt has server-assigned receipt_number

---

## 5. Known Limitations

1. **Invoice lines**: When creating sales invoices offline, only the invoice header is stored locally. Invoice lines are included in the push payload but not individually queryable offline.
2. **Join fields**: List screens that showed related entity names (e.g., customer name on a receipt) show null for those fields locally because SQLite doesn't have the joins. Names appear after sync.
3. **No local tables for**: CashTransfers, SalesReturns, SalesQuotations, PurchaseReturns — these screens require connectivity.
4. **Conflict visibility**: Server-wins conflicts are logged in SyncConflicts table but not surfaced to the mobile UI.

---

## 6. File Inventory (All New/Modified Files)

### Server Side (C# / .NET)
```
src/MarcoERP.Domain/Entities/Common/SoftDeletableEntity.cs        [MODIFIED]
src/MarcoERP.Domain/Entities/Sync/SyncDevice.cs                   [NEW]
src/MarcoERP.Domain/Entities/Sync/SyncConflict.cs                 [NEW]
src/MarcoERP.Domain/Entities/Sync/IdempotencyRecord.cs            [NEW]
src/MarcoERP.Persistence/Configurations/SyncDeviceConfiguration.cs        [NEW]
src/MarcoERP.Persistence/Configurations/SyncConflictConfiguration.cs      [NEW]
src/MarcoERP.Persistence/Configurations/IdempotencyRecordConfiguration.cs [NEW]
src/MarcoERP.Persistence/Interceptors/SyncVersionInterceptor.cs   [NEW]
src/MarcoERP.Persistence/Services/Sync/SyncService.cs             [NEW]
src/MarcoERP.Persistence/MarcoDbContext.cs                        [MODIFIED]
src/MarcoERP.Application/Interfaces/Sync/ISyncService.cs          [NEW]
src/MarcoERP.Application/DTOs/Sync/SyncDtos.cs                    [NEW]
src/MarcoERP.API/Controllers/SyncController.cs                    [NEW]
src/MarcoERP.API/Middleware/IdempotencyMiddleware.cs               [NEW]
src/MarcoERP.API/Program.cs                                       [MODIFIED]
```

### Mobile Side (Flutter / Dart)
```
mobile/marco_erp/pubspec.yaml                                     [MODIFIED]
mobile/marco_erp/lib/main.dart                                    [REWRITTEN]
mobile/marco_erp/lib/core/database/local_database.dart            [NEW]
mobile/marco_erp/lib/core/network/connectivity_service.dart       [NEW]
mobile/marco_erp/lib/core/sync/sync_engine.dart                   [NEW]
mobile/marco_erp/lib/core/providers/offline_data_provider.dart    [NEW]
mobile/marco_erp/lib/core/constants/api_constants.dart            [MODIFIED]
mobile/marco_erp/lib/widgets/sync_status_widget.dart              [NEW]
mobile/marco_erp/lib/features/products/screens/products_screen.dart         [MODIFIED]
mobile/marco_erp/lib/features/products/screens/product_detail_screen.dart   [MODIFIED]
mobile/marco_erp/lib/features/customers/screens/customers_screen.dart       [MODIFIED]
mobile/marco_erp/lib/features/customers/screens/customer_detail_screen.dart [MODIFIED]
mobile/marco_erp/lib/features/suppliers/screens/suppliers_screen.dart       [MODIFIED]
mobile/marco_erp/lib/features/inventory/screens/categories_screen.dart      [MODIFIED]
mobile/marco_erp/lib/features/inventory/screens/units_screen.dart           [MODIFIED]
mobile/marco_erp/lib/features/inventory/screens/warehouses_screen.dart      [MODIFIED]
mobile/marco_erp/lib/features/treasury/screens/cashboxes_screen.dart        [MODIFIED]
mobile/marco_erp/lib/features/treasury/screens/cash_receipts_screen.dart    [MODIFIED]
mobile/marco_erp/lib/features/treasury/screens/cash_payments_screen.dart    [MODIFIED]
mobile/marco_erp/lib/features/treasury/screens/bank_accounts_screen.dart    [MODIFIED]
mobile/marco_erp/lib/features/treasury/screens/cash_receipt_create_screen.dart  [MODIFIED]
mobile/marco_erp/lib/features/treasury/screens/cash_payment_create_screen.dart  [MODIFIED]
mobile/marco_erp/lib/features/sales/screens/sales_invoices_screen.dart      [MODIFIED]
mobile/marco_erp/lib/features/sales/screens/sales_invoice_create_screen.dart[MODIFIED]
mobile/marco_erp/lib/features/purchases/screens/purchase_invoices_screen.dart[MODIFIED]
mobile/marco_erp/lib/features/dashboard/screens/dashboard_screen.dart       [MODIFIED]
```

### Migration
```
migration_sync_infrastructure.sql                                 [NEW]
```
