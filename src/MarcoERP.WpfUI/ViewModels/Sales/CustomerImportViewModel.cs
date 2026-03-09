using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.WpfUI.Common;
using Microsoft.Win32;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    /// <summary>
    /// ViewModel for the Customer Import screen.
    /// Supports: pick Excel file -> preview with validation -> import confirmed rows.
    /// </summary>
    public sealed class CustomerImportViewModel : BaseViewModel
    {
        private readonly ICustomerImportService _importService;

        public CustomerImportViewModel(ICustomerImportService importService)
        {
            _importService = importService ?? throw new ArgumentNullException(nameof(importService));

            PreviewRows = new ObservableCollection<CustomerImportRowDto>();
            ValidationErrors = new ObservableCollection<string>();

            SelectFileCommand = new AsyncRelayCommand(SelectFileAsync);
            ImportCommand = new AsyncRelayCommand(ImportAsync);
            DownloadTemplateCommand = new AsyncRelayCommand(DownloadTemplateAsync);
            CancelCommand = new RelayCommand(_ => Cancel());
        }

        // ══════════════════════════════════════════════════════════
        // PROPERTIES
        // ══════════════════════════════════════════════════════════

        private string _selectedFilePath;
        public string SelectedFilePath
        {
            get => _selectedFilePath;
            set
            {
                if (SetProperty(ref _selectedFilePath, value))
                {
                    OnPropertyChanged(nameof(HasFile));
                    OnPropertyChanged(nameof(FileName));
                }
            }
        }

        public bool HasFile => !string.IsNullOrEmpty(SelectedFilePath);
        public string FileName => System.IO.Path.GetFileName(SelectedFilePath);

        public ObservableCollection<CustomerImportRowDto> PreviewRows { get; }
        public ObservableCollection<string> ValidationErrors { get; }

        private bool _isPreviewed;
        public bool IsPreviewed
        {
            get => _isPreviewed;
            set => SetProperty(ref _isPreviewed, value);
        }

        private int _totalRows;
        public int TotalRows
        {
            get => _totalRows;
            set => SetProperty(ref _totalRows, value);
        }

        private int _validRows;
        public int ValidRows
        {
            get => _validRows;
            set => SetProperty(ref _validRows, value);
        }

        private int _invalidRows;
        public int InvalidRows
        {
            get => _invalidRows;
            set => SetProperty(ref _invalidRows, value);
        }

        // ── Import Result ──
        private bool _isImportDone;
        public bool IsImportDone
        {
            get => _isImportDone;
            set => SetProperty(ref _isImportDone, value);
        }

        private int _importedCount;
        public int ImportedCount
        {
            get => _importedCount;
            set => SetProperty(ref _importedCount, value);
        }

        private int _failedCount;
        public int FailedCount
        {
            get => _failedCount;
            set
            {
                if (SetProperty(ref _failedCount, value))
                    OnPropertyChanged(nameof(HasFailures));
            }
        }

        /// <summary>True when at least one row failed import -- used for Visibility binding.</summary>
        public bool HasFailures => _failedCount > 0;

        // ══════════════════════════════════════════════════════════
        // COMMANDS
        // ══════════════════════════════════════════════════════════

        public ICommand SelectFileCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand DownloadTemplateCommand { get; }
        public ICommand CancelCommand { get; }

        // ══════════════════════════════════════════════════════════
        // COMMAND IMPLEMENTATIONS
        // ══════════════════════════════════════════════════════════

        private async Task SelectFileAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title = "اختر ملف Excel لاستيراد العملاء",
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls",
                DefaultExt = ".xlsx"
            };

            if (dlg.ShowDialog() != true) return;

            SelectedFilePath = dlg.FileName;
            IsImportDone = false;
            ClearError();
            IsBusy = true;

            try
            {
                var result = await _importService.ParseExcelAsync(SelectedFilePath);

                PreviewRows.Clear();
                ValidationErrors.Clear();

                if (result.IsSuccess && result.Data != null)
                {
                    foreach (var row in result.Data)
                        PreviewRows.Add(row);

                    TotalRows = result.Data.Count;
                    ValidRows = result.Data.Count(r => r.IsValid);
                    InvalidRows = result.Data.Count(r => !r.IsValid);

                    // Collect all errors for summary
                    foreach (var row in result.Data.Where(r => !r.IsValid))
                    {
                        foreach (var err in row.Errors)
                            ValidationErrors.Add($"صف {row.RowNumber}: {err}");
                    }

                    IsPreviewed = true;
                    var topReasons = result.Data
                        .Where(r => !r.IsValid)
                        .SelectMany(r => r.Errors)
                        .GroupBy(e => e)
                        .OrderByDescending(g => g.Count())
                        .Take(3)
                        .Select(g => $"{g.Key} ({g.Count()})");

                    var reasonsText = string.Join(" | ", topReasons);
                    StatusMessage = $"تم تحليل {TotalRows} عميل — {ValidRows} صالح، {InvalidRows} يحتاج مراجعة"
                        + (string.IsNullOrWhiteSpace(reasonsText) ? string.Empty : $" | الأسباب الأكثر تكراراً: {reasonsText}");
                }
                else
                {
                    ErrorMessage = result.ErrorMessage ?? "فشل في قراءة الملف.";
                    IsPreviewed = false;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("العملية", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ImportAsync()
        {
            if (!IsPreviewed || ValidRows == 0) return;
            if (IsBusy) return;

            ClearError();
            IsBusy = true;

            try
            {
                var validRows = PreviewRows.Where(r => r.IsValid).ToList();
                var result = await _importService.ImportAsync(validRows);

                if (result.IsSuccess && result.Data != null)
                {
                    ImportedCount = result.Data.SuccessCount;
                    FailedCount = result.Data.FailedCount;
                    IsImportDone = true;

                    if (result.Data.FailedCount > 0)
                    {
                        // Update preview with failed rows
                        foreach (var failed in result.Data.FailedRows)
                        {
                            var existing = PreviewRows.FirstOrDefault(r => r.RowNumber == failed.RowNumber);
                            if (existing != null)
                            {
                                existing.IsValid = false;
                                existing.Errors = failed.Errors;
                            }
                            foreach (var err in failed.Errors)
                                ValidationErrors.Add($"صف {failed.RowNumber}: {err}");
                        }
                    }

                    StatusMessage = $"تم استيراد {ImportedCount} عميل بنجاح" +
                        (FailedCount > 0 ? $" — فشل {FailedCount} عميل" : "");
                }
                else
                {
                    ErrorMessage = result.ErrorMessage ?? "فشل في الاستيراد.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("العملية", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DownloadTemplateAsync()
        {
            var dlg = new SaveFileDialog
            {
                Title = "حفظ قالب استيراد العملاء",
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = "قالب_استيراد_عملاء.xlsx"
            };

            if (dlg.ShowDialog() != true) return;

            ClearError();
            IsBusy = true;

            try
            {
                var result = await _importService.GenerateTemplateAsync(dlg.FileName);
                if (result.IsSuccess)
                    StatusMessage = "تم حفظ القالب بنجاح";
                else
                    ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("العملية", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void Cancel()
        {
            PreviewRows.Clear();
            ValidationErrors.Clear();
            SelectedFilePath = null;
            IsPreviewed = false;
            IsImportDone = false;
            ClearError();
            StatusMessage = null;
        }
    }
}
