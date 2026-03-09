using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Security;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Security;

namespace MarcoERP.WpfUI.ViewModels.Settings
{
    /// <summary>
    /// ViewModel for Role Management screen.
    /// Full CRUD + Permission assignment.
    /// </summary>
    public sealed class RoleManagementViewModel : BaseViewModel
    {
        private readonly IRoleService _roleService;
        private readonly IDialogService _dialog;

        public RoleManagementViewModel(IRoleService roleService, IDialogService dialog)
        {
            _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            AllRoles = new ObservableCollection<RoleListDto>();
            PermissionItems = new ObservableCollection<PermissionCheckItem>();

            LoadCommand = new AsyncRelayCommand(LoadRolesAsync);
            NewCommand = new RelayCommand(PrepareNew);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => CanDelete);
            CancelCommand = new RelayCommand(CancelEditing);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());

            InitializePermissions();
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<RoleListDto> AllRoles { get; }
        public ObservableCollection<PermissionCheckItem> PermissionItems { get; }

        // ── Selection ────────────────────────────────────────────

        private RoleListDto _selectedRole;
        public RoleListDto SelectedRole
        {
            get => _selectedRole;
            set
            {
                if (SetProperty(ref _selectedRole, value))
                {
                    if (value != null && !IsEditing)
                        _ = LoadRoleDetailAsync(value.Id);
                    OnPropertyChanged(nameof(CanDelete));
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

        private int _editingId;

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

        private string _formDescription;
        public string FormDescription
        {
            get => _formDescription;
            set => SetProperty(ref _formDescription, value);
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand LoadCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand EditSelectedCommand { get; }

        // ── Can Execute ──────────────────────────────────────────

        public bool CanSave => !string.IsNullOrWhiteSpace(FormNameAr);
        public bool CanDelete => SelectedRole != null && !SelectedRole.IsSystem && !IsEditing;

        // ── Permissions ──────────────────────────────────────────

        private void InitializePermissions()
        {
            var allPermissions = new List<(string Key, string Display)>
            {
                (PermissionKeys.AccountsView, "عرض الحسابات"),
                (PermissionKeys.AccountsCreate, "إنشاء حسابات"),
                (PermissionKeys.AccountsEdit, "تعديل حسابات"),
                (PermissionKeys.AccountsDelete, "حذف حسابات"),
                (PermissionKeys.JournalView, "عرض القيود"),
                (PermissionKeys.JournalCreate, "إنشاء قيود"),
                (PermissionKeys.JournalPost, "ترحيل قيود"),
                (PermissionKeys.JournalReverse, "عكس قيود"),
                (PermissionKeys.FiscalYearManage, "إدارة السنوات المالية"),
                (PermissionKeys.FiscalPeriodManage, "إدارة الفترات المالية"),
                (PermissionKeys.InventoryView, "عرض المخزون"),
                (PermissionKeys.InventoryManage, "إدارة المخزون"),
                (PermissionKeys.InventoryAdjustmentView, "عرض تسويات المخزون"),
                (PermissionKeys.InventoryAdjustmentCreate, "إنشاء تسوية"),
                (PermissionKeys.InventoryAdjustmentPost, "ترحيل تسوية"),
                (PermissionKeys.SalesView, "عرض المبيعات"),
                (PermissionKeys.SalesCreate, "إنشاء فاتورة بيع"),
                (PermissionKeys.SalesPost, "ترحيل فاتورة بيع"),
                (PermissionKeys.SalesQuotationView, "عرض عروض الأسعار"),
                (PermissionKeys.SalesQuotationCreate, "إنشاء عرض سعر"),
                (PermissionKeys.PurchasesView, "عرض المشتريات"),
                (PermissionKeys.PurchasesCreate, "إنشاء فاتورة شراء"),
                (PermissionKeys.PurchasesPost, "ترحيل فاتورة شراء"),
                (PermissionKeys.PurchaseQuotationView, "عرض طلبات الشراء"),
                (PermissionKeys.PurchaseQuotationCreate, "إنشاء طلب شراء"),
                (PermissionKeys.TreasuryView, "عرض الخزينة"),
                (PermissionKeys.TreasuryCreate, "إنشاء سندات"),
                (PermissionKeys.TreasuryPost, "ترحيل سندات"),
                (PermissionKeys.PriceListView, "عرض قوائم الأسعار"),
                (PermissionKeys.PriceListManage, "إدارة قوائم الأسعار"),
                (PermissionKeys.PosAccess, "الوصول لنقطة البيع"),
                (PermissionKeys.ReportsView, "عرض التقارير"),
                (PermissionKeys.SettingsManage, "إدارة الإعدادات"),
                (PermissionKeys.UsersManage, "إدارة المستخدمين"),
                (PermissionKeys.RolesManage, "إدارة الأدوار"),
                (PermissionKeys.AuditLogView, "عرض سجل المراجعة"),
            };

            foreach (var (key, display) in allPermissions)
                PermissionItems.Add(new PermissionCheckItem { Key = key, DisplayName = display });
        }

        private List<string> GetSelectedPermissions()
        {
            return PermissionItems.Where(p => p.IsChecked).Select(p => p.Key).ToList();
        }

        private void SetPermissions(IEnumerable<string> permissions)
        {
            var permSet = new HashSet<string>(permissions ?? Enumerable.Empty<string>());
            foreach (var item in PermissionItems)
                item.IsChecked = permSet.Contains(item.Key);
        }

        private void ClearPermissions()
        {
            foreach (var item in PermissionItems)
                item.IsChecked = false;
        }

        // ── Load ─────────────────────────────────────────────────

        public async Task LoadRolesAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _roleService.GetAllAsync();
                if (result.IsSuccess)
                {
                    AllRoles.Clear();
                    foreach (var r in result.Data)
                        AllRoles.Add(r);
                }
                StatusMessage = $"تم تحميل {AllRoles.Count} دور";
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

        private async Task LoadRoleDetailAsync(int roleId)
        {
            try
            {
                var result = await _roleService.GetByIdAsync(roleId);
                if (result.IsSuccess)
                {
                    _editingId = result.Data.Id;
                    FormNameAr = result.Data.NameAr;
                    FormNameEn = result.Data.NameEn;
                    FormDescription = result.Data.Description;
                    SetPermissions(result.Data.Permissions);
                    IsEditing = false;
                    IsNew = false;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تحميل تفاصيل الدور", ex);
            }
        }

        // ── New ──────────────────────────────────────────────────

        private void PrepareNew(object parameter)
        {
            IsEditing = true;
            IsNew = true;
            ClearError();

            _editingId = 0;
            FormNameAr = "";
            FormNameEn = "";
            FormDescription = "";
            ClearPermissions();
            StatusMessage = "إدخال دور جديد...";
        }

        // ── Save ─────────────────────────────────────────────────

        private async Task SaveAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var selectedPermissions = GetSelectedPermissions();

                if (IsNew)
                {
                    var dto = new CreateRoleDto
                    {
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        Description = FormDescription,
                        Permissions = selectedPermissions
                    };
                    var result = await _roleService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء الدور: {result.Data.NameAr}";
                        IsEditing = false;
                        IsNew = false;
                        await LoadRolesAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else
                {
                    var dto = new UpdateRoleDto
                    {
                        Id = _editingId,
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        Description = FormDescription,
                        Permissions = selectedPermissions
                    };
                    var result = await _roleService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث الدور: {result.Data.NameAr}";
                        IsEditing = false;
                        await LoadRolesAsync();
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

        // ── Delete ──────────────────────────────────────────────

        private async Task DeleteAsync()
        {
            if (SelectedRole == null || SelectedRole.IsSystem) return;

            if (!_dialog.Confirm(
                $"هل أنت متأكد من حذف الدور «{SelectedRole.NameAr}»؟",
                "تأكيد الحذف")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _roleService.DeleteAsync(SelectedRole.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم حذف الدور";
                    FormNameAr = "";
                    FormNameEn = "";
                    FormDescription = "";
                    ClearPermissions();
                    await LoadRolesAsync();
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
            if (SelectedRole != null)
                _ = LoadRoleDetailAsync(SelectedRole.Id);
        }

        // ── Edit Selected ───────────────────────────────────────

        public void EditSelected()
        {
            if (SelectedRole == null) return;
            IsEditing = true;
            IsNew = false;
        }
    }

    /// <summary>
    /// Represents a single permission checkbox item for the UI.
    /// </summary>
    public sealed class PermissionCheckItem : INotifyPropertyChanged
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
