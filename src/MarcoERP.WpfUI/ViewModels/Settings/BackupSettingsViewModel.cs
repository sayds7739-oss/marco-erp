using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;

namespace MarcoERP.WpfUI.ViewModels.Settings
{
    /// <summary>
    /// ViewModel for Backup &amp; Restore screen (النسخ الاحتياطي والاستعادة).
    /// Phase C.1: Backup, Restore, and History.
    /// </summary>
    public sealed class BackupSettingsViewModel : BaseViewModel
    {
        private readonly IBackupService _backupService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IDialogService _dialog;

        public BackupSettingsViewModel(IBackupService backupService, IDateTimeProvider dateTimeProvider, IDialogService dialog)
        {
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            BackupHistory = new ObservableCollection<BackupHistoryDto>();

            BackupCommand = new AsyncRelayCommand(ExecuteBackupAsync);
            RestoreCommand = new AsyncRelayCommand(ExecuteRestoreAsync);
            LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<BackupHistoryDto> BackupHistory { get; }

        // ── Properties ──────────────────────────────────────────

        private string _lastBackupInfo;
        public string LastBackupInfo
        {
            get => _lastBackupInfo;
            set => SetProperty(ref _lastBackupInfo, value);
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand BackupCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand LoadHistoryCommand { get; }

        // ── Load History ─────────────────────────────────────────

        public async Task LoadHistoryAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _backupService.GetHistoryAsync();
                if (result.IsSuccess)
                {
                    BackupHistory.Clear();
                    foreach (var item in result.Data)
                        BackupHistory.Add(item);

                    StatusMessage = $"تم تحميل {BackupHistory.Count} سجل";
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

        // ── Backup ──────────────────────────────────────────────

        private async Task ExecuteBackupAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                // Use WPF save file dialog to pick backup destination
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "حفظ النسخة الاحتياطية",
                    Filter = "Backup Files (*.bak)|*.bak",
                    DefaultExt = ".bak",
                    FileName = $"MarcoERP_{_dateTimeProvider.UtcNow:yyyyMMdd_HHmmss}.bak"
                };

                if (dialog.ShowDialog() != true)
                {
                    IsBusy = false;
                    return;
                }

                var backupDir = System.IO.Path.GetDirectoryName(dialog.FileName);

                StatusMessage = "جاري إنشاء النسخة الاحتياطية...";
                var result = await _backupService.BackupAsync(backupDir);

                if (result.IsSuccess)
                {
                    var sizeMb = result.Data.FileSizeBytes / (1024.0 * 1024.0);
                    LastBackupInfo = $"آخر نسخة: {result.Data.BackupDate:yyyy/MM/dd HH:mm} — {sizeMb:F1} MB";
                    StatusMessage = $"تم النسخ الاحتياطي بنجاح: {result.Data.FilePath}";
                    await LoadHistoryAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("النسخ الاحتياطي", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Restore ─────────────────────────────────────────────

        private async Task ExecuteRestoreAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                // Use WPF open file dialog
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "اختر ملف النسخة الاحتياطية",
                    Filter = "Backup Files (*.bak)|*.bak|All Files (*.*)|*.*",
                    DefaultExt = ".bak"
                };

                if (dialog.ShowDialog() != true)
                {
                    IsBusy = false;
                    return;
                }

                // Confirm with user
                if (!_dialog.Confirm(
                    "تحذير: سيتم استبدال قاعدة البيانات الحالية بالكامل.\nهل أنت متأكد من الاستعادة؟",
                    "تأكيد الاستعادة"))
                {
                    IsBusy = false;
                    return;
                }

                StatusMessage = "جاري استعادة قاعدة البيانات...";
                var result = await _backupService.RestoreAsync(dialog.FileName);

                if (result.IsSuccess)
                {
                    StatusMessage = "تمت الاستعادة بنجاح. يُنصح بإعادة تشغيل التطبيق.";
                    _dialog.ShowInfo(
                        "تمت استعادة قاعدة البيانات بنجاح.\nيُنصح بإعادة تشغيل التطبيق.",
                        "نجاح");
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("الاستعادة", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
