using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Accounting;
using MarcoERP.Application.Interfaces.Treasury;

namespace MarcoERP.WpfUI.ViewModels.Treasury
{
    /// <summary>
    /// ViewModel for Cashbox management screen.
    /// </summary>
    public sealed class CashboxViewModel : BaseViewModel
    {
        private readonly ICashboxService _cashboxService;
        private readonly IAccountService _accountService;
        private readonly IDialogService _dialog;

        public CashboxViewModel(ICashboxService cashboxService, IAccountService accountService, IDialogService dialog)
        {
            _cashboxService = cashboxService ?? throw new ArgumentNullException(nameof(cashboxService));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            AllCashboxes = new ObservableCollection<CashboxDto>();
            Accounts = new ObservableCollection<AccountDto>();

            LoadCommand = new AsyncRelayCommand(LoadCashboxesAsync);
            NewCommand = new AsyncRelayCommand(PrepareNewAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            SetDefaultCommand = new AsyncRelayCommand(SetDefaultAsync, () => SelectedItem != null);
            DeleteCommand = new AsyncRelayCommand(DeactivateAsync, () => CanDeactivate);
            ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => CanActivate);
            CancelCommand = new RelayCommand(CancelEditing);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<CashboxDto> AllCashboxes { get; }
        public ObservableCollection<AccountDto> Accounts { get; }

        // ── Selection ────────────────────────────────────────────

        private CashboxDto _selectedItem;
        public CashboxDto SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    if (value != null && !IsEditing)
                        PopulateForm(value);
                    OnPropertyChanged(nameof(CanDeactivate));
                    OnPropertyChanged(nameof(CanActivate));
                }
            }
        }

        // ── Form Fields ─────────────────────────────────────────

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        private bool _isNew;
        public bool IsNew
        {
            get => _isNew;
            set => SetProperty(ref _isNew, value);
        }

        private string _formCode;
        public string FormCode
        {
            get => _formCode;
            set => SetProperty(ref _formCode, value);
        }

        private string _formNameAr;
        public string FormNameAr
        {
            get => _formNameAr;
            set { SetProperty(ref _formNameAr, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private string _formNameEn;
        public string FormNameEn
        {
            get => _formNameEn;
            set => SetProperty(ref _formNameEn, value);
        }

        private int? _formAccountId;
        public int? FormAccountId
        {
            get => _formAccountId;
            set => SetProperty(ref _formAccountId, value);
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand LoadCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SetDefaultCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ActivateCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand EditSelectedCommand { get; }

        // ── Can Execute ──────────────────────────────────────────

        public bool CanSave => !string.IsNullOrWhiteSpace(FormNameAr);
        public bool CanDeactivate => SelectedItem != null && SelectedItem.IsActive;
        public bool CanActivate => SelectedItem != null && !SelectedItem.IsActive;

        // ── Load ─────────────────────────────────────────────────

        public async Task LoadCashboxesAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var cashboxResult = await _cashboxService.GetAllAsync();
                var accountResult = await _accountService.GetAllAsync();

                if (cashboxResult.IsSuccess)
                {
                    AllCashboxes.Clear();
                    foreach (var c in cashboxResult.Data)
                        AllCashboxes.Add(c);
                }

                if (accountResult.IsSuccess)
                {
                    Accounts.Clear();
                    foreach (var a in accountResult.Data)
                    {
                        if (a.AllowPosting)
                            Accounts.Add(a);
                    }
                }

                StatusMessage = $"تم تحميل {AllCashboxes.Count} خزنة";
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

        // ── New ──────────────────────────────────────────────────

        private async Task PrepareNewAsync()
        {
            IsEditing = true;
            IsNew = true;
            ClearError();

            var nextCodeResult = await _cashboxService.GetNextCodePreviewAsync();
            FormCode = nextCodeResult.IsSuccess && !string.IsNullOrWhiteSpace(nextCodeResult.Data)
                ? nextCodeResult.Data
                : "CBX-0001";
            FormNameAr = "";
            FormNameEn = "";
            FormAccountId = null;
            StatusMessage = "إدخال خزنة جديدة...";
        }

        // ── Save ─────────────────────────────────────────────────

        private async Task SaveAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                if (IsNew)
                {
                    var dto = new CreateCashboxDto
                    {
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        AccountId = FormAccountId
                    };
                    var result = await _cashboxService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء الخزنة: {result.Data.Code} — {result.Data.NameAr}";
                        IsEditing = false;
                        IsNew = false;
                        await LoadCashboxesAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else
                {
                    var dto = new UpdateCashboxDto
                    {
                        Id = SelectedItem.Id,
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        AccountId = FormAccountId
                    };
                    var result = await _cashboxService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث الخزنة: {result.Data.NameAr}";
                        IsEditing = false;
                        await LoadCashboxesAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
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

        // ── Set Default ─────────────────────────────────────────

        private async Task SetDefaultAsync()
        {
            if (SelectedItem == null) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _cashboxService.SetDefaultAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم تعيين «{SelectedItem.NameAr}» كخزنة افتراضية";
                    await LoadCashboxesAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تعيين الافتراضي", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Deactivate ──────────────────────────────────────────

        private async Task DeactivateAsync()
        {
            if (SelectedItem == null) return;

            if (!_dialog.Confirm(
                $"هل أنت متأكد من تعطيل الخزنة «{SelectedItem.NameAr}»؟",
                "تأكيد التعطيل")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _cashboxService.DeactivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تعطيل الخزنة";
                    await LoadCashboxesAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("التعطيل", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Activate ────────────────────────────────────────────

        private async Task ActivateAsync()
        {
            if (SelectedItem == null) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _cashboxService.ActivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تفعيل الخزنة";
                    await LoadCashboxesAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("التفعيل", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Cancel ──────────────────────────────────────────────

        private void CancelEditing(object parameter)
        {
            IsEditing = false;
            IsNew = false;
            ClearError();
            StatusMessage = "تم الإلغاء";
            if (SelectedItem != null) PopulateForm(SelectedItem);
        }

        // ── Helpers ─────────────────────────────────────────────

        private void PopulateForm(CashboxDto item)
        {
            FormCode = item.Code;
            FormNameAr = item.NameAr;
            FormNameEn = item.NameEn;
            FormAccountId = item.AccountId;
            IsEditing = false;
            IsNew = false;
        }

        public void EditSelected()
        {
            if (SelectedItem == null) return;
            IsEditing = true;
            IsNew = false;
            PopulateForm(SelectedItem);
            IsEditing = true;
        }
    }
}
