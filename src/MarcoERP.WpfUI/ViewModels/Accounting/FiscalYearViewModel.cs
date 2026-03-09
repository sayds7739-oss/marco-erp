using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Accounting;
using MarcoERP.Domain.Enums;

namespace MarcoERP.WpfUI.ViewModels.Accounting
{
    /// <summary>
    /// ViewModel for Fiscal Year and Period management.
    /// Supports creating years, activating, closing, and locking/unlocking periods.
    /// </summary>
    public sealed class FiscalYearViewModel : BaseViewModel
    {
        private readonly IFiscalYearService _fiscalYearService;
        private readonly IDialogService _dialog;

        public FiscalYearViewModel(IFiscalYearService fiscalYearService, IDialogService dialog)
        {
            _fiscalYearService = fiscalYearService ?? throw new ArgumentNullException(nameof(fiscalYearService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            FiscalYears = new ObservableCollection<FiscalYearDto>();
            Periods = new ObservableCollection<FiscalPeriodDto>();

            LoadCommand = new AsyncRelayCommand(LoadFiscalYearsAsync);
            CreateYearCommand = new AsyncRelayCommand(CreateYearAsync, () => CanCreateYear);
            ActivateCommand = new AsyncRelayCommand(ActivateYearAsync, () => CanActivate);
            CloseCommand = new AsyncRelayCommand(CloseYearAsync, () => CanClose);
            LockPeriodCommand = new AsyncRelayCommand(LockPeriodAsync, () => CanLockPeriod);
            UnlockPeriodCommand = new AsyncRelayCommand(UnlockPeriodAsync, () => CanUnlockPeriod);

            NewYear = DateTime.Today.Year;
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<FiscalYearDto> FiscalYears { get; }
        public ObservableCollection<FiscalPeriodDto> Periods { get; }

        // ── Selected ─────────────────────────────────────────────

        private FiscalYearDto _selectedYear;
        public FiscalYearDto SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (SetProperty(ref _selectedYear, value))
                {
                    LoadPeriods();
                    OnPropertyChanged(nameof(CanActivate));
                    OnPropertyChanged(nameof(CanClose));
                    OnPropertyChanged(nameof(SelectedYearStatus));
                }
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

        public string SelectedYearStatus
        {
            get
            {
                if (SelectedYear == null) return "";
                switch (SelectedYear.Status)
                {
                    case FiscalYearStatus.Setup: return "إعداد";
                    case FiscalYearStatus.Active: return "نشطة";
                    case FiscalYearStatus.Closed: return "مُقفلة";
                    default: return SelectedYear.Status.ToString();
                }
            }
        }

        // ── Form ─────────────────────────────────────────────────

        private int _newYear;
        public int NewYear
        {
            get => _newYear;
            set { SetProperty(ref _newYear, value); OnPropertyChanged(nameof(CanCreateYear)); }
        }

        private string _unlockReason;
        public string UnlockReason
        {
            get => _unlockReason;
            set => SetProperty(ref _unlockReason, value);
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand LoadCommand { get; }
        public ICommand CreateYearCommand { get; }
        public ICommand ActivateCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand LockPeriodCommand { get; }
        public ICommand UnlockPeriodCommand { get; }

        // ── Can Execute ─────────────────────────────────────────

        public bool CanCreateYear => NewYear >= 2020 && NewYear <= 2099;

        public bool CanActivate => SelectedYear != null
                                 && SelectedYear.Status == FiscalYearStatus.Setup;

        public bool CanClose => SelectedYear != null
                              && SelectedYear.Status == FiscalYearStatus.Active;

        public bool CanLockPeriod => SelectedPeriod != null
                                   && SelectedPeriod.Status == PeriodStatus.Open
                                   && SelectedYear != null
                                   && SelectedYear.Status == FiscalYearStatus.Active;

        public bool CanUnlockPeriod => SelectedPeriod != null
                                    && SelectedPeriod.Status == PeriodStatus.Locked
                                    && SelectedYear != null
                                    && SelectedYear.Status == FiscalYearStatus.Active;

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
                    foreach (var fy in result.Data)
                        FiscalYears.Add(fy);

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

        private void LoadPeriods()
        {
            Periods.Clear();
            if (SelectedYear?.Periods != null)
            {
                foreach (var p in SelectedYear.Periods.OrderBy(p => p.PeriodNumber))
                    Periods.Add(p);
            }
        }

        // ── Create Year ──────────────────────────────────────────

        private async Task CreateYearAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var dto = new CreateFiscalYearDto { Year = NewYear };
                var result = await _fiscalYearService.CreateAsync(dto);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم إنشاء السنة المالية {result.Data.Year}";
                    await LoadFiscalYearsAsync();
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

        // ── Activate ─────────────────────────────────────────────

        private async Task ActivateYearAsync()
        {
            if (SelectedYear == null) return;

            if (!_dialog.Confirm(
                $"هل تريد تفعيل السنة المالية {SelectedYear.Year}؟\nسنة واحدة فقط يمكن أن تكون نشطة.",
                "تأكيد التفعيل")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _fiscalYearService.ActivateAsync(SelectedYear.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم تفعيل السنة المالية {SelectedYear.Year}";
                    await LoadFiscalYearsAsync();
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

        // ── Close ────────────────────────────────────────────────

        private async Task CloseYearAsync()
        {
            if (SelectedYear == null) return;

            if (!_dialog.Confirm(
                $"هل تريد إقفال السنة المالية {SelectedYear.Year}؟\n⚠️ الإقفال غير قابل للعكس!\nيجب أن تكون كل الفترات مُقفلة.",
                "تأكيد الإقفال")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _fiscalYearService.CloseAsync(SelectedYear.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم إقفال السنة المالية {SelectedYear.Year}";
                    await LoadFiscalYearsAsync();
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

        // ── Lock Period ──────────────────────────────────────────

        private async Task LockPeriodAsync()
        {
            if (SelectedPeriod == null) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _fiscalYearService.LockPeriodAsync(SelectedPeriod.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم قفل الفترة {SelectedPeriod.PeriodNumber}";
                    // Reload the year to refresh periods
                    var yearResult = await _fiscalYearService.GetByIdAsync(SelectedYear.Id);
                    if (yearResult.IsSuccess)
                    {
                        SelectedYear = yearResult.Data;
                    }
                    await LoadFiscalYearsAsync();
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
                ErrorMessage = "يجب إدخال سبب فتح الفترة";
                return;
            }

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _fiscalYearService.UnlockPeriodAsync(
                    SelectedPeriod.Id, UnlockReason);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم فتح الفترة {SelectedPeriod.PeriodNumber}";
                    UnlockReason = "";
                    var yearResult = await _fiscalYearService.GetByIdAsync(SelectedYear.Id);
                    if (yearResult.IsSuccess)
                    {
                        SelectedYear = yearResult.Data;
                    }
                    await LoadFiscalYearsAsync();
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
    }
}
