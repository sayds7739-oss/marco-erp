using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Accounting;

namespace MarcoERP.WpfUI.ViewModels.Accounting
{
    /// <summary>
    /// ViewModel for Fiscal Periods screen (الفترات المالية).
    /// Uses existing FiscalYearService for Lock/Unlock period operations.
    /// </summary>
    public sealed class FiscalPeriodViewModel : BaseViewModel
    {
        private readonly IFiscalYearService _fiscalYearService;
        private readonly IDialogService _dialog;

        public FiscalPeriodViewModel(IFiscalYearService fiscalYearService, IDialogService dialog)
        {
            _fiscalYearService = fiscalYearService ?? throw new ArgumentNullException(nameof(fiscalYearService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            FiscalYears = new ObservableCollection<FiscalYearDto>();
            Periods = new ObservableCollection<FiscalPeriodDto>();

            LoadCommand = new AsyncRelayCommand(LoadFiscalYearsAsync);
            LockPeriodCommand = new AsyncRelayCommand(LockPeriodAsync, () => CanLockPeriod);
            UnlockPeriodCommand = new AsyncRelayCommand(UnlockPeriodAsync, () => CanUnlockPeriod);
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<FiscalYearDto> FiscalYears { get; }
        public ObservableCollection<FiscalPeriodDto> Periods { get; }

        // ── Selection ────────────────────────────────────────────

        private FiscalYearDto _selectedYear;
        public FiscalYearDto SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (SetProperty(ref _selectedYear, value))
                    LoadPeriodsForYear();
            }
        }

        private FiscalPeriodDto _selectedPeriod;
        public FiscalPeriodDto SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                if (SetProperty(ref _selectedPeriod, value))
                {
                    OnPropertyChanged(nameof(CanLockPeriod));
                    OnPropertyChanged(nameof(CanUnlockPeriod));
                }
            }
        }

        private string _unlockReason;
        public string UnlockReason
        {
            get => _unlockReason;
            set => SetProperty(ref _unlockReason, value);
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand LoadCommand { get; }
        public ICommand LockPeriodCommand { get; }
        public ICommand UnlockPeriodCommand { get; }

        // ── Can Execute ──────────────────────────────────────────

        public bool CanLockPeriod => SelectedPeriod != null && SelectedPeriod.Status == Domain.Enums.PeriodStatus.Open;
        public bool CanUnlockPeriod => SelectedPeriod != null && SelectedPeriod.Status == Domain.Enums.PeriodStatus.Locked;

        // ── Load ─────────────────────────────────────────────────

        public async Task LoadFiscalYearsAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _fiscalYearService.GetAllAsync();
                if (result.IsSuccess)
                {
                    FiscalYears.Clear();
                    Periods.Clear();
                    foreach (var fy in result.Data.OrderByDescending(y => y.Year))
                        FiscalYears.Add(fy);

                    if (FiscalYears.Count > 0)
                        SelectedYear = FiscalYears[0];

                    StatusMessage = $"تم تحميل {FiscalYears.Count} سنة مالية";
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

        private void LoadPeriodsForYear()
        {
            Periods.Clear();
            if (_selectedYear?.Periods != null)
            {
                foreach (var p in _selectedYear.Periods.OrderBy(p => p.PeriodNumber))
                    Periods.Add(p);
            }
        }

        // ── Lock Period ──────────────────────────────────────────

        private async Task LockPeriodAsync()
        {
            if (SelectedPeriod == null) return;
            if (!_dialog.Confirm(
                $"هل أنت متأكد من قفل الفترة {SelectedPeriod.PeriodNumber} ({GetMonthName(SelectedPeriod.Month)})؟",
                "تأكيد قفل الفترة")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _fiscalYearService.LockPeriodAsync(SelectedPeriod.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم قفل الفترة {SelectedPeriod.PeriodNumber} بنجاح";
                    await ReloadCurrentYear();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
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

        // ── Unlock Period ────────────────────────────────────────

        private async Task UnlockPeriodAsync()
        {
            if (SelectedPeriod == null) return;
            if (string.IsNullOrWhiteSpace(UnlockReason))
            {
                ErrorMessage = "يجب إدخال سبب فتح الفترة — يتم تسجيله في سجل المراجعة";
                return;
            }

            if (!_dialog.Confirm(
                $"هل أنت متأكد من فتح الفترة {SelectedPeriod.PeriodNumber} ({GetMonthName(SelectedPeriod.Month)})؟\nالسبب: {UnlockReason}",
                "تأكيد فتح الفترة")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _fiscalYearService.UnlockPeriodAsync(SelectedPeriod.Id, UnlockReason);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم فتح الفترة {SelectedPeriod.PeriodNumber} بنجاح";
                    UnlockReason = "";
                    await ReloadCurrentYear();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
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

        // ── Helpers ─────────────────────────────────────────────

        private async Task ReloadCurrentYear()
        {
            if (_selectedYear == null) return;
            var result = await _fiscalYearService.GetByIdAsync(_selectedYear.Id);
            if (result.IsSuccess)
            {
                var index = FiscalYears.IndexOf(_selectedYear);
                if (index >= 0)
                {
                    FiscalYears[index] = result.Data;
                    SelectedYear = result.Data;
                }
            }
        }

        private static string GetMonthName(int month)
        {
            return month switch
            {
                1 => "يناير",
                2 => "فبراير",
                3 => "مارس",
                4 => "أبريل",
                5 => "مايو",
                6 => "يونيو",
                7 => "يوليو",
                8 => "أغسطس",
                9 => "سبتمبر",
                10 => "أكتوبر",
                11 => "نوفمبر",
                12 => "ديسمبر",
                _ => month.ToString()
            };
        }
    }
}
