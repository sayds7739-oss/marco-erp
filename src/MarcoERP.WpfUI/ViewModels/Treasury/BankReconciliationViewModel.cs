using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Treasury;

namespace MarcoERP.WpfUI.ViewModels.Treasury
{
    /// <summary>
    /// ViewModel for Bank Reconciliation management screen.
    /// </summary>
    public sealed class BankReconciliationViewModel : BaseViewModel
    {
        private readonly IBankReconciliationService _reconciliationService;
        private readonly IBankAccountService _bankAccountService;
        private readonly IDateTimeProvider _dateTime;
        private readonly IDialogService _dialog;

        public BankReconciliationViewModel(
            IBankReconciliationService reconciliationService,
            IBankAccountService bankAccountService,
            IDateTimeProvider dateTime,
            IDialogService dialog)
        {
            _reconciliationService = reconciliationService ?? throw new ArgumentNullException(nameof(reconciliationService));
            _bankAccountService = bankAccountService ?? throw new ArgumentNullException(nameof(bankAccountService));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            AllReconciliations = new ObservableCollection<BankReconciliationDto>();
            BankAccounts = new ObservableCollection<BankAccountDto>();
            ReconciliationItems = new ObservableCollection<BankReconciliationItemDto>();

            LoadCommand = new AsyncRelayCommand(LoadAsync);
            NewCommand = new RelayCommand(PrepareNew);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            CompleteCommand = new AsyncRelayCommand(CompleteAsync, () => SelectedReconciliation != null && !SelectedReconciliation.IsCompleted);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedReconciliation != null && !SelectedReconciliation.IsCompleted);
            CancelCommand = new RelayCommand(CancelEditing);
            AddItemCommand = new AsyncRelayCommand(AddItemAsync, () => CanAddItems && !string.IsNullOrWhiteSpace(ItemDescription));
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<BankReconciliationDto> AllReconciliations { get; }
        public ObservableCollection<BankAccountDto> BankAccounts { get; }
        public ObservableCollection<BankReconciliationItemDto> ReconciliationItems { get; }

        // ── Selection ────────────────────────────────────────────

        private BankAccountDto _selectedBankAccount;
        public BankAccountDto SelectedBankAccount
        {
            get => _selectedBankAccount;
            set
            {
                if (SetProperty(ref _selectedBankAccount, value) && value != null)
                    _ = FilterByBankAccountAsync();
            }
        }

        private BankReconciliationDto _selectedReconciliation;
        public BankReconciliationDto SelectedReconciliation
        {
            get => _selectedReconciliation;
            set
            {
                if (SetProperty(ref _selectedReconciliation, value))
                {
                    if (value != null && !IsEditing)
                        _ = LoadReconciliationDetailAsync(value.Id);
                    OnPropertyChanged(nameof(CanAddItems));
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

        private int _formBankAccountId;
        public int FormBankAccountId
        {
            get => _formBankAccountId;
            set => SetProperty(ref _formBankAccountId, value);
        }

        private DateTime _formDate;
        public DateTime FormDate
        {
            get => _formDate;
            set => SetProperty(ref _formDate, value);
        }

        private string _formStatementBalance = "0";
        public string FormStatementBalance
        {
            get => _formStatementBalance;
            set { SetProperty(ref _formStatementBalance, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private decimal _formSystemBalance;
        public decimal FormSystemBalance
        {
            get => _formSystemBalance;
            set => SetProperty(ref _formSystemBalance, value);
        }

        private decimal _formDifference;
        public decimal FormDifference
        {
            get => _formDifference;
            set
            {
                SetProperty(ref _formDifference, value);
                OnPropertyChanged(nameof(FormDifferenceColor));
            }
        }

        public Brush FormDifferenceColor =>
            FormDifference == 0 ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) // green
            : new SolidColorBrush(Color.FromRgb(244, 67, 54)); // red

        private string _formNotes;
        public string FormNotes
        {
            get => _formNotes;
            set => SetProperty(ref _formNotes, value);
        }

        // ── Item Form Fields ────────────────────────────────────

        private string _itemDescription;
        public string ItemDescription
        {
            get => _itemDescription;
            set => SetProperty(ref _itemDescription, value);
        }

        private string _itemAmount = "0";
        public string ItemAmount
        {
            get => _itemAmount;
            set => SetProperty(ref _itemAmount, value);
        }

        private string _itemReference;
        public string ItemReference
        {
            get => _itemReference;
            set => SetProperty(ref _itemReference, value);
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand LoadCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CompleteCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AddItemCommand { get; }

        // ── Can Execute ──────────────────────────────────────────

        public bool CanSave => decimal.TryParse(FormStatementBalance, out _) && FormBankAccountId > 0;
        public bool CanAddItems => SelectedReconciliation != null && !SelectedReconciliation.IsCompleted;

        // ── Load ─────────────────────────────────────────────────

        public async Task LoadAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var bankResult = await _bankAccountService.GetAllAsync();
                if (bankResult.IsSuccess)
                {
                    BankAccounts.Clear();
                    foreach (var b in bankResult.Data)
                        BankAccounts.Add(b);
                }

                var result = await _reconciliationService.GetAllAsync();
                if (result.IsSuccess)
                {
                    AllReconciliations.Clear();
                    foreach (var r in result.Data)
                        AllReconciliations.Add(r);
                }

                StatusMessage = $"تم تحميل {AllReconciliations.Count} تسوية";
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

        private async Task FilterByBankAccountAsync()
        {
            if (SelectedBankAccount == null) return;
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _reconciliationService.GetByBankAccountAsync(SelectedBankAccount.Id);
                if (result.IsSuccess)
                {
                    AllReconciliations.Clear();
                    foreach (var r in result.Data)
                        AllReconciliations.Add(r);
                    StatusMessage = $"تم تحميل {AllReconciliations.Count} تسوية لحساب «{SelectedBankAccount.NameAr}»";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("التصفية", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadReconciliationDetailAsync(int id)
        {
            IsBusy = true;
            try
            {
                var result = await _reconciliationService.GetByIdAsync(id);
                if (result.IsSuccess)
                {
                    var dto = result.Data;
                    FormBankAccountId = dto.BankAccountId;
                    FormDate = dto.ReconciliationDate;
                    FormStatementBalance = dto.StatementBalance.ToString("F2");
                    FormSystemBalance = dto.SystemBalance;
                    FormDifference = dto.Difference;
                    FormNotes = dto.Notes;

                    ReconciliationItems.Clear();
                    foreach (var item in dto.Items)
                        ReconciliationItems.Add(item);

                    IsEditing = false;
                    IsNew = false;
                    OnPropertyChanged(nameof(CanAddItems));
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تحميل التفاصيل", ex);
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

            FormBankAccountId = SelectedBankAccount?.Id ?? 0;
            FormDate = _dateTime.Today;
            FormStatementBalance = "0";
            FormSystemBalance = 0;
            FormDifference = 0;
            FormNotes = "";
            ReconciliationItems.Clear();
            StatusMessage = "إنشاء تسوية بنكية جديدة...";
        }

        // ── Save ─────────────────────────────────────────────────

        private async Task SaveAsync()
        {
            if (!decimal.TryParse(FormStatementBalance, out var balance))
            {
                ErrorMessage = "رصيد كشف الحساب غير صحيح.";
                return;
            }

            IsBusy = true;
            ClearError();
            try
            {
                if (IsNew)
                {
                    var dto = new CreateBankReconciliationDto
                    {
                        BankAccountId = FormBankAccountId,
                        ReconciliationDate = FormDate,
                        StatementBalance = balance,
                        Notes = FormNotes
                    };
                    var result = await _reconciliationService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = "تم إنشاء التسوية بنجاح";
                        IsEditing = false;
                        IsNew = false;
                        await LoadAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else if (SelectedReconciliation != null)
                {
                    var dto = new UpdateBankReconciliationDto
                    {
                        Id = SelectedReconciliation.Id,
                        ReconciliationDate = FormDate,
                        StatementBalance = balance,
                        Notes = FormNotes
                    };
                    var result = await _reconciliationService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = "تم تحديث التسوية";
                        IsEditing = false;
                        await LoadAsync();
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

        // ── Add Item ─────────────────────────────────────────────

        private async Task AddItemAsync()
        {
            if (SelectedReconciliation == null || string.IsNullOrWhiteSpace(ItemDescription)) return;

            if (!decimal.TryParse(ItemAmount, out var amount))
            {
                ErrorMessage = "مبلغ البند غير صحيح.";
                return;
            }

            IsBusy = true;
            ClearError();
            try
            {
                var dto = new CreateBankReconciliationItemDto
                {
                    BankReconciliationId = SelectedReconciliation.Id,
                    TransactionDate = _dateTime.Today,
                    Description = ItemDescription,
                    Amount = amount,
                    Reference = ItemReference
                };
                var result = await _reconciliationService.AddItemAsync(dto);
                if (result.IsSuccess)
                {
                    ItemDescription = "";
                    ItemAmount = "0";
                    ItemReference = "";
                    await LoadReconciliationDetailAsync(SelectedReconciliation.Id);
                    StatusMessage = "تم إضافة البند";
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("إضافة البند", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Complete ─────────────────────────────────────────────

        private async Task CompleteAsync()
        {
            if (SelectedReconciliation == null) return;

            if (!_dialog.Confirm(
                "هل أنت متأكد من اكتمال التسوية؟ لن تتمكن من تعديلها بعد ذلك.",
                "تأكيد الاكتمال")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _reconciliationService.CompleteAsync(SelectedReconciliation.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم اكتمال التسوية";
                    await LoadAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("اكتمال التسوية", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Delete ──────────────────────────────────────────────

        private async Task DeleteAsync()
        {
            if (SelectedReconciliation == null) return;

            if (!_dialog.Confirm(
                "هل أنت متأكد من حذف هذه التسوية؟",
                "تأكيد الحذف")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _reconciliationService.DeleteAsync(SelectedReconciliation.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم حذف التسوية";
                    ReconciliationItems.Clear();
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

        // ── Cancel ──────────────────────────────────────────────

        private void CancelEditing(object parameter)
        {
            IsEditing = false;
            IsNew = false;
            ClearError();
            StatusMessage = "تم الإلغاء";
            if (SelectedReconciliation != null)
                _ = LoadReconciliationDetailAsync(SelectedReconciliation.Id);
        }
    }
}
