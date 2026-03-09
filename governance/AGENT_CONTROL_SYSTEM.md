> **SUPERSEDED — See [AGENT_POLICY.md](AGENT_POLICY.md) v2.0 (2026-03-06)**
> This document has been merged into AGENT_POLICY.md v2.0. All content from this file
> is preserved in the merged document. This file is retained for historical reference only.

# MarcoERP – Agent Control System (SUPERSEDED)

**Deliverable 7: AI Agent Permissions, Validation Checklists, and Impact Analysis**

---

## 1. Control System Purpose

This document is the **operational manual** for any AI agent working on MarcoERP. It provides:

1. **Permissions matrix** — what the agent can and cannot do
2. **Pre-action validation checklist** — what must be verified before any action
3. **Impact analysis framework** — questions to answer before any feature
4. **Quality gates** — standards for all agent-generated output

This document works in conjunction with [AGENT_POLICY.md](AGENT_POLICY.md). If there is any conflict, AGENT_POLICY.md takes precedence.

---

## 2. Agent Permissions Matrix

### 2.1 Fully Autonomous Actions (No Approval Needed)

| Action Category       | Specific Actions Allowed                                    |
|-----------------------|-------------------------------------------------------------|
| Documentation         | Add/update XML comments on public members                   |
| Documentation         | Fix typos in comments or strings                            |
| Formatting            | Apply code formatting (consistent with project style)       |
| Tests                 | Write new unit tests for existing code                      |
| Tests                 | Fix broken tests (where business logic is not changing)     |
| Repository            | Implement repository interfaces already defined in Domain   |
| DTO                   | Create DTOs matching existing entity structures             |
| Validator             | Create validator classes following FluentValidation pattern |
| Configuration         | Create EF configuration classes following existing patterns |

### 2.2 Propose-Then-Execute Actions (Approval Required)

| Action Category       | Specific Actions Requiring Proposal                        |
|-----------------------|------------------------------------------------------------|
| Entity                | Add new domain entity                                      |
| Entity                | Modify existing entity properties                          |
| Entity                | Add value object                                           |
| Interface             | Define new interface in Domain                             |
| Service               | Create new Application service                             |
| Table                 | Add new database table (via migration)                     |
| Migration             | Any EF Core migration                                      |
| Workflow              | Modify posting workflow                                    |
| Financial Logic       | Add/modify VAT calculation logic                           |
| Financial Logic       | Add/modify balance verification logic                      |
| Window                | Create new WPF window                                 |
| Dependency            | Add new NuGet package                                      |
| Governance            | Any change to governance documents                         |

### 2.3 Absolutely Forbidden Actions (Never Allowed)

| Action Category       | Specific Actions NEVER Allowed                             |
|-----------------------|------------------------------------------------------------|
| Deletion              | Delete any posted financial record                         |
| Deletion              | Delete any version-controlled governance document          |
| Modification          | Modify posted transaction records                          |
| Modification          | Change entity type after transactions exist                |
| Bypass                | Skip balance check on journal entry                        |
| Bypass                | Skip period lock check on posting                          |
| Bypass                | Skip authorization check on sensitive operations           |
| Override              | Disable audit logging                                      |
| Override              | Disable concurrency control (RowVersion)                   |
| Override              | Allow negative financial amounts in journal entries        |
| Direct Access         | Write SQL queries in UI or Application layers              |
| Direct Access         | Instantiate DbContext outside Persistence layer            |
| Type Change           | Use float or double for financial amounts                  |

---

## 3. Pre-Action Validation Checklist

Before creating or modifying ANY file, the agent must complete this checklist:

### 3.1 Architecture Validation

```
[ ] I have read ARCHITECTURE.md and understand layer responsibilities
[ ] This file belongs in layer: ___________
[ ] This file does not create forbidden cross-layer access
[ ] This file follows the namespace convention
[ ] This file follows the naming convention
[ ] This file's dependencies are allowed per ARCHITECTURE.md
[ ] Build order is preserved (no circular dependencies)
```

### 3.2 Financial Validation (if applicable)

```
[ ] Does this involve a financial entity or transaction? YES / NO

If YES:
[ ] Double-entry rule is enforced
[ ] Balance verification is in place
[ ] Posting workflow rules are followed
[ ] Period lock is checked before posting
[ ] VAT calculation is in the correct layer (Domain/Application)
[ ] No hardcoded financial amounts
[ ] All amounts use decimal type
[ ] Rounding is applied correctly
[ ] Audit log will capture this operation
```

### 3.3 Data Integrity Validation (if applicable)

```
[ ] Does this create or modify an entity? YES / NO

If YES:
[ ] Entity has all mandatory base columns (CreatedAt, RowVersion, etc.)
[ ] Soft delete is implemented for financial/master data
[ ] Hard delete is NOT used
[ ] Concurrency control (RowVersion) is present
[ ] Referential integrity is enforced
[ ] Timestamps are UTC
[ ] All string fields have MaxLength defined
```

### 3.4 UI Validation (if applicable)

```
[ ] Does this involve UI code? YES / NO

If YES:
[ ] Window binds to DTOs, not domain entities
[ ] No business logic in the window
[ ] No SQL or DbContext access in the window
[ ] Posted records are shown as read-only
[ ] Validation errors are displayed appropriately
[ ] Window follows UI_GUIDELINES.md layout
[ ] Control naming convention is followed
[ ] Confirmation dialog for destructive actions
```

### 3.5 Test Validation

```
[ ] Is this a new feature or change to existing feature? YES / NO

If YES:
[ ] Unit tests are written or updated
[ ] Test names follow convention: MethodName_Scenario_ExpectedResult
[ ] Tests are independent (no shared state)
[ ] Integration tests are considered if cross-layer logic is involved
[ ] Financial calculations have dedicated test coverage
```

---

## 4. Impact Analysis Framework

Before implementing any feature, the agent must answer these questions and document the answers:

### 4.1 Scope Analysis

| #  | Question                                                     | Answer       |
|----|--------------------------------------------------------------|--------------|
| 1  | Which layers are affected?                                   | [list layers]|
| 2  | Which projects will be modified?                             | [list projs] |
| 3  | What entities are involved?                                  | [list ents]  |
| 4  | What existing interfaces are affected?                       | [list intfs] |
| 5  | Are any new tables required?                                 | YES / NO     |

### 4.2 Financial Impact Analysis

| #  | Question                                                     | Answer       |
|----|--------------------------------------------------------------|--------------|
| 6  | Does this affect financial data?                             | YES / NO     |
| 7  | Does this affect posting workflow?                           | YES / NO     |
| 8  | Does this affect trial balance?                              | YES / NO     |
| 9  | Does this affect VAT calculation?                            | YES / NO     |
| 10 | Does this affect inventory valuation?                        | YES / NO     |

### 4.3 Data Safety Analysis

| #  | Question                                                     | Answer       |
|----|--------------------------------------------------------------|--------------|
| 11 | Can this cause data loss?                                    | YES / NO     |
| 12 | Can this cause duplicate records?                            | YES / NO     |
| 13 | Can this bypass period lock?                                 | YES / NO     |
| 14 | Can this create unbalanced journal entries?                  | YES / NO     |
| 15 | Is concurrency conflict possible?                            | YES / NO     |

### 4.4 Architecture Compliance Analysis

| #  | Question                                                     | Answer       |
|----|--------------------------------------------------------------|--------------|
| 16 | Does this violate any layer boundary?                        | YES / NO     |
| 17 | Does this create a circular dependency?                      | YES / NO     |
| 18 | Does this introduce tight coupling?                          | YES / NO     |
| 19 | Does this require a new NuGet package?                       | YES / NO     |
| 20 | Is this the simplest solution?                               | YES / NO     |

### 4.5 Governance Compliance Analysis

| #  | Question                                                     | Answer       |
|----|--------------------------------------------------------------|--------------|
| 21 | Which governance documents apply?                            | [list docs]  |
| 22 | Are there any rule conflicts?                                | YES / NO     |
| 23 | Does this require a governance document update?              | YES / NO     |
| 24 | Is this consistent with ACCOUNTING_PRINCIPLES.md?            | YES / NO     |
| 25 | Is authorization required for this operation?                | YES / NO     |

---

## 5. Quality Gates for Agent Output

All agent-generated code must meet these standards before being presented:

### 5.1 Compilation Gate

```
[ ] Code compiles without errors
[ ] Code compiles without warnings
[ ] All using directives are necessary (no unused)
[ ] All referenced projects are in the allowed dependency list
```

### 5.2 Naming Gate

```
[ ] All classes use PascalCase
[ ] All methods use PascalCase
[ ] All variables use camelCase
[ ] All constants use UPPER_SNAKE_CASE or PascalCase (constant classes)
[ ] Boolean names are questions (IsValid, CanPost, HasBalance)
[ ] No abbreviations except standard ones (Id, VAT, AR, AP)
```

### 5.3 Documentation Gate

```
[ ] All public classes have XML documentation
[ ] All public methods have XML documentation
[ ] All public properties have XML documentation
[ ] Complex business rules have explanatory comments
[ ] No commented-out code
[ ] No TODO without description
```

### 5.4 Structure Gate

```
[ ] No class exceeds 500 lines
[ ] No method exceeds 50 lines
[ ] No method has more than 5 parameters
[ ] No nesting deeper than 3 levels
[ ] No static mutable state
[ ] No hardcoded strings, numbers, or configuration values
```

### 5.5 Pattern Compliance Gate

```
[ ] Follows established patterns in the codebase
[ ] Uses dependency injection (no direct instantiation)
[ ] Returns result types (not throwing exceptions for business logic)
[ ] Uses async/await where I/O is involved
[ ] Validates input at entry points
[ ] Uses guard clauses for preconditions
```

---

## 6. Agent Reporting Template

After completing any work, the agent must report using this template:

### 6.1 Work Completion Report

```markdown
## Work Summary

**Feature/Task:** [one-sentence description]

**Status:** [Completed / Partially Completed / Blocked]

---

## Files Created

| File Path                                 | Purpose                          |
|-------------------------------------------|----------------------------------|
| [file path]                               | [one-line purpose]               |

---

## Files Modified

| File Path                                 | Change Description               |
|-------------------------------------------|----------------------------------|
| [file path]                               | [one-line change summary]        |

---

## Governance Rules Checked

- [ ] ARCHITECTURE.md — Layer boundaries validated
- [ ] PROJECT_RULES.md — Naming and standards followed
- [ ] DATABASE_POLICY.md — Database rules followed (if applicable)
- [ ] UI_GUIDELINES.md — UI standards followed (if applicable)
- [ ] ACCOUNTING_PRINCIPLES.md — Financial rules followed (if applicable)
- [ ] FINANCIAL_ENGINE_RULES.md — Posting rules followed (if applicable)
- [ ] RECORD_PROTECTION_POLICY.md — Immutability rules followed (if applicable)
- [ ] SECURITY_POLICY.md — Authorization checked (if applicable)
- [ ] AGENT_POLICY.md — Agent boundaries respected

---

## Impact Analysis

**Layers Affected:** [list]

**Entities Involved:** [list]

**New Tables:** YES / NO — [table names if yes]

**Financial Impact:** YES / NO — [details if yes]

**Breaking Changes:** YES / NO — [details if yes]

---

## Assumptions Made

1. [assumption 1]
2. [assumption 2]

---

## Tests Written

- [ ] Unit tests for new logic
- [ ] Integration tests (if applicable)
- [ ] All tests passing

---

## Next Steps / Recommendations

- [next action 1]
- [next action 2]
```

---

## 7. Agent Self-Check Questions

Before concluding any session, the agent asks itself:

| #  | Question                                                                |
|----|-------------------------------------------------------------------------|
| 1  | Did I check all relevant governance documents?                          |
| 2  | Did I violate any FORBIDDEN action?                                     |
| 3  | Did I propose where I should have instead of executing?                 |
| 4  | Did I document all assumptions?                                         |
| 5  | Did I leave any incomplete work without documenting it?                 |
| 6  | Did I create any technical debt?                                        |
| 7  | Would another developer understand what I did and why?                  |
| 8  | Is the system in a stable state after my changes?                       |
| 9  | Did I follow all quality gates?                                         |
| 10 | Did I provide a proper completion report?                               |

---

## 8. Emergency Stop Conditions

The agent must immediately stop and report if:

| Condition                                                                   |
|-----------------------------------------------------------------------------|
| Balance check fails in a way that can't be explained                        |
| Database migration would cause data loss                                     |
| Circular dependency is detected                                              |
| Required governance document is missing or contradictory                     |
| Authorization rules are unclear                                              |
| Financial calculation produces an unexpected result                          |
| Test suite starts failing after changes                                      |
| Layer boundary violation is discovered in existing code                      |
| Any action would cause permanent data corruption                             |
| Unclear whether an action is in EXECUTE, PROPOSE, or FORBIDDEN category     |

**In emergency stop, the agent must:**
1. Document exactly what was attempted
2. Document why it stopped
3. Document what state the code is in
4. Ask for human guidance

---

## Version History

| Version | Date       | Change Description                    |
|---------|------------|---------------------------------------|
| 1.0     | 2026-02-08 | Initial Phase 1 governance release    |
| —       | 2026-03-06 | SUPERSEDED: Merged into AGENT_POLICY.md v2.0 |
