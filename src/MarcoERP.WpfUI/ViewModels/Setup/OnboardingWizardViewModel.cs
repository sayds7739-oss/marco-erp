using System;
using System.Threading.Tasks;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces.Settings;

namespace MarcoERP.WpfUI.ViewModels.Setup
{
    /// <summary>
    /// 4-step onboarding wizard: Company Info → Fiscal Year → Warehouse → Cashbox.
    /// Saves each step's data to SystemSettings and marks onboarding complete.
    /// </summary>
    public sealed class OnboardingWizardViewModel : BaseViewModel
    {
        private readonly ISystemSettingsService _settingsService;

        private const int TotalSteps = 4;

        public OnboardingWizardViewModel(ISystemSettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            NextCommand = CreateCommand(async () => await NextAsync());
            BackCommand = new RelayCommand(_ => GoBack(), _ => CurrentStep > 1);
            SkipCommand = new RelayCommand(_ => Skip());

            CurrentStep = 1;
            FiscalYearStart = new DateTime(DateTime.Today.Year, 1, 1);
            FiscalYearEnd = new DateTime(DateTime.Today.Year, 12, 31);
            WarehouseCode = "WH-001";
            WarehouseName = "المستودع الرئيسي";
            CashboxCode = "CB-001";
            CashboxName = "الصندوق الرئيسي";
        }

        // ── Step Navigation ──
        private int _currentStep = 1;
        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                if (SetProperty(ref _currentStep, value))
                {
                    OnPropertyChanged(nameof(IsStep1));
                    OnPropertyChanged(nameof(IsStep2));
                    OnPropertyChanged(nameof(IsStep3));
                    OnPropertyChanged(nameof(IsStep4));
                    OnPropertyChanged(nameof(IsLastStep));
                    OnPropertyChanged(nameof(CanGoBack));
                    OnPropertyChanged(nameof(ProgressPercent));
                    OnPropertyChanged(nameof(StepTitle));
                    OnPropertyChanged(nameof(StepDescription));
                }
            }
        }

        public bool IsStep1 => CurrentStep == 1;
        public bool IsStep2 => CurrentStep == 2;
        public bool IsStep3 => CurrentStep == 3;
        public bool IsStep4 => CurrentStep == 4;
        public bool IsLastStep => CurrentStep == TotalSteps;
        public bool CanGoBack => CurrentStep > 1;
        public int ProgressPercent => (int)((CurrentStep / (double)TotalSteps) * 100);

        public string StepTitle => CurrentStep switch
        {
            1 => "معلومات الشركة",
            2 => "السنة المالية",
            3 => "المستودع",
            4 => "الصندوق",
            _ => ""
        };

        public string StepDescription => CurrentStep switch
        {
            1 => "أدخل معلومات شركتك الأساسية. هذه المعلومات ستظهر في المستندات المطبوعة.",
            2 => "حدد بداية ونهاية السنة المالية الحالية.",
            3 => "أنشئ المستودع الرئيسي لتتبع المخزون.",
            4 => "أنشئ الصندوق النقدي الرئيسي لعمليات القبض والصرف.",
            _ => ""
        };

        private bool _isCompleted;
        public bool IsCompleted { get => _isCompleted; set => SetProperty(ref _isCompleted, value); }

        // ── Step 1: Company Info ──
        private string _companyName = "";
        public string CompanyName { get => _companyName; set => SetProperty(ref _companyName, value); }

        private string _companyAddress = "";
        public string CompanyAddress { get => _companyAddress; set => SetProperty(ref _companyAddress, value); }

        private string _companyPhone = "";
        public string CompanyPhone { get => _companyPhone; set => SetProperty(ref _companyPhone, value); }

        private string _companyEmail = "";
        public string CompanyEmail { get => _companyEmail; set => SetProperty(ref _companyEmail, value); }

        private string _companyTaxNumber = "";
        public string CompanyTaxNumber { get => _companyTaxNumber; set => SetProperty(ref _companyTaxNumber, value); }

        // ── Step 2: Fiscal Year ──
        private DateTime _fiscalYearStart;
        public DateTime FiscalYearStart { get => _fiscalYearStart; set => SetProperty(ref _fiscalYearStart, value); }

        private DateTime _fiscalYearEnd;
        public DateTime FiscalYearEnd { get => _fiscalYearEnd; set => SetProperty(ref _fiscalYearEnd, value); }

        // ── Step 3: Warehouse ──
        private string _warehouseCode = "WH-001";
        public string WarehouseCode { get => _warehouseCode; set => SetProperty(ref _warehouseCode, value); }

        private string _warehouseName = "المستودع الرئيسي";
        public string WarehouseName { get => _warehouseName; set => SetProperty(ref _warehouseName, value); }

        // ── Step 4: Cashbox ──
        private string _cashboxCode = "CB-001";
        public string CashboxCode { get => _cashboxCode; set => SetProperty(ref _cashboxCode, value); }

        private string _cashboxName = "الصندوق الرئيسي";
        public string CashboxName { get => _cashboxName; set => SetProperty(ref _cashboxName, value); }

        // ── Commands ──
        public ICommand NextCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand SkipCommand { get; }

        private async Task NextAsync()
        {
            ClearError();

            // Validate current step
            if (!ValidateCurrentStep()) return;

            if (IsLastStep)
            {
                await SaveAllAsync();
            }
            else
            {
                CurrentStep++;
            }
        }

        private void GoBack()
        {
            if (CurrentStep > 1)
                CurrentStep--;
        }

        private void Skip()
        {
            // Mark onboarding as completed without saving
            IsCompleted = true;
        }

        private bool ValidateCurrentStep()
        {
            switch (CurrentStep)
            {
                case 1:
                    if (string.IsNullOrWhiteSpace(CompanyName))
                    {
                        ErrorMessage = "اسم الشركة مطلوب.";
                        return false;
                    }
                    break;
                case 2:
                    if (FiscalYearEnd <= FiscalYearStart)
                    {
                        ErrorMessage = "تاريخ النهاية يجب أن يكون بعد تاريخ البداية.";
                        return false;
                    }
                    break;
                case 3:
                    if (string.IsNullOrWhiteSpace(WarehouseName))
                    {
                        ErrorMessage = "اسم المستودع مطلوب.";
                        return false;
                    }
                    break;
                case 4:
                    if (string.IsNullOrWhiteSpace(CashboxName))
                    {
                        ErrorMessage = "اسم الصندوق مطلوب.";
                        return false;
                    }
                    break;
            }
            return true;
        }

        private async Task SaveAllAsync()
        {
            IsBusy = true;
            try
            {
                // Save company info
                await SaveSettingAsync("CompanyName", CompanyName);
                await SaveSettingAsync("CompanyAddress", CompanyAddress);
                await SaveSettingAsync("CompanyPhone", CompanyPhone);
                await SaveSettingAsync("CompanyEmail", CompanyEmail);
                await SaveSettingAsync("CompanyTaxNumber", CompanyTaxNumber);

                // Save fiscal year
                await SaveSettingAsync("FiscalYearStart", FiscalYearStart.ToString("yyyy-MM-dd"));
                await SaveSettingAsync("FiscalYearEnd", FiscalYearEnd.ToString("yyyy-MM-dd"));

                // Save warehouse defaults
                await SaveSettingAsync("DefaultWarehouseCode", WarehouseCode);
                await SaveSettingAsync("DefaultWarehouseName", WarehouseName);

                // Save cashbox defaults
                await SaveSettingAsync("DefaultCashboxCode", CashboxCode);
                await SaveSettingAsync("DefaultCashboxName", CashboxName);

                // Mark onboarding complete
                await SaveSettingAsync("OnboardingCompleted", "true");

                IsCompleted = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"حدث خطأ أثناء حفظ الإعدادات: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveSettingAsync(string key, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            await _settingsService.UpdateAsync(
                new UpdateSystemSettingDto { SettingKey = key, SettingValue = value });
        }
    }
}
