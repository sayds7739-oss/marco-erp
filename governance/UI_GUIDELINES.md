# MarcoERP -- Unified UI Guidelines

**Version 3.0 -- Commercial Enterprise Standard (WPF)**

---

## 1. UI Platform

| Property          | Value                                |
|-------------------|--------------------------------------|
| Framework         | WPF (.NET 8)                         |
| Target OS         | Windows 10/11                        |
| Resolution Target | 1366x768 minimum, 1920x1080 optimal |
| DPI Awareness     | Per-Monitor DPI Aware                |
| Theme             | System default (future theming supported) |
| RTL               | Fully supported                      |

---

## 2. Core UI Philosophy

| ID    | Principle                                                                              |
|-------|----------------------------------------------------------------------------------------|
| UI-P1 | **Separation of concerns.** Windows display data and capture input -- nothing more. No business logic in UI. |
| UI-P2 | **Module-based consistency.** All windows follow the same layout, naming, and interaction patterns across the entire system. |
| UI-P3 | **Immediate visual feedback.** Every action gives immediate visual feedback (loading, success, error). |
| UI-P4 | **Data safety first.** Unsaved changes trigger confirmation before navigation or close. Protection is mandatory. |
| UI-P5 | **Keyboard-first usability.** Tab order is logical, keyboard shortcuts are consistent across all modules. |
| UI-P6 | **No surprises.** Destructive actions always require confirmation dialogs. |
| UI-P7 | **Commercial-grade performance.** UI must meet enterprise performance expectations. |
| UI-P8 | **Scalable navigation architecture.** Navigation design must support growing module count. |

---

## 3. Module-Based Navigation Architecture

### 3.1 Mandatory Modules

Every screen MUST belong to one of:

- Sales
- Purchases
- Inventory
- Treasury
- Accounting
- Reports
- Settings

No screen may exist outside a module.

### 3.2 Sidebar Rules

| Rule ID | Description                                                              |
|---------|--------------------------------------------------------------------------|
| MN-01   | Reports live under their parent module only.                             |
| MN-02   | No duplicated report entries.                                            |
| MN-03   | POS belongs to Sales only.                                               |
| MN-04   | NavigationService registers views by module.                             |
| MN-05   | Sidebar supports collapse animation.                                     |
| MN-06   | Tabbed document hosting inside MainWindow only.                          |
| MN-07   | Sidebar items use clear active-state highlight and minimum hit height 44px. |
| MN-08   | Sidebar expanded width target 300px; collapsed width target 72px.        |

---

## 4. Window Architecture

### 4.1 Main Window

- MainWindow = Navigation shell.
- Hosts tabbed views.
- No direct OS-level window spawning unless modal dialog.
- Windows may be implemented as tab-hosted views inside MainWindow.

### 4.2 Window Types

| Type                | Purpose                                    | Example                        |
|---------------------|--------------------------------------------|--------------------------------|
| List Window         | Displays data grid, filter, search         | `AccountListWindow`            |
| Detail Window       | View/edit single record                    | `AccountDetailWindow`          |
| Transaction Window  | Multi-line financial entry                 | `JournalEntryWindow`           |
| Quotation Window    | Sales/Purchase offers                      | `SalesQuotationWindow`         |
| Price List Window   | Customer pricing management                | `PriceListWindow`              |
| Bulk Update Window  | Batch update operations                    | `BulkPriceUpdateWindow`        |
| Session Window      | POS session management                     | `PosSessionWindow`             |
| Dialog Window       | Quick input or confirmation                | `ConfirmPostDialog`            |
| Report Window       | Displays reports with export options       | `TrialBalanceReportWindow`     |
| Settings Window     | Application configuration                  | `FiscalYearSettingsWindow`     |
| Main Window         | Navigation shell with menu                 | `MainWindow`                   |

### 4.3 Window Naming Convention

```
{Entity}{Type}Window
```

Examples:
- `AccountListWindow`
- `AccountDetailWindow`
- `JournalEntryWindow`
- `SalesInvoiceWindow`
- `SalesQuotationWindow`
- `ConfirmPostDialog`

### 4.4 Shell View Naming (Tabbed/Content Area)

When the UI is hosted inside a single shell window (MainWindow) using tabbed or content-area navigation,
UserControls must follow this naming convention:

```
{Entity}{Type}View
```

Examples:
- `AccountListView`
- `AccountDetailView`
- `JournalEntryView`

**Windows are reserved for modal dialogs only** (confirmations, print preview, etc.).

### 4.5 One Window, One Responsibility

| Rule ID | Rule                                                                     |
|---------|--------------------------------------------------------------------------|
| UIF-01  | Each window handles **one** functional area.                             |
| UIF-02  | No window exceeds 800 lines including XAML + code-behind.                |
| UIF-03  | Complex screens use UserControls for sub-sections.                       |
| UIF-04  | Shared UI logic goes into helper classes, not base windows.              |
| UIF-05  | No window-to-window direct data passing via static fields or globals.    |
| UIF-06  | Max logical complexity controlled -- group large forms into sections.    |

---

## 5. Data Binding Rules

| Rule ID | Rule                                                                       |
|---------|----------------------------------------------------------------------------|
| UDB-01  | Windows bind to **DTOs** (from Application layer), never to domain entities. |
| UDB-02  | DataGrid binds to `ObservableCollection<T>` or `CollectionViewSource`.      |
| UDB-03  | CollectionViewSource is the standard mechanism for list filtering/sorting.  |
| UDB-04  | No manual population of controls in loops -- use data binding.              |
| UDB-05  | ComboBox/ListBox uses `DisplayMemberPath` and `SelectedValuePath`.          |

---

## 6. Layout Standards

### 6.1 Standard Window Layout

```
+----------------------------------------------+
|  Title Bar (Window Title)                     |
+----------------------------------------------+
|  Toolbar Area (Action buttons) [40px height]  |
+----------------------------------------------+
|                                               |
|  Content Area                                 |
|  (DataGrid / Window Fields)                   |
|                                               |
+----------------------------------------------+
|  Status Bar (Record count, status info)       |
+----------------------------------------------+
```

### 6.2 Detail Window Layout

```
+----------------------------------------------+
|  Title Bar                                    |
+----------------------------------------------+
|  Toolbar: [Save] [Cancel] [Print] [Delete]   |
+----------------------------------------------+
|  +- Header Fields ----------------------+    |
|  |  Code:     [________]  Auto-generated |    |
|  |  Date:     [________]                 |    |
|  |  Status:   [Draft/Posted]             |    |
|  +--------------------------------------+    |
|  +- Detail Section ---------------------+    |
|  |  DataGrid for line items              |    |
|  +--------------------------------------+    |
|  +- Totals Section ---------------------+    |
|  |  Subtotal:  [_____]                   |    |
|  |  VAT:       [_____]                   |    |
|  |  Total:     [_____]                   |    |
|  +--------------------------------------+    |
+----------------------------------------------+
|  Status Bar                                   |
+----------------------------------------------+
```

### 6.3 Spacing and Margins

| Element            | Value     |
|--------------------|-----------|
| Window padding     | 10px      |
| Label-to-control   | 5px       |
| Between rows       | 8px       |
| Section spacing    | 15px      |
| Button spacing     | 5px       |
| Toolbar height     | 40px      |

### 6.4 Sidebar and Top Bar

| Element                 | Value                |
|-------------------------|----------------------|
| Sidebar expanded width  | 300px                |
| Sidebar collapsed width | 72px                 |
| Sidebar item min height | 44px                 |
| Active item indicator   | Right-edge highlight |

Top bar includes:
- A quick dropdown menu listing all accessible modules/screens.
- Global search.
- System status icons.

---

## 7. Control Standards

### 7.1 Standard Controls

| Purpose              | Control                  | Notes                          |
|----------------------|--------------------------|--------------------------------|
| Data display         | `DataGrid`               | Read-only by default           |
| Text input           | `TextBox`                | With MaxLength set             |
| Numeric input        | `TextBox` + validation   | Numeric validation + format    |
| Date input           | `DatePicker`             | Format: yyyy-MM-dd             |
| Selection            | `ComboBox`               | IsEditable = false             |
| Boolean              | `CheckBox`               | Clear label text               |
| Action               | `Button`                 | Consistent sizing              |
| Progress             | `ProgressBar`            | For long operations            |
| Grouping             | `GroupBox` / `Panel`     | With clear border/title        |

### 7.2 Control Naming Convention

```
{abbreviation}{Purpose}
```

| Control Type         | Prefix  | Example              |
|----------------------|---------|----------------------|
| TextBox              | `txt`   | `txtAccountCode`     |
| ComboBox             | `cmb`   | `cmbAccountType`     |
| Button               | `btn`   | `btnSave`            |
| Label                | `lbl`   | `lblTotal`           |
| DataGrid             | `dg`    | `dgJournalLines`     |
| DatePicker           | `dp`    | `dpPostingDate`      |
| Numeric input        | `num`   | `numAmount`          |
| CheckBox             | `chk`   | `chkIsActive`        |
| GroupBox             | `grp`   | `grpTotals`          |
| Panel                | `pnl`   | `pnlHeader`          |
| TabControl           | `tab`   | `tabDetails`         |
| StatusBar            | `stb`   | `stbMain`            |
| ToolBar              | `tlb`   | `tlbActions`         |
| CollectionViewSource | `cvs`   | `cvsAccounts`        |

---

## 8. Transaction Window Rules

### 8.1 Core Transaction Rules

| Rule ID | Rule                                                                     |
|---------|--------------------------------------------------------------------------|
| UTR-01  | Transaction windows show a clear **Draft** or **Posted** status indicator (badge). |
| UTR-02  | Posted transactions: all fields become **read-only**.                     |
| UTR-03  | Draft transactions: Save, Post, and Delete buttons visible. Delete allowed only in Draft. |
| UTR-04  | Post button requires confirmation dialog with summary.                   |
| UTR-05  | Posting failure shows detailed error message (which line, what rule).    |
| UTR-06  | Balance mismatch (Debit != Credit) shown with red highlight in real-time. |
| UTR-07  | Auto-calculated totals update as user types (debounced).                 |
| UTR-08  | Line editing uses popup-based **InvoiceAddLineWindow** (not inline DataGrid editing). Add & Next workflow keeps popup open for batch entry. |

### 8.2 Full-Screen Transaction Standard

Applies to Sales & Purchase transactions. Must support:

- Full-screen mode
- Draft / Posted badge
- Sticky totals section (always visible at bottom)
- Real-time debounced totals
- Inline validation
- Quick payment dialog (if applicable)
- Balance mismatch highlight (real-time, red)
- Posted -> Read-only
- Delete allowed only in Draft

---

## 9. Popup-Based Line Editing Standard

All invoice-type detail views (Sales Invoice, Purchase Invoice, Sales Return, Purchase Return) follow this pattern:

1. **Add Line**: Button opens `InvoiceAddLineWindow` via `OpenAddLinePopupCommand`. Popup loops ("Add & Next") until user cancels.
2. **Edit Line**: Row edit button opens same popup via `EditLineCommand` with pre-loaded data.
3. **Delete Line**: Row delete button with confirmation.
4. **DataGrid**: Read-only display only (no inline cell editing).
5. **InvoicePopupMode**: `Sale` shows profit columns + sale price hint; `Purchase` hides profit + shows purchase price hint.

---

## 10. Dirty State Protection (MANDATORY)

| Rule ID | Description                                                              |
|---------|--------------------------------------------------------------------------|
| DSP-01  | All transaction windows implement `UnsavedChangesGuard` / `IDirtyStateAware`. |
| DSP-02  | Window close requires confirmation if dirty.                             |
| DSP-03  | Navigation blocked if unsaved changes exist.                             |
| DSP-04  | Warning dialog used for all dirty-state prompts.                         |

---

## 11. Smart Entry UX Rules

| ID    | Description                                                              |
|-------|--------------------------------------------------------------------------|
| UX-01 | Enter key moves to next field.                                           |
| UX-02 | Enter on last column adds new line.                                      |
| UX-03 | Esc cancels editing.                                                     |
| UX-04 | F2 edits selected line.                                                  |
| UX-05 | Numeric fields select-all on focus.                                      |
| UX-06 | Barcode auto-detection.                                                  |
| UX-07 | Last customer price shown.                                               |
| UX-08 | Live profit margin highlight.                                            |

---

## 12. Validation Display Rules

| Rule ID | Rule                                                                     |
|---------|--------------------------------------------------------------------------|
| UVL-01  | Required fields marked with `*` and red border on validation failure.    |
| UVL-02  | Validation errors shown in a summary panel or tooltip, not alert boxes.  |
| UVL-03  | Validation runs on field exit (per-field) and on Save (full-window).     |
| UVL-04  | Invalid fields keep focus until corrected or explicitly skipped.         |
| UVL-05  | Business validation errors (from Application layer) shown in error panel. |
| UVL-06  | No raw exception messages shown to users.                                |

---

## 13. Long Operation Handling

| Rule ID | Rule                                                                     |
|---------|--------------------------------------------------------------------------|
| ULO-01  | Operations > 1 second show a loading indicator.                          |
| ULO-02  | UI remains responsive during data loading (async/await).                 |
| ULO-03  | Cancel button available for operations > 3 seconds.                      |
| ULO-04  | Progress bar for batch operations (posting, reporting).                  |
| ULO-05  | Status bar shows current operation description.                          |

---

## 14. Pricing UI Standards

Customer Price List must support:

- Filter by Supplier
- Filter by Category
- All products / In-stock only toggle
- Bulk percentage update
- Manual override
- Preview before save
- Export PDF
- Visual pricing priority clarity

---

## 15. Quotation UI Standards

Applies to `SalesQuotationWindow` & `PurchaseQuotationWindow`:

- Expiry date required.
- Status badge (Draft / Approved / Expired / Converted).
- Convert to Invoice button.
- Conversion confirmation dialog.
- Expired quotations visually highlighted.

Quotations do NOT affect:
- Stock
- Journal entries
- Accounting balances

---

## 16. POS Session UI Standard

- Session must be opened before any sale can be processed.
- Session close requires cash count entry.
- Mismatch between expected and actual cash highlighted in red.
- Printable session summary available on close.

---

## 17. Performance Standards

| Rule ID  | Description                                                             |
|----------|-------------------------------------------------------------------------|
| PERF-01  | Async/await mandatory for all data operations.                          |
| PERF-02  | No blocking the UI thread.                                              |
| PERF-03  | Paging required if dataset exceeds 500 rows.                            |
| PERF-04  | Lazy loading for tabs and deferred content.                             |
| PERF-05  | Debounced calculations for real-time totals.                            |
| PERF-06  | CollectionViewSource is the standard for filtered/sorted lists.         |

---

## 18. Global Keyboard Shortcuts

| Shortcut                     | Action                                              |
|------------------------------|-----------------------------------------------------|
| Ctrl+K                       | Command palette / quick search                      |
| Ctrl+N                       | New document                                        |
| Ctrl+S                       | Save                                                |
| Ctrl+E                       | Edit                                                |
| Ctrl+R                       | Refresh                                             |
| Ctrl+P                       | Print                                               |
| F1                           | Open search lookup popup on focused ComboBox (`SearchLookupWindow`) |
| F2                           | Edit selected line                                  |
| F9                           | Post/Submit                                         |
| Esc                          | Cancel edit                                         |
| Alt+Right / Alt+Left         | Next/Previous record (when supported)               |
| Ctrl+Tab / Ctrl+Shift+Tab   | Next/Previous tab                                   |
| Ctrl+W                       | Close active tab                                    |

---

## 19. Global Search & Command Palette

Ctrl+K opens the command palette. Supports search in:

- Customers
- Products
- Invoices
- Journal entries

Results are grouped by module.

---

## 20. Navigation Rules

| Rule ID | Rule                                                                     |
|---------|--------------------------------------------------------------------------|
| UNV-01  | Main menu provides access to all modules (Sales, Purchases, Inventory, Treasury, Accounting, Reports, Settings). |
| UNV-02  | List windows open as tabbed views inside the main window.                |
| UNV-03  | Detail windows open as modal or docked depending on context.             |
| UNV-04  | Double-click on grid row opens the detail window.                        |
| UNV-05  | Back/Close button always available. Escape key closes dialogs.           |
| UNV-06  | Keyboard shortcuts consistent across all views (see Section 18).         |

---

## 21. Message & Dialog Standards

| Type        | Control                 | When                                          |
|-------------|-------------------------|-----------------------------------------------|
| Info        | `MessageBox` (Info)     | Operation completed successfully               |
| Warning     | `MessageBox` (Warning)  | Unsaved changes, about to close                |
| Error       | Error panel on window   | Validation or business rule failure            |
| Confirm     | `MessageBox` (Question) | Before destructive action (Post, Delete)       |
| Fatal Error | Error window            | Unrecoverable exception (with log reference)   |

---

## 22. Forbidden UI Practices

| #  | Forbidden Practice                                                       |
|----|--------------------------------------------------------------------------|
| 1  | Performing business calculations in code-behind                          |
| 2  | Direct SQL or DbContext access from windows                              |
| 3  | Showing raw exception messages to users                                  |
| 4  | Using `Thread.Sleep` for timing or delays                                |
| 5  | Storing application state in static variables                            |
| 6  | Creating windows with more than 30 fields without grouping               |
| 7  | Nesting more than 2 modal dialogs deep                                   |
| 8  | Using `DispatcherFrame`/manual DoEvents for responsiveness               |
| 9  | Hard-coding display text -- use resource files for all labels            |
| 10 | Ignoring window disposal -- all windows implement `IDisposable` properly |
| 11 | Blocking the UI thread for any data operation                            |

---

## 23. Shared UI Components

| Component               | Location            | Purpose |
|-------------------------|---------------------|---------|
| `InvoiceLinePopupState` | ViewModels/Common/  | Shared popup state for add/edit line across all invoice types (Sale, Purchase, Return). Supports dual-unit entry, profit calc, smart-entry data. |
| `InvoiceAddLineWindow`  | Views/Common/       | Shared popup window bound to `LinePopup.*`. Supports Add & Next workflow. |
| `SearchLookupWindow`    | Views/Common/       | Reusable F1 search popup with real-time substring filter, auto-columns, DataGrid results. |
| `F1SearchBehavior`      | Common/             | Attached behavior -- press F1 on any ComboBox to open `SearchLookupWindow`. |
| `QuickTreasuryDialog`   | Views/Common/       | Quick cash receipt/payment from invoice detail. |
| `InvoicePdfPreviewDialog` | Views/Common/     | Invoice PDF preview. |
| `IInvoiceLineFormHost`  | ViewModels/         | Interface providing Products collection + line calculation contract. |
| `IDirtyStateAware`      | Navigation/         | Interface for dirty-state navigation protection. Detail VMs implement this to guard unsaved changes. |

---

## 24. Governance Enforcement

Any new screen MUST:

1. Declare its parent module (see Section 3.1).
2. Follow naming convention (see Sections 4.3 and 4.4).
3. Implement `UnsavedChangesGuard` / `IDirtyStateAware` if transactional.
4. Bind to DTOs only (see Section 5).
5. Respect Clean Architecture boundaries.
6. Pass governance review before merge.

---

## 25. Commercial UX Enhancements

The following commercial-grade UX features are required for enterprise readiness:

- Sidebar collapse animation.
- Sticky totals bar on transaction screens.
- Theme toggle ready (infrastructure in place for future theming).
- Notification indicator in top bar.
- Highlight negative profit margins in red.
- Highlight overdue credit balances.

---

## Final Statement

This document defines the authoritative UI standard for MarcoERP.

- No feature may bypass these standards.
- No UI element may violate separation of concerns.
- All enhancements must align with module structure and accounting integrity.

---

## Version History

| Version | Date       | Change Description                                                                 |
|---------|------------|------------------------------------------------------------------------------------|
| 1.0     | 2026-02-08 | Initial Phase 1 governance release                                                 |
| 1.1     | 2026-02-11 | Allow shell-based View naming alongside Window naming                              |
| 1.2     | 2026-02-11 | Add F1 search, popup editing, shared components, sidebar 210px                     |
| 2.0     | 2026-02-11 | Commercial enterprise UI governance (separate document)                             |
| 3.0     | 2026-03-06 | **Merged v1.2 + v2.0 into single authoritative document.** Includes all WPF-specific component standards from v1.2 and all commercial UX enhancements from v2.0. Where rules conflicted, v2.0 (commercially mature) rules prevail. |
