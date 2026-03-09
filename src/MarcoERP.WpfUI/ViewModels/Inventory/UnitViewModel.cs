using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;

namespace MarcoERP.WpfUI.ViewModels.Inventory
{
    /// <summary>
    /// ViewModel for Unit of Measure management screen.
    /// </summary>
    public sealed class UnitViewModel : BaseViewModel
    {
        private readonly IUnitService _unitService;
        private readonly IDialogService _dialog;

        public UnitViewModel(IUnitService unitService, IDialogService dialog)
        {
            _unitService = unitService ?? throw new ArgumentNullException(nameof(unitService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            AllUnits = new ObservableCollection<UnitDto>();

            LoadCommand = new AsyncRelayCommand(LoadUnitsAsync);
            NewCommand = new RelayCommand(PrepareNew);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            DeleteCommand = new AsyncRelayCommand(DeactivateAsync, () => CanDeactivate);
            ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => CanActivate);
            CancelCommand = new RelayCommand(CancelEditing);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<UnitDto> AllUnits { get; }

        // ── Selection ────────────────────────────────────────────

        private UnitDto _selectedItem;
        public UnitDto SelectedItem
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

        private string _formAbbreviationAr;
        public string FormAbbreviationAr
        {
            get => _formAbbreviationAr;
            set => SetProperty(ref _formAbbreviationAr, value);
        }

        private string _formAbbreviationEn;
        public string FormAbbreviationEn
        {
            get => _formAbbreviationEn;
            set => SetProperty(ref _formAbbreviationEn, value);
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand LoadCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ActivateCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand EditSelectedCommand { get; }

        // ── Can Execute ──────────────────────────────────────────

        public bool CanSave => !string.IsNullOrWhiteSpace(FormNameAr);
        public bool CanDeactivate => SelectedItem != null && SelectedItem.IsActive;
        public bool CanActivate => SelectedItem != null && !SelectedItem.IsActive;

        // ── Load ─────────────────────────────────────────────────

        public async Task LoadUnitsAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _unitService.GetAllAsync();
                if (result.IsSuccess)
                {
                    AllUnits.Clear();
                    foreach (var u in result.Data)
                        AllUnits.Add(u);
                    StatusMessage = $"تم تحميل {AllUnits.Count} وحدة قياس";
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

        private void PrepareNew(object parameter)
        {
            IsEditing = true;
            IsNew = true;
            ClearError();

            FormNameAr = "";
            FormNameEn = "";
            FormAbbreviationAr = "";
            FormAbbreviationEn = "";
            StatusMessage = "إدخال وحدة قياس جديدة...";
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
                    var dto = new CreateUnitDto
                    {
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        AbbreviationAr = FormAbbreviationAr,
                        AbbreviationEn = FormAbbreviationEn
                    };
                    var result = await _unitService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء الوحدة: {result.Data.NameAr}";
                        IsEditing = false;
                        IsNew = false;
                        await LoadUnitsAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else
                {
                    var dto = new UpdateUnitDto
                    {
                        Id = SelectedItem.Id,
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        AbbreviationAr = FormAbbreviationAr,
                        AbbreviationEn = FormAbbreviationEn
                    };
                    var result = await _unitService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث الوحدة: {result.Data.NameAr}";
                        IsEditing = false;
                        await LoadUnitsAsync();
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

        // ── Deactivate ──────────────────────────────────────────

        private async Task DeactivateAsync()
        {
            if (SelectedItem == null) return;

            if (!_dialog.Confirm($"هل أنت متأكد من تعطيل الوحدة «{SelectedItem.NameAr}»؟", "تأكيد التعطيل")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _unitService.DeactivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تعطيل الوحدة";
                    await LoadUnitsAsync();
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
                var result = await _unitService.ActivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تفعيل الوحدة";
                    await LoadUnitsAsync();
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

        private void PopulateForm(UnitDto item)
        {
            FormNameAr = item.NameAr;
            FormNameEn = item.NameEn;
            FormAbbreviationAr = item.AbbreviationAr;
            FormAbbreviationEn = item.AbbreviationEn;
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
