# MarcoERP — Feature Governance Deep Audit

**Date:** 2026-02-13  
**Scope:** Feature Governance Console, Profiles (Simple/Standard/Advanced), Feature flags, Impact Analyzer, UI gating, and operational effects (posting, data, reporting).

---

## 1) What Exists Today (Architecture Summary)

### Data Model
- **Features**: [src/MarcoERP.Domain/Entities/Settings/Feature.cs](src/MarcoERP.Domain/Entities/Settings/Feature.cs)
- **Feature change log**: [src/MarcoERP.Domain/Entities/Settings/FeatureChangeLog.cs](src/MarcoERP.Domain/Entities/Settings/FeatureChangeLog.cs)
- **Profiles**: [src/MarcoERP.Domain/Entities/Settings/SystemProfile.cs](src/MarcoERP.Domain/Entities/Settings/SystemProfile.cs)
- **Profile-feature mapping**: [src/MarcoERP.Domain/Entities/Settings/ProfileFeature.cs](src/MarcoERP.Domain/Entities/Settings/ProfileFeature.cs)

### Services
- **Feature toggle service**: [src/MarcoERP.Application/Services/Settings/FeatureService.cs](src/MarcoERP.Application/Services/Settings/FeatureService.cs)
- **Profile apply service**: [src/MarcoERP.Application/Services/Settings/ProfileService.cs](src/MarcoERP.Application/Services/Settings/ProfileService.cs)
- **Impact analyzer (warning only)**: [src/MarcoERP.Application/Services/Settings/ImpactAnalyzerService.cs](src/MarcoERP.Application/Services/Settings/ImpactAnalyzerService.cs)
- **FeatureGuard (not used anywhere)**: [src/MarcoERP.Application/Common/FeatureGuard.cs](src/MarcoERP.Application/Common/FeatureGuard.cs)

### UI
- **Governance Console view**: [src/MarcoERP.WpfUI/Views/Settings/GovernanceConsoleView.xaml](src/MarcoERP.WpfUI/Views/Settings/GovernanceConsoleView.xaml)
- **Governance Console VM**: [src/MarcoERP.WpfUI/ViewModels/Settings/GovernanceConsoleViewModel.cs](src/MarcoERP.WpfUI/ViewModels/Settings/GovernanceConsoleViewModel.cs)
- **Navigation gating**: [src/MarcoERP.WpfUI/ViewModels/Shell/MainWindowViewModel.cs](src/MarcoERP.WpfUI/ViewModels/Shell/MainWindowViewModel.cs#L835-L920)

---

## 2) Seeded Profiles and Their Features

### Profiles
Defined in [src/MarcoERP.Persistence/Seeds/ProfileSeed.cs](src/MarcoERP.Persistence/Seeds/ProfileSeed.cs)

- **Simple**
  - Accounting
  - Inventory
  - Sales
  - Treasury
  - UserManagement

- **Standard (default active)**
  - Simple + Purchases, POS, Reporting

- **Advanced**
  - **Same as Standard in current seed** (all features enabled)

**Note:** There is no additional behavior difference between Standard and Advanced at the data level in the current seed. This is a gap if you expect Advanced to unlock extra features beyond Standard.

---

## 3) Actual Enforcement vs. Expected Enforcement

### What is enforced now
- **UI navigation visibility only**
  - `MainWindowViewModel.RefreshNavigationAsync` hides sidebar sections based on enabled feature keys.
  - If feature lookup fails, all items remain visible.

- **Governance console warnings**
  - Impact Analyzer shows risk, impact areas, dependencies, and migration warnings.
  - It **blocks enabling** a feature when dependencies are disabled.
  - It **does not block disabling** a dependency feature.

### What is NOT enforced
- **No Application-layer guard**
  - `FeatureGuard` is not used anywhere.
  - All services (posting, CRUD, reporting) run even if feature is disabled.

- **No blocking of background services**
  - Background jobs continue regardless of feature state.

- **No DB-level enforcement**
  - Feature flags do not restrict data operations at persistence level.

**Conclusion:** The system is currently **UI-only gating**. Feature toggles do not prevent posting, editing, or reporting when called from open screens or API pathways.

---

## 4) Impact by Area (Actual Behavior)

### UI / Navigation
- Sidebar sections are hidden based on feature keys.
- Any window opened before disabling will still work (no runtime guard).

### Posting and Accounting
- Posting services for Sales/Purchases/Returns **do not check feature flags**. Posting will still succeed even if Accounting or module feature is disabled.

### Inventory
- Inventory operations continue normally even if Inventory feature is disabled.

### Reporting
- Report screens are hidden if Reporting is disabled, but report generation still works if screen already open.
- Report complexity toggle is UI-only (not tied to profiles).

### Security / User Management
- User/Role management is only hidden in UI; backend services still operate.

---

## 5) Dependency Enforcement Gaps

- Dependencies are **only enforced when enabling** a feature.
- Disabling a dependency (e.g., Accounting) does not auto-disable dependent features (Sales, Purchases, Treasury).
- Integrity check can **detect** invalid states but **does not block** them.

---

## 6) Risks and Gaps

1. **Logic-level bypass**: Feature flags are not enforced in Application services.
2. **Advanced profile has no extra scope**: Advanced == Standard in current seed.
3. **Dependency inconsistencies**: Possible to disable Accounting while Sales is still enabled.
4. **Long-lived sessions**: UI gating applies only on refresh; open windows remain operational.

---

## 7) Recommendations (If You Want Full Enforcement)

1. **Add FeatureGuard to service entry points**
   - Sales, Purchases, Inventory, Treasury, Reporting services.
   - Block Post/Create/Edit when feature is disabled.

2. **Harden dependency rules**
   - Block disabling a feature if another enabled feature depends on it.

3. **Differentiate Advanced profile**
   - Add actual advanced-only features (cost centers, budgets, approvals, etc.) or treat Advanced as a superset with new flags.

4. **Add UI runtime checks**
   - Prevent commands on open windows when features are disabled.

---

## 8) Immediate Questions (Only if you want enforcement)

- Do you want feature flags to block posting and CRUD at Application services?
- Should disabling Accounting automatically disable Sales/Purchases/Treasury?
- What features should be Advanced-only (not in Standard)?
