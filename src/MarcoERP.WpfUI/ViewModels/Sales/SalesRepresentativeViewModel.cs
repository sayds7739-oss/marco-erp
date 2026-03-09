using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Common;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    /// <summary>
    /// ViewModel for Sales Representative management screen.
    /// </summary>
    public sealed class SalesRepresentativeViewModel : BaseViewModel
    {
        private readonly ISalesRepresentativeService _service;
        private readonly IDialogService _dialog;

        public SalesRepresentativeViewModel(ISalesRepresentativeService service, IDialogService dialog)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            AllRepresentatives = new ObservableCollection<SalesRepresentativeDto>();

            LoadCommand = new AsyncRelayCommand(LoadAsync);
            NewCommand = new AsyncRelayCommand(PrepareNewAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedItem != null);
            DeactivateCommand = new AsyncRelayCommand(DeactivateAsync, () => CanDeactivate);
            ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => CanActivate);
            CancelCommand = new RelayCommand(CancelEditing);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<SalesRepresentativeDto> AllRepresentatives { get; }

        // ── Selection ────────────────────────────────────────────

        private SalesRepresentativeDto _selectedItem;
        public SalesRepresentativeDto SelectedItem
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

        private string _formEmail;
        public string FormEmail
        {
            get => _formEmail;
            set => SetProperty(ref _formEmail, value);
        }

        private decimal _formCommissionRate;
        public decimal FormCommissionRate
        {
            get => _formCommissionRate;
            set => SetProperty(ref _formCommissionRate, value);
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

        public async Task LoadAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _service.GetAllAsync();
                if (result.IsSuccess)
                {
                    AllRepresentatives.Clear();
                    foreach (var r in result.Data)
                        AllRepresentatives.Add(r);
                    StatusMessage = $"تم تحميل {AllRepresentatives.Count} مندوب";
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
                var codeResult = await _service.GetNextCodeAsync();
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
            FormEmail = "";
            FormCommissionRate = 0;
            FormNotes = "";
            StatusMessage = "إدخال مندوب جديد...";
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
                    var dto = new CreateSalesRepresentativeDto
                    {
                        Code = FormCode,
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        Phone = FormPhone,
                        Mobile = FormMobile,
                        Email = FormEmail,
                        CommissionRate = FormCommissionRate,
                        Notes = FormNotes
                    };
                    var result = await _service.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء المندوب: {result.Data.Code} — {result.Data.NameAr}";
                        IsEditing = false;
                        IsNew = false;
                        await LoadAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else
                {
                    var dto = new UpdateSalesRepresentativeDto
                    {
                        Id = SelectedItem.Id,
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        Phone = FormPhone,
                        Mobile = FormMobile,
                        Email = FormEmail,
                        CommissionRate = FormCommissionRate,
                        Notes = FormNotes
                    };
                    var result = await _service.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث المندوب: {result.Data.NameAr}";
                        IsEditing = false;
                        await LoadAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
            }
            catch (ConcurrencyConflictException ex)
            {
                await ConcurrencyHelper.ShowConflictAndRefreshAsync(ex, LoadAsync);
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
                $"هل أنت متأكد من حذف المندوب «{SelectedItem.NameAr}»؟\nالحذف سيكون ناعم (Soft Delete).",
                "تأكيد الحذف")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _service.DeleteAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم حذف المندوب بنجاح";
                    await LoadAsync();
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

        // ── Deactivate / Activate ───────────────────────────────

        private async Task DeactivateAsync()
        {
            if (SelectedItem == null) return;
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _service.DeactivateAsync(SelectedItem.Id);
                if (result.IsSuccess) { StatusMessage = "تم تعطيل المندوب"; await LoadAsync(); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("التعطيل", ex); }
            finally { IsBusy = false; }
        }

        private async Task ActivateAsync()
        {
            if (SelectedItem == null) return;
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _service.ActivateAsync(SelectedItem.Id);
                if (result.IsSuccess) { StatusMessage = "تم تفعيل المندوب"; await LoadAsync(); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("التفعيل", ex); }
            finally { IsBusy = false; }
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

        private void PopulateForm(SalesRepresentativeDto item)
        {
            FormCode = item.Code;
            FormNameAr = item.NameAr;
            FormNameEn = item.NameEn;
            FormPhone = item.Phone;
            FormMobile = item.Mobile;
            FormEmail = item.Email;
            FormCommissionRate = item.CommissionRate;
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
