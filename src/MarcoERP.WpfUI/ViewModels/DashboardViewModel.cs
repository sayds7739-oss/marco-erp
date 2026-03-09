using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MarcoERP.Application.DTOs.Reports;
using MarcoERP.Application.Interfaces.Reports;
using MarcoERP.WpfUI.Models;
using MarcoERP.WpfUI.Navigation;
using MarcoERP.WpfUI.Views.Shell;
using MaterialDesignThemes.Wpf;

namespace MarcoERP.WpfUI.ViewModels
{
    /// <summary>
    /// ViewModel for the main Dashboard view.
    /// Displays key business metrics and alerts.
    /// </summary>
    public sealed class DashboardViewModel : BaseViewModel
    {
        private readonly IReportService _reportService;
        private readonly INavigationService _navigationService;
        private readonly DispatcherTimer _refreshTimer;

        private static readonly string ShortcutsFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dashboard_shortcuts.json");

        /// <summary>All navigable screens with their metadata.</summary>
        private static readonly List<(string Key, string Title, string IconKind)> AllScreens = new()
        {
            ("SalesInvoices",        "فواتير البيع",      "ReceiptLong"),
            ("SalesInvoiceDetail",   "فاتورة بيع جديدة",  "CashRegister"),
            ("SalesReturns",         "مرتجعات البيع",     "UndoVariant"),
            ("Customers",            "العملاء",           "AccountGroup"),
            ("PriceLists",           "قوائم الأسعار",     "TagMultiple"),
            ("SalesQuotations",      "عروض الأسعار",      "FormatListBulleted"),
            ("PurchaseInvoices",     "فواتير الشراء",     "CartArrowDown"),
            ("PurchaseInvoiceDetail","فاتورة شراء جديدة", "CartOutline"),
            ("PurchaseReturns",      "مرتجعات الشراء",    "CartRemove"),
            ("Suppliers",            "الموردين",          "TruckDeliveryOutline"),
            ("PurchaseQuotations",   "طلبات الشراء",      "ClipboardTextOutline"),
            ("Products",             "الأصناف",           "PackageVariant"),
            ("Categories",           "التصنيفات",         "Shape"),
            ("Warehouses",           "المخازن",           "Warehouse"),
            ("InventoryAdjustments", "تسويات المخزون",    "PackageVariantClosedCheck"),
            ("CashReceipts",         "سندات القبض",       "CashPlus"),
            ("CashPayments",         "سندات الصرف",       "CashMinus"),
            ("CashTransfers",        "التحويلات",         "SwapHorizontal"),
            ("Cashboxes",            "الخزن",             "Safe"),
            ("BankAccounts",         "الحسابات البنكية",  "Bank"),
            ("ChartOfAccounts",      "شجرة الحسابات",     "FileTree"),
            ("JournalEntries",       "القيود اليومية",    "BookOpenPageVariant"),
            ("Reports",              "التقارير",          "ChartBar"),
            ("TrialBalance",         "ميزان المراجعة",    "Scale"),
            ("AccountStatement",     "كشف حساب",          "FileDocumentOutline"),
        };

        /// <summary>Default icon colors mapped by ViewKey for visual distinction.</summary>
        private static readonly Dictionary<string, string> DefaultColorMap = new()
        {
            ["SalesInvoiceDetail"]   = "#4CAF50", // Green
            ["PurchaseInvoiceDetail"]= "#FF9800", // Amber
            ["CashReceipts"]         = "#2196F3", // Blue
            ["CashPayments"]         = "#F44336", // Red
            ["Products"]             = "#FFC107", // Yellow
            ["Customers"]            = "#9C27B0", // Purple
        };

        private static readonly List<DashboardShortcut> DefaultShortcuts = new()
        {
            new() { ViewKey = "SalesInvoiceDetail",    Title = "فاتورة بيع",  IconKind = "CashRegister"  },
            new() { ViewKey = "PurchaseInvoiceDetail",  Title = "فاتورة شراء", IconKind = "CartOutline"   },
            new() { ViewKey = "CashReceipts",           Title = "سندات القبض", IconKind = "CashPlus"      },
            new() { ViewKey = "CashPayments",           Title = "سندات الصرف", IconKind = "CashMinus"     },
            new() { ViewKey = "Products",               Title = "الأصناف",     IconKind = "PackageVariant" },
            new() { ViewKey = "Customers",              Title = "العملاء",     IconKind = "AccountGroup"  },
        };

        public DashboardViewModel(IReportService reportService, INavigationService navigationService)
        {
            _reportService = reportService;
            _navigationService = navigationService;

            RefreshCommand = new AsyncRelayCommand(LoadDataAsync);
            ConfigureShortcutsCommand = new RelayCommand(ShowConfigureShortcutsDialog);
            ConfigureKpiCommand = new RelayCommand(ShowConfigureKpiDialog);

            LoadKpiConfig();
            LoadShortcuts();
            EnqueueDbWork(LoadDataAsync);

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _refreshTimer.Tick += OnRefreshTimerTick;
            _refreshTimer.Start();
        }

        private void OnRefreshTimerTick(object sender, EventArgs e)
        {
            EnqueueDbWork(LoadDataAsync);
        }

        // ── Shortcut Items ──
        public ObservableCollection<DashboardShortcutItem> Shortcuts { get; } = new();

        public ICommand ConfigureShortcutsCommand { get; }

        // ── Shortcut persistence ──

        private void LoadShortcuts()
        {
            List<DashboardShortcut> saved;
            try
            {
                if (File.Exists(ShortcutsFilePath))
                {
                    var json = File.ReadAllText(ShortcutsFilePath);
                    saved = JsonSerializer.Deserialize<List<DashboardShortcut>>(json) ?? DefaultShortcuts;
                }
                else
                {
                    saved = DefaultShortcuts;
                }
            }
            catch
            {
                saved = DefaultShortcuts;
            }

            RebuildShortcutItems(saved);
        }

        private void SaveShortcuts(List<DashboardShortcut> shortcuts)
        {
            try
            {
                var json = JsonSerializer.Serialize(shortcuts, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ShortcutsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardVM] Failed to save shortcuts: {ex.Message}");
            }
        }

        private void RebuildShortcutItems(List<DashboardShortcut> shortcuts)
        {
            Shortcuts.Clear();
            foreach (var sc in shortcuts)
            {
                var iconKind = PackIconKind.OpenInNew;
                if (Enum.TryParse<PackIconKind>(sc.IconKind, true, out var parsed))
                    iconKind = parsed;

                var brush = GetBrushForKey(sc.ViewKey);
                var viewKey = sc.ViewKey;

                Shortcuts.Add(new DashboardShortcutItem
                {
                    Title = sc.Title,
                    IconKind = iconKind,
                    IconBrush = brush,
                    NavigateCommand = new RelayCommand(() => _navigationService.NavigateTo(viewKey))
                });
            }
        }

        private static SolidColorBrush GetBrushForKey(string viewKey)
        {
            if (DefaultColorMap.TryGetValue(viewKey, out var hex))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

            // Fallback: hash-based color for any screen
            var hash = Math.Abs(viewKey.GetHashCode());
            var fallbackColors = new[] { "#4CAF50", "#2196F3", "#FF9800", "#9C27B0", "#009688", "#795548" };
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                fallbackColors[hash % fallbackColors.Length]));
        }

        private void ShowConfigureShortcutsDialog()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var currentKeys = Shortcuts.Select((s, i) =>
                {
                    // Reverse-lookup the ViewKey from the AllScreens list by title match
                    var match = AllScreens.FirstOrDefault(a => a.Title == s.Title
                        || DefaultShortcuts.Any(d => d.Title == s.Title && d.ViewKey == a.Key));
                    return match.Key;
                }).Where(k => k != null).ToList();

                // Also match by icon for accuracy
                var saved = LoadShortcutsFromFile();
                var savedKeys = saved.Select(s => s.ViewKey).ToList();

                var dialog = new ShortcutConfigDialog(AllScreens, savedKeys)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                if (dialog.ShowDialog() == true && dialog.SelectedKeys.Count > 0)
                {
                    var newShortcuts = dialog.SelectedKeys
                        .Take(6)
                        .Select(key =>
                        {
                            var screen = AllScreens.FirstOrDefault(s => s.Key == key);
                            return new DashboardShortcut
                            {
                                ViewKey = key,
                                Title = screen.Title ?? key,
                                IconKind = screen.IconKind ?? "OpenInNew"
                            };
                        })
                        .ToList();

                    SaveShortcuts(newShortcuts);
                    RebuildShortcutItems(newShortcuts);
                }
            });
        }

        private List<DashboardShortcut> LoadShortcutsFromFile()
        {
            try
            {
                if (File.Exists(ShortcutsFilePath))
                {
                    var json = File.ReadAllText(ShortcutsFilePath);
                    return JsonSerializer.Deserialize<List<DashboardShortcut>>(json) ?? DefaultShortcuts;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] Failed to load shortcuts: {ex.Message}");
            }
            return DefaultShortcuts;
        }

        // ── Commands ──
        public ICommand RefreshCommand { get; }

        // ── Today ──
        private decimal _todaySales;
        public decimal TodaySales { get => _todaySales; set => SetProperty(ref _todaySales, value); }

        private decimal _todayPurchases;
        public decimal TodayPurchases { get => _todayPurchases; set => SetProperty(ref _todayPurchases, value); }

        private decimal _todayReceipts;
        public decimal TodayReceipts { get => _todayReceipts; set => SetProperty(ref _todayReceipts, value); }

        private decimal _todayPayments;
        public decimal TodayPayments { get => _todayPayments; set => SetProperty(ref _todayPayments, value); }

        private int _todaySalesCount;
        public int TodaySalesCount { get => _todaySalesCount; set => SetProperty(ref _todaySalesCount, value); }

        private int _todayPurchasesCount;
        public int TodayPurchasesCount { get => _todayPurchasesCount; set => SetProperty(ref _todayPurchasesCount, value); }

        private decimal _dailyNetProfit;
        public decimal DailyNetProfit { get => _dailyNetProfit; set => SetProperty(ref _dailyNetProfit, value); }

        private decimal _todaySalesDelta;
        public decimal TodaySalesDelta { get => _todaySalesDelta; set => SetProperty(ref _todaySalesDelta, value); }

        private decimal _todayPurchasesDelta;
        public decimal TodayPurchasesDelta { get => _todayPurchasesDelta; set => SetProperty(ref _todayPurchasesDelta, value); }

        private decimal _todayReceiptsDelta;
        public decimal TodayReceiptsDelta { get => _todayReceiptsDelta; set => SetProperty(ref _todayReceiptsDelta, value); }

        private decimal _todayPaymentsDelta;
        public decimal TodayPaymentsDelta { get => _todayPaymentsDelta; set => SetProperty(ref _todayPaymentsDelta, value); }

        // ── Month ──
        private decimal _monthSales;
        public decimal MonthSales { get => _monthSales; set => SetProperty(ref _monthSales, value); }

        private decimal _monthPurchases;
        public decimal MonthPurchases { get => _monthPurchases; set => SetProperty(ref _monthPurchases, value); }

        private decimal _monthReceipts;
        public decimal MonthReceipts { get => _monthReceipts; set => SetProperty(ref _monthReceipts, value); }

        private decimal _monthPayments;
        public decimal MonthPayments { get => _monthPayments; set => SetProperty(ref _monthPayments, value); }

        private decimal _grossMarginPercent;
        public decimal GrossMarginPercent { get => _grossMarginPercent; set => SetProperty(ref _grossMarginPercent, value); }

        private decimal _monthSalesDelta;
        public decimal MonthSalesDelta { get => _monthSalesDelta; set => SetProperty(ref _monthSalesDelta, value); }

        private decimal _monthPurchasesDelta;
        public decimal MonthPurchasesDelta { get => _monthPurchasesDelta; set => SetProperty(ref _monthPurchasesDelta, value); }

        private decimal _monthReceiptsDelta;
        public decimal MonthReceiptsDelta { get => _monthReceiptsDelta; set => SetProperty(ref _monthReceiptsDelta, value); }

        private decimal _monthPaymentsDelta;
        public decimal MonthPaymentsDelta { get => _monthPaymentsDelta; set => SetProperty(ref _monthPaymentsDelta, value); }

        // ── Alerts ──
        private int _lowStockCount;
        public int LowStockCount { get => _lowStockCount; set => SetProperty(ref _lowStockCount, value); }

        private int _totalProducts;
        public int TotalProducts { get => _totalProducts; set => SetProperty(ref _totalProducts, value); }

        private int _pendingSalesInvoices;
        public int PendingSalesInvoices { get => _pendingSalesInvoices; set => SetProperty(ref _pendingSalesInvoices, value); }

        private int _pendingPurchaseInvoices;
        public int PendingPurchaseInvoices { get => _pendingPurchaseInvoices; set => SetProperty(ref _pendingPurchaseInvoices, value); }

        private int _pendingJournalEntries;
        public int PendingJournalEntries { get => _pendingJournalEntries; set => SetProperty(ref _pendingJournalEntries, value); }

        // ── Running Balances ──
        private decimal _cashBalance;
        public decimal CashBalance { get => _cashBalance; set => SetProperty(ref _cashBalance, value); }

        private decimal _totalCustomerBalance;
        public decimal TotalCustomerBalance { get => _totalCustomerBalance; set => SetProperty(ref _totalCustomerBalance, value); }

        private decimal _totalSupplierBalance;
        public decimal TotalSupplierBalance { get => _totalSupplierBalance; set => SetProperty(ref _totalSupplierBalance, value); }

        private decimal _monthGrossProfit;
        public decimal MonthGrossProfit { get => _monthGrossProfit; set => SetProperty(ref _monthGrossProfit, value); }

        // ── Chart Data ──
        public ObservableCollection<DailyTrendPoint> SalesTrendData { get; } = new();
        public ObservableCollection<TopProductDto> TopProductsData { get; } = new();

        // ── KPI Widget Visibility (Feature 17) ──
        private static readonly string KpiConfigFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dashboard_kpi_config.json");

        private bool _showTodaySection = true;
        public bool ShowTodaySection { get => _showTodaySection; set { if (SetProperty(ref _showTodaySection, value)) SaveKpiConfig(); } }

        private bool _showMonthSection = true;
        public bool ShowMonthSection { get => _showMonthSection; set { if (SetProperty(ref _showMonthSection, value)) SaveKpiConfig(); } }

        private bool _showAlertsSection = true;
        public bool ShowAlertsSection { get => _showAlertsSection; set { if (SetProperty(ref _showAlertsSection, value)) SaveKpiConfig(); } }

        private bool _showBalancesSection = true;
        public bool ShowBalancesSection { get => _showBalancesSection; set { if (SetProperty(ref _showBalancesSection, value)) SaveKpiConfig(); } }

        private bool _showChartsSection = true;
        public bool ShowChartsSection { get => _showChartsSection; set { if (SetProperty(ref _showChartsSection, value)) SaveKpiConfig(); } }

        private bool _showShortcutsSection = true;
        public bool ShowShortcutsSection { get => _showShortcutsSection; set { if (SetProperty(ref _showShortcutsSection, value)) SaveKpiConfig(); } }

        public ICommand ConfigureKpiCommand { get; }

        private void LoadKpiConfig()
        {
            try
            {
                if (File.Exists(KpiConfigFilePath))
                {
                    var json = File.ReadAllText(KpiConfigFilePath);
                    var cfg = JsonSerializer.Deserialize<KpiWidgetConfig>(json);
                    if (cfg != null)
                    {
                        _showTodaySection = cfg.ShowToday;
                        _showMonthSection = cfg.ShowMonth;
                        _showAlertsSection = cfg.ShowAlerts;
                        _showBalancesSection = cfg.ShowBalances;
                        _showChartsSection = cfg.ShowCharts;
                        _showShortcutsSection = cfg.ShowShortcuts;
                    }
                }
            }
            catch { /* use defaults */ }
        }

        private void SaveKpiConfig()
        {
            try
            {
                var cfg = new KpiWidgetConfig
                {
                    ShowToday = ShowTodaySection,
                    ShowMonth = ShowMonthSection,
                    ShowAlerts = ShowAlertsSection,
                    ShowBalances = ShowBalancesSection,
                    ShowCharts = ShowChartsSection,
                    ShowShortcuts = ShowShortcutsSection
                };
                var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(KpiConfigFilePath, json);
            }
            catch { /* non-critical */ }
        }

        private void ShowConfigureKpiDialog()
        {
            // Toggle visibility directly — the section bindings auto-update
            // A simple dialog with checkboxes would be ideal, but for now
            // we expose the toggle properties and let the dashboard UI
            // offer a settings gear that opens a popup.
        }

        private async Task LoadDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            ClearError();

            try
            {
                var result = await _reportService.GetDashboardSummaryAsync();
                if (result.IsSuccess && result.Data != null)
                {
                    var d = result.Data;
                    TodaySales = d.TodaySales;
                    TodayPurchases = d.TodayPurchases;
                    TodayReceipts = d.TodayReceipts;
                    TodayPayments = d.TodayPayments;
                    TodaySalesCount = d.TodaySalesCount;
                    TodayPurchasesCount = d.TodayPurchasesCount;
                    DailyNetProfit = d.DailyNetProfit;
                    TodaySalesDelta = d.TodaySalesDelta;
                    TodayPurchasesDelta = d.TodayPurchasesDelta;
                    TodayReceiptsDelta = d.TodayReceiptsDelta;
                    TodayPaymentsDelta = d.TodayPaymentsDelta;
                    MonthSales = d.MonthSales;
                    MonthPurchases = d.MonthPurchases;
                    MonthReceipts = d.MonthReceipts;
                    MonthPayments = d.MonthPayments;
                    GrossMarginPercent = d.GrossMarginPercent;
                    MonthSalesDelta = d.MonthSalesDelta;
                    MonthPurchasesDelta = d.MonthPurchasesDelta;
                    MonthReceiptsDelta = d.MonthReceiptsDelta;
                    MonthPaymentsDelta = d.MonthPaymentsDelta;
                    LowStockCount = d.LowStockCount;
                    TotalProducts = d.TotalProducts;
                    PendingSalesInvoices = d.PendingSalesInvoices;
                    PendingPurchaseInvoices = d.PendingPurchaseInvoices;
                    PendingJournalEntries = d.PendingJournalEntries;
                    CashBalance = d.CashBalance;
                    TotalCustomerBalance = d.TotalCustomerBalance;
                    TotalSupplierBalance = d.TotalSupplierBalance;
                    MonthGrossProfit = d.MonthGrossProfit;
                    StatusMessage = "تم تحديث البيانات";
                }
                else
                {
                    ErrorMessage = result.ErrorMessage ?? "فشل تحميل بيانات لوحة التحكم.";
                }

                // Load chart data in parallel
                var trendTask = _reportService.GetWeeklySalesTrendAsync(4);
                var topTask = _reportService.GetTopProductsAsync(5);

                await Task.WhenAll(trendTask, topTask);

                var trendResult = trendTask.Result;
                if (trendResult.IsSuccess && trendResult.Data != null)
                {
                    SalesTrendData.Clear();
                    foreach (var point in trendResult.Data)
                        SalesTrendData.Add(point);
                }

                var topResult = topTask.Result;
                if (topResult.IsSuccess && topResult.Data != null)
                {
                    TopProductsData.Clear();
                    foreach (var item in topResult.Data)
                        TopProductsData.Add(item);
                }
            }
            catch (System.Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("العملية", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_refreshTimer != null)
                {
                    _refreshTimer.Stop();
                    _refreshTimer.Tick -= OnRefreshTimerTick;
                }
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Display item for a dashboard shortcut card, including the resolved icon and command.
    /// </summary>
    public sealed class DashboardShortcutItem
    {
        public string Title { get; set; } = string.Empty;
        public PackIconKind IconKind { get; set; }
        public Brush IconBrush { get; set; } = Brushes.Gray;
        public ICommand NavigateCommand { get; set; }
    }

    /// <summary>Persisted KPI widget visibility configuration.</summary>
    public sealed class KpiWidgetConfig
    {
        public bool ShowToday { get; set; } = true;
        public bool ShowMonth { get; set; } = true;
        public bool ShowAlerts { get; set; } = true;
        public bool ShowBalances { get; set; } = true;
        public bool ShowCharts { get; set; } = true;
        public bool ShowShortcuts { get; set; } = true;
    }
}
