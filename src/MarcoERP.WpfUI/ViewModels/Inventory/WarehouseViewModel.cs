using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Accounting;
using MarcoERP.Application.Interfaces.Inventory;

namespace MarcoERP.WpfUI.ViewModels.Inventory
{
    /// <summary>
    /// ViewModel for Warehouse management screen.
    /// </summary>
    public sealed class WarehouseViewModel : BaseViewModel
    {
        private readonly IWarehouseService _warehouseService;
        private readonly IAccountService _accountService;
        private readonly IDialogService _dialog;

        public WarehouseViewModel(IWarehouseService warehouseService, IAccountService accountService, IDialogService dialog)
        {
            _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            AllWarehouses = new ObservableCollection<WarehouseDto>();
            Accounts = new ObservableCollection<AccountDto>();

            LoadCommand = new AsyncRelayCommand(LoadWarehousesAsync);
            NewCommand = new RelayCommand(PrepareNew);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            SetDefaultCommand = new AsyncRelayCommand(SetDefaultAsync, () => SelectedItem != null);
            DeleteCommand = new AsyncRelayCommand(DeactivateAsync, () => CanDeactivate);
            ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => CanActivate);
            CancelCommand = new RelayCommand(CancelEditing);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<WarehouseDto> AllWarehouses { get; }
        public ObservableCollection<AccountDto> Accounts { get; }

        // ── Selection ────────────────────────────────────────────

        private WarehouseDto _selectedItem;
        public WarehouseDto SelectedItem
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
            set { SetProperty(ref _formCode, value); OnPropertyChanged(nameof(CanSave)); }
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

        private string _formAddress;
        public string FormAddress
        {
            get => _formAddress;
            set => SetProperty(ref _formAddress, value);
        }

        private string _formPhone;
        public string FormPhone
        {
            get => _formPhone;
            set => SetProperty(ref _formPhone, value);
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

        public bool CanSave => !string.IsNullOrWhiteSpace(FormNameAr)
                             && (!IsNew || !string.IsNullOrWhiteSpace(FormCode));
        public bool CanDeactivate => SelectedItem != null && SelectedItem.IsActive;
        public bool CanActivate => SelectedItem != null && !SelectedItem.IsActive;

        // ── Load ─────────────────────────────────────────────────

        public async Task LoadWarehousesAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var warehouseResult = await _warehouseService.GetAllAsync();
                var accountResult = await _accountService.GetAllAsync();

                if (warehouseResult.IsSuccess)
                {
                    AllWarehouses.Clear();
                    foreach (var w in warehouseResult.Data)
                        AllWarehouses.Add(w);
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

                StatusMessage = $"تم تحميل {AllWarehouses.Count} مخزن";
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

            FormCode = "";
            FormNameAr = "";
            FormNameEn = "";
            FormAddress = "";
            FormPhone = "";
            FormAccountId = null;
            StatusMessage = "إدخال مخزن جديد...";
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
                    var dto = new CreateWarehouseDto
                    {
                        Code = FormCode,
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        Address = FormAddress,
                        Phone = FormPhone,
                        AccountId = FormAccountId
                    };
                    var result = await _warehouseService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء المخزن: {result.Data.Code} — {result.Data.NameAr}";
                        IsEditing = false;
                        IsNew = false;
                        await LoadWarehousesAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else
                {
                    var dto = new UpdateWarehouseDto
                    {
                        Id = SelectedItem.Id,
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        Address = FormAddress,
                        Phone = FormPhone,
                        AccountId = FormAccountId
                    };
                    var result = await _warehouseService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث المخزن: {result.Data.NameAr}";
                        IsEditing = false;
                        await LoadWarehousesAsync();
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
                var result = await _warehouseService.SetDefaultAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم تعيين «{SelectedItem.NameAr}» كمخزن افتراضي";
                    await LoadWarehousesAsync();
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

        // ── Deactivate ──────────────────────────────────────────

        private async Task DeactivateAsync()
        {
            if (SelectedItem == null) return;

            if (!_dialog.Confirm($"هل أنت متأكد من تعطيل المخزن «{SelectedItem.NameAr}»؟", "تأكيد التعطيل")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _warehouseService.DeactivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تعطيل المخزن";
                    await LoadWarehousesAsync();
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

        // ── Activate ────────────────────────────────────────────

        private async Task ActivateAsync()
        {
            if (SelectedItem == null) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _warehouseService.ActivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تفعيل المخزن";
                    await LoadWarehousesAsync();
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

        private void PopulateForm(WarehouseDto item)
        {
            FormCode = item.Code;
            FormNameAr = item.NameAr;
            FormNameEn = item.NameEn;
            FormAddress = item.Address;
            FormPhone = item.Phone;
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
