using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Common;

namespace MarcoERP.WpfUI.ViewModels.Purchases
{
    /// <summary>
    /// ViewModel for Supplier management screen.
    /// </summary>
    public sealed class SupplierViewModel : BaseViewModel
    {
        private readonly ISupplierService _supplierService;
        private readonly IDialogService _dialog;

        public SupplierViewModel(ISupplierService supplierService, IDialogService dialog)
        {
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            AllSuppliers = new ObservableCollection<SupplierDto>();

            LoadCommand = new AsyncRelayCommand(LoadSuppliersAsync);
            NewCommand = new AsyncRelayCommand(PrepareNewAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedItem != null);
            DeactivateCommand = new AsyncRelayCommand(DeactivateAsync, () => CanDeactivate);
            ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => CanActivate);
            CancelCommand = new RelayCommand(CancelEditing);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<SupplierDto> AllSuppliers { get; }

        // ── Selection ────────────────────────────────────────────

        private SupplierDto _selectedItem;
        public SupplierDto SelectedItem
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

        private string _formPhone;
        public string FormPhone
        {
            get => _formPhone;
            set => SetProperty(ref _formPhone, value);
        }

        private string _formMobile;
        public string FormMobile
        {
            get => _formMobile;
            set => SetProperty(ref _formMobile, value);
        }

        private string _formAddress;
        public string FormAddress
        {
            get => _formAddress;
            set => SetProperty(ref _formAddress, value);
        }

        private string _formCity;
        public string FormCity
        {
            get => _formCity;
            set => SetProperty(ref _formCity, value);
        }

        private string _formTaxNumber;
        public string FormTaxNumber
        {
            get => _formTaxNumber;
            set => SetProperty(ref _formTaxNumber, value);
        }

        private decimal _formPreviousBalance;
        public decimal FormPreviousBalance
        {
            get => _formPreviousBalance;
            set => SetProperty(ref _formPreviousBalance, value);
        }

        private string _formEmail;
        public string FormEmail
        {
            get => _formEmail;
            set => SetProperty(ref _formEmail, value);
        }

        private string _formCommercialRegister;
        public string FormCommercialRegister
        {
            get => _formCommercialRegister;
            set => SetProperty(ref _formCommercialRegister, value);
        }

        private string _formCountry;
        public string FormCountry
        {
            get => _formCountry;
            set => SetProperty(ref _formCountry, value);
        }

        private string _formPostalCode;
        public string FormPostalCode
        {
            get => _formPostalCode;
            set => SetProperty(ref _formPostalCode, value);
        }

        private string _formContactPerson;
        public string FormContactPerson
        {
            get => _formContactPerson;
            set => SetProperty(ref _formContactPerson, value);
        }

        private string _formWebsite;
        public string FormWebsite
        {
            get => _formWebsite;
            set => SetProperty(ref _formWebsite, value);
        }

        private decimal _formCreditLimit;
        public decimal FormCreditLimit
        {
            get => _formCreditLimit;
            set => SetProperty(ref _formCreditLimit, value);
        }

        private int? _formDaysAllowed;
        public int? FormDaysAllowed
        {
            get => _formDaysAllowed;
            set => SetProperty(ref _formDaysAllowed, value);
        }

        private string _formBankName;
        public string FormBankName
        {
            get => _formBankName;
            set => SetProperty(ref _formBankName, value);
        }

        private string _formBankAccountName;
        public string FormBankAccountName
        {
            get => _formBankAccountName;
            set => SetProperty(ref _formBankAccountName, value);
        }

        private string _formBankAccountNumber;
        public string FormBankAccountNumber
        {
            get => _formBankAccountNumber;
            set => SetProperty(ref _formBankAccountNumber, value);
        }

        private string _formIBAN;
        public string FormIBAN
        {
            get => _formIBAN;
            set => SetProperty(ref _formIBAN, value);
        }

        private string _formNotes;
        public string FormNotes
        {
            get => _formNotes;
            set => SetProperty(ref _formNotes, value);
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand LoadCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand DeactivateCommand { get; }
        public ICommand ActivateCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand EditSelectedCommand { get; }

        // ── Can Execute ──────────────────────────────────────────

        public bool CanSave => !string.IsNullOrWhiteSpace(FormNameAr);
        public bool CanDeactivate => SelectedItem != null && SelectedItem.IsActive;
        public bool CanActivate => SelectedItem != null && !SelectedItem.IsActive;

        // ── Load ─────────────────────────────────────────────────

        public async Task LoadSuppliersAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _supplierService.GetAllAsync();
                if (result.IsSuccess)
                {
                    AllSuppliers.Clear();
                    foreach (var s in result.Data)
                        AllSuppliers.Add(s);
                    StatusMessage = $"تم تحميل {AllSuppliers.Count} مورد";
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

        // ── New ──────────────────────────────────────────────────

        private async Task PrepareNewAsync()
        {
            IsEditing = true;
            IsNew = true;
            ClearError();

            try
            {
                var codeResult = await _supplierService.GetNextCodeAsync();
                FormCode = codeResult.IsSuccess ? codeResult.Data : "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupplierVM] Failed to get next code: {ex.Message}");
                FormCode = "";
            }

            FormNameAr = "";
            FormNameEn = "";
            FormPhone = "";
            FormMobile = "";
            FormAddress = "";
            FormCity = "";
            FormTaxNumber = "";
            FormEmail = "";
            FormCommercialRegister = "";
            FormCountry = "";
            FormPostalCode = "";
            FormContactPerson = "";
            FormWebsite = "";
            FormCreditLimit = 0;
            FormDaysAllowed = null;
            FormBankName = "";
            FormBankAccountName = "";
            FormBankAccountNumber = "";
            FormIBAN = "";
            FormPreviousBalance = 0;
            FormNotes = "";
            StatusMessage = "إدخال مورد جديد...";
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
                    var dto = new CreateSupplierDto
                    {
                        Code = FormCode,
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        Phone = FormPhone,
                        Mobile = FormMobile,
                        Address = FormAddress,
                        City = FormCity,
                        TaxNumber = FormTaxNumber,
                        Email = FormEmail,
                        CommercialRegister = FormCommercialRegister,
                        Country = FormCountry,
                        PostalCode = FormPostalCode,
                        ContactPerson = FormContactPerson,
                        Website = FormWebsite,
                        CreditLimit = FormCreditLimit,
                        DaysAllowed = FormDaysAllowed,
                        BankName = FormBankName,
                        BankAccountName = FormBankAccountName,
                        BankAccountNumber = FormBankAccountNumber,
                        IBAN = FormIBAN,
                        PreviousBalance = FormPreviousBalance,
                        Notes = FormNotes
                    };
                    var result = await _supplierService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء المورد: {result.Data.Code} — {result.Data.NameAr}";
                        IsEditing = false;
                        IsNew = false;
                        await LoadSuppliersAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else
                {
                    var dto = new UpdateSupplierDto
                    {
                        Id = SelectedItem.Id,
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        Phone = FormPhone,
                        Mobile = FormMobile,
                        Address = FormAddress,
                        City = FormCity,
                        TaxNumber = FormTaxNumber,
                        Email = FormEmail,
                        CommercialRegister = FormCommercialRegister,
                        Country = FormCountry,
                        PostalCode = FormPostalCode,
                        ContactPerson = FormContactPerson,
                        Website = FormWebsite,
                        CreditLimit = FormCreditLimit,
                        DaysAllowed = FormDaysAllowed,
                        BankName = FormBankName,
                        BankAccountName = FormBankAccountName,
                        BankAccountNumber = FormBankAccountNumber,
                        IBAN = FormIBAN,
                        Notes = FormNotes
                    };
                    var result = await _supplierService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث المورد: {result.Data.NameAr}";
                        IsEditing = false;
                        await LoadSuppliersAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
            }
            catch (ConcurrencyConflictException ex)
            {
                await ConcurrencyHelper.ShowConflictAndRefreshAsync(ex, LoadSuppliersAsync);
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

        // ── Delete ──────────────────────────────────────────────

        private async Task DeleteAsync()
        {
            if (SelectedItem == null) return;

            if (!_dialog.Confirm($"هل أنت متأكد من حذف المورد «{SelectedItem.NameAr}»؟\nالحذف سيكون ناعم (Soft Delete).", "تأكيد الحذف")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _supplierService.DeleteAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم حذف المورد بنجاح";
                    await LoadSuppliersAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("الحذف", ex);
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

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _supplierService.DeactivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تعطيل المورد";
                    await LoadSuppliersAsync();
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
                var result = await _supplierService.ActivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تفعيل المورد";
                    await LoadSuppliersAsync();
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

        private void PopulateForm(SupplierDto item)
        {
            FormCode = item.Code;
            FormNameAr = item.NameAr;
            FormNameEn = item.NameEn;
            FormPhone = item.Phone;
            FormMobile = item.Mobile;
            FormAddress = item.Address;
            FormCity = item.City;
            FormTaxNumber = item.TaxNumber;
            FormEmail = item.Email;
            FormCommercialRegister = item.CommercialRegister;
            FormCountry = item.Country;
            FormPostalCode = item.PostalCode;
            FormContactPerson = item.ContactPerson;
            FormWebsite = item.Website;
            FormCreditLimit = item.CreditLimit;
            FormDaysAllowed = item.DaysAllowed;
            FormBankName = item.BankName;
            FormBankAccountName = item.BankAccountName;
            FormBankAccountNumber = item.BankAccountNumber;
            FormIBAN = item.IBAN;
            FormPreviousBalance = item.PreviousBalance;
            FormNotes = item.Notes;
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
