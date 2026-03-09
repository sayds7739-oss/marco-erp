# MarcoERP – Versioning Policy

**Version Numbering, Release Policy, and Change Tracking**

---

## 1. Version Numbering Scheme

MarcoERP uses **Semantic Versioning (SemVer)** with phase awareness:

```
{Major}.{Minor}.{Patch}-{Phase}
```

| Segment  | Meaning                                                      | Example    |
|----------|--------------------------------------------------------------|------------|
| Major    | Breaking changes, major architecture shifts                  | `2.0.0`    |
| Minor    | New features, new modules, non-breaking additions            | `1.3.0`    |
| Patch    | Bug fixes, minor corrections, documentation updates          | `1.3.2`    |
| Phase    | Development phase tag (for pre-release tracking)             | `1.0.0-P2` |

### Current Version

```
1.0.0-P8 (Phase 8: Production Hardening, Mobile & API Integration)
```

> The system has completed Phases 1 through 8, encompassing full double-entry
> accounting, sales, purchases, inventory, treasury, POS, security,
> governance, reporting, API layer (.NET 9.0), mobile app (Flutter/Dart),
> opening balances, year-end closing, data purge, recycle bin, and print
> center (ESC/POS).

---

## 2. Phase Versioning

| Phase | Version Range | Description                                          | Status      |
|-------|---------------|------------------------------------------------------|-------------|
| P1    | `0.1.x`       | Foundation & Governance                              | Completed   |
| P2    | `0.2.x`       | Core Accounting Engine (Chart of Accounts, Journals, Fiscal Years/Periods) | Completed   |
| P3    | `0.3.x`       | Inventory & Warehousing (Products, Categories, Units, Warehouses, Adjustments) | Completed   |
| P4    | `0.4.x`       | Sales & Purchasing (Invoices, Quotations, Returns, Suppliers, Customers, Representatives, Price Lists) | Completed   |
| P5    | `0.5.x`       | Treasury (Cashboxes, Bank Accounts, Receipts, Payments, Transfers, Bank Reconciliation) | Completed   |
| P6    | `0.6.x`       | Reporting, Dashboards & POS (Reports, Export, POS Sessions/Sales/Reports) | Completed   |
| P7    | `0.7.x`       | Security, Settings & Governance (Users, Roles, AuthorizationProxy, Feature Governance, Profiles, Integrity, Audit Log, Backup, Recycle Bin, Data Purge) | Completed   |
| P8    | `0.8.x`–`1.0.0` | API Layer (.NET 9.0), Mobile App (Flutter/Dart), Opening Balances, Year-End Closing, Print Center (ESC/POS), Production Hardening | In Progress |
| GA    | `1.0.0`       | General Availability (Production)                    | Approaching |

---

## 3. Version Increment Rules

| Change Type                         | Version Increment | Example               |
|-------------------------------------|-------------------|-----------------------|
| Governance document update          | Patch             | `0.1.0` -> `0.1.1`   |
| New entity or table                 | Minor             | `0.2.0` -> `0.2.1`   |
| New module (Inventory, Sales)       | Minor             | `0.2.x` -> `0.3.0`   |
| Bug fix                             | Patch             | `0.2.1` -> `0.2.2`   |
| Architecture change                 | Major             | `0.x.x` -> `1.0.0`   |
| Breaking API change                 | Major             | `1.x.x` -> `2.0.0`   |
| New feature within existing module  | Minor             | `0.2.3` -> `0.2.4`   |

---

## 4. Governance Document Versioning

Each governance document has its own internal version table:

```markdown
## Version History

| Version | Date       | Change Description                    |
|---------|------------|---------------------------------------|
| 1.0     | 2026-02-08 | Initial Phase 1 governance release    |
```

### Rules for Governance Changes

| Rule ID | Rule                                                                     |
|---------|--------------------------------------------------------------------------|
| GOV-01  | Every governance change includes a version history entry.                |
| GOV-02  | Changes require explicit justification in version history.               |
| GOV-03  | Governance changes increment the system Patch version.                   |
| GOV-04  | Major governance changes (architecture shifts) require Major increment.  |
| GOV-05  | All governance documents are reviewed at each phase transition.          |

---

## 5. Release Process

### 5.1 Development Releases

| Step | Action                                                                  |
|------|-------------------------------------------------------------------------|
| 1    | Development on feature branch                                           |
| 2    | All tests pass                                                          |
| 3    | Code review (or self-review with governance checklist)                  |
| 4    | Merge to main branch                                                    |
| 5    | Version tag applied                                                     |

### 5.2 Phase Releases

| Step | Action                                                                  |
|------|-------------------------------------------------------------------------|
| 1    | All features for the phase are complete                                 |
| 2    | All governance documents updated for the phase                          |
| 3    | Full regression test suite passes                                       |
| 4    | Phase release notes written                                             |
| 5    | Version tagged with phase marker (e.g., `v0.8.0-P8`)                   |
| 6    | Governance review for next phase                                        |

### 5.3 Production Releases

| Step | Action                                                                  |
|------|-------------------------------------------------------------------------|
| 1    | All phases complete                                                     |
| 2    | Full system test in staging environment                                 |
| 3    | Database backup before deployment                                       |
| 4    | Migration script reviewed                                               |
| 5    | Deployment to production                                                |
| 6    | Post-deployment verification                                            |
| 7    | Version tagged as `v1.0.0`                                              |

---

## 6. Changelog Format

A `CHANGELOG.md` file is maintained at the repository root:

```markdown
# Changelog

## [1.0.0-P8] - 2026-03-06

### Added
- API layer (MarcoERP.API, .NET 9.0) with Controllers and Middleware
- Mobile app scaffolding (Flutter/Dart)
- Opening balances module (OpeningBalance, OpeningBalanceLine entities)
- Year-end closing service
- Print center with ESC/POS thermal printer support
- Data purge service
- Recycle bin service
- AuthorizationProxy for permission enforcement
- Comprehensive database hardening migrations (Phase 2/3/7)
- Cashbox balance constraints
- Invoice header discount and delivery fields
- Filtered unique invoice number indexes
- Counterparty and SalesRep fields on invoices/returns
- Negative stock and receipt settings
- Sync infrastructure tables (SyncDevice, SyncConflict, IdempotencyRecord)

### Changed
- Upgraded all modules to production-hardened state
- Expanded governance console with integrity checks and impact analysis

### Fixed
- Comprehensive audit fixes (2026-03)
- Money precision in journal entries
- Cascade delete violations removed
- Duplicate invoice number bug resolved

## [0.7.0-P7] - 2026-02-14

### Added
- Feature governance system (feature flags, profiles, impact analysis)
- Company isolation (multi-tenant data segregation)
- Version integrity engine
- Migration execution engine
- Audit log with change tracking columns
- Invoice line soft-delete and audit columns
- User account locking (LockedAt column)

## [0.6.0-P6] - 2026-02-12

### Added
- Bank account management
- Bank reconciliation module
- POS module (sessions, sales, reports)
- Dashboard and reporting services
- Report export service
- Sync missing columns migration

## [0.5.0-P5] - 2026-02-10

### Added
- Treasury module (cashboxes, cash receipts, cash payments, cash transfers)
- Treasury-invoice integration links
- Price lists and inventory adjustments
- Credit control fields
- Quotations module (sales and purchase quotations)

## [0.4.0-P4] - 2026-02-09

### Added
- Sales module (invoices, returns, representatives)
- Purchases module (invoices, returns)
- Customers and suppliers entities
- Security and settings modules (users, roles, system settings)
- Journal balance check and cascade delete restrictions
- Journal entry money precision fix

## [0.2.0-P2] - 2026-02-08

### Added
- Core accounting engine (accounts, journal entries, fiscal years)
- Inventory module (products, categories, units, warehouses)

## [0.1.0-P1] - 2026-02-08

### Added
- Foundation & Governance documents
- Solution structure definition
- Architecture contract
```

### Changelog Rules

| Rule ID | Rule                                                                     |
|---------|--------------------------------------------------------------------------|
| CHL-01  | Every version increment updates the CHANGELOG.md.                        |
| CHL-02  | Entries grouped by: Added, Changed, Fixed, Removed.                      |
| CHL-03  | Entries written in past tense, concise, factual.                         |
| CHL-04  | Reference related governance docs or issue numbers.                      |

---

## 7. Git Tag Convention

```
v{Major}.{Minor}.{Patch}[-{Phase}]
```

Examples:
- `v0.1.0-P1` — Phase 1 initial release (Foundation)
- `v0.2.0-P2` — Phase 2 initial release (Accounting)
- `v0.4.0-P4` — Phase 4 initial release (Sales & Purchasing)
- `v0.7.0-P7` — Phase 7 initial release (Security & Governance)
- `v1.0.0-P8` — Phase 8 release (API, Mobile, Production Hardening)
- `v1.0.0` — General availability

---

## 8. Branch Strategy

| Branch Pattern                    | Purpose                              |
|-----------------------------------|--------------------------------------|
| `main`                            | Stable, released code                |
| `develop`                         | Integration branch for current phase |
| `feature/{module}/{description}`  | New feature development              |
| `fix/{description}`               | Bug fix                              |
| `governance/{document}`           | Governance document changes          |

---

## 9. Compatibility Policy

| Rule ID | Rule                                                                     |
|---------|--------------------------------------------------------------------------|
| CMP-01  | Database migrations must be forward-compatible within a phase.           |
| CMP-02  | No breaking schema changes mid-phase without migration path.             |
| CMP-03  | Application settings format must be backward-compatible.                 |
| CMP-04  | API endpoints follow versioned URL patterns (`/api/v1/`).               |

---

## Version History

| Version | Date       | Change Description                                                              |
|---------|------------|---------------------------------------------------------------------------------|
| 1.0     | 2026-02-08 | Initial Phase 1 governance release                                              |
| 2.0     | 2026-03-06 | Major update: reflect Phases 1-8 completion, add phase status table, update changelog with full history, update current version to 1.0.0-P8 |
