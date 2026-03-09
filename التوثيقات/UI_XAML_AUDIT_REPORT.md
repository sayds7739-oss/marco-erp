# MarcoERP – Comprehensive XAML UI Audit Report

**Date:** 2026-02-16  
**Scope:** All 78 XAML files under `src/MarcoERP.WpfUI/Views/`  
**Excluded files:** QuickCashReceiptWindow.xaml, QuickCashPaymentWindow.xaml, SuperAdminAuthDialog.xaml, AppStyles.xaml (already fixed)

---

## Summary

| Severity | Count |
|----------|-------|
| HIGH     | 5     |
| MEDIUM   | 18    |
| LOW      | 40+   |

- **RTL FlowDirection**: ✅ All 78 files correctly set `FlowDirection="RightToLeft"`.
- **DataGrid ColumnHeaderStyle**: ✅ Global style applied via AppStyles.xaml — no per-file overrides needed (files with inline overrides are acceptable).
- **Small Buttons / FontSize**: ✅ No systemic issues found. Buttons consistently use Height="30-32", FontSize="12". Toolbar icons use Width/Height="16".

---

## HIGH Severity Issues

### H-1: ChangePasswordDialog — Fixed size, NoResize, no ScrollViewer
- **File:** `src/MarcoERP.WpfUI/Views/Common/ChangePasswordDialog.xaml`
- **Line:** 6–8
- **Issue:** Window has `Height="320" Width="420" ResizeMode="NoResize"` with no `ScrollViewer`. At high DPI or with large system fonts, content will clip and become inaccessible.
- **Fix:** Add `SizeToContent="Height"` and remove fixed `Height`, or wrap content in `<ScrollViewer VerticalScrollBarVisibility="Auto">`.
- **Severity:** HIGH

### H-2: QuickTreasuryDialog — Fixed size, NoResize, no ScrollViewer
- **File:** `src/MarcoERP.WpfUI/Views/Common/QuickTreasuryDialog.xaml`
- **Line:** 7–9
- **Issue:** Window has `Height="360" Width="520" ResizeMode="NoResize"` with no `ScrollViewer`. Content may clip at high DPI.
- **Fix:** Change to `SizeToContent="Height" MaxHeight="500"` or add `<ScrollViewer VerticalScrollBarVisibility="Auto">` around form content.
- **Severity:** HIGH

### H-3: PosOpenSessionDialog — Fixed size, NoResize, no ScrollViewer
- **File:** `src/MarcoERP.WpfUI/Views/Sales/PosOpenSessionDialog.xaml`
- **Line:** 5–6
- **Issue:** `Height="400" Width="440" ResizeMode="NoResize"` with no `ScrollViewer`. Content will clip on smaller screens or high DPI.
- **Fix:** Use `SizeToContent="Height" MaxHeight="500"` or wrap body in `<ScrollViewer>`.
- **Severity:** HIGH

### H-4: PosCloseSessionDialog — Fixed size, NoResize, no ScrollViewer
- **File:** `src/MarcoERP.WpfUI/Views/Sales/PosCloseSessionDialog.xaml`
- **Line:** 5–6
- **Issue:** `Height="480" Width="460" ResizeMode="NoResize"` with no `ScrollViewer`. More content than PosOpenSessionDialog, higher clipping risk.
- **Fix:** Use `SizeToContent="Height" MaxHeight="600"` or wrap body in `<ScrollViewer>`.
- **Severity:** HIGH

### H-5: ShortcutConfigDialog — Fixed size, NoResize, buttons may clip
- **File:** `src/MarcoERP.WpfUI/Views/Shell/ShortcutConfigDialog.xaml`
- **Line:** 7–9
- **Issue:** `Height="560" Width="480" ResizeMode="NoResize"`. Contains a ListBox (which scrolls) but bottom buttons are outside the ListBox and could clip at high DPI since the Window itself has fixed height.
- **Fix:** Add `SizeToContent="Height" MaxHeight="700"` or ensure bottom buttons are within a non-clipping layout (e.g., DockPanel with buttons Docked to Bottom).
- **Severity:** HIGH

---

## MEDIUM Severity Issues

### M-1: MigrationCenterView — Hardcoded dark header background
- **File:** `src/MarcoERP.WpfUI/Views/Settings/MigrationCenterView.xaml`
- **Line:** 17
- **Issue:** `Background="#37474F"` on header Border. Should use a theme StaticResource.
- **Fix:** Replace with `Background="{StaticResource PrimaryDarkBrush}"` or a new dedicated brush.
- **Severity:** MEDIUM

### M-2: LoginWindow — Fixed size, NoResize, no accessibility fallback
- **File:** `src/MarcoERP.WpfUI/Views/Shell/LoginWindow.xaml`
- **Line:** 7–9
- **Issue:** `Height="520" Width="420" ResizeMode="NoResize" WindowStyle="None"`. While typical for login screens, users with accessibility needs (large fonts) have no recourse.
- **Fix:** Consider `SizeToContent="Height" MinHeight="520" MaxHeight="700"`, or add a `ScrollViewer` around the form.
- **Severity:** MEDIUM

### M-3: PurchaseQuotationDetailView — Hardcoded `Background="#1565C0"` on status badge
- **File:** `src/MarcoERP.WpfUI/Views/Purchases/PurchaseQuotationDetailView.xaml`
- **Line:** 102
- **Issue:** Uses `Background="#1565C0"` instead of `{StaticResource InfoBrush}` or `{StaticResource PrimaryBrush}`.
- **Fix:** Replace with `Background="{StaticResource InfoBrush}"`.
- **Severity:** MEDIUM

### M-4: SalesQuotationDetailView — Hardcoded `Background="#1565C0"` on status badge
- **File:** `src/MarcoERP.WpfUI/Views/Sales/SalesQuotationDetailView.xaml`
- **Line:** 112
- **Issue:** Same as M-3 — hardcoded blue instead of theme brush.
- **Fix:** Replace with `Background="{StaticResource InfoBrush}"`.
- **Severity:** MEDIUM

### M-5: InventoryAdjustmentDetailView — Hardcoded info-blue colors
- **File:** `src/MarcoERP.WpfUI/Views/Inventory/InventoryAdjustmentDetailView.xaml`
- **Lines:** 33, 35
- **Issue:** `Background="#E3F2FD"` and `Foreground="#1565C0"` on status badge. Not theme-consistent.
- **Fix:** Replace with `Background="{StaticResource InfoLightBrush}"` (if exists) or define one, and `Foreground="{StaticResource InfoBrush}"`.
- **Severity:** MEDIUM

### M-6: SearchLookupWindow — Hardcoded `Background="#1976D2"` on confirm button
- **File:** `src/MarcoERP.WpfUI/Views/Common/SearchLookupWindow.xaml`
- **Line:** 112
- **Issue:** `Background="#1976D2"` instead of `{StaticResource PrimaryBrush}`.
- **Fix:** Replace with `Background="{StaticResource PrimaryBrush}"`.
- **Severity:** MEDIUM

### M-7: MainWindow — Multiple hardcoded sidebar/footer colors
- **File:** `src/MarcoERP.WpfUI/Views/Shell/MainWindow.xaml`
- **Lines:** 144, 251, 265, 268, 279, 316, 396, 452, 453, 521, 525, 539, 555
- **Issue:** Extensive use of hardcoded hex colors: `#37474F`, `#263238`, `#1a252f`, `#90CAF9`, `#F0F0F0`, `#F0F2F5`, `#E0E0E0`, `#607D8B`, `#90A4AE`. The sidebar, tab bar, and status bar all use inline colors.
- **Fix:** Extract to named brushes in AppStyles.xaml (e.g., `SidebarBrush`, `SidebarDarkBrush`, `SidebarAccentBrush`, `TabBarBrush`, etc.). This is acceptable for the shell, but should be centralized for theme maintainability.
- **Severity:** MEDIUM

### M-8: PosWindow — Extensive hardcoded colors
- **File:** `src/MarcoERP.WpfUI/Views/Sales/PosWindow.xaml`
- **Lines:** 54, 63, 94, 97, 98, 140, 222, 424, 441
- **Issue:** Uses `#FFCDD2`, `#546E7A`, `#E8F5E9`, `#388E3C`, `#BDBDBD`, `#FAFAFA`, `#263238`, `#80000000`. POS window has its own mini-theme of hardcoded colors inconsistent with the global palette.
- **Fix:** Define POS-specific brushes in AppStyles.xaml or PosWindow.Resources that reference the theme colors.
- **Severity:** MEDIUM

### M-9–M-12: Totals bars use `Foreground="Gray"` instead of theme brush
- **Files & Lines:**
  - `src/MarcoERP.WpfUI/Views/Sales/SalesInvoiceView.xaml` — Lines 283, 287, 291, 295
  - `src/MarcoERP.WpfUI/Views/Sales/SalesReturnView.xaml` — Lines 325, 330, 335, 340
  - `src/MarcoERP.WpfUI/Views/Sales/SalesQuotationDetailView.xaml` — Lines 360, 364, 368, 372
  - `src/MarcoERP.WpfUI/Views/Purchases/PurchaseInvoiceView.xaml` — Lines matching pattern (totals bar)
- **Issue:** Using `Foreground="Gray"` named color instead of the standard `{StaticResource SubtitleBrush}` or `{StaticResource MutedTextBrush}`.
- **Fix:** Replace `Foreground="Gray"` with `Foreground="{StaticResource SubtitleBrush}"`.
- **Severity:** MEDIUM

### M-13: ProductView — Hardcoded icon colors `#2E7D32`, `#6A1B9A`, `#0277BD`
- **File:** `src/MarcoERP.WpfUI/Views/Inventory/ProductView.xaml`
- **Lines:** 295, 359, 391
- **Issue:** Section icons use hardcoded Foreground colors: green `#2E7D32`, purple `#6A1B9A`, blue `#0277BD`. These aren't aligned with the global theme palette.
- **Fix:** Use `{StaticResource SuccessBrush}`, `{StaticResource PrimaryBrush}`, `{StaticResource InfoBrush}` respectively, or define semantic brushes.
- **Severity:** MEDIUM

### M-14: SystemSettingsView — Hardcoded `Foreground="#333"`, `BorderBrush="#E0E0E0"`, `BorderBrush="#E8E8E8"`
- **File:** `src/MarcoERP.WpfUI/Views/Settings/SystemSettingsView.xaml`
- **Lines:** 50, 71, 84, 109
- **Issue:** Uses `#333` for description text, `#E0E0E0` / `#E8E8E8` for borders instead of theme resources.
- **Fix:** Replace `Foreground="#333"` → `Foreground="{StaticResource SecondaryTextBrush}"`, borders → `BorderBrush="{StaticResource BackgroundBrush}"`.
- **Severity:** MEDIUM

### M-15: ReportHubView — Hardcoded icon colors `#FFB300`, `#26A69A`
- **File:** `src/MarcoERP.WpfUI/Views/Reports/ReportHubView.xaml`
- **Lines:** 148, 161, 179
- **Issue:** Report card icons use `#FFB300` (amber) and `#26A69A` (teal) instead of theme brushes.
- **Fix:** Use `{StaticResource WarningBrush}` and `{StaticResource SuccessBrush}` or define report-category brushes.
- **Severity:** MEDIUM

### M-16: SalesInvoiceDetailView — Hardcoded color `#217346`
- **File:** `src/MarcoERP.WpfUI/Views/Sales/SalesInvoiceDetailView.xaml`
- **Line:** 125
- **Issue:** Uses `Foreground="#217346"` (dark green) — not part of the standard theme palette.
- **Fix:** Replace with `Foreground="{StaticResource SuccessBrush}"`.
- **Severity:** MEDIUM

### M-17: ProductImportView — Hardcoded `#4CAF50` on Excel icon
- **File:** `src/MarcoERP.WpfUI/Views/Inventory/ProductImportView.xaml`
- **Line:** 49
- **Issue:** `Foreground="#4CAF50"` on Excel file icon.
- **Fix:** Use `Foreground="{StaticResource SuccessBrush}"`.
- **Severity:** MEDIUM

### M-18: FiscalPeriodView — DataGrid missing standard sizing props
- **File:** `src/MarcoERP.WpfUI/Views/Accounting/FiscalPeriodView.xaml`
- **Line:** ~57
- **Issue:** DataGrid has `SelectionChanged="DataGrid_SelectionChanged"` — a code-behind event handler instead of command binding. Also doesn't set `FontSize`, `RowHeight`, `ColumnHeaderHeight`, or `BorderThickness` like other DataGrids.
- **Fix:** Add `FontSize="13" RowHeight="36" ColumnHeaderHeight="32" BorderThickness="0"` and consider converting SelectionChanged to command binding for consistency.
- **Severity:** MEDIUM

---

## LOW Severity Issues

### L-1 through L-8: Hardcoded `BorderBrush="#E0E0E0"` on drawer footers (consistent pattern)

These files all use `BorderBrush="#E0E0E0"` for the side drawer footer separator instead of `{StaticResource BackgroundBrush}`:

| # | File | Line |
|---|------|------|
| L-1 | `Views/Inventory/ProductView.xaml` | 186 |
| L-2 | `Views/Inventory/UnitView.xaml` | 144 |
| L-3 | `Views/Inventory/WarehouseView.xaml` | 161 |
| L-4 | `Views/Inventory/CategoryView.xaml` | 144 |
| L-5 | `Views/Sales/SalesRepresentativeView.xaml` | 158 |
| L-6 | `Views/Sales/CustomerView.xaml` | 170 |
| L-7 | `Views/Purchases/SupplierView.xaml` | 162 |
| L-8 | `Views/Settings/RoleManagementView.xaml` | 144 |

**Fix:** Replace `BorderBrush="#E0E0E0"` with `BorderBrush="{StaticResource BackgroundBrush}"`.

---

### L-9 through L-16: Hardcoded `BorderBrush="#ECEFF1"` on form section dividers

| # | File | Lines |
|---|------|-------|
| L-9 | `Views/Inventory/ProductView.xaml` | 228, 292, 324, 356, 388 |
| L-10 | `Views/Sales/CustomerView.xaml` | 206, 231 |
| L-11 | `Views/Sales/SalesRepresentativeView.xaml` | 193, 217 |
| L-12 | `Views/Purchases/SupplierView.xaml` | 197, 221 |
| L-13 | `Views/Settings/RoleManagementView.xaml` | 165 |
| L-14 | `Views/Common/QuickAddProductDialog.xaml` | 39, 96 |
| L-15 | `Views/Treasury/BankAccountView.xaml` | 210, 234 |
| L-16 | `Views/Settings/GovernanceConsoleView.xaml` | 33 *(partial)* |

**Fix:** Define a `SectionDividerBrush` in AppStyles.xaml for `#ECEFF1`, or use `{StaticResource BackgroundBrush}`.

---

### L-17 through L-24: Hardcoded `Background="#ECEFF1"` on status bars

These files use `Background="#ECEFF1"` on the bottom status bar instead of `{StaticResource BackgroundBrush}`:

| # | File | Line |
|---|------|------|
| L-17 | `Views/Sales/SalesInvoiceView.xaml` | 352 |
| L-18 | `Views/Sales/SalesInvoiceListView.xaml` | 102 |
| L-19 | `Views/Sales/SalesReturnView.xaml` | 412 |
| L-20 | `Views/Sales/SalesReturnListView.xaml` | 102 |
| L-21 | `Views/Sales/SalesQuotationListView.xaml` | 110 |
| L-22 | `Views/Purchases/PurchaseInvoiceView.xaml` | 405 |
| L-23 | `Views/Purchases/PurchaseInvoiceListView.xaml` | 102 |
| L-24 | `Views/Purchases/PurchaseReturnListView.xaml` | 102 |
| — | `Views/Purchases/PurchaseReturnView.xaml` | 412 |
| — | `Views/Purchases/PurchaseQuotationListView.xaml` | 107 |
| — | `Views/Sales/SalesQuotationDetailView.xaml` | 379 |
| — | `Views/Purchases/PurchaseQuotationDetailView.xaml` | 355 |
| — | `Views/Treasury/CashReceiptView.xaml` | 302 |
| — | `Views/Treasury/CashPaymentView.xaml` | 294 |

**Fix:** Replace `Background="#ECEFF1"` with `Background="{StaticResource BackgroundBrush}"`.

---

### L-25 through L-30: Hardcoded `BorderBrush="#E0E0E0"` on DataGrid/invoice lines borders

| # | File | Line |
|---|------|------|
| L-25 | `Views/Sales/SalesInvoiceView.xaml` | 202 |
| L-26 | `Views/Sales/SalesReturnView.xaml` | 229 |
| L-27 | `Views/Purchases/PurchaseInvoiceView.xaml` | 205 |
| L-28 | `Views/Purchases/PurchaseReturnView.xaml` | 229 |
| L-29 | `Views/Sales/SalesInvoiceDetailView.xaml` | 309, 420 |
| L-30 | `Views/Purchases/PurchaseInvoiceDetailView.xaml` | 246, 357 |
| — | `Views/Purchases/PurchaseReturnDetailView.xaml` | 208, 304 |
| — | `Views/Sales/SalesReturnDetailView.xaml` | 209, 305 |
| — | `Views/Sales/SalesQuotationDetailView.xaml` | 268 |
| — | `Views/Purchases/PurchaseQuotationDetailView.xaml` | 245 |

**Fix:** Replace `BorderBrush="#E0E0E0"` with `BorderBrush="{StaticResource BackgroundBrush}"`.

---

### L-31 through L-34: Hardcoded `Background="#ECEFF1"` on detail view status bars

| # | File | Line |
|---|------|------|
| L-31 | `Views/Sales/SalesInvoiceDetailView.xaml` | 460 |
| L-32 | `Views/Sales/SalesReturnDetailView.xaml` | 335 |
| L-33 | `Views/Purchases/PurchaseInvoiceDetailView.xaml` | 397 |
| L-34 | `Views/Purchases/PurchaseReturnDetailView.xaml` | 334 |

**Fix:** Same as L-17 group.

---

### L-35: Hardcoded section icon colors across drawer forms

Multiple files use hardcoded hex colors on `materialDesign:PackIcon` elements in side drawer section headers:

| Color | Usage | Files |
|-------|-------|-------|
| `#0277BD` (blue) | Phone/contact icon | CustomerView L234, SalesRepView L220, SupplierView L224, ProductView L391 |
| `#2E7D32` (green) | Financial/pricing icon | CustomerView L282, SalesRepView L252, SupplierView L271, ProductView L295, QuickAddProductDialog L99, BankAccountView L261 |
| `#FFA000` (amber) | Default-star icon | CashboxView L45, BankAccountView L45 |

**Fix:** Replace with appropriate theme brushes (`InfoBrush`, `SuccessBrush`, `WarningBrush`).

---

### L-36: BackupSettingsView — `Foreground="#666"` 
- **File:** `src/MarcoERP.WpfUI/Views/Settings/BackupSettingsView.xaml`
- **Lines:** 54, 74
- **Fix:** Use `{StaticResource SubtitleBrush}`.

### L-37: GovernanceConsoleView — `Foreground="#666"` 
- **File:** `src/MarcoERP.WpfUI/Views/Settings/GovernanceConsoleView.xaml`
- **Line:** 53
- **Fix:** Use `{StaticResource SubtitleBrush}`.

### L-38: GovernanceIntegrityView — `Foreground="#999"` 
- **File:** `src/MarcoERP.WpfUI/Views/Settings/GovernanceIntegrityView.xaml`
- **Line:** 94
- **Fix:** Use `{StaticResource MutedTextBrush}`.

### L-39: IntegrityCheckView — `Foreground="#555"` (×3)
- **File:** `src/MarcoERP.WpfUI/Views/Settings/IntegrityCheckView.xaml`
- **Lines:** 107, 147, 186
- **Fix:** Use `{StaticResource SecondaryTextBrush}`.

### L-40: ProductView DataGrid code column — `Foreground="#546E7A"`
- **File:** `src/MarcoERP.WpfUI/Views/Inventory/ProductView.xaml`
- **Line:** ~89 (ElementStyle)
- **Note:** Same pattern appears in WarehouseView, SupplierView. This color matches `StatusTextBrush` in the theme but is hardcoded.
- **Fix:** Use `Foreground="{StaticResource StatusTextBrush}"`.

---

### L-41: QuickAddProductDialog — Hardcoded error/warning colors
- **File:** `src/MarcoERP.WpfUI/Views/Common/QuickAddProductDialog.xaml`
- **Lines:** 33, 35
- **Issue:** `Background="#FFF3E0" BorderBrush="#FFE0B2"` and `Foreground="#E65100"` for error display.
- **Fix:** Use `{StaticResource ErrorBackgroundBrush}` / `{StaticResource ErrorForegroundBrush}` or define a `WarningBackgroundBrush`.

### L-42: InvoiceAddLineWindow — Hardcoded colors
- **File:** `src/MarcoERP.WpfUI/Views/Common/InvoiceAddLineWindow.xaml`
- **Lines:** 73, 149, 150
- **Issue:** `BorderBrush="#E0E0E0"`, `Background="#F5F7FA"`, `BorderBrush="#E8EAF0"`.
- **Fix:** Use `{StaticResource BackgroundBrush}` for background and borders.

### L-43: PriceHistoryDialog — Hardcoded colors
- **File:** `src/MarcoERP.WpfUI/Views/Common/PriceHistoryDialog.xaml`
- **Line:** 28
- **Issue:** `Background="#F5F7FA" BorderBrush="#E0E0E0"`.
- **Fix:** Use `{StaticResource BackgroundBrush}`.

### L-44: LoginWindow — `Foreground="#CCFFFFFF"`
- **File:** `src/MarcoERP.WpfUI/Views/Shell/LoginWindow.xaml`
- **Line:** 55
- **Fix:** Define a semi-transparent white brush or use `Opacity`.

---

## Clean Files (No Issues Detected)

The following files passed all audit checks with no issues:

| Area | Files |
|------|-------|
| **Treasury** | CashTransferView, CashReceiptView (minor L-class only), CashPaymentView (minor L-class only), BankReconciliationView |
| **Settings** | AuditLogView, UserManagementView |
| **Accounting** | ChartOfAccountsView, JournalEntryView, FiscalYearView, OpeningBalanceWizardView |
| **Inventory** | BulkPriceUpdateView, InventoryAdjustmentListView |
| **Dashboard** | DashboardView |

---

## Recommendations (Priority Order)

1. **Fix HIGH H-1 through H-5** — These are genuine usability risks that cause content clipping on high-DPI displays or with accessibility font sizes. Estimated effort: ~30 minutes total.

2. **Centralize hardcoded hex colors** — Define the following brushes in AppStyles.xaml if not already present:
   - `SidebarBrush` → `#263238`
   - `SidebarDarkBrush` → `#1a252f`
   - `SectionDividerBrush` → `#ECEFF1`
   - `StatusBarBrush` → `#ECEFF1`
   - `BorderDefaultBrush` → `#E0E0E0`
   
   Then do a global find-and-replace across all XAML files. Estimated effort: ~1 hour.

3. **Replace `Foreground="Gray"`** in totals bars across Sales/Purchase invoice views with `{StaticResource SubtitleBrush}`. Estimated effort: ~15 minutes.

4. **Standardize status badge colors** in PurchaseQuotationDetailView, SalesQuotationDetailView, and InventoryAdjustmentDetailView to use `{StaticResource InfoBrush}` instead of `#1565C0`.

---

*End of audit report.*
