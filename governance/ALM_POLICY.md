# MarcoERP – ALM Policy

**Application Lifecycle Management, CI/CD, Quality Gates, and Enforcement Framework**

---

## 1. Purpose

This document defines how MarcoERP code is built, validated, tested, merged, and released.

Governance defines the rules.
ALM enforces them.

No code reaches production without passing ALM gates.

---

## 2. ALM Principles

| Principle ID | Principle |
|--------------|----------|
| ALM-01 | Automation over manual discipline |
| ALM-02 | Build must fail on structural violations |
| ALM-03 | No feature is complete without tests |
| ALM-04 | No deployment without database safety verification |
| ALM-05 | Governance rules must be technically enforceable |
| ALM-06 | Code quality is measured, not assumed |

---

## 3. Continuous Integration (CI) Rules

> **Note:** The CI/CD pipeline described in this section is planned but not yet implemented. The rules below are requirements for the future pipeline implementation. Until then, these checks should be performed manually before merging.

Every commit to `develop` or `main` triggers automated validation.

### 3.1 Build Validation

Build MUST fail if:

- Circular dependency detected
- Domain references external layer
- Application references Persistence implementation
- Infrastructure references Application
- Missing XML documentation on public members
- Compilation warnings present
- Any test fails

---

### 3.2 Static Code Analysis Rules

The following thresholds are enforced:

| Rule | Limit |
|------|-------|
| Class length | 800 lines (hard fail) |
| Service class length | 500 lines (warning) |
| Method length | 50 lines |
| Cyclomatic complexity | 10 |
| Nesting depth | 3 |
| Public methods per class | 15 |

Violations block merge unless explicitly approved.

---

### 3.3 Architecture Enforcement

The MarcoERP solution comprises the following deployable layers:

- **MarcoERP.WpfUI** – Desktop client (WPF, .NET 8.0)
- **MarcoERP.API** – REST API layer (.NET 9.0) at `src/MarcoERP.API/`
- **MarcoERP.Application** – Business logic and service orchestration
- **MarcoERP.Domain** – Core entities, enums, and domain rules
- **MarcoERP.Persistence** – EF Core data access and migrations
- **MarcoERP.Infrastructure** – Cross-cutting infrastructure services
- **Mobile App** – Flutter/Dart client at `mobile/marco_erp/`

Automated checks verify:

- No business logic in WpfUI or API controllers
- No SQL outside Persistence
- No DateTime.Now usage
- No float/double in financial calculations
- All entities include RowVersion
- All financial entities include soft-delete

---

## 4. Test Enforcement Rules

| Rule ID | Rule |
|---------|------|
| TST-ALM-01 | Domain project must have unit tests before merge |
| TST-ALM-02 | Application services must have mocked repository tests |
| TST-ALM-03 | Posting workflows must have integration test coverage |
| TST-ALM-04 | Minimum coverage threshold: 70% Domain layer |
| TST-ALM-05 | Financial calculation changes require new tests |

Build fails if:

- Coverage drops below threshold
- New domain logic added without test

---

## 5. Branch Protection Rules

### Protected Branches:
- `main`
- `develop`

Direct commits to `main` are forbidden.

### Pull Request Requirements:
- CI must pass
- At least one review (self-review allowed in solo dev with checklist)
- Governance impact section completed

---

## 6. Database Migration Control

Before merging any migration:

- Migration reviewed
- No data-loss operation
- Down() method verified
- Backup confirmation documented
- Naming follows MIG-02 convention

Production deployment requires:

- Pre-deployment backup
- Post-migration verification
- Trial balance validation

---

## 7. Release Quality Gates

Before tagging a release:

- All tests pass
- No critical architecture violations
- No TODO markers in production code
- Version increment validated
- CHANGELOG updated
- Governance documents synchronized

---

## 8. Security Enforcement

Automated checks verify:

- No plain-text passwords
- No sensitive data in logs
- AuthorizationProxy (DispatchProxy pattern) used in Application services
- No UI-only authorization enforcement
- No exposed internal IDs in DTOs

---

## 9. Performance Safeguards

Before release:

- All foreign keys indexed
- No unbounded queries
- No lazy loading enabled
- Large dataset queries paginated
- Retry policy configured

---

## 10. Governance Synchronization

Each phase transition requires:

- Governance review
- Structural health check
- Version bump validation
- Risk assessment review

If governance changes:
- Patch version increment required
- Version history table updated

---

## 11. Deployment Policy

Deployment Steps:

1. Backup database
2. Validate migration scripts
3. Deploy binaries
4. Apply migration
5. Run smoke tests
6. Validate posting workflow
7. Validate trial balance

If failure:
- Immediate rollback to backup
- Root cause analysis
- Patch release required

---

## 12. Technical Debt Logging

All shortcuts must be logged in:

`governance/TECHNICAL_DEBT.md`

Debt must include:
- Description
- Risk level
- Planned resolution phase

Technical debt cannot be ignored silently.

---

## 13. Structural Health Monitoring

Quarterly health check:

- Layer validation
- Complexity report
- Dead code scan
- Unused dependency scan
- Governance compliance review

Red flags trigger immediate refactoring cycle.

---

## 14. Enforcement Authority

Violation of ALM rules is a development blocker.

If ALM and other governance conflict,
PROJECT_RULES.md takes precedence.

---

## Version History

| Version | Date       | Change Description |
|---------|------------|-------------------|
| 1.0     | 2026-02-09 | Initial ALM framework |
| 2.0     | 2026-03-06 | Updated AuthorizationGuard references to AuthorizationProxy (DispatchProxy pattern); added API layer (.NET 9.0) and mobile app (Flutter/Dart) to architecture; noted CI/CD pipeline as planned but not yet implemented |
