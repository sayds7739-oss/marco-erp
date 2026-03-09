using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;

namespace MarcoERP.WpfUI.ViewModels.Inventory
{
    /// <summary>
    /// ViewModel for Category management screen.
    /// </summary>
    public sealed class CategoryViewModel : BaseViewModel
    {
        private readonly ICategoryService _categoryService;
        private readonly IDialogService _dialog;

        public CategoryViewModel(ICategoryService categoryService, IDialogService dialog)
        {
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            AllCategories = new ObservableCollection<CategoryDto>();
            ParentCategories = new ObservableCollection<CategoryDto>();

            LoadCommand = new AsyncRelayCommand(LoadCategoriesAsync);
            NewCommand = new RelayCommand(PrepareNew);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            DeleteCommand = new AsyncRelayCommand(DeactivateAsync, () => CanDeactivate);
            ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => CanActivate);
            CancelCommand = new RelayCommand(CancelEditing);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<CategoryDto> AllCategories { get; }
        public ObservableCollection<CategoryDto> ParentCategories { get; }

        // ── Selection ────────────────────────────────────────────

        private CategoryDto _selectedItem;
        public CategoryDto SelectedItem
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

        private int? _formParentCategoryId;
        public int? FormParentCategoryId
        {
            get => _formParentCategoryId;
            set => SetProperty(ref _formParentCategoryId, value);
        }

        private int _formLevel = 1;
        public int FormLevel
        {
            get => _formLevel;
            set => SetProperty(ref _formLevel, value);
        }

        private string _formDescription;
        public string FormDescription
        {
            get => _formDescription;
            set => SetProperty(ref _formDescription, value);
        }

        public IReadOnlyList<int> Levels { get; } = new List<int> { 1, 2, 3 };

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

        public async Task LoadCategoriesAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _categoryService.GetAllAsync();
                if (result.IsSuccess)
                {
                    AllCategories.Clear();
                    ParentCategories.Clear();
                    foreach (var c in result.Data)
                    {
                        AllCategories.Add(c);
                        if (c.Level < 3)
                            ParentCategories.Add(c);
                    }
                    StatusMessage = $"تم تحميل {AllCategories.Count} تصنيف";
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

            if (SelectedItem != null)
            {
                FormParentCategoryId = SelectedItem.Id;
                FormLevel = Math.Min(SelectedItem.Level + 1, 3);
            }
            else
            {
                FormParentCategoryId = null;
                FormLevel = 1;
            }

            FormNameAr = "";
            FormNameEn = "";
            FormDescription = "";
            StatusMessage = "إدخال تصنيف جديد...";
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
                    var dto = new CreateCategoryDto
                    {
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        ParentCategoryId = FormParentCategoryId,
                        Level = FormLevel,
                        Description = FormDescription
                    };
                    var result = await _categoryService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء التصنيف: {result.Data.NameAr}";
                        IsEditing = false;
                        IsNew = false;
                        await LoadCategoriesAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else
                {
                    var dto = new UpdateCategoryDto
                    {
                        Id = SelectedItem.Id,
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        Description = FormDescription
                    };
                    var result = await _categoryService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث التصنيف: {result.Data.NameAr}";
                        IsEditing = false;
                        await LoadCategoriesAsync();
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

            if (!_dialog.Confirm($"هل أنت متأكد من تعطيل التصنيف «{SelectedItem.NameAr}»؟", "تأكيد التعطيل")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _categoryService.DeactivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تعطيل التصنيف";
                    await LoadCategoriesAsync();
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
                var result = await _categoryService.ActivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تفعيل التصنيف";
                    await LoadCategoriesAsync();
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

        private void PopulateForm(CategoryDto item)
        {
            FormNameAr = item.NameAr;
            FormNameEn = item.NameEn;
            FormParentCategoryId = item.ParentCategoryId;
            FormLevel = item.Level;
            FormDescription = item.Description;
            IsEditing = false;
            IsNew = false;
        }

        /// <summary>Starts editing the selected item.</summary>
        public void EditSelected()
        {
            if (SelectedItem == null) return;
            IsEditing = true;
            IsNew = false;
            PopulateForm(SelectedItem);
            IsEditing = true; // re-set after populate resets it
        }
    }
}
