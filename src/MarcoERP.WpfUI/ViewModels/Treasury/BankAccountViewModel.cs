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
    /// ViewModel for Bank Account management screen.
    /// </summary>
    public sealed class BankAccountViewModel : BaseViewModel
    {
        private readonly IBankAccountService _bankAccountService;
        private readonly IAccountService _accountService;
        private readonly IDialogService _dialog;

        public BankAccountViewModel(IBankAccountService bankAccountService, IAccountService accountService, IDialogService dialog)
        {
            _bankAccountService = bankAccountService ?? throw new ArgumentNullException(nameof(bankAccountService));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            AllBankAccounts = new ObservableCollection<BankAccountDto>();
            Accounts = new ObservableCollection<AccountDto>();

            LoadCommand = new AsyncRelayCommand(LoadBankAccountsAsync);
            NewCommand = new RelayCommand(PrepareNew);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            SetDefaultCommand = new AsyncRelayCommand(SetDefaultAsync, () => SelectedItem != null);
            DeleteCommand = new AsyncRelayCommand(DeactivateAsync, () => CanDeactivate);
            ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => CanActivate);
            CancelCommand = new RelayCommand(CancelEditing);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<BankAccountDto> AllBankAccounts { get; }
        public ObservableCollection<AccountDto> Accounts { get; }

        // ── Selection ────────────────────────────────────────────

        private BankAccountDto _selectedItem;
        public BankAccountDto SelectedItem
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

        private string _formBankName;
        public string FormBankName
        {
            get => _formBankName;
            set => SetProperty(ref _formBankName, value);
        }

        private string _formAccountNumber;
        public string FormAccountNumber
        {
            get => _formAccountNumber;
            set => SetProperty(ref _formAccountNumber, value);
        }

        private string _formIBAN;
        public string FormIBAN
        {
            get => _formIBAN;
            set => SetProperty(ref _formIBAN, value);
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

        public async Task LoadBankAccountsAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var bankResult = await _bankAccountService.GetAllAsync();
                var accountResult = await _accountService.GetAllAsync();

                if (bankResult.IsSuccess)
                {
                    AllBankAccounts.Clear();
                    foreach (var b in bankResult.Data)
                        AllBankAccounts.Add(b);
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

                StatusMessage = $"تم تحميل {AllBankAccounts.Count} حساب بنكي";
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

        private void PrepareNew(object parameter)
        {
            IsEditing = true;
            IsNew = true;
            ClearError();

            FormCode = "(تلقائي)";
            FormNameAr = "";
            FormNameEn = "";
            FormBankName = "";
            FormAccountNumber = "";
            FormIBAN = "";
            FormAccountId = null;
            StatusMessage = "إدخال حساب بنكي جديد...";
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
                    var dto = new CreateBankAccountDto
                    {
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        BankName = FormBankName,
                        AccountNumber = FormAccountNumber,
                        IBAN = FormIBAN,
                        AccountId = FormAccountId
                    };
                    var result = await _bankAccountService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء الحساب البنكي: {result.Data.Code} — {result.Data.NameAr}";
                        IsEditing = false;
                        IsNew = false;
                        await LoadBankAccountsAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else
                {
                    var dto = new UpdateBankAccountDto
                    {
                        Id = SelectedItem.Id,
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        BankName = FormBankName,
                        AccountNumber = FormAccountNumber,
                        IBAN = FormIBAN,
                        AccountId = FormAccountId
                    };
                    var result = await _bankAccountService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث الحساب البنكي: {result.Data.NameAr}";
                        IsEditing = false;
                        await LoadBankAccountsAsync();
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
                var result = await _bankAccountService.SetDefaultAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم تعيين «{SelectedItem.NameAr}» كحساب بنكي افتراضي";
                    await LoadBankAccountsAsync();
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
                $"هل أنت متأكد من تعطيل الحساب البنكي «{SelectedItem.NameAr}»؟",
                "تأكيد التعطيل")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _bankAccountService.DeactivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تعطيل الحساب البنكي";
                    await LoadBankAccountsAsync();
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
                var result = await _bankAccountService.ActivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تفعيل الحساب البنكي";
                    await LoadBankAccountsAsync();
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

        private void PopulateForm(BankAccountDto item)
        {
            FormCode = item.Code;
            FormNameAr = item.NameAr;
            FormNameEn = item.NameEn;
            FormBankName = item.BankName;
            FormAccountNumber = item.AccountNumber;
            FormIBAN = item.IBAN;
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
