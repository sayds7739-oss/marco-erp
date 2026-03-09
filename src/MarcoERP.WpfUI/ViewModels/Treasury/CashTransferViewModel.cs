using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Treasury;
using MarcoERP.Application.DTOs.Printing;
using MarcoERP.Application.Interfaces.Printing;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Common;
using MarcoERP.WpfUI.Services;

namespace MarcoERP.WpfUI.ViewModels.Treasury
{
    /// <summary>
    /// ViewModel for Cash Transfer management screen (تحويل بين الخزن).
    /// </summary>
    public sealed class CashTransferViewModel : BaseViewModel
    {
        private readonly ICashTransferService _transferService;
        private readonly ICashboxService _cashboxService;
        private readonly IDateTimeProvider _dateTime;
        private readonly IDialogService _dialog;
        private readonly IInvoicePdfPreviewService _previewService;
        private readonly IDocumentHtmlBuilder _htmlBuilder;

        public CashTransferViewModel(ICashTransferService transferService, ICashboxService cashboxService, IDateTimeProvider dateTime, IDialogService dialog,
            IInvoicePdfPreviewService previewService, IDocumentHtmlBuilder htmlBuilder)
        {
            _transferService = transferService ?? throw new ArgumentNullException(nameof(transferService));
            _cashboxService = cashboxService ?? throw new ArgumentNullException(nameof(cashboxService));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
            _htmlBuilder = htmlBuilder ?? throw new ArgumentNullException(nameof(htmlBuilder));

            AllTransfers = new ObservableCollection<CashTransferListDto>();
            Cashboxes = new ObservableCollection<CashboxDto>();

            LoadCommand = new AsyncRelayCommand(LoadTransfersAsync);
            NewCommand = new RelayCommand(PrepareNew);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            PostCommand = new AsyncRelayCommand(PostAsync, () => CanPost);
            CancelTransferCommand = new AsyncRelayCommand(CancelTransferAsync, () => CanCancelTransfer);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => CanDelete);
            CancelEditCommand = new RelayCommand(CancelEditing);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());
            JumpToTransferCommand = new AsyncRelayCommand(JumpToTransferAsync);
            PrintCommand = new AsyncRelayCommand(PrintAsync);
        }

        private async Task PrintAsync(object _)
        {
            if (_currentDetail == null) return;
            IsBusy = true;
            ClearError();
            try
            {
                var data = new DocumentData
                {
                    Title = $"تحويل بين الخزن رقم {_currentDetail.TransferNumber}",
                    DocumentType = PrintableDocumentType.CashTransfer,
                    MetaFields = new()
                    {
                        new("رقم التحويل", _currentDetail.TransferNumber),
                        new("التاريخ", _currentDetail.TransferDate.ToString("yyyy-MM-dd")),
                        new("من خزنة", _currentDetail.SourceCashboxName ?? "—"),
                        new("إلى خزنة", _currentDetail.TargetCashboxName ?? "—"),
                        new("الحالة", _currentDetail.Status, true)
                    },
                    SummaryFields = new()
                    {
                        new("المبلغ", _currentDetail.Amount.ToString("N2"), true)
                    },
                    Notes = _currentDetail.Notes ?? _currentDetail.Description
                };
                var html = await _htmlBuilder.BuildAsync(data);
                await _previewService.ShowHtmlPreviewAsync(new InvoicePdfPreviewRequest
                {
                    Title = data.Title, FilePrefix = "cash_transfer", HtmlContent = html
                });
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الطباعة", ex); }
            finally { IsBusy = false; }
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<CashTransferListDto> AllTransfers { get; }
        public ObservableCollection<CashboxDto> Cashboxes { get; }

        private Dictionary<string, int> _transferNumberToId = new(StringComparer.OrdinalIgnoreCase);

        private string _jumpTransferNumber;
        public string JumpTransferNumber
        {
            get => _jumpTransferNumber;
            set => SetProperty(ref _jumpTransferNumber, value);
        }

        // ── Selection ────────────────────────────────────────────

        private CashTransferListDto _selectedItem;
        public CashTransferListDto SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    if (value != null && !IsEditing)
                        _ = LoadDetailAsync(value.Id);
                    OnPropertyChanged(nameof(CanPost));
                    OnPropertyChanged(nameof(CanCancelTransfer));
                    OnPropertyChanged(nameof(CanDelete));
                }
            }
        }

        private CashTransferDto _currentDetail;

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

        private string _formTransferNumber;
        public string FormTransferNumber
        {
            get => _formTransferNumber;
            set => SetProperty(ref _formTransferNumber, value);
        }

        private DateTime _formTransferDate;
        public DateTime FormTransferDate
        {
            get => _formTransferDate;
            set => SetProperty(ref _formTransferDate, value);
        }

        private int? _formSourceCashboxId;
        public int? FormSourceCashboxId
        {
            get => _formSourceCashboxId;
            set { SetProperty(ref _formSourceCashboxId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private int? _formTargetCashboxId;
        public int? FormTargetCashboxId
        {
            get => _formTargetCashboxId;
            set { SetProperty(ref _formTargetCashboxId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private decimal _formAmount;
        public decimal FormAmount
        {
            get => _formAmount;
            set { SetProperty(ref _formAmount, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private string _formDescription;
        public string FormDescription
        {
            get => _formDescription;
            set { SetProperty(ref _formDescription, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private string _formNotes;
        public string FormNotes
        {
            get => _formNotes;
            set => SetProperty(ref _formNotes, value);
        }

        private string _formStatus;
        public string FormStatus
        {
            get => _formStatus;
            set => SetProperty(ref _formStatus, value);
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand LoadCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand PostCommand { get; }
        public ICommand CancelTransferCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand EditSelectedCommand { get; }
        public ICommand JumpToTransferCommand { get; }
        public ICommand PrintCommand { get; }

        // ── Can Execute ──────────────────────────────────────────

        public bool CanSave => FormSourceCashboxId.HasValue && FormTargetCashboxId.HasValue
            && FormAmount > 0 && !string.IsNullOrWhiteSpace(FormDescription);
        public bool CanPost => _currentDetail != null && _currentDetail.Status == "Draft";
        public bool CanCancelTransfer => _currentDetail != null && _currentDetail.Status == "Posted";
        public bool CanDelete => _currentDetail != null && _currentDetail.Status == "Draft";

        // ── Load ─────────────────────────────────────────────────

        public async Task LoadTransfersAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var transferResult = await _transferService.GetAllAsync();
                var cashboxResult = await _cashboxService.GetActiveAsync();

                if (transferResult.IsSuccess)
                {
                    AllTransfers.Clear();
                    var list = transferResult.Data.ToList();
                    foreach (var t in list)
                        AllTransfers.Add(t);
                    _transferNumberToId = list
                        .Where(t => !string.IsNullOrWhiteSpace(t.TransferNumber))
                        .GroupBy(t => t.TransferNumber)
                        .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    _transferNumberToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                }

                if (cashboxResult.IsSuccess)
                {
                    Cashboxes.Clear();
                    foreach (var c in cashboxResult.Data)
                        Cashboxes.Add(c);
                }

                StatusMessage = $"تم تحميل {AllTransfers.Count} تحويل";
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
                var result = await _transferService.GetByIdAsync(id);
                if (result.IsSuccess)
                {
                    _currentDetail = result.Data;
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
            ClearError();

            FormTransferNumber = "(تلقائي)";
            FormTransferDate = _dateTime.Today;
            FormSourceCashboxId = null;
            FormTargetCashboxId = null;
            FormAmount = 0;
            FormDescription = "";
            FormNotes = "";
            FormStatus = "مسودة";
            StatusMessage = "إدخال تحويل جديد...";
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
                    var dto = new CreateCashTransferDto
                    {
                        TransferDate = FormTransferDate,
                        SourceCashboxId = FormSourceCashboxId.Value,
                        TargetCashboxId = FormTargetCashboxId.Value,
                        Amount = FormAmount,
                        Description = FormDescription,
                        Notes = FormNotes
                    };
                    var result = await _transferService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء التحويل: {result.Data.TransferNumber}";
                        IsEditing = false;
                        IsNew = false;
                        await LoadTransfersAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else if (_currentDetail != null)
                {
                    var dto = new UpdateCashTransferDto
                    {
                        Id = _currentDetail.Id,
                        TransferDate = FormTransferDate,
                        SourceCashboxId = FormSourceCashboxId.Value,
                        TargetCashboxId = FormTargetCashboxId.Value,
                        Amount = FormAmount,
                        Description = FormDescription,
                        Notes = FormNotes
                    };
                    var result = await _transferService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث التحويل: {result.Data.TransferNumber}";
                        IsEditing = false;
                        await LoadTransfersAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
            }
            catch (ConcurrencyConflictException ex)
            {
                await ConcurrencyHelper.ShowConflictAndRefreshAsync(ex, LoadTransfersAsync);
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

        // ── Post ─────────────────────────────────────────────────

        private async Task PostAsync()
        {
            if (_currentDetail == null) return;

            if (!_dialog.Confirm(
                "هل تريد ترحيل هذا التحويل؟ سيتم إنشاء قيد محاسبي تلقائياً.",
                "تأكيد الترحيل")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _transferService.PostAsync(_currentDetail.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = string.IsNullOrWhiteSpace(result.Data.WarningMessage)
                        ? "تم ترحيل التحويل بنجاح"
                        : $"تم ترحيل التحويل بنجاح  |  ⚠ {result.Data.WarningMessage}";
                    await LoadTransfersAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("الترحيل", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Cancel Transfer ─────────────────────────────────────

        private async Task CancelTransferAsync()
        {
            if (_currentDetail == null) return;

            if (!_dialog.Confirm(
                "هل تريد إلغاء هذا التحويل؟",
                "تأكيد الإلغاء")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _transferService.CancelAsync(_currentDetail.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم إلغاء التحويل";
                    await LoadTransfersAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("الإلغاء", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Delete ──────────────────────────────────────────────

        private async Task DeleteAsync()
        {
            if (_currentDetail == null) return;

            if (!_dialog.Confirm(
                "هل تريد حذف هذا التحويل (المسودة)؟",
                "تأكيد الحذف")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _transferService.DeleteDraftAsync(_currentDetail.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم حذف التحويل";
                    _currentDetail = null;
                    await LoadTransfersAsync();
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

        // ── Cancel Editing ──────────────────────────────────────

        private void CancelEditing(object parameter)
        {
            IsEditing = false;
            IsNew = false;
            ClearError();
            StatusMessage = "تم الإلغاء";
            if (_currentDetail != null) PopulateForm(_currentDetail);
        }

        // ── Helpers ─────────────────────────────────────────────

        private void PopulateForm(CashTransferDto item)
        {
            FormTransferNumber = item.TransferNumber;
            FormTransferDate = item.TransferDate;
            FormSourceCashboxId = item.SourceCashboxId;
            FormTargetCashboxId = item.TargetCashboxId;
            FormAmount = item.Amount;
            FormDescription = item.Description;
            FormNotes = item.Notes;
            FormStatus = item.Status;
            IsEditing = false;
            IsNew = false;
            OnPropertyChanged(nameof(CanPost));
            OnPropertyChanged(nameof(CanCancelTransfer));
            OnPropertyChanged(nameof(CanDelete));
        }

        private async Task JumpToTransferAsync()
        {
            if (IsEditing)
            {
                _dialog.ShowInfo("يرجى إنهاء التعديل قبل التنقل.", "تنقل التحويلات");
                return;
            }

            if (string.IsNullOrWhiteSpace(JumpTransferNumber))
                return;

            if (_transferNumberToId.Count == 0)
                await LoadTransfersAsync();

            if (!_transferNumberToId.TryGetValue(JumpTransferNumber.Trim(), out var id))
            {
                _dialog.ShowInfo("رقم التحويل غير موجود.", "تنقل التحويلات");
                return;
            }

            var item = AllTransfers.FirstOrDefault(t => t.Id == id);
            if (item != null)
                SelectedItem = item;
            else
                await LoadDetailAsync(id);
        }

        public void EditSelected()
        {
            if (_currentDetail == null || _currentDetail.Status != "Draft") return;
            IsEditing = true;
            IsNew = false;
        }
    }
}
