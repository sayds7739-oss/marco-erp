using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarcoERP.Application.Common;
using MarcoERP.Application.Interfaces.Settings;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace MarcoERP.WpfUI.Navigation
{
    public sealed class ViewRegistry : IViewRegistry
    {
        private readonly Dictionary<string, NavigationRoute> _routes = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Maps navigation keys to their required feature keys.
        /// Views NOT in this map are always accessible.
        /// </summary>
        private static readonly Dictionary<string, string> _featureMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // Accounting
            ["ChartOfAccounts"] = FeatureKeys.Accounting,
            ["JournalEntries"] = FeatureKeys.Accounting,
            ["FiscalPeriods"] = FeatureKeys.Accounting,
            ["FiscalYear"] = FeatureKeys.Accounting,
            ["OpeningBalance"] = FeatureKeys.Accounting,

            // Inventory
            ["Categories"] = FeatureKeys.Inventory,
            ["Units"] = FeatureKeys.Inventory,
            ["Products"] = FeatureKeys.Inventory,
            ["Warehouses"] = FeatureKeys.Inventory,
            ["BulkPriceUpdate"] = FeatureKeys.Inventory,
            ["InventoryAdjustments"] = FeatureKeys.Inventory,
            ["InventoryAdjustmentDetail"] = FeatureKeys.Inventory,
            ["ProductImport"] = FeatureKeys.Inventory,

            // Sales
            ["SalesInvoices"] = FeatureKeys.Sales,
            ["SalesInvoiceDetail"] = FeatureKeys.Sales,
            ["SalesReturns"] = FeatureKeys.Sales,
            ["SalesReturnDetail"] = FeatureKeys.Sales,
            ["Customers"] = FeatureKeys.Sales,
            ["SalesRepresentatives"] = FeatureKeys.Sales,
            ["PriceLists"] = FeatureKeys.Sales,
            ["SalesQuotations"] = FeatureKeys.Sales,
            ["SalesQuotationDetail"] = FeatureKeys.Sales,

            // Purchases
            ["PurchaseInvoices"] = FeatureKeys.Purchases,
            ["PurchaseInvoiceDetail"] = FeatureKeys.Purchases,
            ["PurchaseReturns"] = FeatureKeys.Purchases,
            ["PurchaseReturnDetail"] = FeatureKeys.Purchases,
            ["Suppliers"] = FeatureKeys.Purchases,
            ["PurchaseQuotations"] = FeatureKeys.Purchases,
            ["PurchaseQuotationDetail"] = FeatureKeys.Purchases,

            // Treasury
            ["Cashboxes"] = FeatureKeys.Treasury,
            ["BankAccounts"] = FeatureKeys.Treasury,
            ["BankReconciliation"] = FeatureKeys.Treasury,
            ["CashReceipts"] = FeatureKeys.Treasury,
            ["CashPayments"] = FeatureKeys.Treasury,
            ["CashTransfers"] = FeatureKeys.Treasury,

            // POS
            ["POS"] = FeatureKeys.POS,

            // Reporting
            ["Reports"] = FeatureKeys.Reporting,
            ["TrialBalance"] = FeatureKeys.Reporting,
            ["AccountStatement"] = FeatureKeys.Reporting,
            ["IncomeStatement"] = FeatureKeys.Reporting,
            ["BalanceSheet"] = FeatureKeys.Reporting,
            ["SalesReport"] = FeatureKeys.Reporting,
            ["PurchaseReport"] = FeatureKeys.Reporting,
            ["ProfitReport"] = FeatureKeys.Reporting,
            ["VatReport"] = FeatureKeys.Reporting,
            ["InventoryReport"] = FeatureKeys.Reporting,
            ["StockCard"] = FeatureKeys.Reporting,
            ["CashboxMovement"] = FeatureKeys.Reporting,
            ["AgingReport"] = FeatureKeys.Reporting,

            // User Management
            ["UserManagement"] = FeatureKeys.UserManagement,
            ["RoleManagement"] = FeatureKeys.UserManagement,
        };

        public void Register<TView, TViewModel>(string key, string title)
            where TView : UserControl
            where TViewModel : class
        {
            _routes[key] = new NavigationRoute(title, serviceProvider =>
            {
                var view = serviceProvider.GetRequiredService<TView>();
                var viewModel = serviceProvider.GetRequiredService<TViewModel>();
                view.DataContext = viewModel;
                return view;
            });
        }

        public void Register<TView, TViewModel>(string key, string title, PackIconKind iconKind, Brush iconBrush = null)
            where TView : UserControl
            where TViewModel : class
        {
            _routes[key] = new NavigationRoute(title, serviceProvider =>
            {
                var view = serviceProvider.GetRequiredService<TView>();
                var viewModel = serviceProvider.GetRequiredService<TViewModel>();
                view.DataContext = viewModel;
                return view;
            })
            {
                IconKind = iconKind,
                IconBrush = iconBrush
            };
        }

        public bool TryGet(string key, out NavigationRoute route)
        {
            return _routes.TryGetValue(key, out route);
        }

        public async Task<UserControl> CreateViewAsync(string key, IServiceProvider serviceProvider)
        {
            // Feature guard: check if the required feature is enabled
            if (_featureMap.TryGetValue(key, out var featureKey))
            {
                var featureService = serviceProvider.GetService<IFeatureService>();
                if (featureService != null)
                {
                    var result = await featureService.IsEnabledAsync(featureKey);
                    // Fail-closed: block navigation if feature is disabled OR if the check itself failed
                    if (!result.IsSuccess || !result.Data)
                    {
                        return new UserControl
                        {
                            Content = new TextBlock
                            {
                                Text = $"هذه الميزة معطلة حاليًا: {featureKey}",
                                FontSize = 18,
                                Margin = new Thickness(16),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        };
                    }
                }
            }

            if (TryGet(key, out var route))
            {
                return route.Factory(serviceProvider);
            }

            return new UserControl
            {
                Content = new TextBlock
                {
                    Text = $"صفحة غير مسجلة: {key}",
                    FontSize = 18,
                    Margin = new Thickness(16),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }
    }
}
