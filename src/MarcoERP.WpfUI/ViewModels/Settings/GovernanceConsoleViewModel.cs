using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Enums;

namespace MarcoERP.WpfUI.ViewModels.Settings
{
    /// <summary>
    /// ViewModel for the Governance Console screen (وحدة التحكم).
    /// Phase 2: Feature Governance Engine — read/toggle features.
    /// Phase 3: Profile Selection — apply complexity profiles + custom profile designer.
    /// Phase 4: Impact Analyzer — pre-toggle analysis + dependency blocking.
    /// Phase 8F: Module Dependency Graph visualization.
    /// </summary>
    public sealed class GovernanceConsoleViewModel : BaseViewModel
    {
        private readonly IFeatureService _featureService;
        private readonly IProfileService _profileService;
        private readonly IImpactAnalyzerService _impactAnalyzer;
        private readonly IServiceProvider _serviceProvider;
        private readonly IModuleDependencyInspector _dependencyInspector;
        private readonly IDialogService _dialog;

        /// <summary>Tracks PropertyChanged subscriptions on CustomProfileFeatureRow items to allow cleanup.</summary>
        private readonly List<(CustomProfileFeatureRow Row, PropertyChangedEventHandler Handler)> _featureSubscriptions = new();

        public GovernanceConsoleViewModel(
            IFeatureService featureService,
            IProfileService profileService,
            IImpactAnalyzerService impactAnalyzer,
            IServiceProvider serviceProvider,
            IModuleDependencyInspector dependencyInspector = null,
            IDialogService dialog = null)
        {
            _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
            _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
            _impactAnalyzer = impactAnalyzer ?? throw new ArgumentNullException(nameof(impactAnalyzer));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _dependencyInspector = dependencyInspector;
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            Features = new ObservableCollection<FeatureRowViewModel>();
            ProfileNames = new ObservableCollection<string> { "Simple", "Standard", "Advanced" };
            DependencyRows = new ObservableCollection<DependencyGraphRow>();
            CustomProfileFeatures = new ObservableCollection<CustomProfileFeatureRow>();

            LoadCommand = new AsyncRelayCommand(LoadAllAsync);
            ApplyProfileCommand = new AsyncRelayCommand(ApplySelectedProfileAsync);
            ApplyCustomProfileCommand = new AsyncRelayCommand(ApplyCustomProfileAsync);
            SelectAllFeaturesCommand = new RelayCommand(_ => SetAllCustomFeatures(true));
            DeselectAllFeaturesCommand = new RelayCommand(_ => SetAllCustomFeatures(false));
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<FeatureRowViewModel> Features { get; }
        public ObservableCollection<string> ProfileNames { get; }

        /// <summary>Phase 8F: Module dependency graph rows.</summary>
        public ObservableCollection<DependencyGraphRow> DependencyRows { get; }

        /// <summary>Custom profile designer: all features with checkbox.</summary>
        public ObservableCollection<CustomProfileFeatureRow> CustomProfileFeatures { get; }

        // ── Profile Selection ────────────────────────────────────

        private string _selectedProfile;
        public string SelectedProfile
        {
            get => _selectedProfile;
            set => SetProperty(ref _selectedProfile, value);
        }

        private string _currentProfileDisplay;
        public string CurrentProfileDisplay
        {
            get => _currentProfileDisplay;
            set => SetProperty(ref _currentProfileDisplay, value);
        }

        // ── Custom Profile Stats ─────────────────────────────────

        private int _customEnabledCount;
        public int CustomEnabledCount
        {
            get => _customEnabledCount;
            set => SetProperty(ref _customEnabledCount, value);
        }

        private int _customTotalCount;
        public int CustomTotalCount
        {
            get => _customTotalCount;
            set => SetProperty(ref _customTotalCount, value);
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand LoadCommand { get; }
        public ICommand ApplyProfileCommand { get; }
        public ICommand ApplyCustomProfileCommand { get; }
        public ICommand SelectAllFeaturesCommand { get; }
        public ICommand DeselectAllFeaturesCommand { get; }

        // ── Load ─────────────────────────────────────────────────

        private async Task LoadAllAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                // Load features
                var featResult = await _featureService.GetAllAsync(cts.Token);
                if (featResult.IsSuccess)
                {
                    Features.Clear();
                    foreach (var dto in featResult.Data)
                    {
                        var row = new FeatureRowViewModel(dto, ToggleFeatureAsync);
                        Features.Add(row);
                    }
                }
                else
                {
                    ErrorMessage = featResult.ErrorMessage;
                    return;
                }

                // Load available profiles (DB) with fallback
                var profilesResult = await _profileService.GetAllProfilesAsync(cts.Token);
                ProfileNames.Clear();
                if (profilesResult.IsSuccess && profilesResult.Data != null && profilesResult.Data.Count > 0)
                {
                    foreach (var profile in profilesResult.Data.OrderBy(p => p.ProfileName))
                        ProfileNames.Add(profile.ProfileName);
                }
                else
                {
                    ProfileNames.Add("Simple");
                    ProfileNames.Add("Standard");
                    ProfileNames.Add("Advanced");
                }

                // Load current profile
                var profileResult = await _profileService.GetCurrentProfileAsync(cts.Token);
                if (profileResult.IsSuccess)
                {
                    CurrentProfileDisplay = profileResult.Data;
                    SelectedProfile = profileResult.Data;
                }

                StatusMessage = $"تم تحميل {Features.Count} ميزة — البروفايل الحالي: {CurrentProfileDisplay ?? "غير محدد"}";

                // Load custom profile designer checkboxes
                LoadCustomProfileFeatures();

                // Phase 8F: Load dependency graph
                await LoadDependencyGraphAsync();
            }
            catch (OperationCanceledException)
            {
                ErrorMessage = "انتهت مهلة تحميل وحدة التحكم. تحقق من اتصال قاعدة البيانات ثم اضغط تحديث.";
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("التحميل", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Toggle ───────────────────────────────────────────────

        private async Task ToggleFeatureAsync(FeatureRowViewModel row)
        {
            bool isEnabling = !row.IsEnabled;
            string action = isEnabling ? "تفعيل" : "تعطيل";

            // ── Phase 4: Impact Analysis before toggle ──────────
            IsBusy = true;
            ClearError();
            FeatureImpactReport report;
            try
            {
                report = await _impactAnalyzer.AnalyzeAsync(row.FeatureKey);
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تحليل التأثير", ex);
                IsBusy = false;
                return;
            }
            finally
            {
                IsBusy = false;
            }

            // ── Phase 4E: Block if dependencies are disabled ────
            if (isEnabling && !report.CanProceed && report.DisabledDependencies.Count > 0)
            {
                _dialog.ShowError(
                    $"🚫 لا يمكن تفعيل '{row.NameAr}'\n\n" +
                    $"التبعيات التالية غير مفعلة:\n" +
                    $"  • {string.Join("\n  • ", report.DisabledDependencies)}\n\n" +
                    "يجب تفعيل هذه الميزات أولاً.",
                    "تبعيات غير مفعلة");
                return;
            }

            // ── Cascade warning: show which features will also be disabled ──
            var cascadeList = new List<string>();    // Arabic names for display
            var cascadeKeys = new List<string>();    // FeatureKeys for matching
            if (!isEnabling)
            {
                CollectCascadeDependents(row.FeatureKey, cascadeList, cascadeKeys);
            }

            // ── Show Impact Report ──────────────────────────────
            string cascadeText = cascadeList.Count > 0
                ? $"\n⚠️ الميزات التالية ستُعطَّل تلقائياً (تبعيات):\n  • {string.Join("\n  • ", cascadeList)}\n"
                : "";

            string reportText =
                $"📊 تقرير تأثير {action} '{row.NameAr}'\n" +
                $"━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                $"مستوى الخطورة: {report.RiskLevel}\n" +
                (report.RequiresMigration ? "⚠️ يتطلب Migration\n" : "") +
                (report.ImpactAreas.Count > 0 ? $"المناطق المتأثرة: {string.Join("، ", report.ImpactAreas)}\n" : "") +
                (report.Dependencies.Count > 0 ? $"يعتمد على: {string.Join("، ", report.Dependencies)}\n" : "") +
                cascadeText +
                $"━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                report.WarningMessage +
                $"\n\nهل تريد المتابعة بعملية {action}؟";

            if (!_dialog.Confirm(
                reportText,
                $"تقرير التأثير — {action} '{row.NameAr}'"))
                return;

            // ── Phase 4D: Double confirmation for High risk ─────
            if (report.RiskLevel == "High")
            {
                if (!_dialog.Confirm(
                    $"⚠️⚠️ تأكيد نهائي ⚠️⚠️\n\n" +
                    $"أنت على وشك {action} ميزة عالية الخطورة: '{row.NameAr}'\n\n" +
                    "هذا الإجراء قد يؤثر على عمليات حساسة في النظام.\n\n" +
                    "هل أنت متأكد تماماً؟",
                    "تأكيد نهائي — ميزة عالية الخطورة"))
                    return;
            }

            // ── Execute toggle ──────────────────────────────────
            IsBusy = true;
            ClearError();
            try
            {
                var dto = new ToggleFeatureDto
                {
                    FeatureKey = row.FeatureKey,
                    IsEnabled = isEnabling
                };

                var result = await _featureService.ToggleAsync(dto);
                if (result.IsSuccess)
                {
                    row.IsEnabled = dto.IsEnabled;

                    // Reflect cascade disables in the UI rows (match by FeatureKey, not NameAr)
                    if (!dto.IsEnabled && cascadeKeys.Count > 0)
                    {
                        foreach (var f in Features)
                        {
                            if (cascadeKeys.Contains(f.FeatureKey))
                                f.IsEnabled = false;
                        }
                    }

                    StatusMessage = $"تم {action} '{row.NameAr}' — {report.RiskLevel} Risk";
                    if (cascadeKeys.Count > 0)
                        StatusMessage += $" + {cascadeKeys.Count} تبعية معطلة تلقائياً";

                    // Reload custom profile to reflect changes
                    LoadCustomProfileFeatures();
                    await RefreshMainNavigationAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("التبديل", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Cascade dependency helpers ───────────────────────────

        /// <summary>
        /// Collects Arabic names and FeatureKeys of all features that will be cascade-disabled
        /// when <paramref name="parentKey"/> is disabled.
        /// </summary>
        private void CollectCascadeDependents(string parentKey, List<string> nameList, List<string> keyList, HashSet<string> visited = null)
        {
            visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!visited.Add(parentKey)) return; // prevent cycles

            foreach (var f in Features)
            {
                if (!f.IsEnabled) continue;
                if (string.IsNullOrWhiteSpace(f.DependsOn)) continue;

                var deps = f.DependsOn
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (deps.Any(d => string.Equals(d, parentKey, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!keyList.Contains(f.FeatureKey))
                    {
                        nameList.Add(f.NameAr);
                        keyList.Add(f.FeatureKey);
                        // Recurse for transitive dependents
                        CollectCascadeDependents(f.FeatureKey, nameList, keyList, visited);
                    }
                }
            }
        }

        // ── Apply Profile ────────────────────────────────────────

        private async Task ApplySelectedProfileAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedProfile))
            {
                ErrorMessage = "اختر بروفايل أولاً.";
                return;
            }

            // Confirmation dialog
            if (!_dialog.Confirm(
                $"هل تريد تطبيق البروفايل '{SelectedProfile}'؟\n\n" +
                "⚠️ سيتم تفعيل/تعطيل الميزات حسب البروفايل المختار.\n" +
                "💾 يُنصح بأخذ نسخة احتياطية قبل التغيير.\n\n" +
                "لن يتم حذف أي بيانات — فقط تغيير ظهور الشاشات.",
                "تأكيد تغيير البروفايل"))
                return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _profileService.ApplyProfileAsync(SelectedProfile);
                if (result.IsSuccess)
                {
                    CurrentProfileDisplay = SelectedProfile;
                    StatusMessage = $"تم تطبيق البروفايل '{SelectedProfile}' بنجاح";
                    // Reload features to reflect changes
                    await LoadAllAsync();
                    // Refresh main window navigation
                    await RefreshMainNavigationAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تطبيق البروفايل", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Navigation Refresh ───────────────────────────────────

        private async Task RefreshMainNavigationAsync()
        {
            try
            {
                var mainVm = _serviceProvider.GetService(typeof(Shell.MainWindowViewModel)) as Shell.MainWindowViewModel;
                if (mainVm != null)
                    await mainVm.RefreshNavigationAsync();
            }
            catch
            {
                // Best-effort refresh — sidebar will update on next app restart
            }
        }

        // ── Custom Profile Designer ─────────────────────────────

        private void ClearFeatureSubscriptions()
        {
            foreach (var (row, handler) in _featureSubscriptions)
                row.PropertyChanged -= handler;
            _featureSubscriptions.Clear();
            CustomProfileFeatures.Clear();
        }

        private void LoadCustomProfileFeatures()
        {
            ClearFeatureSubscriptions();
            foreach (var f in Features)
            {
                var row = new CustomProfileFeatureRow
                {
                    FeatureKey = f.FeatureKey,
                    NameAr = f.NameAr,
                    Description = f.Description,
                    RiskLevel = f.RiskLevel,
                    DependsOn = f.DependsOn,
                    IsSelected = f.IsEnabled
                };
                PropertyChangedEventHandler handler = (_, _) => UpdateCustomStats();
                row.PropertyChanged += handler;
                _featureSubscriptions.Add((row, handler));
                CustomProfileFeatures.Add(row);
            }
            UpdateCustomStats();
        }

        private void UpdateCustomStats()
        {
            CustomTotalCount = CustomProfileFeatures.Count;
            CustomEnabledCount = CustomProfileFeatures.Count(r => r.IsSelected);
        }

        private void SetAllCustomFeatures(bool selected)
        {
            foreach (var row in CustomProfileFeatures)
                row.IsSelected = selected;
        }

        private async Task ApplyCustomProfileAsync()
        {
            var enabledKeys = CustomProfileFeatures.Where(r => r.IsSelected).Select(r => r.FeatureKey).ToList();
            var disabledKeys = CustomProfileFeatures.Where(r => !r.IsSelected).Select(r => r.FeatureKey).ToList();

            if (!_dialog.Confirm(
                $"هل تريد تطبيق البروفايل المخصص؟\n\n" +
                $"✅ سيتم تفعيل: {enabledKeys.Count} ميزة\n" +
                $"❌ سيتم تعطيل: {disabledKeys.Count} ميزة\n\n" +
                "⚠️ سيتم تغيير إعدادات الميزات حسب اختيارك.\n" +
                "لن يتم حذف أي بيانات — فقط تغيير ظهور الشاشات.",
                "تأكيد تطبيق البروفايل المخصص"))
                return;

            IsBusy = true;
            ClearError();
            try
            {
                // Toggle each feature to match the custom selection
                foreach (var row in CustomProfileFeatures)
                {
                    var matchingFeature = Features.FirstOrDefault(f => f.FeatureKey == row.FeatureKey);
                    if (matchingFeature == null) continue;
                    if (matchingFeature.IsEnabled == row.IsSelected) continue;

                    var dto = new ToggleFeatureDto
                    {
                        FeatureKey = row.FeatureKey,
                        IsEnabled = row.IsSelected
                    };
                    var result = await _featureService.ToggleAsync(dto);
                    if (result.IsFailure)
                    {
                        ErrorMessage = $"فشل تبديل '{row.NameAr}': {result.ErrorMessage}";
                        return;
                    }
                }

                CurrentProfileDisplay = "مخصص";
                StatusMessage = $"تم تطبيق البروفايل المخصص بنجاح — {enabledKeys.Count} ميزة مفعلة";
                await LoadAllAsync();
                await RefreshMainNavigationAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تطبيق البروفايل المخصص", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Phase 8F: Dependency Graph ──────────────────────────

        private async Task LoadDependencyGraphAsync()
        {
            DependencyRows.Clear();

            var violations = await Task.Run(() =>
                _dependencyInspector?.ValidateDependencies() ?? new List<ModuleDependencyViolation>());

            foreach (var def in ModuleRegistry.Definitions)
            {
                if (def.Module == SystemModule.Common) continue;

                // Self row (no dependency — just shows the module exists)
                if (def.AllowedDependencies.Count == 0)
                {
                    DependencyRows.Add(new DependencyGraphRow
                    {
                        Module = GetModuleArabicName(def.Module),
                        DependsOn = "—",
                        IsAllowed = true
                    });
                    continue;
                }

                foreach (var dep in def.AllowedDependencies)
                {
                    DependencyRows.Add(new DependencyGraphRow
                    {
                        Module = GetModuleArabicName(def.Module),
                        DependsOn = GetModuleArabicName(dep),
                        IsAllowed = true
                    });
                }
            }

            // Add violation rows (unauthorized dependencies — red)
            foreach (var v in violations)
            {
                if (!Enum.TryParse<SystemModule>(v.SourceModule, out var srcModule) ||
                    !Enum.TryParse<SystemModule>(v.DependencyModule, out var depModule))
                    continue; // skip violations with unrecognized module names

                // Avoid duplicates
                var srcName = GetModuleArabicName(srcModule);
                var depName = GetModuleArabicName(depModule);
                var exists = DependencyRows.Any(r => r.Module == srcName && r.DependsOn == depName);
                if (!exists)
                {
                    DependencyRows.Add(new DependencyGraphRow
                    {
                        Module = srcName,
                        DependsOn = depName,
                        IsAllowed = false,
                        ViolationDetail = v.Message
                    });
                }
            }
        }

        private static string GetModuleArabicName(SystemModule module) => module switch
        {
            SystemModule.Sales => "المبيعات",
            SystemModule.Inventory => "المخزون",
            SystemModule.Accounting => "المحاسبة",
            SystemModule.Purchases => "المشتريات",
            SystemModule.Treasury => "الخزينة",
            SystemModule.Reporting => "التقارير",
            SystemModule.Security => "الأمان",
            SystemModule.Settings => "الإعدادات",
            SystemModule.Governance => "الحوكمة",
            SystemModule.POS => "نقطة البيع",
            SystemModule.Common => "عام",
            _ => module.ToString()
        };
    }

    /// <summary>
    /// Phase 8F: A row in the dependency graph table.
    /// </summary>
    public sealed class DependencyGraphRow
    {
        public string Module { get; set; }
        public string DependsOn { get; set; }
        public bool IsAllowed { get; set; }
        public string Status => IsAllowed ? "مصرح" : "غير مصرح";
        public string ViolationDetail { get; set; }
    }

    /// <summary>
    /// Row-level ViewModel wrapping a FeatureDto for DataGrid binding with toggle support.
    /// </summary>
    public sealed class FeatureRowViewModel : BaseViewModel
    {
        private readonly Func<FeatureRowViewModel, Task> _toggleCallback;

        public FeatureRowViewModel(FeatureDto dto, Func<FeatureRowViewModel, Task> toggleCallback)
        {
            _toggleCallback = toggleCallback;
            Id = dto.Id;
            FeatureKey = dto.FeatureKey;
            NameAr = dto.NameAr;
            NameEn = dto.NameEn;
            Description = dto.Description;
            _isEnabled = dto.IsEnabled;
            RiskLevel = dto.RiskLevel;
            DependsOn = dto.DependsOn;

            ToggleCommand = new AsyncRelayCommand(() => _toggleCallback(this));
        }

        public int Id { get; }
        public string FeatureKey { get; }
        public string NameAr { get; }
        public string NameEn { get; }
        public string Description { get; }
        public string RiskLevel { get; }
        public string DependsOn { get; }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public ICommand ToggleCommand { get; }
    }

    /// <summary>
    /// Row in the custom profile designer — each feature with a checkbox.
    /// </summary>
    public sealed class CustomProfileFeatureRow : BaseViewModel
    {
        public string FeatureKey { get; set; }
        public string NameAr { get; set; }
        public string Description { get; set; }
        public string RiskLevel { get; set; }
        public string DependsOn { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
