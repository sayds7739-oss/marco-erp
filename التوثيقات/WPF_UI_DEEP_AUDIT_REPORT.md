# MarcoERP — WPF UI Layer Deep Audit Report

**Date:** 2026-02-14  
**Scope:** `src/MarcoERP.WpfUI/` — Full UI layer (Views, ViewModels, Themes, Navigation, Services, Converters, Common)  
**Files Analyzed:** ~70+ files across all modules  
**Methodology:** Full-content read and analysis against 7 audit categories

---

## Executive Summary

The WPF UI layer is a substantial, well-structured MVVM application with consistent Material Design theming, proper RTL support, and a solid tab-based navigation architecture. However, several patterns — particularly the widespread use of `MessageBox.Show` directly in ViewModels and the lack of data paging — create testability and scalability concerns that should be addressed before production deployment.

| Severity | Count | Status |
|----------|-------|--------|
| CRITICAL | 3 | Active |
| HIGH | 12 | Active |
| MEDIUM | 15 | Active |
| LOW | 12 | Active |

---

## CRITICAL Issues

### C-01: MessageBox.Show Used Directly in ViewModels (MVVM Violation)

**Impact:** Prevents unit testing of all confirmation dialogs; couples VMs to System.Windows  
**Files affected:**

| File | Lines |
|------|-------|
| `ViewModels/Sales/SalesInvoiceDetailViewModel.cs` | Post/Cancel/Delete confirmations |
| `ViewModels/Sales/SalesInvoiceViewModel.cs` | Delete confirmations |
| `ViewModels/Sales/PosViewModel.cs` | Reset sale confirmation |
| `ViewModels/Purchases/PurchaseInvoiceDetailViewModel.cs` | Post/Cancel/Delete/Jump confirmations |
| `ViewModels/Treasury/CashReceiptViewModel.cs` | Post/Cancel/Delete confirmations |
| `ViewModels/Treasury/CashPaymentViewModel.cs` | Post/Cancel/Delete confirmations |
| `ViewModels/Accounting/JournalEntryViewModel.cs` | Delete draft confirmation |
| `ViewModels/Accounting/ChartOfAccountsViewModel.cs` | Deactivate confirmation |
| `ViewModels/Inventory/ProductViewModel.cs` | Delete/Deactivate confirmations |
| `ViewModels/Inventory/WarehouseViewModel.cs` | Deactivate confirmation |
| `ViewModels/Settings/RoleManagementViewModel.cs` | Delete confirmation |
| `ViewModels/Settings/UserManagementViewModel.cs` | Deactivate confirmation |
| `ViewModels/LoginViewModel.cs` | Password change success/failure |
| `Common/DirtyStateGuard.cs` | Unsaved changes dialog |
| `Common/ConcurrencyHelper.cs` | Conflict resolution dialog |
| `Services/InvoiceTreasuryIntegrationService.cs` | Payment creation prompts |

**Pattern found:**
```csharp
var confirm = MessageBox.Show(
    $"هل أنت متأكد من حذف الدور «{SelectedRole.NameAr}»؟",
    "تأكيد الحذف",
    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
if (confirm != MessageBoxResult.Yes) return;
```

**Recommendation:** Introduce an `IDialogService` interface with `Task<bool> ConfirmAsync(string message, string title)` and `Task ShowInfoAsync(string message)`. Inject it into all VMs. This is the single most impactful refactoring for testability.

---

### C-02: LoginViewModel Stores Password in Plain-Text String Property

**File:** `ViewModels/LoginViewModel.cs`

The `Password` property is a standard `string` property bound directly from the UI. Additionally, `CredentialStore.Save(Username, Password)` persists raw credentials.

```csharp
private string _password;
public string Password
{
    get => _password;
    set { if (SetProperty(ref _password, value)) RelayCommand.RaiseCanExecuteChanged(); }
}

// Persists raw credentials
if (RememberMe)
    CredentialStore.Save(Username?.Trim(), Password);
```

**Risks:**
- Password remains in memory as a `string` (not `SecureString`) and is subject to memory dumps
- `CredentialStore` persistence method unknown — if file-based, passwords may be stored in cleartext on disk
- Password is passed to `ChangePasswordDto.CurrentPassword` directly

**Recommendation:** Use WPF `PasswordBox` with `SecurePassword` property passed via a behavior or code-behind helper. For "Remember Me," use Windows Credential Manager (`CredentialManager` API) or DPAPI-encrypted storage.

---

### C-03: No Data Paging — All Collections Loaded Into Memory

**Impact:** Performance degradation and potential OOM with large datasets  
**Files affected:**

| File | Collection | Issue |
|------|-----------|-------|
| `SalesInvoiceViewModel.cs` | `Invoices`, `Customers`, `Products` | `GetAllAsync()` loaded into ObservableCollection |
| `SalesInvoiceDetailViewModel.cs` | All lookups + `_invoiceIds` | All invoice IDs loaded for navigation |
| `PurchaseInvoiceDetailViewModel.cs` | `Suppliers`, `Customers`, `Products`, `Warehouses` | `GetAllAsync()` for all lookups |
| `ProductViewModel.cs` | `AllProducts`, `Categories`, `Units`, `Suppliers` | All loaded via `GetAllAsync()` |
| `WarehouseViewModel.cs` | `AllWarehouses`, `Accounts` | All loaded |
| `CashReceiptViewModel.cs` | `AllReceipts` | All loaded |
| `CashPaymentViewModel.cs` | `AllPayments` | All loaded |
| `JournalEntryViewModel.cs` | `Entries` | All loaded |
| `ChartOfAccountsViewModel.cs` | `AccountTree` + accounts | All loaded |
| `SalesInvoiceListViewModel.cs` | `Invoices` | All loaded, `ApplyFilter()` is a stub |
| `PosViewModel.cs` | `_productCache` | All active products cached |

**Example:**
```csharp
var productsResult = await _productService.GetAllAsync();
AllProducts.Clear();
if (productsResult.IsSuccess)
    foreach (var p in productsResult.Data) AllProducts.Add(p);
```

**Recommendation:** Implement server-side paging with `GetPagedAsync(int page, int pageSize, string filter)`. For lookup dropdowns, use virtualized ComboBoxes with search-on-type that query the server. For invoice navigation, use cursor-based pagination.

---

## HIGH Issues

### H-01: Chart of Accounts Uses DataGrid Instead of TreeView

**File:** `Views/Accounting/ChartOfAccountsView.xaml`  
**ViewModel:** `ViewModels/Accounting/ChartOfAccountsViewModel.cs`

The chart of accounts is inherently hierarchical (parent-child with levels 1-4), and the ViewModel exposes `AccountTree` with `ParentAccountId` and `Level`. However, the view renders it as a flat `DataGrid`.

**Impact:** Users cannot visually see the account hierarchy, expand/collapse branches, or understand parent-child relationships at a glance.

**Recommendation:** Replace the DataGrid with a `TreeView` using `HierarchicalDataTemplate`, or use a DataGrid with tree-column indentation (MaterialDesign TreeListView). Bind to a hierarchical collection structure.

---

### H-02: Sequential Async Calls Where Task.WhenAll Should Be Used

**Files affected:**

| File | Method | Sequential calls |
|------|--------|-----------------|
| `WarehouseViewModel.cs` | `LoadWarehousesAsync()` | `GetAllAsync()` + `GetAllAsync()` (2 awaits) |
| `ProductViewModel.cs` | `LoadProductsAsync()` | 4 sequential `GetAllAsync()` calls |
| `UserManagementViewModel.cs` | `LoadUsersAsync()` | `GetAllAsync()` + `GetAllAsync()` (2 awaits) |
| `PurchaseInvoiceDetailViewModel.cs` | `LoadLookupsAsync()` | 5 sequential `GetAllAsync()` calls |

**Pattern found:**
```csharp
var warehouseResult = await _warehouseService.GetAllAsync();
var accountResult = await _accountService.GetAllAsync();
// Each await waits for the previous to complete
```

**Recommendation:** Use `Task.WhenAll` as is already done in `SalesInvoiceDetailViewModel`:
```csharp
await Task.WhenAll(
    LoadSuppliersAsync(),
    LoadCustomersAsync(),
    LoadProductsAsync(),
    LoadWarehousesAsync()
);
```

---

### H-03: DashboardViewModel Creates Non-Frozen Brushes and Uses Non-Deterministic GetHashCode

**File:** `ViewModels/DashboardViewModel.cs`

```csharp
private static SolidColorBrush GetBrushForKey(string viewKey)
{
    // ... creates new SolidColorBrush each time, never frozen
    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
}
```

Also uses `string.GetHashCode()` for fallback color selection, which is non-deterministic across .NET versions/processes:
```csharp
var hash = Math.Abs(viewKey.GetHashCode());
var fallbackColors = new[] { ... };
return new SolidColorBrush((Color)ColorConverter.ConvertFromString(
    fallbackColors[hash % fallbackColors.Length]));
```

**Impact:** Unfrozen brushes prevent cross-thread access; hashcode instability means colors may change between sessions.

**Recommendation:** Freeze brushes after creation. Use a stable hash algorithm (e.g., FNV-1a on string bytes) for deterministic color assignment.

---

### H-04: DashboardViewModel DispatcherTimer Never Stops on Navigation Away

**File:** `ViewModels/DashboardViewModel.cs`

```csharp
_refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
_refreshTimer.Tick += (_, _) => EnqueueDbWork(LoadDataAsync);
_refreshTimer.Start();
```

The timer only stops in `Dispose()`, but the `DashboardViewModel` is registered as `Transient` and disposal depends on the tab being closed. If the user navigates away from the dashboard tab but doesn't close it, the timer keeps firing every 60 seconds, issuing database queries in the background.

**Recommendation:** Implement `INavigationAware` on `DashboardViewModel` to start/stop the timer on navigation enter/leave, or stop the timer when the dashboard tab loses focus.

---

### H-05: SalesInvoiceView Redundancy with List/Detail Pattern

**Files:**
- `Views/Sales/SalesInvoiceView.xaml` — Combined list + inline form
- `Views/Sales/SalesInvoiceListView.xaml` — Separate list view
- `Views/Sales/SalesInvoiceDetailView.xaml` — Separate full-screen detail view
- `ViewModels/Sales/SalesInvoiceViewModel.cs` — VM for combined view
- `ViewModels/Sales/SalesInvoiceListViewModel.cs` — VM for list-only view
- `ViewModels/Sales/SalesInvoiceDetailViewModel.cs` — VM for detail view

The combined `SalesInvoiceView` (list + inline DataGrid form) coexists with the separate `SalesInvoiceListView` + `SalesInvoiceDetailView` pattern. Both are registered in `ViewRegistry`:
```csharp
registry.Register<SalesInvoiceListView, SalesInvoiceListViewModel>("SalesInvoices", ...);
registry.Register<SalesInvoiceDetailView, SalesInvoiceDetailViewModel>("SalesInvoiceDetail", ...);
```

The combined `SalesInvoiceView` with `SalesInvoiceViewModel` appears to be unused (not registered for navigation with a unique key in the active registry).

**Impact:** Maintenance burden, confusion about which view is active, duplicated business logic between `SalesInvoiceViewModel` and `SalesInvoiceDetailViewModel`.

**Recommendation:** Remove the unused combined view (`SalesInvoiceView.xaml` + `SalesInvoiceViewModel.cs`) or document its purpose clearly if it's intended for a specific workflow.

---

### H-06: SalesInvoiceListViewModel.ApplyFilter() Is a Stub

**File:** `ViewModels/Sales/SalesInvoiceListViewModel.cs`

```csharp
// TODO: use CollectionViewSource
private void ApplyFilter() { }
```

The filter bar is visible in the UI but does nothing.

**Recommendation:** Implement filtering using `CollectionViewSource` or `ICollectionView.Filter`.

---

### H-07: EnqueueDbWork Fire-and-Forget May Swallow Exceptions

**File:** `ViewModels/BaseViewModel.cs`

`EnqueueDbWork()` queues async work behind `DbGuard` semaphore but exceptions are handled with a generic catch. If the UI doesn't observe the `ErrorMessage`, the user has no feedback.

**Impact:** Silent failures in background operations like smart entry refresh.

**Recommendation:** Add logging for all exceptions in `EnqueueDbWork`. Consider surfacing errors as notifications rather than relying on `ErrorMessage` binding.

---

### H-08: LoginWindow.xaml.cs Uses Service Locator Anti-Pattern

**File:** `Views/Shell/LoginWindow.xaml.cs`

```csharp
DataContext = ((App)System.Windows.Application.Current).GetRequiredService<LoginViewModel>();
```

While this works, it bypasses the DI container's normal resolution and creates a direct dependency on the `App` class.

**Recommendation:** Pass the ViewModel via constructor injection or use a `ViewModelLocator` pattern consistent with the rest of the application.

---

### H-09: PosWindow WindowStyle="None" Without Custom Title Bar Drag

**File:** `Views/Sales/PosWindow.xaml`

```xml
WindowState="Maximized" WindowStyle="None"
```

The POS window removes the system title bar entirely. While there's a `CloseWindowBehavior` for the exit button, there's no visible minimize/restore capability and no drag behavior on the header bar.

**Impact:** Users cannot move, minimize, or resize the POS window. If `WindowState` changes from `Maximized`, the window becomes stuck.

**Recommendation:** Add `WindowDragBehavior` to the header `Border` and provide minimize/restore buttons in the toolbar if needed.

---

### H-10: Static Mutable State in SessionSelections

**File:** `Common/SessionSelections.cs`

```csharp
public static class SessionSelections
{
    public static int? LastWarehouseId { get; set; }
}
```

Static mutable state is shared across the entire application lifetime. If multiple sessions (e.g., POS + main window) exist, they share this value without synchronization.

**Recommendation:** Move to a scoped service or singleton service with proper encapsulation.

---

### H-11: Hard-Coded Status Strings Throughout ViewModels

**Files:** All invoice/receipt/payment ViewModels

Status comparisons use magic strings:
```csharp
public bool IsDraft => CurrentInvoice != null && CurrentInvoice.Status == "Draft";
public bool IsPosted => CurrentInvoice != null && CurrentInvoice.Status == "Posted";
public bool IsCancelled => CurrentInvoice != null && CurrentInvoice.Status == "Cancelled";
```

And in ProductViewModel:
```csharp
foreach (var p in productsResult.Data.Where(x => x.Status == "Active"))
```

**Impact:** Typos cause silent bugs; status values are not centralized.

**Recommendation:** Use the `DomainConstants` class or a dedicated `StatusConstants` class for all status string comparisons.

---

### H-12: WarehouseViewModel.EditSelected() Double-Sets IsEditing

**Files:** `WarehouseViewModel.cs`, `ChartOfAccountsViewModel.cs`

```csharp
public void EditSelected()
{
    if (SelectedItem == null) return;
    IsEditing = true;
    IsNew = false;
    PopulateForm(SelectedItem); // Sets IsEditing = false internally
    IsEditing = true;           // Re-sets to true
}
```

The same pattern exists in `ChartOfAccountsViewModel.EditSelectedAccount()`. `PopulateForm` resets `IsEditing` to `false`, so the caller must set it again.

**Impact:** Unnecessary property change notifications; confusing control flow.

**Recommendation:** Refactor `PopulateForm` to not reset `IsEditing`, or add a parameter to control the behavior.

---

## MEDIUM Issues

### M-01: Font Is "Segoe UI" for Arabic Content

**Files:** `App.xaml`, `AppStyles.xaml`, `PosWindow.xaml`, `InvoiceAddLineWindow.xaml`, `SearchLookupWindow.xaml`

```xml
FontFamily="Segoe UI"
```

While Segoe UI supports Arabic, it's not optimized for Arabic text rendering. `Sakkal Majalla`, `Calibri`, or `Segoe UI` with `Cairo`/`Tajawal` fallback would provide better readability.

**Recommendation:** Define a font family with Arabic-optimized fallback:
```xml
<FontFamily x:Key="AppFont">Cairo, Sakkal Majalla, Segoe UI</FontFamily>
```

---

### M-02: No Accessibility Attributes (AutomationProperties)

**Files:** All XAML views

None of the views use `AutomationProperties.Name`, `AutomationProperties.AutomationId`, or `AutomationProperties.HelpText`. This prevents:
- Screen reader support for visually impaired users
- UI automation testing
- Compliance with accessibility standards

**Recommendation:** Add `AutomationProperties.Name` to all interactive controls, at minimum buttons and input fields.

---

### M-03: Dashboard UniformGrid With Fixed Columns Not Responsive

**File:** `Views/DashboardView.xaml`

```xml
<UniformGrid Columns="5" Margin="0,0,0,16">
```

The dashboard uses `UniformGrid` with fixed 5 columns. On smaller screens or lower resolutions, the cards become unreadably compressed.

**Recommendation:** Use a `WrapPanel` or responsive layout strategy. Consider checking `ActualWidth` and adjusting columns dynamically.

---

### M-04: Multiple Inline Styles Instead of Shared Resources

**Files:** Various views

Example from `GovernanceConsoleView.xaml`:
```xml
<DataGrid.ColumnHeaderStyle>
    <Style TargetType="DataGridColumnHeader">
        <Setter Property="Background" Value="{StaticResource BackgroundBrush}"/>
        <Setter Property="Foreground" Value="#333"/>
        ...
    </Style>
</DataGrid.ColumnHeaderStyle>
```

This same column header style is duplicated across multiple views.

**Recommendation:** Define shared DataGrid styles in `AppStyles.xaml` and reference them via `StaticResource`.

---

### M-05: SearchLookupWindow Has Significant Code-Behind

**Files:** `Views/Common/SearchLookupWindow.xaml`, `Views/Common/SearchLookupWindow.xaml.cs`

The SearchLookupWindow uses code-behind event handlers for: `SearchBox_TextChanged`, `DataGrid_MouseDoubleClick`, `DataGrid_KeyDown`, `SelectButton_Click`, `Window_PreviewKeyDown`.

**Impact:** Business logic (filtering, selection) is in code-behind rather than ViewModel, making it untestable.

**Recommendation:** Create a `SearchLookupViewModel` with proper commands and data binding. Use behaviors for keyboard shortcuts.

---

### M-06: InvoiceAddLineWindow Uses Code-Behind for Tab Navigation

**File:** `Views/Common/InvoiceAddLineWindow.xaml`

```xml
PreviewKeyDown="Field_EnterToNext"
```

Multiple fields use code-behind handlers for Enter-to-next-field navigation.

**Recommendation:** Use a reusable attached behavior (like `TextBoxEnterCommandBehavior` already exists) for consistent tab navigation.

---

### M-07: Tab Strip Lacks Mouse Wheel Scrolling

**File:** `Views/Shell/MainWindow.xaml`

The tab strip uses a horizontal `ScrollViewer` but there's no visible handling for mouse wheel events to scroll tabs left/right. With many open tabs, users must use the scroll arrows.

**Recommendation:** Add a `PreviewMouseWheel` handler (via behavior) that translates vertical wheel input to horizontal scroll.

---

### M-08: Hardcoded Colors in XAML Instead of Semantic Brushes

**Files:** Various

Examples:
- `PosWindow.xaml`: `Foreground="#FFCDD2"`, `Background="#546E7A"`, `Foreground="#388E3C"`
- `DashboardView.xaml`: Mostly uses semantic brushes (good), but some hardcoded values remain
- `GovernanceConsoleView.xaml`: `Foreground="#333"`, `Foreground="#666"`, `Foreground="#999"`

**Impact:** Theme switching won't update hardcoded colors; inconsistency with the design system.

**Recommendation:** Define additional semantic brushes in `AppStyles.xaml` (e.g., `TertiaryTextBrush`, `PosHeaderBrush`) and reference them.

---

### M-09: DirtyStateGuard Uses Synchronous MessageBox in Async Context

**File:** `Common/DirtyStateGuard.cs`

```csharp
public static async Task<bool> ConfirmContinueAsync(IDirtyStateAware vm)
{
    // ...
    var result = MessageBox.Show(
        UiStrings.UnsavedChangesMessage,
        UiStrings.UnsavedChangesTitle,
        MessageBoxButton.YesNoCancel, ...);
    // ...
}
```

The method is `async` but calls synchronous `MessageBox.Show`. While functionally correct (it blocks the UI thread), it's semantically misleading and blocks the message pump.

**Recommendation:** Use an async dialog service that returns `Task<MessageBoxResult>`.

---

### M-10: ViewModel Boilerplate Duplication

Nearly every ViewModel repeats the same pattern:
```csharp
IsBusy = true;
ClearError();
try
{
    var result = await _service.SomeOperationAsync();
    if (result.IsSuccess) { /* success */ }
    else { ErrorMessage = result.ErrorMessage; }
}
catch (ConcurrencyConflictException ex) { await ConcurrencyHelper.ShowConflictAndRefreshAsync(ex, LoadAsync); }
catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("العملية", ex); }
finally { IsBusy = false; }
```

**Recommendation:** Add a `RunBusyAsync` helper to `BaseViewModel`:
```csharp
protected async Task RunBusyAsync(Func<Task> action, string operationName)
{
    IsBusy = true; ClearError();
    try { await action(); }
    catch (ConcurrencyConflictException ex) { await ConcurrencyHelper.ShowConflictAsync(ex); }
    catch (Exception ex) { ErrorMessage = FriendlyErrorMessage(operationName, ex); }
    finally { IsBusy = false; }
}
```

---

### M-11: PosViewModel.CompleteSaleAsync Lacks Receipt Printing Integration

**File:** `ViewModels/Sales/PosViewModel.cs`

After a successful sale, the POS shows a status message but doesn't trigger receipt printing or ask the user if they want to print:
```csharp
if (result.IsSuccess)
{
    StatusMessage = $"✓ تمت عملية البيع — فاتورة: {result.Data.InvoiceNumber}...";
    CancelCart();
}
```

An `IReceiptPrinterService` is registered in DI (`WindowsEscPosPrinterService`), but the POS ViewModel doesn't inject or use it.

**Recommendation:** Inject `IReceiptPrinterService` and add a configurable auto-print or prompt-to-print after sale completion.

---

### M-12: Multiple Views Don't Set VirtualizingPanel Properties

**Files:** Several DataGrid views

While `SalesInvoiceListView.xaml` properly sets virtualization:
```xml
VirtualizingPanel.IsVirtualizing="True"
VirtualizingPanel.VirtualizationMode="Recycling"
```

Most other DataGrids (PosWindow cart, ChartOfAccountsView, CashReceiptView, JournalEntryView, ProductView, etc.) don't explicitly enable recycling virtualization.

**Recommendation:** Add virtualization properties to all DataGrids holding potentially large datasets.

---

### M-13: App.xaml.cs Is a 919-Line God Class

**File:** `App.xaml.cs`

The composition root contains:
- Global exception handling
- Database initialization + migration
- Seed data creation
- Startup integrity checks
- DI container configuration (~500 service registrations)
- View/ViewModel registry (~80 registrations)

**Recommendation:** Extract DI configuration into extension methods:
```csharp
services.AddDomainServices();
services.AddInfrastructureServices(connectionString);
services.AddApplicationServices();
services.AddWpfViews();
```

---

### M-14: PosWindow Search Popup Uses InputBindings for Double-Click (Potential RTL Issue)

**File:** `Views/Sales/PosWindow.xaml`

```xml
<ListBox.InputBindings>
    <MouseBinding Gesture="LeftDoubleClick"
                  Command="{Binding AddToCartCommand}"
                  CommandParameter="{Binding SelectedItem, RelativeSource={RelativeSource AncestorType=ListBox}}" />
</ListBox.InputBindings>
```

The `CommandParameter` binds to `SelectedItem` of the `ListBox` at the time of click, but `SelectedItem` may not be set until after the first click. Double-click behavior with `InputBindings` on `ListBox` can be unreliable.

**Recommendation:** Use `SelectionChanged` event via behavior or bind the `SelectedItem` explicitly with `UpdateSourceTrigger=PropertyChanged`.

---

### M-15: QuickTreasuryDialogService and InvoicePdfPreviewService Registered as Singletons

**File:** `App.xaml.cs`

```csharp
services.AddSingleton<IQuickTreasuryDialogService, QuickTreasuryDialogService>();
services.AddSingleton<IInvoicePdfPreviewService, InvoicePdfPreviewService>();
```

But `InvoiceTreasuryIntegrationService` is scoped:
```csharp
services.AddScoped<IInvoiceTreasuryIntegrationService, InvoiceTreasuryIntegrationService>();
```

Singleton services that show dialogs may hold stale references if they capture scoped services.

**Recommendation:** Verify that singleton dialog services don't capture scoped dependencies. Consider making them transient if they need scoped services.

---

## LOW Issues

### L-01: Missing Dispose in Some ViewModels

Several ViewModels subscribe to `PropertyChanged` events on child items (e.g., `PurchaseInvoiceDetailViewModel` hooks `FormLines` items) but only unhook when lines are explicitly removed. If the VM is disposed while lines are still hooked, event handlers may keep objects alive.

**Recommendation:** Override `Dispose(bool)` to call `UnhookAllLines()` in all invoice detail ViewModels.

---

### L-02: ReportHubViewModel Is Minimal — No Report Preview

**File:** `ViewModels/Reports/ReportHubViewModel.cs`

The Report Hub is a simple navigation launcher with no search, no favorites, no recently-used reports.

**Recommendation:** Add search filtering across report cards, recent/favorite reports, and description text for each report type.

---

### L-03: ShortcutConfigDialog Referenced But Not Analyzed

**File:** `ViewModels/DashboardViewModel.cs`

```csharp
var dialog = new ShortcutConfigDialog(AllScreens, savedKeys)
{
    Owner = System.Windows.Application.Current.MainWindow
};
```

Dialog is instantiated directly in ViewModel — same MVVM concern as C-01 but lower impact since it's a configuration dialog.

---

### L-04: PosProductLookupDto Searched via LINQ on In-Memory Cache

**File:** `ViewModels/Sales/PosViewModel.cs`

Product search in POS uses LINQ on the full cached collection:
```csharp
var unitBarcodeMatch = _productCache
    .Select(p => new { Product = p, Unit = p.Units?.FirstOrDefault(...) })
    .FirstOrDefault(x => x.Unit != null);
```

For large product catalogs (10,000+ items), this O(n) search runs on every character typed.

**Recommendation:** Build a `Dictionary<string, PosProductLookupDto>` index by barcode/code for O(1) lookup, and use prefix trie for name search.

---

### L-05: JournalLineFormItem Holds Reference to Parent ViewModel

**File:** `ViewModels/Accounting/JournalEntryViewModel.cs`

```csharp
public sealed class JournalLineFormItem : BaseViewModel
{
    private readonly JournalEntryViewModel _parent;
    public JournalLineFormItem(JournalEntryViewModel parent) { _parent = parent; }
}
```

Similar pattern in `PurchaseInvoiceLineFormItem`, `SalesInvoiceLineFormItem`. While functional, this creates tight coupling between line items and parent VMs.

**Recommendation:** Use events or `IInvoiceLineFormHost` interface (which already exists) consistently instead of direct VM references.

---

### L-06: StatusMessage Not Self-Clearing

Many ViewModels set `StatusMessage` after operations but never clear it. Status messages from previous operations persist until the next operation.

**Recommendation:** Add a timer-based auto-clear for `StatusMessage` in `BaseViewModel` (e.g., clear after 5 seconds), or clear on next operation start.

---

### L-07: ProductUnitFormItem Uses Math.Round with MidpointRounding Default

**File:** `ViewModels/Inventory/ProductViewModel.cs`

```csharp
SalePrice = Math.Round(baseSale / ConversionFactor, 4);
```

`Math.Round` defaults to `MidpointRounding.ToEven` (banker's rounding), which may surprise users expecting conventional rounding.

**Recommendation:** Use `Math.Round(value, 4, MidpointRounding.AwayFromZero)` for financial calculations, or use the `ILineCalculationService` for consistency.

---

### L-08: Inconsistent Command Type Declarations

Some ViewModels expose commands as `ICommand`:
```csharp
public ICommand LoadCommand { get; }
```

Others expose the concrete type:
```csharp
public AsyncRelayCommand LoadCommand { get; }
```

**Impact:** Minor inconsistency; concrete types allow calling `RaiseCanExecuteChanged()` but reduce flexibility.

**Recommendation:** Standardize on `ICommand` for public properties unless the concrete type is needed externally.

---

### L-09: GovernanceConsoleView Feature Toggle Uses `IsEnabled` Property Name Collision

**File:** `Views/Settings/GovernanceConsoleView.xaml`

```xml
<DataTrigger Binding="{Binding IsEnabled}" Value="False">
```

The feature DTO has a property `IsEnabled` which collides with WPF's `UIElement.IsEnabled`. While the `DataTrigger` correctly resolves to the DataContext's `IsEnabled`, this could cause confusion during maintenance.

**Recommendation:** Rename the DTO property to `IsFeatureEnabled` or `IsActive`.

---

### L-10: Dashboard Shortcut Cards Don't Reflect Feature Governance State

**File:** `ViewModels/DashboardViewModel.cs`

Dashboard shortcuts are loaded from a JSON file and displayed regardless of whether the corresponding feature is enabled in the governance console.

**Impact:** Users may see shortcuts for disabled features, leading to navigation errors.

**Recommendation:** Filter shortcuts against `IFeatureService` enabled features.

---

### L-11: No Loading Indicator on Dashboard Tiles During Refresh

**File:** `Views/DashboardView.xaml`  

Dashboard tiles show data but don't display a skeleton/shimmer or loading indicator while `IsBusy` is true during the 60-second auto-refresh.

**Recommendation:** Add a subtle loading overlay or skeleton animation bound to `IsBusy`.

---

### L-12: App.xaml Global TextBox Select-All Handler Not Scoped

**File:** `App.xaml.cs`

A global `EventManager` handler selects all text on focus for every `TextBox` in the application:
```csharp
EventManager.RegisterClassHandler(typeof(TextBox), TextBox.GotFocusEvent, ...);
```

This may conflict with TextBoxes where partial selection is expected (e.g., search boxes, multi-line notes).

**Recommendation:** Use the existing `SelectAllOnFocusBehavior` attached behavior only on specific fields, or add an opt-out attached property.

---

## Positive Observations

| Area | Finding |
|------|---------|
| **MVVM Architecture** | Clean ViewModel-first pattern with proper separation. `BaseViewModel` provides consistent infrastructure (INotifyPropertyChanged, IsBusy, ErrorMessage, DbGuard). |
| **Tab Navigation** | `TabNavigationService` is well-implemented with DI scopes per tab, SHA256-based parameter keys, and proper disposal. |
| **Dirty State Management** | `IDirtyStateAware` + `DirtyStateGuard` provides consistent unsaved-changes handling across all windows and tab switches. |
| **Concurrency Handling** | `DbGuard` semaphore prevents concurrent DB operations per VM. `ConcurrencyConflictException` is handled consistently. |
| **RTL Support** | Consistent `FlowDirection="RightToLeft"` at window/control level. Arabic strings used throughout. |
| **Design System** | `AppStyles.xaml` defines a coherent color palette with semantic brushes. Most views reference shared styles. |
| **Smart Entry System** | Inline stock/cost/last-price data on invoice line editing is well-designed with version-based stale-data protection. |
| **Invoice Lifecycle** | Clear Draft → Posted → Cancelled workflow with treasury integration prompts. |
| **Command Palette** | Global Ctrl+K search with debouncing and keyboard navigation. |
| **Keyboard Shortcuts** | Comprehensive shortcut mapping (Ctrl+N/S/E/R/P, F9, F1, Alt+Arrows, Ctrl+Tab). |
| **Authorization Proxy** | `AddAuthorizedService<>()` wraps all services with permission checks. |
| **Feature Governance** | Feature toggles with impact analysis, profiles, and dependency graph visualization. |
| **DataGrid Smart Entry Behavior** | Well-implemented Enter-key navigation and inline editing with F2/Escape support. |

---

## Recommended Priority Actions

| Priority | Action | Effort | Impact |
|----------|--------|--------|--------|
| 1 | Introduce `IDialogService` to replace all `MessageBox.Show` in VMs | HIGH | Enables unit testing of all 16+ files |
| 2 | Implement data paging for large collections | HIGH | Prevents performance issues at scale |
| 3 | Replace Chart of Accounts DataGrid with TreeView | MEDIUM | Critical UX improvement |
| 4 | Parallelize sequential async calls with `Task.WhenAll` | LOW | Easy performance wins |
| 5 | Implement `SalesInvoiceListViewModel.ApplyFilter()` | LOW | Completes existing feature |
| 6 | Secure password handling in LoginViewModel | MEDIUM | Security requirement |
| 7 | Extract App.xaml.cs DI config into extension methods | LOW | Maintainability |
| 8 | Add `AutomationProperties` for accessibility | MEDIUM | Accessibility compliance |
| 9 | Define shared DataGrid styles in AppStyles.xaml | LOW | Reduces XAML duplication |
| 10 | Implement DashboardVM timer lifecycle management | LOW | Prevents unnecessary DB queries |

---

*End of Audit Report*
