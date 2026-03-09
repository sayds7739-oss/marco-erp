using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Common;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    /// <summary>
    /// ViewModel for Customer management screen.
    /// </summary>
    public sealed class CustomerViewModel : BaseViewModel
    {
        private readonly ICustomerService _customerService;
        private readonly IDialogService _dialog;

        public CustomerViewModel(ICustomerService customerService, IDialogService dialog)
        {
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            AllCustomers = new ObservableCollection<CustomerDto>();
            CustomerTypeItems = new ObservableCollection<CustomerTypeOption>
            {
                new CustomerTypeOption(CustomerType.Individual, "فرد"),
                new CustomerTypeOption(CustomerType.Company, "شركة"),
                new CustomerTypeOption(CustomerType.Government, "جهة حكومية")
            };

            LoadCommand = new AsyncRelayCommand(LoadCustomersAsync);
            NewCommand = new AsyncRelayCommand(PrepareNewAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedItem != null);
            DeactivateCommand = new AsyncRelayCommand(DeactivateAsync, () => CanDeactivate);
            ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => CanActivate);
            CancelCommand = new RelayCommand(CancelEditing);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<CustomerDto> AllCustomers { get; }
        public ObservableCollection<CustomerTypeOption> CustomerTypeItems { get; }

        // ── Selection ────────────────────────────────────────────

        private CustomerDto _selectedItem;
        public CustomerDto SelectedItem
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

        private decimal _formCreditLimit;
        public decimal FormCreditLimit
        {
            get => _formCreditLimit;
            set => SetProperty(ref _formCreditLimit, value);
        }

        private string _formNotes;
        public string FormNotes
        {
            get => _formNotes;
            set => SetProperty(ref _formNotes, value);
        }

        private string _formEmail;
        public string FormEmail
        {
            get => _formEmail;
            set => SetProperty(ref _formEmail, value);
        }

        private CustomerTypeOption _formCustomerType;
        public CustomerTypeOption FormCustomerType
        {
            get => _formCustomerType;
            set => SetProperty(ref _formCustomerType, value);
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

        private decimal _formDefaultDiscountPercent;
        public decimal FormDefaultDiscountPercent
        {
            get => _formDefaultDiscountPercent;
            set => SetProperty(ref _formDefaultDiscountPercent, value);
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

        public async Task LoadCustomersAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _customerService.GetAllAsync();
                if (result.IsSuccess)
                {
                    AllCustomers.Clear();
                    foreach (var c in result.Data)
                        AllCustomers.Add(c);
                    StatusMessage = $"تم تحميل {AllCustomers.Count} عميل";
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

            // Get auto-generated code
            try
            {
                var codeResult = await _customerService.GetNextCodeAsync();
                FormCode = codeResult.IsSuccess ? codeResult.Data : "";
            }
            catch
            {
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
            FormCustomerType = CustomerTypeItems.FirstOrDefault(x => x.Value == CustomerType.Individual);
            FormCommercialRegister = "";
            FormCountry = "";
            FormPostalCode = "";
            FormContactPerson = "";
            FormWebsite = "";
            FormDefaultDiscountPercent = 0;
            FormPreviousBalance = 0;
            FormCreditLimit = 0;
            FormNotes = "";
            StatusMessage = "إدخال عميل جديد...";
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
                    var dto = new CreateCustomerDto
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
                        CustomerType = FormCustomerType?.Value ?? CustomerType.Individual,
                        CommercialRegister = FormCommercialRegister,
                        Country = FormCountry,
                        PostalCode = FormPostalCode,
                        ContactPerson = FormContactPerson,
                        Website = FormWebsite,
                        DefaultDiscountPercent = FormDefaultDiscountPercent,
                        PreviousBalance = FormPreviousBalance,
                        CreditLimit = FormCreditLimit,
                        Notes = FormNotes
                    };
                    var result = await _customerService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء العميل: {result.Data.Code} — {result.Data.NameAr}";
                        IsEditing = false;
                        IsNew = false;
                        await LoadCustomersAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else
                {
                    var dto = new UpdateCustomerDto
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
                        CustomerType = FormCustomerType?.Value ?? CustomerType.Individual,
                        CommercialRegister = FormCommercialRegister,
                        Country = FormCountry,
                        PostalCode = FormPostalCode,
                        ContactPerson = FormContactPerson,
                        Website = FormWebsite,
                        DefaultDiscountPercent = FormDefaultDiscountPercent,
                        CreditLimit = FormCreditLimit,
                        Notes = FormNotes
                    };
                    var result = await _customerService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث العميل: {result.Data.NameAr}";
                        IsEditing = false;
                        await LoadCustomersAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
            }
            catch (ConcurrencyConflictException ex)
            {
                await ConcurrencyHelper.ShowConflictAndRefreshAsync(ex, LoadCustomersAsync);
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

            if (!_dialog.Confirm(
                $"هل أنت متأكد من حذف العميل «{SelectedItem.NameAr}»؟\nالحذف سيكون ناعم (Soft Delete).",
                "تأكيد الحذف")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _customerService.DeleteAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم حذف العميل بنجاح";
                    await LoadCustomersAsync();
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
                var result = await _customerService.DeactivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تعطيل العميل";
                    await LoadCustomersAsync();
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
                var result = await _customerService.ActivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تفعيل العميل";
                    await LoadCustomersAsync();
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

        private void PopulateForm(CustomerDto item)
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
            FormCustomerType = CustomerTypeItems.FirstOrDefault(x => x.Value == item.CustomerType)
                               ?? CustomerTypeItems.FirstOrDefault();
            FormCommercialRegister = item.CommercialRegister;
            FormCountry = item.Country;
            FormPostalCode = item.PostalCode;
            FormContactPerson = item.ContactPerson;
            FormWebsite = item.Website;
            FormDefaultDiscountPercent = item.DefaultDiscountPercent;
            FormPreviousBalance = item.PreviousBalance;
            FormCreditLimit = item.CreditLimit;
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

        // ── Nested Types ─────────────────────────────────────────

        public sealed record CustomerTypeOption(CustomerType Value, string DisplayName)
        {
            public override string ToString() => DisplayName;
        }
    }
}
