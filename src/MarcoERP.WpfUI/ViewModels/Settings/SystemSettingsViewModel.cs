using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;

namespace MarcoERP.WpfUI.ViewModels.Settings
{
    /// <summary>
    /// ViewModel for System Settings screen (إعدادات النظام).
    /// Phase 5D: Grouped key-value settings with batch save.
    /// </summary>
    public sealed class SystemSettingsViewModel : BaseViewModel
    {
        private readonly ISystemSettingsService _settingsService;
        private readonly IDataPurgeService _dataPurgeService;
        private readonly IDialogService _dialog;

        public SystemSettingsViewModel(ISystemSettingsService settingsService, IDataPurgeService dataPurgeService, IDialogService dialog)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dataPurgeService = dataPurgeService ?? throw new ArgumentNullException(nameof(dataPurgeService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            AllSettings = new ObservableCollection<SystemSettingDto>();
            GroupNames = new ObservableCollection<string>();
            FilteredSettings = new ObservableCollection<SystemSettingDto>();

            LoadCommand = new AsyncRelayCommand(LoadSettingsAsync);
            SaveAllCommand = new AsyncRelayCommand(SaveAllAsync);
            PurgeDataCommand = new AsyncRelayCommand(PurgeDataAsync, () => !IsBusy);
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<SystemSettingDto> AllSettings { get; }
        public ObservableCollection<string> GroupNames { get; }
        public ObservableCollection<SystemSettingDto> FilteredSettings { get; }

        // ── Selected Group ──────────────────────────────────────

        private string _selectedGroup;
        public string SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (SetProperty(ref _selectedGroup, value))
                    ApplyGroupFilter();
            }
        }

        private bool _keepCustomers = true;
        public bool KeepCustomers
        {
            get => _keepCustomers;
            set => SetProperty(ref _keepCustomers, value);
        }

        private bool _keepSuppliers = true;
        public bool KeepSuppliers
        {
            get => _keepSuppliers;
            set => SetProperty(ref _keepSuppliers, value);
        }

        private bool _keepProducts = true;
        public bool KeepProducts
        {
            get => _keepProducts;
            set => SetProperty(ref _keepProducts, value);
        }

        private bool _keepSalesRepresentatives = true;
        public bool KeepSalesRepresentatives
        {
            get => _keepSalesRepresentatives;
            set => SetProperty(ref _keepSalesRepresentatives, value);
        }

        private string _purgeSummary;
        public string PurgeSummary
        {
            get => _purgeSummary;
            set => SetProperty(ref _purgeSummary, value);
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand LoadCommand { get; }
        public ICommand SaveAllCommand { get; }
        public ICommand PurgeDataCommand { get; }

        // ── Load ─────────────────────────────────────────────────

        public async Task LoadSettingsAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _settingsService.GetAllAsync();
                if (result.IsSuccess)
                {
                    AllSettings.Clear();
                    GroupNames.Clear();
                    FilteredSettings.Clear();

                    foreach (var s in result.Data)
                        AllSettings.Add(s);

                    var groups = result.Data.Select(s => s.GroupName).Distinct().OrderBy(g => g).ToList();
                    groups.Insert(0, "الكل");
                    foreach (var g in groups)
                        GroupNames.Add(g);

                    SelectedGroup = "الكل";
                    StatusMessage = $"تم تحميل {AllSettings.Count} إعداد";
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
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

        // ── Save All ─────────────────────────────────────────────

        private async Task SaveAllAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                // Save ALL settings, not just the currently filtered group
                var updates = AllSettings
                    .Select(s => new UpdateSystemSettingDto
                    {
                        SettingKey = s.SettingKey,
                        SettingValue = s.SettingValue
                    })
                    .ToList();

                var result = await _settingsService.UpdateBatchAsync(updates);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم حفظ {updates.Count} إعداد بنجاح";
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("الحفظ", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Group Filter ────────────────────────────────────────

        private void ApplyGroupFilter()
        {
            FilteredSettings.Clear();
            var source = _selectedGroup == "الكل"
                ? AllSettings
                : AllSettings.Where(s => s.GroupName == _selectedGroup);

            foreach (var s in source)
                FilteredSettings.Add(s);
        }

        private async Task PurgeDataAsync()
        {
            var confirmationText =
                "سيتم مسح البيانات التشغيلية (فواتير، سندات، قيود، حركات مخزون...) من قاعدة البيانات.\n\n" +
                $"- إبقاء العملاء: {(KeepCustomers ? "نعم" : "لا")}\n" +
                $"- إبقاء الموردين: {(KeepSuppliers ? "نعم" : "لا")}\n" +
                $"- إبقاء الأصناف: {(KeepProducts ? "نعم" : "لا")}\n" +
                $"- إبقاء مندوبي المبيعات: {(KeepSalesRepresentatives ? "نعم" : "لا")}\n\n" +
                "هل تريد المتابعة؟";

            if (!_dialog.Confirm(
                confirmationText,
                "تأكيد مسح البيانات"))
                return;

            IsBusy = true;
            ClearError();
            try
            {
                var options = new DataPurgeOptionsDto
                {
                    KeepCustomers = KeepCustomers,
                    KeepSuppliers = KeepSuppliers,
                    KeepProducts = KeepProducts,
                    KeepSalesRepresentatives = KeepSalesRepresentatives
                };

                var result = await _dataPurgeService.PurgeAsync(options);
                if (result.IsFailure)
                {
                    ErrorMessage = result.ErrorMessage;
                    return;
                }

                var topItems = string.Join("\n", result.Data.Items
                    .Where(i => i.DeletedRows > 0)
                    .OrderByDescending(i => i.DeletedRows)
                    .Take(8)
                    .Select(i => $"- {i.EntityName}: {i.DeletedRows:N0}"));

                PurgeSummary =
                    $"آخر تنفيذ: {result.Data.ExecutedAtUtc:yyyy-MM-dd HH:mm} UTC | " +
                    $"إجمالي الصفوف المحذوفة: {result.Data.TotalDeletedRows:N0}";

                StatusMessage = "تم مسح البيانات بنجاح.";
                _dialog.ShowInfo(
                    $"تم تنفيذ مسح البيانات بنجاح.\n\nإجمالي الصفوف المحذوفة: {result.Data.TotalDeletedRows:N0}\n\n{topItems}",
                    "نجاح العملية");

                await LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("مسح البيانات", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
