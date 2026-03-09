using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Security;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Security;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Common;

namespace MarcoERP.WpfUI.ViewModels.Settings
{
    /// <summary>
    /// ViewModel for User Management screen (إدارة المستخدمين).
    /// Phase 5C: Full CRUD for users with role assignment.
    /// </summary>
    public sealed class UserManagementViewModel : BaseViewModel
    {
        private readonly IUserService _userService;
        private readonly IRoleService _roleService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IDialogService _dialog;

        public UserManagementViewModel(IUserService userService, IRoleService roleService, IDateTimeProvider dateTimeProvider, IDialogService dialog)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            AllUsers = new ObservableCollection<UserListDto>();
            Roles = new ObservableCollection<RoleListDto>();

            LoadCommand = new AsyncRelayCommand(LoadUsersAsync);
            NewCommand = new RelayCommand(PrepareNew);
            EditCommand = new RelayCommand(EditSelected, () => _currentDetail != null);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            UnlockCommand = new AsyncRelayCommand(UnlockAsync, () => CanUnlock);
            DeactivateCommand = new AsyncRelayCommand(DeactivateAsync, () => CanDeactivate);
            ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => CanActivate);
            ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, () => SelectedItem != null);
            CancelCommand = new RelayCommand(CancelEditing);
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<UserListDto> AllUsers { get; }
        public ObservableCollection<RoleListDto> Roles { get; }

        // ── Selection ────────────────────────────────────────────

        private UserListDto _selectedItem;
        public UserListDto SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    if (value != null && !IsEditing)
                        _ = LoadDetailAsync(value.Id);
                    OnPropertyChanged(nameof(CanUnlock));
                    OnPropertyChanged(nameof(CanDeactivate));
                    OnPropertyChanged(nameof(CanActivate));
                }
            }
        }

        private UserDto _currentDetail;

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

        private string _formUsername;
        public string FormUsername
        {
            get => _formUsername;
            set { SetProperty(ref _formUsername, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private string _formPassword;
        public string FormPassword
        {
            get => _formPassword;
            set => SetProperty(ref _formPassword, value);
        }

        private string _formConfirmPassword;
        public string FormConfirmPassword
        {
            get => _formConfirmPassword;
            set => SetProperty(ref _formConfirmPassword, value);
        }

        private string _formFullNameAr;
        public string FormFullNameAr
        {
            get => _formFullNameAr;
            set { SetProperty(ref _formFullNameAr, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private string _formFullNameEn;
        public string FormFullNameEn
        {
            get => _formFullNameEn;
            set => SetProperty(ref _formFullNameEn, value);
        }

        private string _formEmail;
        public string FormEmail
        {
            get => _formEmail;
            set => SetProperty(ref _formEmail, value);
        }

        private string _formPhone;
        public string FormPhone
        {
            get => _formPhone;
            set => SetProperty(ref _formPhone, value);
        }

        private int? _formRoleId;
        public int? FormRoleId
        {
            get => _formRoleId;
            set { SetProperty(ref _formRoleId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand LoadCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand UnlockCommand { get; }
        public ICommand DeactivateCommand { get; }
        public ICommand ActivateCommand { get; }
        public ICommand ResetPasswordCommand { get; }
        public ICommand CancelCommand { get; }

        // ── Can Execute ──────────────────────────────────────────

        public bool CanSave => !string.IsNullOrWhiteSpace(FormFullNameAr) && FormRoleId.HasValue
            && (IsNew ? !string.IsNullOrWhiteSpace(FormUsername) : true);
        public bool CanUnlock => SelectedItem != null && SelectedItem.IsLocked;
        public bool CanDeactivate => SelectedItem != null && SelectedItem.IsActive;
        public bool CanActivate => SelectedItem != null && !SelectedItem.IsActive;

        // ── Load ─────────────────────────────────────────────────

        public async Task LoadUsersAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var usersResult = await _userService.GetAllAsync();
                var rolesResult = await _roleService.GetAllAsync();

                if (usersResult.IsSuccess)
                {
                    AllUsers.Clear();
                    foreach (var u in usersResult.Data)
                        AllUsers.Add(u);
                }

                if (rolesResult.IsSuccess)
                {
                    Roles.Clear();
                    foreach (var r in rolesResult.Data)
                        Roles.Add(r);
                }

                StatusMessage = $"تم تحميل {AllUsers.Count} مستخدم";
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

        private async Task LoadDetailAsync(int id)
        {
            try
            {
                var result = await _userService.GetByIdAsync(id);
                if (result.IsSuccess)
                {
                    _currentDetail = result.Data;
                    RelayCommand.RaiseCanExecuteChanged();
                    PopulateForm(result.Data);
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تحميل التفاصيل", ex);
            }
        }

        // ── New ──────────────────────────────────────────────────

        private void PrepareNew(object parameter)
        {
            IsEditing = true;
            IsNew = true;
            _currentDetail = null;
            RelayCommand.RaiseCanExecuteChanged();
            ClearError();

            FormUsername = "";
            FormPassword = "";
            FormConfirmPassword = "";
            FormFullNameAr = "";
            FormFullNameEn = "";
            FormEmail = "";
            FormPhone = "";
            FormRoleId = null;
            StatusMessage = "إدخال مستخدم جديد...";
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
                    var dto = new CreateUserDto
                    {
                        Username = FormUsername,
                        Password = FormPassword,
                        ConfirmPassword = FormConfirmPassword,
                        FullNameAr = FormFullNameAr,
                        FullNameEn = FormFullNameEn,
                        Email = FormEmail,
                        Phone = FormPhone,
                        RoleId = FormRoleId.Value
                    };
                    var result = await _userService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء المستخدم: {result.Data.Username}";
                        IsEditing = false;
                        IsNew = false;
                        await LoadUsersAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else if (_currentDetail != null)
                {
                    var dto = new UpdateUserDto
                    {
                        Id = _currentDetail.Id,
                        FullNameAr = FormFullNameAr,
                        FullNameEn = FormFullNameEn,
                        Email = FormEmail,
                        Phone = FormPhone,
                        RoleId = FormRoleId.Value
                    };
                    var result = await _userService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث المستخدم: {result.Data.Username}";
                        IsEditing = false;
                        await LoadUsersAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
            }
            catch (ConcurrencyConflictException ex)
            {
                await ConcurrencyHelper.ShowConflictAndRefreshAsync(ex, LoadUsersAsync);
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

        // ── Unlock ──────────────────────────────────────────────

        private async Task UnlockAsync()
        {
            if (SelectedItem == null) return;
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _userService.UnlockAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم فتح قفل الحساب";
                    await LoadUsersAsync();
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
            if (!_dialog.Confirm(
                $"هل أنت متأكد من تعطيل حساب «{SelectedItem.FullNameAr}»؟",
                "تأكيد التعطيل")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _userService.DeactivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تعطيل الحساب";
                    await LoadUsersAsync();
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
                var result = await _userService.ActivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم تفعيل الحساب";
                    await LoadUsersAsync();
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

        // ── Reset Password ──────────────────────────────────────

        private async Task ResetPasswordAsync()
        {
            if (SelectedItem == null) return;

            // H-12 fix: Generate a cryptographically random temporary password
            var tempPassword = GenerateSecureTemporaryPassword();
            if (!_dialog.Confirm(
                $"هل تريد إعادة تعيين كلمة مرور «{SelectedItem.FullNameAr}»؟\nكلمة المرور المؤقتة: {tempPassword}\nسيُطلب من المستخدم تغييرها عند الدخول.",
                "إعادة تعيين كلمة المرور")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var dto = new ResetPasswordDto
                {
                    UserId = SelectedItem.Id,
                    NewPassword = tempPassword,
                    ConfirmNewPassword = tempPassword
                };
                var result = await _userService.ResetPasswordAsync(dto);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم إعادة تعيين كلمة المرور بنجاح";
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
            if (_currentDetail != null) PopulateForm(_currentDetail);
            RelayCommand.RaiseCanExecuteChanged();
        }

        // ── Helpers ─────────────────────────────────────────────

        private void PopulateForm(UserDto item)
        {
            FormUsername = item.Username;
            FormPassword = "";
            FormConfirmPassword = "";
            FormFullNameAr = item.FullNameAr;
            FormFullNameEn = item.FullNameEn;
            FormEmail = item.Email;
            FormPhone = item.Phone;
            FormRoleId = item.RoleId;
            IsEditing = false;
            IsNew = false;
        }

        public void EditSelected()
        {
            if (_currentDetail == null) return;
            IsEditing = true;
            IsNew = false;
        }

        /// <summary>
        /// H-12 fix: Generates a cryptographically random temporary password.
        /// </summary>
        private static string GenerateSecureTemporaryPassword(int length = 12)
        {
            const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lower = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string special = "@#$%&*!";
            const string all = upper + lower + digits + special;

            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[length];
            rng.GetBytes(bytes);

            var chars = new char[length];
            // Guarantee at least one of each category
            chars[0] = upper[bytes[0] % upper.Length];
            chars[1] = lower[bytes[1] % lower.Length];
            chars[2] = digits[bytes[2] % digits.Length];
            chars[3] = special[bytes[3] % special.Length];
            for (int i = 4; i < length; i++)
                chars[i] = all[bytes[i] % all.Length];

            // Shuffle using Fisher-Yates
            for (int i = length - 1; i > 0; i--)
            {
                int j = bytes[i] % (i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
            return new string(chars);
        }
    }
}
