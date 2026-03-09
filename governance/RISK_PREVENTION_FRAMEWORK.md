# MarcoERP – Risk Prevention Framework

**Deliverable 6: Structural Failure Prevention and Long-Term Maintainability**

---

## 1. Purpose

ERP systems are among the most complex software projects. The majority of ERP projects fail — not because the technology is wrong, but because the architecture erodes over time. This document identifies how ERP systems fail structurally and defines preventive measures to protect MarcoERP from each failure mode.

---

## 2. How ERP Systems Fail Structurally

### 2.1 The Seven Deadly Sins of ERP Architecture

| #  | Failure Mode              | Description                                              | Severity  |
|----|---------------------------|----------------------------------------------------------|-----------|
| 1  | **Layer Erosion**         | Business logic leaks into UI or database                 | Critical  |
| 2  | **Tight Coupling**        | Components cannot change independently                   | Critical  |
| 3  | **Spaghetti Logic**       | Business rules scattered across unrelated locations      | Critical  |
| 4  | **God Object Syndrome**   | Single classes/windows doing too many things             | High      |
| 5  | **Data Corruption**       | Inconsistent financial records due to broken invariants  | Critical  |
| 6  | **Configuration Drift**   | Settings become inconsistent across environments         | Medium    |
| 7  | **Knowledge Decay**       | System understanding fades as team changes               | High      |

---

### 2.2 Detailed Failure Analysis

#### Failure 1: Layer Erosion

**How it happens:**
- Developer adds "just a small calculation" in the UI for speed
- Repository starts validating business rules "for safety"
- Windows start querying the database directly "just this once"

**How it manifests:**
- Same business rule duplicated in 3+ places
- Bug fix required in multiple files for one logical change
- Tests become impossible without launching the full UI

**In MarcoERP, this is prevented by:**
- Strict ARCHITECTURE.md contract with forbidden access lists
- Project reference restrictions enforced at solution level
- Agent pre-action checklist validates layer placement
- Code review checklist includes layer boundary verification

---

#### Failure 2: Tight Coupling

**How it happens:**
- Services reference concrete classes instead of interfaces
- Windows create their own service instances
- Domain entities depend on infrastructure (database, logging)
- Changing one module requires changing five others

**How it manifests:**
- Cannot replace a database provider without rewriting business logic
- Cannot test business logic without a running database
- Changes cascade unpredictably through the system

**In MarcoERP, this is prevented by:**
- Dependency Inversion: interfaces in Domain, implementations in Persistence/Infrastructure
- Dependency Injection as the only cross-layer communication mechanism
- Composition Root as the single wiring point (App.xaml.cs)
- Domain layer has ZERO external dependencies

---

#### Failure 3: Spaghetti Logic

**How it happens:**
- Posting validation in 4 different services
- VAT calculation in UI, Application, and a stored procedure
- Period lock check in some services but not others
- "I found this check elsewhere so I added it here too"

**How it manifests:**
- Contradictory validation results depending on code path
- Bugs where rule works in one window but not another
- No single source of truth for any business rule

**In MarcoERP, this is prevented by:**
- Single Responsibility: each business rule has ONE home
- Domain Rules folder for cross-entity business rules
- Validators in Application layer as the sole validation orchestrator
- Rule location mapping in ACCOUNTING_PRINCIPLES.md

---

#### Failure 4: God Object Syndrome

**How it happens:**
- "One window to rule them all" — the invoice window does everything
- Service class grows to 3000 lines because "it's all related"
- Entity holds logic for creation, validation, posting, reversal, reporting

**How it manifests:**
- Files no one dares to modify
- Bugs impossible to isolate
- New developers need weeks to understand one class

**In MarcoERP, this is prevented by:**
- 500-line limit on any single class (PROJECT_RULES DEV-06)
- 800-line limit on windows including XAML (UI_GUIDELINES UIF-02)
- One responsibility per class, window, and service
- UserControls for complex window sections
- Separate service classes for distinct use cases

---

#### Failure 5: Data Corruption

**How it happens:**
- Transaction saved without balance check in one code path
- Posting bypassed for "admin import"
- Concurrency conflict overwrites another user's changes
- Inventory quantity goes negative silently

**How it manifests:**
- Trial balance doesn't balance
- Audit trail shows impossible transitions
- Financial reports produce wrong numbers
- Regulatory non-compliance

**In MarcoERP, this is prevented by:**
- Multi-layer balance verification (Domain, Application, Database constraint)
- Immutable posted transactions (RECORD_PROTECTION_POLICY)
- RowVersion concurrency tokens on all editable entities
- No bypass path for posting workflow
- Negative inventory prevention

---

#### Failure 6: Configuration Drift

**How it happens:**
- Developer changes a setting locally, forgets the server
- Connection string different between dev and production
- One environment has tax enabled, another doesn't
- "It works on my machine"

**How it manifests:**
- Production bugs that can't be reproduced in development
- Data differences between environments
- Deployment failures

**In MarcoERP, this is prevented by:**
- External configuration files (not hardcoded)
- Configuration validation at startup (fail-fast)
- Environment-specific settings files
- Deployment checklist includes configuration review
- IDateTimeProvider abstraction (no hidden time dependencies)

---

#### Failure 7: Knowledge Decay

**How it happens:**
- Original developer leaves, no documentation
- "It's obvious from the code" — it isn't
- Business rules live in people's heads
- Governance documents never written or never updated

**How it manifests:**
- New developer breaks things they don't understand
- Business rules silently change because nobody remembers the original
- System becomes unmaintainable

**In MarcoERP, this is prevented by:**
- Comprehensive governance documents (this very set of files)
- XML documentation on all public members
- Named constants and enums instead of magic values
- Self-documenting code with clear naming conventions
- Design documents required before implementation (DOC-02)
- Governance documents version-controlled alongside code

---

## 3. Prevention: Tight Coupling

### 3.1 Coupling Prevention Rules

| Rule ID | Rule                                                                     |
|---------|--------------------------------------------------------------------------|
| TCP-01  | No class may directly instantiate a class from another layer.            |
| TCP-02  | All cross-layer communication uses injected interfaces.                  |
| TCP-03  | DTOs are the ONLY objects that cross layer boundaries.                   |
| TCP-04  | Domain entities never leave the Application layer (mapped to DTOs).      |
| TCP-05  | Window code-behind references only Application-layer interfaces.         |
| TCP-06  | No static service locator patterns.                                      |
| TCP-07  | If removing a class requires changing 5+ files, it's too coupled.        |

### 3.2 Coupling Detection Checklist

Ask these questions during code review:

| #  | Question                                                                |
|----|-------------------------------------------------------------------------|
| 1  | Can this class be unit-tested without a database?                       |
| 2  | Can this class be unit-tested without launching a window?                |
| 3  | If I change the database schema, does this class need to change?         |
| 4  | If I change the UI framework, does this class need to change?            |
| 5  | Does this class know about classes it shouldn't know about?              |
| 6  | Is there a circular dependency between any two classes?                   |

If any answer is "wrong," refactor before proceeding.

---

## 4. Prevention: Spaghetti Logic

### 4.1 Rule Placement Guide

| Business Rule Category         | Home Location                      |
|--------------------------------|------------------------------------|
| Entity invariants              | Domain Entity (self-validation)    |
| Cross-entity validation        | Domain Rules folder                |
| Workflow orchestration          | Application Service                |
| Input format validation        | Application Validator              |
| Database constraint            | Persistence Configuration          |
| Display formatting             | WpfUI ViewModel/Helper             |
| External interaction           | Infrastructure Service             |

### 4.2 Anti-Spaghetti Rules

| Rule ID | Rule                                                                     |
|---------|--------------------------------------------------------------------------|
| ASP-01  | Every business rule has exactly ONE canonical location.                   |
| ASP-02  | If a rule is needed in multiple places, call the canonical location.     |
| ASP-03  | Never copy-paste a validation — extract and reference.                   |
| ASP-04  | If you can't find where a rule lives, the architecture has a problem.    |
| ASP-05  | New rules require documentation of where they live before coding.        |

---

## 5. Long-Term Maintainability Rules

### 5.1 Code Health Rules

| Rule ID | Rule                                                                     |
|---------|--------------------------------------------------------------------------|
| LTM-01  | No class exceeds 500 lines.                                             |
| LTM-02  | No method exceeds 50 lines.                                             |
| LTM-03  | No more than 5 parameters on any method (use parameter objects).        |
| LTM-04  | Cyclomatic complexity per method: max 10.                               |
| LTM-05  | Nesting depth: max 3 levels (if > 3, extract methods).                  |
| LTM-06  | Public API surface of any class: max 10 public methods.                 |

### 5.2 Naming as Documentation

| Rule ID | Rule                                                                     |
|---------|--------------------------------------------------------------------------|
| LTM-07  | Class names describe what they ARE: `PostingService`, not `Helper`.       |
| LTM-08  | Method names describe what they DO: `PostJournalEntry`, not `Process`.    |
| LTM-09  | Variable names describe what they HOLD: `totalDebit`, not `d`.           |
| LTM-10  | Boolean names are questions: `IsPosted`, `HasBalance`, `CanPost`.        |
| LTM-11  | No abbreviations unless universally understood (Id, VAT, AR, AP).        |

### 5.3 Change Safety Rules

| Rule ID | Rule                                                                     |
|---------|--------------------------------------------------------------------------|
| LTM-12  | Every change has at least one test.                                      |
| LTM-13  | Refactoring commits are separate from feature commits.                   |
| LTM-14  | No feature is "done" until tests pass and documentation is updated.     |
| LTM-15  | Technical debt must be logged and prioritized, not ignored.              |
| LTM-16  | Deprecated code is marked with `[Obsolete]` attribute, never silently removed. |

---

## 6. Structural Health Monitoring

### 6.1 Periodic Health Checks

Several of these checks are now automated via `GovernanceIntegrityCheckService` (in Persistence layer) and surfaced through `GovernanceConsoleViewModel` (in WpfUI). The table below defines the full set of required checks, including those already implemented and those still manual.

| Check                            | Frequency     | Owner       | Status |
|----------------------------------|---------------|-------------|--------|
| Layer boundary verification      | Per commit    | Developer   | Manual |
| Circular dependency check        | Per build     | Build system| Manual |
| Code complexity analysis         | Monthly       | Tech Lead   | Manual |
| Governance document review       | Per phase     | Team        | Manual |
| Test coverage review             | Per phase     | Tech Lead   | Manual |
| Unused code/dead code removal    | Quarterly     | Developer   | Manual |
| Structural integrity checks      | On demand     | GovernanceIntegrityCheckService | Automated |
| Governance console dashboard     | On demand     | GovernanceConsoleViewModel | Automated |

### 6.2 Red Flags That Require Immediate Action

| Red Flag                                                                 |
|--------------------------------------------------------------------------|
| A window file exceeds 1000 lines                                         |
| A service class exceeds 500 lines                                        |
| A method exceeds 50 lines                                                |
| Business logic found in WpfUI layer                                      |
| SQL found anywhere except Persistence layer                              |
| A new dependency direction not in ARCHITECTURE.md                        |
| Tests are failing and being skipped instead of fixed                     |
| A financial calculation that is NOT in the Domain layer                  |
| Multiple places checking the same business rule                          |
| An entity without RowVersion concurrency control                         |

---

## 7. Recovery Procedures

### 7.1 When Architecture Violations Are Found

| Step | Action                                                                  |
|------|-------------------------------------------------------------------------|
| 1    | Document the violation (what, where, why)                               |
| 2    | Assess blast radius (what else is affected)                             |
| 3    | Stop feature development on affected area                               |
| 4    | Create a refactoring plan                                               |
| 5    | Execute refactoring with full test coverage                             |
| 6    | Verify governance compliance                                            |
| 7    | Update governance docs if the violation exposed a gap                   |

### 7.2 When Data Corruption Is Suspected

| Step | Action                                                                  |
|------|-------------------------------------------------------------------------|
| 1    | Immediately stop all posting operations                                 |
| 2    | Run trial balance verification                                          |
| 3    | Check audit log for suspicious activity                                 |
| 4    | Identify the source of corruption                                       |
| 5    | Determine scope (which records, which period)                           |
| 6    | Use reversal/adjustment to correct (never edit directly)                |
| 7    | Root cause analysis — update rules to prevent recurrence                |

---

## Version History

| Version | Date       | Change Description                    |
|---------|------------|---------------------------------------|
| 1.0     | 2026-02-08 | Initial governance release            |
| 2.0     | 2026-03-06 | Updated phase references to current project state (Phase 8+); added GovernanceIntegrityCheckService and GovernanceConsoleViewModel references to structural health monitoring |
