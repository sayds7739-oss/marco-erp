using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Accounting;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Application.Interfaces.Treasury;
using MarcoERP.Application.DTOs.Printing;
using MarcoERP.Application.Interfaces.Printing;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Common;
using MarcoERP.WpfUI.Navigation;
using MarcoERP.WpfUI.Services;

namespace MarcoERP.WpfUI.ViewModels.Treasury
{
    /// <summary>
    /// ViewModel for Cash Receipt (سند قبض) management screen.
    /// Flat form — no lines, just header fields + amount.
    /// </summary>
    public sealed class CashReceiptViewModel : BaseViewModel, INavigationAware
    {
        private readonly ICashReceiptService _receiptService;
        private readonly ICashboxService _cashboxService;
        private readonly IAccountService _accountService;
        private readonly ICustomerService _customerService;
        private readonly IDateTimeProvider _dateTime;
        private readonly IDialogService _dialog;
        private readonly IInvoicePdfPreviewService _previewService;
        private readonly IDocumentHtmlBuilder _htmlBuilder;

        private int? _linkedSalesInvoiceId;

        public ObservableCollection<CashReceiptListDto> Receipts { get; } = new();
        public ObservableCollection<CashboxDto> Cashboxes { get; } = new();
        public ObservableCollection<AccountDto> PostableAccounts { get; } = new();
        public ObservableCollection<CustomerDto> Customers { get; } = new();

        private CashReceiptListDto _selectedItem;
        public CashReceiptListDto SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value) && value != null && !IsEditing)
                    _ = LoadReceiptDetailAsync(value.Id);
            }
        }

        private CashReceiptDto _currentReceipt;
        public CashReceiptDto CurrentReceipt
        {
            get => _currentReceipt;
            set
            {
                SetProperty(ref _currentReceipt, value);
                OnPropertyChanged(nameof(IsDraft)); OnPropertyChanged(nameof(IsPosted));
                OnPropertyChanged(nameof(IsCancelled)); OnPropertyChanged(nameof(CanPost));
                OnPropertyChanged(nameof(CanCancel)); OnPropertyChanged(nameof(CanDeleteDraft));
            }
        }

        private bool _isEditing;
        public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }

        private bool _isNew;
        public bool IsNew { get => _isNew; set => SetProperty(ref _isNew, value); }

        public bool IsDraft => CurrentReceipt != null && CurrentReceipt.Status == "Draft";
        public bool IsPosted => CurrentReceipt != null && CurrentReceipt.Status == "Posted";
        public bool IsCancelled => CurrentReceipt != null && CurrentReceipt.Status == "Cancelled";

        private Dictionary<string, int> _receiptNumberToId = new(StringComparer.OrdinalIgnoreCase);

        private string _jumpReceiptNumber;
        public string JumpReceiptNumber
        {
            get => _jumpReceiptNumber;
            set => SetProperty(ref _jumpReceiptNumber, value);
        }

        // ── Form Fields ─────────────────────────────────────────
        private string _formNumber;
        public string FormNumber { get => _formNumber; set => SetProperty(ref _formNumber, value); }

        private DateTime _formDate;
        public DateTime FormDate { get => _formDate; set => SetProperty(ref _formDate, value); }

        private int? _formCashboxId;
        public int? FormCashboxId
        {
            get => _formCashboxId;
            set { SetProperty(ref _formCashboxId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private int? _formAccountId;
        public int? FormAccountId
        {
            get => _formAccountId;
            set { SetProperty(ref _formAccountId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private int? _formCustomerId;
        public int? FormCustomerId
        {
            get => _formCustomerId;
            set => SetProperty(ref _formCustomerId, value);
        }

        private decimal _formAmount;
        public decimal FormAmount
        {
            get => _formAmount;
            set { SetProperty(ref _formAmount, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private string _formDescription;
        public string FormDescription { get => _formDescription; set => SetProperty(ref _formDescription, value); }

        private string _formNotes;
        public string FormNotes { get => _formNotes; set => SetProperty(ref _formNotes, value); }

        public bool CanSave => IsEditing && FormCashboxId > 0 && FormAccountId > 0 && FormAmount > 0;
        public bool CanPost => CurrentReceipt != null && IsDraft && !IsEditing;
        public bool CanCancel => CurrentReceipt != null && IsPosted && !IsEditing;
        public bool CanDeleteDraft => CurrentReceipt != null && IsDraft && !IsEditing;

        public ICommand LoadCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand PostCommand { get; }
        public ICommand CancelReceiptCommand { get; }
        public ICommand DeleteDraftCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand EditSelectedCommand { get; }
        public ICommand JumpToReceiptCommand { get; }
        public ICommand PrintCommand { get; }

        public CashReceiptViewModel(
            ICashReceiptService receiptService,
            ICashboxService cashboxService,
            IAccountService accountService,
            ICustomerService customerService,
            IDateTimeProvider dateTime,
            IDialogService dialog,
            IInvoicePdfPreviewService previewService,
            IDocumentHtmlBuilder htmlBuilder)
        {
            _receiptService = receiptService ?? throw new ArgumentNullException(nameof(receiptService));
            _cashboxService = cashboxService ?? throw new ArgumentNullException(nameof(cashboxService));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
            _htmlBuilder = htmlBuilder ?? throw new ArgumentNullException(nameof(htmlBuilder));

            LoadCommand = new AsyncRelayCommand(LoadReceiptsAsync);
            NewCommand = new AsyncRelayCommand(PrepareNewAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            PostCommand = new AsyncRelayCommand(PostAsync, () => CanPost);
            CancelReceiptCommand = new AsyncRelayCommand(CancelReceiptAsync, () => CanCancel);
            DeleteDraftCommand = new AsyncRelayCommand(DeleteDraftAsync, () => CanDeleteDraft);
            CancelEditCommand = new RelayCommand(CancelEditing);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());
            JumpToReceiptCommand = new AsyncRelayCommand(JumpToReceiptAsync);
            PrintCommand = new AsyncRelayCommand(PrintAsync);
        }

        private async Task PrintAsync(object _)
        {
            if (CurrentReceipt == null) return;
            IsBusy = true;
            ClearError();
            try
            {
                var data = new DocumentData
                {
                    Title = $"سند قبض رقم {CurrentReceipt.ReceiptNumber}",
                    DocumentType = PrintableDocumentType.CashReceipt,
                    MetaFields = new()
                    {
                        new("رقم السند", CurrentReceipt.ReceiptNumber),
                        new("التاريخ", CurrentReceipt.ReceiptDate.ToString("yyyy-MM-dd")),
                        new("العميل", CurrentReceipt.CustomerName ?? "—"),
                        new("الخزنة", CurrentReceipt.CashboxName ?? "—"),
                        new("الحساب", CurrentReceipt.AccountName ?? "—"),
                        new("الحالة", CurrentReceipt.Status, true)
                    },
                    SummaryFields = new()
                    {
                        new("المبلغ", CurrentReceipt.Amount.ToString("N2"), true)
                    },
                    Notes = CurrentReceipt.Notes ?? CurrentReceipt.Description
                };
                var html = await _htmlBuilder.BuildAsync(data);
                await _previewService.ShowHtmlPreviewAsync(new InvoicePdfPreviewRequest
                {
                    Title = data.Title, FilePrefix = "cash_receipt", HtmlContent = html
                });
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الطباعة", ex); }
            finally { IsBusy = false; }
        }

        public async Task OnNavigatedToAsync(object parameter)
        {
            if (parameter is not CashReceiptNavigationParams p)
                return;

            IsBusy = true;
            ClearError();
            try
            {
                await LoadLookupsAsync();

                _linkedSalesInvoiceId = p.SalesInvoiceId;

                IsEditing = true;
                IsNew = true;
                CurrentReceipt = null;

                try
                {
                    var numResult = await _receiptService.GetNextNumberAsync();
                    FormNumber = numResult.IsSuccess ? numResult.Data : "";
                }
                catch { FormNumber = ""; }

                FormDate = p.Date ?? _dateTime.Today;
                FormCustomerId = p.CustomerId;
                FormAmount = p.Amount ?? 0;
                FormDescription = p.Description ?? "";
                FormNotes = p.Notes ?? "";

                FormCashboxId = null;
                FormAccountId = null;

                var defaultCb = Cashboxes.FirstOrDefault(c => c.IsDefault) ?? Cashboxes.FirstOrDefault();
                if (defaultCb != null) FormCashboxId = defaultCb.Id;

                StatusMessage = "إنشاء سند قبض جديد...";
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تهيئة البيانات", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task LoadReceiptsAsync()
        {
            IsBusy = true; ClearError();
            try
            {
                await LoadLookupsAsync();
                var result = await _receiptService.GetAllAsync();
                Receipts.Clear();
                _receiptNumberToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (result.IsSuccess)
                {
                    var list = result.Data.ToList();
                    foreach (var r in list) Receipts.Add(r);
                    _receiptNumberToId = list
                        .Where(r => !string.IsNullOrWhiteSpace(r.ReceiptNumber))
                        .GroupBy(r => r.ReceiptNumber)
                        .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
                }
                StatusMessage = $"تم تحميل {Receipts.Count} سند قبض";
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("التحميل", ex); }
            finally { IsBusy = false; }
        }

        private async Task LoadLookupsAsync()
        {
            var cbResult = await _cashboxService.GetAllAsync();
            Cashboxes.Clear();
            if (cbResult.IsSuccess)
                foreach (var c in cbResult.Data.Where(x => x.IsActive)) Cashboxes.Add(c);

            var accResult = await _accountService.GetAllAsync();
            PostableAccounts.Clear();
            if (accResult.IsSuccess)
                foreach (var a in accResult.Data.Where(x => x.AllowPosting && x.IsActive)) PostableAccounts.Add(a);

            var custResult = await _customerService.GetAllAsync();
            Customers.Clear();
            if (custResult.IsSuccess)
                foreach (var c in custResult.Data.Where(x => x.IsActive)) Customers.Add(c);
        }

        private async Task LoadReceiptDetailAsync(int id)
        {
            IsBusy = true; ClearError();
            try
            {
                var result = await _receiptService.GetByIdAsync(id);
                if (result.IsSuccess)
                {
                    CurrentReceipt = result.Data;
                    PopulateForm(result.Data);
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("تحميل التفاصيل", ex); }
            finally { IsBusy = false; }
        }

        private async Task PrepareNewAsync()
        {
            IsEditing = true; IsNew = true; CurrentReceipt = null; ClearError();
            try
            {
                var numResult = await _receiptService.GetNextNumberAsync();
                FormNumber = numResult.IsSuccess ? numResult.Data : "";
            }
            catch { FormNumber = ""; }

            FormDate = _dateTime.Today; FormCashboxId = null; FormAccountId = null;
            FormCustomerId = null; FormAmount = 0; FormDescription = ""; FormNotes = "";

            // Default to first cashbox
            var defaultCb = Cashboxes.FirstOrDefault(c => c.IsDefault) ?? Cashboxes.FirstOrDefault();
            if (defaultCb != null) FormCashboxId = defaultCb.Id;

            StatusMessage = "إنشاء سند قبض جديد...";
        }

        private async Task SaveAsync()
        {
            IsBusy = true; ClearError();
            try
            {
                if (IsNew)
                {
                    var dto = new CreateCashReceiptDto
                    {
                        ReceiptDate = FormDate, CashboxId = FormCashboxId ?? 0,
                        AccountId = FormAccountId ?? 0, CustomerId = FormCustomerId,
                        SalesInvoiceId = _linkedSalesInvoiceId,
                        Amount = FormAmount, Description = FormDescription?.Trim(),
                        Notes = FormNotes?.Trim()
                    };
                    var result = await _receiptService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء سند القبض «{result.Data.ReceiptNumber}» بنجاح";
                        IsEditing = false; IsNew = false; await LoadReceiptsAsync();
                    }
                    else ErrorMessage = result.ErrorMessage;
                }
                else
                {
                    var dto = new UpdateCashReceiptDto
                    {
                        Id = CurrentReceipt.Id, ReceiptDate = FormDate,
                        CashboxId = FormCashboxId ?? 0, AccountId = FormAccountId ?? 0,
                        CustomerId = FormCustomerId, Amount = FormAmount,
                        SalesInvoiceId = CurrentReceipt.SalesInvoiceId,
                        Description = FormDescription?.Trim(), Notes = FormNotes?.Trim()
                    };
                    var result = await _receiptService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث سند القبض «{result.Data.ReceiptNumber}» بنجاح";
                        IsEditing = false; IsNew = false; await LoadReceiptsAsync();
                    }
                    else ErrorMessage = result.ErrorMessage;
                }
            }
            catch (ConcurrencyConflictException ex)
            {
                await ConcurrencyHelper.ShowConflictAndRefreshAsync(ex, LoadReceiptsAsync);
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الحفظ", ex); }
            finally { IsBusy = false; }
        }

        private async Task PostAsync()
        {
            if (CurrentReceipt == null) return;
            if (!_dialog.Confirm(
                $"هل تريد ترحيل سند القبض «{CurrentReceipt.ReceiptNumber}»؟\nبعد الترحيل لا يمكن التعديل.",
                "تأكيد الترحيل")) return;
            IsBusy = true; ClearError();
            try
            {
                var result = await _receiptService.PostAsync(CurrentReceipt.Id);
                if (result.IsSuccess)
                { StatusMessage = $"تم ترحيل سند القبض «{result.Data.ReceiptNumber}» بنجاح"; await LoadReceiptsAsync(); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الترحيل", ex); }
            finally { IsBusy = false; }
        }

        private async Task CancelReceiptAsync()
        {
            if (CurrentReceipt == null) return;
            if (!_dialog.Confirm(
                $"هل تريد إلغاء سند القبض «{CurrentReceipt.ReceiptNumber}»؟",
                "تأكيد الإلغاء")) return;
            IsBusy = true; ClearError();
            try
            {
                var result = await _receiptService.CancelAsync(CurrentReceipt.Id);
                if (result.IsSuccess) { StatusMessage = "تم إلغاء سند القبض بنجاح"; await LoadReceiptsAsync(); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الإلغاء", ex); }
            finally { IsBusy = false; }
        }

        private async Task DeleteDraftAsync()
        {
            if (CurrentReceipt == null) return;
            if (!_dialog.Confirm(
                $"هل تريد حذف مسودة سند القبض «{CurrentReceipt.ReceiptNumber}»؟",
                "تأكيد الحذف")) return;
            IsBusy = true; ClearError();
            try
            {
                var result = await _receiptService.DeleteDraftAsync(CurrentReceipt.Id);
                if (result.IsSuccess)
                { StatusMessage = "تم حذف المسودة بنجاح"; CurrentReceipt = null; ClearForm(); await LoadReceiptsAsync(); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الحذف", ex); }
            finally { IsBusy = false; }
        }

        public void EditSelected()
        {
            if (CurrentReceipt == null || !IsDraft) return;
            PopulateForm(CurrentReceipt); IsEditing = true; IsNew = false;
        }

        private async Task JumpToReceiptAsync()
        {
            if (IsEditing)
            {
                _dialog.ShowInfo("يرجى إنهاء التعديل قبل التنقل.", "تنقل السندات");
                return;
            }

            if (string.IsNullOrWhiteSpace(JumpReceiptNumber))
                return;

            if (_receiptNumberToId.Count == 0)
                await LoadReceiptsAsync();

            if (!_receiptNumberToId.TryGetValue(JumpReceiptNumber.Trim(), out var id))
            {
                _dialog.ShowInfo("رقم السند غير موجود.", "تنقل السندات");
                return;
            }

            var item = Receipts.FirstOrDefault(r => r.Id == id);
            if (item != null)
                SelectedItem = item;
            else
                await LoadReceiptDetailAsync(id);
        }

        private void CancelEditing(object _)
        {
            IsEditing = false; IsNew = false; ClearError();
            if (CurrentReceipt != null) PopulateForm(CurrentReceipt); else ClearForm();
            StatusMessage = "تم الإلغاء";
        }

        private void PopulateForm(CashReceiptDto receipt)
        {
            FormNumber = receipt.ReceiptNumber; FormDate = receipt.ReceiptDate;
            FormCashboxId = receipt.CashboxId; FormAccountId = receipt.AccountId;
            FormCustomerId = receipt.CustomerId; FormAmount = receipt.Amount;
            FormDescription = receipt.Description; FormNotes = receipt.Notes;
            _linkedSalesInvoiceId = receipt.SalesInvoiceId;
            IsEditing = false; IsNew = false;
        }

        private void ClearForm()
        {
            FormNumber = ""; FormDate = _dateTime.Today; FormCashboxId = null;
            FormAccountId = null; FormCustomerId = null; FormAmount = 0;
            FormDescription = ""; FormNotes = "";
            _linkedSalesInvoiceId = null;
        }
    }
}
