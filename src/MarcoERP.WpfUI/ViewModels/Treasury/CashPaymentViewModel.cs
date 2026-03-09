using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Accounting;
using MarcoERP.Application.Interfaces.Purchases;
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
    /// ViewModel for Cash Payment (سند صرف) management screen.
    /// Flat form — no lines, just header fields + amount.
    /// </summary>
    public sealed class CashPaymentViewModel : BaseViewModel, INavigationAware
    {
        private readonly ICashPaymentService _paymentService;
        private readonly ICashboxService _cashboxService;
        private readonly IAccountService _accountService;
        private readonly ISupplierService _supplierService;
        private readonly IDateTimeProvider _dateTime;
        private readonly IDialogService _dialog;
        private readonly IInvoicePdfPreviewService _previewService;
        private readonly IDocumentHtmlBuilder _htmlBuilder;

        private int? _linkedPurchaseInvoiceId;

        public ObservableCollection<CashPaymentListDto> Payments { get; } = new();
        public ObservableCollection<CashboxDto> Cashboxes { get; } = new();
        public ObservableCollection<AccountDto> PostableAccounts { get; } = new();
        public ObservableCollection<SupplierDto> Suppliers { get; } = new();

        private CashPaymentListDto _selectedItem;
        public CashPaymentListDto SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value) && value != null && !IsEditing)
                    _ = LoadPaymentDetailAsync(value.Id);
            }
        }

        private CashPaymentDto _currentPayment;
        public CashPaymentDto CurrentPayment
        {
            get => _currentPayment;
            set
            {
                SetProperty(ref _currentPayment, value);
                OnPropertyChanged(nameof(IsDraft)); OnPropertyChanged(nameof(IsPosted));
                OnPropertyChanged(nameof(IsCancelled)); OnPropertyChanged(nameof(CanPost));
                OnPropertyChanged(nameof(CanCancel)); OnPropertyChanged(nameof(CanDeleteDraft));
            }
        }

        private bool _isEditing;
        public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }

        private bool _isNew;
        public bool IsNew { get => _isNew; set => SetProperty(ref _isNew, value); }

        public bool IsDraft => CurrentPayment != null && CurrentPayment.Status == "Draft";
        public bool IsPosted => CurrentPayment != null && CurrentPayment.Status == "Posted";
        public bool IsCancelled => CurrentPayment != null && CurrentPayment.Status == "Cancelled";

        private Dictionary<string, int> _paymentNumberToId = new(StringComparer.OrdinalIgnoreCase);

        private string _jumpPaymentNumber;
        public string JumpPaymentNumber
        {
            get => _jumpPaymentNumber;
            set => SetProperty(ref _jumpPaymentNumber, value);
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

        private int? _formSupplierId;
        public int? FormSupplierId
        {
            get => _formSupplierId;
            set => SetProperty(ref _formSupplierId, value);
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
        public bool CanPost => CurrentPayment != null && IsDraft && !IsEditing;
        public bool CanCancel => CurrentPayment != null && IsPosted && !IsEditing;
        public bool CanDeleteDraft => CurrentPayment != null && IsDraft && !IsEditing;

        public ICommand LoadCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand PostCommand { get; }
        public ICommand CancelPaymentCommand { get; }
        public ICommand DeleteDraftCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand EditSelectedCommand { get; }
        public ICommand JumpToPaymentCommand { get; }
        public ICommand PrintCommand { get; }

        public CashPaymentViewModel(
            ICashPaymentService paymentService,
            ICashboxService cashboxService,
            IAccountService accountService,
            ISupplierService supplierService,
            IDateTimeProvider dateTime,
            IDialogService dialog,
            IInvoicePdfPreviewService previewService,
            IDocumentHtmlBuilder htmlBuilder)
        {
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _cashboxService = cashboxService ?? throw new ArgumentNullException(nameof(cashboxService));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
            _htmlBuilder = htmlBuilder ?? throw new ArgumentNullException(nameof(htmlBuilder));

            LoadCommand = new AsyncRelayCommand(LoadPaymentsAsync);
            NewCommand = new AsyncRelayCommand(PrepareNewAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            PostCommand = new AsyncRelayCommand(PostAsync, () => CanPost);
            CancelPaymentCommand = new AsyncRelayCommand(CancelPaymentAsync, () => CanCancel);
            DeleteDraftCommand = new AsyncRelayCommand(DeleteDraftAsync, () => CanDeleteDraft);
            CancelEditCommand = new RelayCommand(CancelEditing);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());
            JumpToPaymentCommand = new AsyncRelayCommand(JumpToPaymentAsync);
            PrintCommand = new AsyncRelayCommand(PrintAsync);
        }

        private async Task PrintAsync(object _)
        {
            if (CurrentPayment == null) return;
            IsBusy = true;
            ClearError();
            try
            {
                var data = new DocumentData
                {
                    Title = $"سند صرف رقم {CurrentPayment.PaymentNumber}",
                    DocumentType = PrintableDocumentType.CashPayment,
                    MetaFields = new()
                    {
                        new("رقم السند", CurrentPayment.PaymentNumber),
                        new("التاريخ", CurrentPayment.PaymentDate.ToString("yyyy-MM-dd")),
                        new("المورد", CurrentPayment.SupplierName ?? "—"),
                        new("الخزنة", CurrentPayment.CashboxName ?? "—"),
                        new("الحساب", CurrentPayment.AccountName ?? "—"),
                        new("الحالة", CurrentPayment.Status, true)
                    },
                    SummaryFields = new()
                    {
                        new("المبلغ", CurrentPayment.Amount.ToString("N2"), true)
                    },
                    Notes = CurrentPayment.Notes ?? CurrentPayment.Description
                };
                var html = await _htmlBuilder.BuildAsync(data);
                await _previewService.ShowHtmlPreviewAsync(new InvoicePdfPreviewRequest
                {
                    Title = data.Title, FilePrefix = "cash_payment", HtmlContent = html
                });
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الطباعة", ex); }
            finally { IsBusy = false; }
        }

        public async Task OnNavigatedToAsync(object parameter)
        {
            if (parameter is not CashPaymentNavigationParams p)
                return;

            IsBusy = true;
            ClearError();
            try
            {
                await LoadLookupsAsync();

                _linkedPurchaseInvoiceId = p.PurchaseInvoiceId;

                IsEditing = true;
                IsNew = true;
                CurrentPayment = null;

                try
                {
                    var numResult = await _paymentService.GetNextNumberAsync();
                    FormNumber = numResult.IsSuccess ? numResult.Data : "";
                }
                catch { FormNumber = ""; }

                FormDate = p.Date ?? _dateTime.Today;
                FormSupplierId = p.SupplierId;
                FormAmount = p.Amount ?? 0;
                FormDescription = p.Description ?? "";
                FormNotes = p.Notes ?? "";

                FormCashboxId = null;
                FormAccountId = null;

                var defaultCb = Cashboxes.FirstOrDefault(c => c.IsDefault) ?? Cashboxes.FirstOrDefault();
                if (defaultCb != null) FormCashboxId = defaultCb.Id;

                StatusMessage = "إنشاء سند صرف جديد...";
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

        public async Task LoadPaymentsAsync()
        {
            IsBusy = true; ClearError();
            try
            {
                await LoadLookupsAsync();
                var result = await _paymentService.GetAllAsync();
                Payments.Clear();
                _paymentNumberToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (result.IsSuccess)
                {
                    var list = result.Data.ToList();
                    foreach (var p in list) Payments.Add(p);
                    _paymentNumberToId = list
                        .Where(p => !string.IsNullOrWhiteSpace(p.PaymentNumber))
                        .GroupBy(p => p.PaymentNumber)
                        .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
                }
                StatusMessage = $"تم تحميل {Payments.Count} سند صرف";
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

            var suppResult = await _supplierService.GetAllAsync();
            Suppliers.Clear();
            if (suppResult.IsSuccess)
                foreach (var s in suppResult.Data.Where(x => x.IsActive)) Suppliers.Add(s);
        }

        private async Task LoadPaymentDetailAsync(int id)
        {
            IsBusy = true; ClearError();
            try
            {
                var result = await _paymentService.GetByIdAsync(id);
                if (result.IsSuccess)
                {
                    CurrentPayment = result.Data;
                    PopulateForm(result.Data);
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("تحميل التفاصيل", ex); }
            finally { IsBusy = false; }
        }

        private async Task PrepareNewAsync()
        {
            IsEditing = true; IsNew = true; CurrentPayment = null; ClearError();
            try
            {
                var numResult = await _paymentService.GetNextNumberAsync();
                FormNumber = numResult.IsSuccess ? numResult.Data : "";
            }
            catch { FormNumber = ""; }

            FormDate = _dateTime.Today; FormCashboxId = null; FormAccountId = null;
            FormSupplierId = null; FormAmount = 0; FormDescription = ""; FormNotes = "";

            var defaultCb = Cashboxes.FirstOrDefault(c => c.IsDefault) ?? Cashboxes.FirstOrDefault();
            if (defaultCb != null) FormCashboxId = defaultCb.Id;

            StatusMessage = "إنشاء سند صرف جديد...";
        }

        private async Task SaveAsync()
        {
            IsBusy = true; ClearError();
            try
            {
                if (IsNew)
                {
                    var dto = new CreateCashPaymentDto
                    {
                        PaymentDate = FormDate, CashboxId = FormCashboxId ?? 0,
                        AccountId = FormAccountId ?? 0, SupplierId = FormSupplierId,
                        PurchaseInvoiceId = _linkedPurchaseInvoiceId,
                        Amount = FormAmount, Description = FormDescription?.Trim(),
                        Notes = FormNotes?.Trim()
                    };
                    var result = await _paymentService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء سند الصرف «{result.Data.PaymentNumber}» بنجاح";
                        IsEditing = false; IsNew = false; await LoadPaymentsAsync();
                    }
                    else ErrorMessage = result.ErrorMessage;
                }
                else
                {
                    var dto = new UpdateCashPaymentDto
                    {
                        Id = CurrentPayment.Id, PaymentDate = FormDate,
                        CashboxId = FormCashboxId ?? 0, AccountId = FormAccountId ?? 0,
                        SupplierId = FormSupplierId, Amount = FormAmount,
                        PurchaseInvoiceId = CurrentPayment.PurchaseInvoiceId,
                        Description = FormDescription?.Trim(), Notes = FormNotes?.Trim()
                    };
                    var result = await _paymentService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث سند الصرف «{result.Data.PaymentNumber}» بنجاح";
                        IsEditing = false; IsNew = false; await LoadPaymentsAsync();
                    }
                    else ErrorMessage = result.ErrorMessage;
                }
            }
            catch (ConcurrencyConflictException ex)
            {
                await ConcurrencyHelper.ShowConflictAndRefreshAsync(ex, LoadPaymentsAsync);
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الحفظ", ex); }
            finally { IsBusy = false; }
        }

        private async Task PostAsync()
        {
            if (CurrentPayment == null) return;
            if (!_dialog.Confirm(
                $"هل تريد ترحيل سند الصرف «{CurrentPayment.PaymentNumber}»؟\nبعد الترحيل لا يمكن التعديل.",
                "تأكيد الترحيل")) return;
            IsBusy = true; ClearError();
            try
            {
                var result = await _paymentService.PostAsync(CurrentPayment.Id);
                if (result.IsSuccess)
                { StatusMessage = $"تم ترحيل سند الصرف «{result.Data.PaymentNumber}» بنجاح"; await LoadPaymentsAsync(); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الترحيل", ex); }
            finally { IsBusy = false; }
        }

        private async Task CancelPaymentAsync()
        {
            if (CurrentPayment == null) return;
            if (!_dialog.Confirm(
                $"هل تريد إلغاء سند الصرف «{CurrentPayment.PaymentNumber}»؟",
                "تأكيد الإلغاء")) return;
            IsBusy = true; ClearError();
            try
            {
                var result = await _paymentService.CancelAsync(CurrentPayment.Id);
                if (result.IsSuccess) { StatusMessage = "تم إلغاء سند الصرف بنجاح"; await LoadPaymentsAsync(); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الإلغاء", ex); }
            finally { IsBusy = false; }
        }

        private async Task DeleteDraftAsync()
        {
            if (CurrentPayment == null) return;
            if (!_dialog.Confirm(
                $"هل تريد حذف مسودة سند الصرف «{CurrentPayment.PaymentNumber}»؟",
                "تأكيد الحذف")) return;
            IsBusy = true; ClearError();
            try
            {
                var result = await _paymentService.DeleteDraftAsync(CurrentPayment.Id);
                if (result.IsSuccess)
                { StatusMessage = "تم حذف المسودة بنجاح"; CurrentPayment = null; ClearForm(); await LoadPaymentsAsync(); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الحذف", ex); }
            finally { IsBusy = false; }
        }

        public void EditSelected()
        {
            if (CurrentPayment == null || !IsDraft) return;
            PopulateForm(CurrentPayment); IsEditing = true; IsNew = false;
        }

        private async Task JumpToPaymentAsync()
        {
            if (IsEditing)
            {
                _dialog.ShowInfo("يرجى إنهاء التعديل قبل التنقل.", "تنقل السندات");
                return;
            }

            if (string.IsNullOrWhiteSpace(JumpPaymentNumber))
                return;

            if (_paymentNumberToId.Count == 0)
                await LoadPaymentsAsync();

            if (!_paymentNumberToId.TryGetValue(JumpPaymentNumber.Trim(), out var id))
            {
                _dialog.ShowInfo("رقم السند غير موجود.", "تنقل السندات");
                return;
            }

            var item = Payments.FirstOrDefault(p => p.Id == id);
            if (item != null)
                SelectedItem = item;
            else
                await LoadPaymentDetailAsync(id);
        }

        private void CancelEditing(object _)
        {
            IsEditing = false; IsNew = false; ClearError();
            if (CurrentPayment != null) PopulateForm(CurrentPayment); else ClearForm();
            StatusMessage = "تم الإلغاء";
        }

        private void PopulateForm(CashPaymentDto payment)
        {
            FormNumber = payment.PaymentNumber; FormDate = payment.PaymentDate;
            FormCashboxId = payment.CashboxId; FormAccountId = payment.AccountId;
            FormSupplierId = payment.SupplierId; FormAmount = payment.Amount;
            FormDescription = payment.Description; FormNotes = payment.Notes;
            _linkedPurchaseInvoiceId = payment.PurchaseInvoiceId;
            IsEditing = false; IsNew = false;
        }

        private void ClearForm()
        {
            FormNumber = ""; FormDate = _dateTime.Today; FormCashboxId = null;
            FormAccountId = null; FormSupplierId = null; FormAmount = 0;
            FormDescription = ""; FormNotes = "";
            _linkedPurchaseInvoiceId = null;
        }
    }
}
