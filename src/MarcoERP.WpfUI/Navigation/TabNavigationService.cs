using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using MarcoERP.WpfUI.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace MarcoERP.WpfUI.Navigation
{
    public sealed class TabNavigationService : INavigationService, INotifyPropertyChanged
    {
        private readonly IViewRegistry _viewRegistry;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, TabEntry> _tabs = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _sync = new(1, 1);

        public TabNavigationService(IViewRegistry viewRegistry, IServiceProvider serviceProvider)
        {
            _viewRegistry = viewRegistry ?? throw new ArgumentNullException(nameof(viewRegistry));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public ObservableCollection<DocumentTab> OpenTabs { get; } = new();

        public DocumentTab ActiveTab { get; private set; }

        public UserControl CurrentView => ActiveTab?.View;

        public event EventHandler<NavigationChangedEventArgs> NavigationChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public void NavigateTo(string key)
        {
            NavigateTo(key, null);
        }

        public async void NavigateTo(string key, object parameter)
        {
            try
            {
                await NavigateToAsync(key, parameter);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabNavigationService] NavigateTo unhandled: {ex}");
            }
        }

        public async Task NavigateToAsync(string key, object parameter = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            await _sync.WaitAsync();
            try
            {
                var tabKey = BuildTabKey(key, parameter);
                if (_tabs.TryGetValue(tabKey, out var existing))
                {
                    await ActivateTabInternalAsync(existing.Tab);
                    return;
                }

                if (!await CanLeaveActiveTabAsync())
                    return;

                var scope = _serviceProvider.CreateScope();
                try
                {
                    var view = await _viewRegistry.CreateViewAsync(key, scope.ServiceProvider);
                    if (view.DataContext is INavigationAware aware)
                        await aware.OnNavigatedToAsync(parameter);

                    var title = _viewRegistry.TryGet(key, out var route) ? route.Title : key;
                    if (string.IsNullOrWhiteSpace(title))
                        title = key;
                    
                    var tab = new DocumentTab(tabKey, title, view, parameter)
                    {
                        ViewKey = key,
                        IconKind = route?.IconKind ?? MaterialDesignThemes.Wpf.PackIconKind.FileDocumentOutline,
                        IconBrush = route?.IconBrush
                    };

                    _tabs[tabKey] = new TabEntry(tab, scope);
                    OpenTabs.Add(tab);

                    await ActivateTabInternalAsync(tab, skipDirtyCheck: true);
                    Debug.WriteLine($"[TabNavigationService] Created tab: key={key}, tabKey={tabKey}, openTabs={OpenTabs.Count}");
                }
                catch
                {
                    scope.Dispose();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabNavigationService] Navigation to '{key}' failed: {ex.Message}");

                var errorView = CreateNavigationErrorView(key, ex);
                var title = _viewRegistry.TryGet(key, out var route) ? route.Title : key;
                NavigationChanged?.Invoke(this, new NavigationChangedEventArgs(key, title, errorView, parameter));
            }
            finally
            {
                _sync.Release();
            }
        }

        public async Task<bool> ActivateTabAsync(DocumentTab tab)
        {
            if (tab == null)
                return false;

            await _sync.WaitAsync();
            try
            {
                return await ActivateTabInternalAsync(tab);
            }
            finally
            {
                _sync.Release();
            }
        }

        public async Task<bool> CloseTabAsync(DocumentTab tab)
        {
            if (tab == null)
                return false;

            await _sync.WaitAsync();
            try
            {
                return await CloseTabInternalAsync(tab, confirmDirty: true);
            }
            finally
            {
                _sync.Release();
            }
        }

        public async Task CloseOtherTabsAsync(DocumentTab keepTab)
        {
            if (keepTab == null)
                return;

            await _sync.WaitAsync();
            try
            {
                var tabsToClose = OpenTabs.Where(t => !ReferenceEquals(t, keepTab)).ToList();
                foreach (var tab in tabsToClose)
                {
                    var closed = await CloseTabInternalAsync(tab, confirmDirty: true, activateFallback: false);
                    if (!closed)
                        return;
                }

                await ActivateTabInternalAsync(keepTab, skipDirtyCheck: true);
            }
            finally
            {
                _sync.Release();
            }
        }

        public async Task CloseAllTabsAsync()
        {
            await _sync.WaitAsync();
            try
            {
                var tabsToClose = OpenTabs.ToList();
                foreach (var tab in tabsToClose)
                {
                    var closed = await CloseTabInternalAsync(tab, confirmDirty: true, activateFallback: false);
                    if (!closed)
                        return;
                }

                SetActiveTabCore(null);
            }
            finally
            {
                _sync.Release();
            }
        }

        public void CloseView(string key, object parameter)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            var tabKey = BuildTabKey(key, parameter);
            if (!_tabs.TryGetValue(tabKey, out var entry))
                return;

            _tabs.Remove(tabKey);
            entry.Tab.UnhookDirtyTracking();
            OpenTabs.Remove(entry.Tab);
            DisposeTab(entry);

            if (ReferenceEquals(ActiveTab, entry.Tab))
            {
                SetActiveTabCore(OpenTabs.LastOrDefault());
            }

            Debug.WriteLine($"[TabNavigationService] Disposed tab via CloseView: key={key}, tabKey={tabKey}, openTabs={OpenTabs.Count}");
        }

        private async Task<bool> ActivateTabInternalAsync(DocumentTab tab, bool skipDirtyCheck = false)
        {
            if (tab == null)
                return false;

            if (ReferenceEquals(ActiveTab, tab))
                return true;

            if (!skipDirtyCheck && !await CanLeaveActiveTabAsync())
                return false;

            SetActiveTabCore(tab);
            Debug.WriteLine($"[TabNavigationService] Activated tab: key={tab.ViewKey}, tabKey={tab.TabKey}");
            NavigationChanged?.Invoke(this, new NavigationChangedEventArgs(tab.ViewKey, tab.Title, tab.View, tab.Parameter));
            return true;
        }

        private void SetActiveTabCore(DocumentTab tab)
        {
            foreach (var open in OpenTabs)
                open.IsActive = ReferenceEquals(open, tab);

            ActiveTab = tab;
            OnPropertyChanged(nameof(ActiveTab));
            OnPropertyChanged(nameof(CurrentView));
        }

        private async Task<bool> CloseTabInternalAsync(DocumentTab tab, bool confirmDirty, bool activateFallback = true)
        {
            if (tab == null)
                return false;

            if (confirmDirty && tab.View?.DataContext is IDirtyStateAware dirty)
            {
                var canClose = await DirtyStateGuard.ConfirmContinueAsync(dirty);
                if (!canClose)
                    return false;
            }

            if (!_tabs.Remove(tab.TabKey, out var entry))
                return false;

            var index = OpenTabs.IndexOf(tab);
            var wasActive = ReferenceEquals(ActiveTab, tab);

            tab.UnhookDirtyTracking();
            OpenTabs.Remove(tab);
            DisposeTab(entry);

            if (wasActive && activateFallback)
            {
                var next = index >= 0 && index < OpenTabs.Count
                    ? OpenTabs[index]
                    : OpenTabs.LastOrDefault();
                SetActiveTabCore(next);
                if (next != null)
                    NavigationChanged?.Invoke(this, new NavigationChangedEventArgs(next.ViewKey, next.Title, next.View, next.Parameter));
            }

            Debug.WriteLine($"[TabNavigationService] Disposed tab: key={tab.ViewKey}, tabKey={tab.TabKey}, openTabs={OpenTabs.Count}");
            return true;
        }

        private async Task<bool> CanLeaveActiveTabAsync()
        {
            if (CurrentView?.DataContext is not IDirtyStateAware dirty)
                return true;

            return await DirtyStateGuard.ConfirmContinueAsync(dirty);
        }

        private static void DisposeTab(TabEntry entry)
        {
            if (entry.Tab.View?.DataContext is IDisposable vmDisposable)
                vmDisposable.Dispose();

            if (entry.Tab.View is IDisposable viewDisposable)
                viewDisposable.Dispose();

            entry.Scope.Dispose();
        }

        private static string BuildTabKey(string key, object parameter)
        {
            if (parameter == null)
                return key;

            var hash = ComputeStableHash(parameter);
            return $"{key}:{hash}";
        }

        private static string ComputeStableHash(object parameter)
        {
            string raw;
            try
            {
                raw = parameter switch
                {
                    string s => s,
                    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                    _ => JsonSerializer.Serialize(parameter)
                };
            }
            catch
            {
                raw = parameter.ToString() ?? string.Empty;
            }

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
        }

        private static UserControl CreateNavigationErrorView(string key, Exception ex)
        {
            return new UserControl
            {
                Content = new System.Windows.Controls.Border
                {
                    Margin = new System.Windows.Thickness(16),
                    Padding = new System.Windows.Thickness(16),
                    BorderThickness = new System.Windows.Thickness(1),
                    BorderBrush = System.Windows.Media.Brushes.IndianRed,
                    Child = new System.Windows.Controls.StackPanel
                    {
                        Children =
                        {
                            new System.Windows.Controls.TextBlock
                            {
                                Text = "تعذر فتح الشاشة المطلوبة",
                                FontSize = 18,
                                FontWeight = System.Windows.FontWeights.SemiBold,
                                Margin = new System.Windows.Thickness(0, 0, 0, 8)
                            },
                            new System.Windows.Controls.TextBlock
                            {
                                Text = $"المفتاح: {key}",
                                FontSize = 13,
                                Margin = new System.Windows.Thickness(0, 0, 0, 6)
                            },
                            new System.Windows.Controls.TextBlock
                            {
                                Text = ex.Message,
                                FontSize = 12,
                                TextWrapping = System.Windows.TextWrapping.Wrap
                            }
                        }
                    }
                }
            };
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private sealed class TabEntry
        {
            public TabEntry(DocumentTab tab, IServiceScope scope)
            {
                Tab = tab;
                Scope = scope;
            }

            public DocumentTab Tab { get; }
            public IServiceScope Scope { get; }
        }
    }
}