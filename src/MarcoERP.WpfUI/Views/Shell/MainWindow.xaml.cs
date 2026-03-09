using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MarcoERP.WpfUI.ViewModels.Shell;
using MarcoERP.WpfUI.ViewModels;
using MarcoERP.WpfUI.ViewModels.Settings;
using MarcoERP.WpfUI.Views.Settings;
using MarcoERP.WpfUI.Navigation;
using MarcoERP.WpfUI.Services;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Security;
using MarcoERP.Application.Interfaces.Settings;

namespace MarcoERP.WpfUI.Views.Shell
{
    /// <summary>
    /// Main application shell (MVVM only, no navigation logic).
    /// Phase 7B: Hidden governance activation trigger (5 clicks / 3s + Ctrl+Shift+Alt+G).
    /// AUTH-08: Session timeout auto-logout after configurable inactivity period.
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _allowClose;
        private DispatcherTimer _sessionTimer;
        private IActivityTracker _activityTracker;
        private IWindowService _windowService;
        private int _sessionTimeoutMinutes = 30;

        // ── Phase 7B: Click counter for hidden trigger ──────────
        private int _governanceClickCount;
        private DateTime _governanceFirstClickTime;
        private const int RequiredClicks = 5;
        private static readonly TimeSpan ClickWindow = TimeSpan.FromSeconds(3);

        public MainWindow()
            : this(App.Services?.GetRequiredService<MainWindowViewModel>())
        {
        }

        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Phase 7B: Register Ctrl+Shift+Alt+G keyboard shortcut
            InputBindings.Add(new KeyBinding(
                new RelayCommand(_ => OpenGovernanceAuth()),
                new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt)));

            // AUTH-08: Initialize session timeout monitoring
            InitializeSessionTimeout();
        }

        /// <summary>
        /// AUTH-08: Sets up activity tracking and idle timer for session timeout.
        /// Hooks PreviewMouseMove and PreviewKeyDown for input detection.
        /// </summary>
        private void InitializeSessionTimeout()
        {
            try
            {
                _activityTracker = App.Services?.GetService<IActivityTracker>();
                _windowService = App.Services?.GetService<IWindowService>();
                var config = App.Services?.GetService<IConfiguration>();

                if (_activityTracker == null || _windowService == null) return;

                // Read configurable timeout from appsettings
                var timeoutSetting = config?.GetValue<int>("AppSettings:SessionTimeoutMinutes");
                if (timeoutSetting.HasValue && timeoutSetting.Value > 0)
                    _sessionTimeoutMinutes = timeoutSetting.Value;

                // Hook input events to track activity
                PreviewMouseMove += (_, _) => _activityTracker.RecordActivity();
                PreviewKeyDown += (_, _) => _activityTracker.RecordActivity();
                PreviewMouseDown += (_, _) => _activityTracker.RecordActivity();

                // Check every 60 seconds for idle timeout
                _sessionTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(60)
                };
                _sessionTimer.Tick += SessionTimer_Tick;
                _sessionTimer.Start();
            }
            catch
            {
                // Fail silently — session timeout is a safety feature, not critical path
            }
        }

        /// <summary>AUTH-08: Checks if session is idle and forces logout.</summary>
        private void SessionTimer_Tick(object sender, EventArgs e)
        {
            if (_activityTracker == null || _windowService == null) return;

            if (_activityTracker.IsIdle(TimeSpan.FromMinutes(_sessionTimeoutMinutes)))
            {
                _sessionTimer?.Stop();

                MessageBox.Show(
                    $"تم تسجيل الخروج تلقائياً بسبب عدم النشاط لمدة {_sessionTimeoutMinutes} دقيقة.",
                    "انتهاء الجلسة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                _windowService.LogoutToLogin();
            }
        }

        /// <summary>Phase 7B: 5 clicks within 3 seconds on the shield icon.</summary>
        private void GovernanceTrigger_Click(object sender, MouseButtonEventArgs e)
        {
            // TODO: Replace with IDateTimeProvider when refactored — code-behind cannot easily use DI
            var now = DateTime.UtcNow;

            if (_governanceClickCount == 0 || (now - _governanceFirstClickTime) > ClickWindow)
            {
                _governanceClickCount = 1;
                _governanceFirstClickTime = now;
            }
            else
            {
                _governanceClickCount++;
            }

            if (_governanceClickCount >= RequiredClicks)
            {
                _governanceClickCount = 0;
                OpenGovernanceAuth();
            }
        }

        /// <summary>
        /// Phase 7C: Open SuperAdminAuthenticationDialog.
        /// 7G: Opens as modal dialog — NOT through NavigationService.
        /// 7E: Does NOT change CurrentUser session.
        /// </summary>
        private void OpenGovernanceAuth()
        {
            var scope = App.Services.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
            var auditService = scope.ServiceProvider.GetRequiredService<IGovernanceAuditService>();
            var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            var vm = new SuperAdminAuthViewModel(authService, auditService, dateTimeProvider);
            var dialog = new SuperAdminAuthDialog
            {
                DataContext = vm,
                Owner = this
            };

            vm.RequestClose = success =>
            {
                dialog.DialogResult = success;
                dialog.Close();
            };

            var result = dialog.ShowDialog();

            if (result == true && !string.IsNullOrEmpty(vm.AuthenticatedUsername))
            {
                // 7G: Open GovernanceConsole as modal dialog — not through navigation
                OpenGovernanceConsole(vm.AuthenticatedUsername, scope);
            }
            else
            {
                scope.Dispose();
            }
        }

        /// <summary>
        /// Phase 7G: Opens Governance Console in a standalone modal Window (not NavigationService).
        /// Phase 7E: Passes governance username as context — does NOT modify ICurrentUserService.
        /// </summary>
        private void OpenGovernanceConsole(string governanceUser, IServiceScope scope)
        {
            var consoleView = scope.ServiceProvider.GetRequiredService<GovernanceConsoleView>();
            var consoleVm = scope.ServiceProvider.GetRequiredService<MarcoERP.WpfUI.ViewModels.Settings.GovernanceConsoleViewModel>();
            consoleView.DataContext = consoleVm;

            var governanceWindow = new Window
            {
                Title = $"وحدة التحكم — {governanceUser}",
                Content = consoleView,
                Width = 900,
                Height = 580,
                MinWidth = 700,
                MinHeight = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                FlowDirection = FlowDirection.RightToLeft,
                Background = System.Windows.Media.Brushes.White,
                ResizeMode = ResizeMode.CanResizeWithGrip
            };

            governanceWindow.Closed += (_, _) => scope.Dispose();
            governanceWindow.ShowDialog();
        }

        private void CommandPaletteOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
                vm.IsCommandPaletteOpen = false;
        }

        private void CommandPaletteInner_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Prevent the overlay click-to-close from firing when clicking inside the palette
            e.Handled = true;
        }

        private void SearchBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.IsCommandPaletteOpen = true;
                vm.CommandPaletteSearch = string.Empty;
                // Focus the command palette search box after it renders
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
                {
                    CommandPaletteSearchBox?.Focus();
                    System.Windows.Input.Keyboard.Focus(CommandPaletteSearchBox);
                });
            }
        }

        private async void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            // AUTH-08: Stop session timer to prevent resource leak
            _sessionTimer?.Stop();
            _sessionTimer = null;

            if (_allowClose)
                return;

            if (DataContext is not MainWindowViewModel vm)
                return;

            // Check ALL open tabs for unsaved changes, not just the active one
            var dirtyTabs = vm.OpenTabs
                .Where(t => t.View?.DataContext is IDirtyStateAware d && d.IsDirty)
                .ToList();

            if (dirtyTabs.Count == 0)
                return;

            e.Cancel = true;

            // Prompt for each dirty tab
            foreach (var tab in dirtyTabs)
            {
                if (tab.View?.DataContext is not IDirtyStateAware dirty)
                    continue;

                var canClose = await DirtyStateGuard.ConfirmContinueAsync(dirty, tab.Title);
                if (!canClose)
                    return; // User chose to cancel — abort closing
            }

            _allowClose = true;
            Close();
        }
    }
}
