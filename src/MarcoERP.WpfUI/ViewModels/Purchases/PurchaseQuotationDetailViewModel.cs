using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Common;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Application.DTOs.Printing;
using MarcoERP.Application.Interfaces.Printing;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Common;
using MarcoERP.WpfUI.Navigation;
using MarcoERP.WpfUI.Services;

namespace MarcoERP.WpfUI.ViewModels.Purchases
{
    /// <summary>
    /// Full-screen ViewModel for Purchase Quotation detail — create, edit, send, accept, reject, convert to invoice.
    /// </summary>
    public sealed class PurchaseQuotationDetailViewModel : BaseViewModel, INavigationAware, IInvoiceLineFormHost, IDirtyStateAware
    {
        private readonly IPurchaseQuotationService _quotationService;
        private readonly IProductService _productService;
        private readonly IWarehouseService _warehouseService;
        private readonly ISupplierService _supplierService;
        private readonly INavigationService _navigationService;
        private readonly ILineCalculationService _lineCalculationService;
        private readonly IDialogService _dialog;
        private readonly IInvoicePdfPreviewService _previewService;
        private readonly IDocumentHtmlBuilder _htmlBuilder;

        // ── Collections ──────────────────────────────────────────
        public ObservableCollection<SupplierDto> Suppliers { get; } = new();
        public ObservableCollection<WarehouseDto> Warehouses { get; } = new();
        public ObservableCollection<ProductDto> Products { get; } = new();
        public ObservableCollection<PurchaseQuotationLineFormItem> FormLines { get; } = new();

        // ── Quotation Navigation ─────────────────────────────────
        private List<int> _quotationIds = new();
        private int _currentQuotationIndex = -1;
        private Dictionary<string, int> _quotationNumberToId = new(StringComparer.OrdinalIgnoreCase);

        public bool CanGoNext => _currentQuotationIndex >= 0 && _currentQuotationIndex < _quotationIds.Count - 1;
        public bool CanGoPrevious => _currentQuotationIndex > 0;
        public string NavigationPositionText => _quotationIds.Count > 0
            ? $"{_currentQuotationIndex + 1} / {_quotationIds.Count}"
            : string.Empty;

        // ── IDirtyStateAware ─────────────────────────────────────
        public void ResetDirtyState() => ResetDirtyTracking();

        public async Task<bool> SaveChangesAsync()
        {
            if (!CanSave)
                return false;

            await SaveAsync();
            return !IsDirty && !HasError;
        }

        // ── State ───────────────────────────────────────────────
        private PurchaseQuotationDto _currentQuotation;
        public PurchaseQuotationDto CurrentQuotation
        {
            get => _currentQuotation;
            set
            {
                SetProperty(ref _currentQuotation, value);
                OnPropertyChanged(nameof(IsDraft));
                OnPropertyChanged(nameof(IsSent));
                OnPropertyChanged(nameof(IsAccepted));
                OnPropertyChanged(nameof(IsRejected));
                OnPropertyChanged(nameof(IsConverted));
                OnPropertyChanged(nameof(CanEdit));
                OnPropertyChanged(nameof(CanSend));
                OnPropertyChanged(nameof(CanAccept));
                OnPropertyChanged(nameof(CanReject));
                OnPropertyChanged(nameof(CanConvert));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(CanDeleteDraft));
            }
        }

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

        // ── Status Helpers ──────────────────────────────────────
        public bool IsDraft => CurrentQuotation != null && CurrentQuotation.Status == "Draft";
        public bool IsSent => CurrentQuotation != null && CurrentQuotation.Status == "Sent";
        public bool IsAccepted => CurrentQuotation != null && CurrentQuotation.Status == "Accepted";
        public bool IsRejected => CurrentQuotation != null && CurrentQuotation.Status == "Rejected";
        public bool IsConverted => CurrentQuotation != null && CurrentQuotation.Status == "Converted";

        // ── Form Header Fields ──────────────────────────────────
        private string _formNumber;
        public string FormNumber
        {
            get => _formNumber;
            set => SetProperty(ref _formNumber, value);
        }

        private DateTime _formDate = DateTime.Today;
        public DateTime FormDate
        {
            get => _formDate;
            set { if (SetProperty(ref _formDate, value)) MarkDirty(); }
        }

        private DateTime _formValidUntil = DateTime.Today.AddDays(30);
        public DateTime FormValidUntil
        {
            get => _formValidUntil;
            set { if (SetProperty(ref _formValidUntil, value)) MarkDirty(); }
        }

        private int? _formSupplierId;
        public int? FormSupplierId
        {
            get => _formSupplierId;
            set { if (SetProperty(ref _formSupplierId, value)) { MarkDirty(); OnPropertyChanged(nameof(CanSave)); } }
        }

        private int? _formWarehouseId;
        public int? FormWarehouseId
        {
            get => _formWarehouseId;
            set { if (SetProperty(ref _formWarehouseId, value)) { MarkDirty(); OnPropertyChanged(nameof(CanSave)); } }
        }

        private string _formNotes;
        public string FormNotes
        {
            get => _formNotes;
            set { if (SetProperty(ref _formNotes, value)) MarkDirty(); }
        }

        private string _jumpQuotationNumber;
        public string JumpQuotationNumber
        {
            get => _jumpQuotationNumber;
            set => SetProperty(ref _jumpQuotationNumber, value);
        }

        // ── Totals ──────────────────────────────────────────────
        private decimal _totalSubtotal;
        public decimal TotalSubtotal
        {
            get => _totalSubtotal;
            private set => SetProperty(ref _totalSubtotal, value);
        }

        private decimal _totalDiscount;
        public decimal TotalDiscount
        {
            get => _totalDiscount;
            private set => SetProperty(ref _totalDiscount, value);
        }

        private decimal _totalVat;
        public decimal TotalVat
        {
            get => _totalVat;
            private set => SetProperty(ref _totalVat, value);
        }

        private decimal _totalNet;
        public decimal TotalNet
        {
            get => _totalNet;
            private set => SetProperty(ref _totalNet, value);
        }

        public void RefreshTotals()
        {
            var totals = CalculateTotals(FormLines.Select(l => l.GetCalculationRequest()));
            TotalSubtotal = totals.Subtotal;
            TotalDiscount = totals.DiscountTotal;
            TotalVat = totals.VatTotal;
            TotalNet = totals.NetTotal;
            OnPropertyChanged(nameof(CanSave));
        }

        public LineCalculationResult CalculateLine(LineCalculationRequest request)
        {
            return _lineCalculationService.CalculateLine(request);
        }

        public InvoiceTotalsResult CalculateTotals(IEnumerable<LineCalculationRequest> lines)
        {
            return _lineCalculationService.CalculateTotals(lines);
        }

        public decimal ConvertQuantity(decimal quantity, decimal factor)
        {
            return _lineCalculationService.ConvertQuantity(quantity, factor);
        }

        public decimal ConvertPrice(decimal price, decimal factor)
        {
            return _lineCalculationService.ConvertPrice(price, factor);
        }

        public async Task RefreshProductsAsync()
        {
            var prodResult = await _productService.GetAllAsync();
            Products.Clear();
            if (prodResult.IsSuccess)
                foreach (var p in prodResult.Data.Where(x => x.Status == "Active"))
                    Products.Add(p);
        }

        // ── CanExecute Guards ───────────────────────────────────
        public bool CanSave => IsEditing
                               && FormSupplierId.HasValue && FormSupplierId > 0
                               && FormWarehouseId.HasValue && FormWarehouseId > 0
                               && FormLines.Count > 0
                               && FormLines.All(l => l.ProductId > 0 && l.Quantity > 0 && l.UnitPrice > 0);

        public bool CanSend => CurrentQuotation != null && IsDraft && !IsEditing;
        public bool CanAccept => CurrentQuotation != null && IsSent && !IsEditing;
        public bool CanReject => CurrentQuotation != null && IsSent && !IsEditing;
        public bool CanConvert => CurrentQuotation != null && IsAccepted && !IsEditing;
        public bool CanCancel => CurrentQuotation != null && (IsDraft || IsSent) && !IsEditing;
        public bool CanDeleteDraft => CurrentQuotation != null && IsDraft && !IsEditing;
        public bool CanEdit => CurrentQuotation != null && IsDraft && !IsEditing;

        // ── Commands ────────────────────────────────────────────
        public ICommand SaveCommand { get; }
        public ICommand SendCommand { get; }
        public ICommand AcceptCommand { get; }
        public ICommand RejectCommand { get; }
        public ICommand ConvertToInvoiceCommand { get; }
        public ICommand CancelQuotationCommand { get; }
        public ICommand DeleteDraftCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand GoToNextCommand { get; }
        public ICommand GoToPreviousCommand { get; }
        public ICommand JumpToQuotationCommand { get; }
        public ICommand PrintCommand { get; }

        public PurchaseQuotationDetailViewModel(
            IPurchaseQuotationService quotationService,
            IProductService productService,
            IWarehouseService warehouseService,
            ISupplierService supplierService,
            INavigationService navigationService,
            ILineCalculationService lineCalculationService,
            IDialogService dialog,
            IInvoicePdfPreviewService previewService,
            IDocumentHtmlBuilder htmlBuilder)
        {
            _quotationService = quotationService ?? throw new ArgumentNullException(nameof(quotationService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _lineCalculationService = lineCalculationService ?? throw new ArgumentNullException(nameof(lineCalculationService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
            _htmlBuilder = htmlBuilder ?? throw new ArgumentNullException(nameof(htmlBuilder));

            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            SendCommand = new AsyncRelayCommand(SendAsync, () => CanSend);
            AcceptCommand = new AsyncRelayCommand(AcceptAsync, () => CanAccept);
            RejectCommand = new AsyncRelayCommand(RejectAsync, () => CanReject);
            ConvertToInvoiceCommand = new AsyncRelayCommand(ConvertToInvoiceAsync, () => CanConvert);
            CancelQuotationCommand = new AsyncRelayCommand(CancelQuotationAsync, () => CanCancel);
            DeleteDraftCommand = new AsyncRelayCommand(DeleteDraftAsync, () => CanDeleteDraft);
            CancelEditCommand = new RelayCommand(CancelEditing);
            AddLineCommand = new RelayCommand(AddLine);
            RemoveLineCommand = new RelayCommand(RemoveLine);
            EditCommand = new RelayCommand(_ => StartEditing());
            BackCommand = new RelayCommand(_ => NavigateBack());
            GoToNextCommand = new AsyncRelayCommand(GoToNextAsync, () => CanGoNext);
            GoToPreviousCommand = new AsyncRelayCommand(GoToPreviousAsync, () => CanGoPrevious);
            JumpToQuotationCommand = new AsyncRelayCommand(JumpToQuotationAsync);
            PrintCommand = new AsyncRelayCommand(PrintAsync);
        }

        private async Task PrintAsync(object _)
        {
            if (CurrentQuotation == null) return;
            IsBusy = true;
            ClearError();
            try
            {
                var data = new DocumentData
                {
                    Title = $"عرض سعر شراء رقم {CurrentQuotation.QuotationNumber}",
                    DocumentType = PrintableDocumentType.PurchaseQuotation,
                    MetaFields = new()
                    {
                        new("رقم العرض", CurrentQuotation.QuotationNumber),
                        new("التاريخ", CurrentQuotation.QuotationDate.ToString("yyyy-MM-dd")),
                        new("صالح حتى", CurrentQuotation.ValidUntil.ToString("yyyy-MM-dd")),
                        new("المورد", CurrentQuotation.SupplierNameAr ?? "—"),
                        new("المستودع", CurrentQuotation.WarehouseNameAr ?? "—"),
                        new("الحالة", CurrentQuotation.Status, true)
                    },
                    Columns = new()
                    {
                        new("#"), new("كود الصنف"), new("اسم الصنف"),
                        new("الوحدة"), new("الكمية", true), new("السعر", true),
                        new("الخصم", true), new("الضريبة", true), new("الصافي", true)
                    },
                    Notes = CurrentQuotation.Notes
                };
                int row = 1;
                foreach (var l in CurrentQuotation.Lines)
                    data.Rows.Add(new() { (row++).ToString(), l.ProductCode, l.ProductNameAr, l.UnitNameAr,
                        l.Quantity.ToString("N2"), l.UnitPrice.ToString("N2"), l.DiscountAmount.ToString("N2"),
                        l.VatAmount.ToString("N2"), l.NetTotal.ToString("N2") });
                data.SummaryFields = new()
                {
                    new("الإجمالي", CurrentQuotation.Subtotal.ToString("N2")),
                    new("الخصم", CurrentQuotation.DiscountTotal.ToString("N2")),
                    new("الضريبة", CurrentQuotation.VatTotal.ToString("N2")),
                    new("الصافي", CurrentQuotation.NetTotal.ToString("N2"), true)
                };
                var html = await _htmlBuilder.BuildAsync(data);
                await _previewService.ShowHtmlPreviewAsync(new InvoicePdfPreviewRequest
                {
                    Title = data.Title, FilePrefix = "purchase_quotation", HtmlContent = html
                });
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الطباعة", ex); }
            finally { IsBusy = false; }
        }

        public async Task OnNavigatedToAsync(object parameter)
        {
            await LoadLookupsAsync();
            await LoadQuotationIdsAsync();

            if (parameter is int quotationId && quotationId > 0)
            {
                _currentQuotationIndex = _quotationIds.IndexOf(quotationId);
                await LoadQuotationDetailAsync(quotationId);
            }
            else
            {
                await PrepareNewAsync();
            }

            UpdateNavigationState();
        }

        private async Task LoadLookupsAsync()
        {
            var suppResult = await _supplierService.GetAllAsync();
            Suppliers.Clear();
            if (suppResult.IsSuccess)
                foreach (var s in suppResult.Data.Where(x => x.IsActive))
                    Suppliers.Add(s);

            var whResult = await _warehouseService.GetAllAsync();
            Warehouses.Clear();
            if (whResult.IsSuccess)
                foreach (var w in whResult.Data.Where(x => x.IsActive))
                    Warehouses.Add(w);

            var prodResult = await _productService.GetAllAsync();
            Products.Clear();
            if (prodResult.IsSuccess)
                foreach (var p in prodResult.Data.Where(x => x.Status == "Active"))
                    Products.Add(p);
        }

        private async Task LoadQuotationDetailAsync(int id)
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _quotationService.GetByIdAsync(id);
                if (result.IsSuccess)
                {
                    CurrentQuotation = result.Data;
                    PopulateFormFromQuotation(result.Data);
                    StatusMessage = $"طلب شراء «{result.Data.QuotationNumber}»";
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("تحميل طلب الشراء", ex); }
            finally { IsBusy = false; }
        }

        private async Task PrepareNewAsync()
        {
            IsEditing = true;
            IsNew = true;
            CurrentQuotation = null;
            ClearError();

            try
            {
                var numResult = await _quotationService.GetNextNumberAsync();
                FormNumber = numResult.IsSuccess ? numResult.Data : "";
            }
            catch { FormNumber = ""; }

            FormDate = DateTime.Today;
            FormValidUntil = DateTime.Today.AddDays(30);
            FormSupplierId = null;
            FormWarehouseId = null;
            FormNotes = "";
            FormLines.Clear();
            AddLine(null);
            RefreshTotals();
            ResetDirtyTracking();
            StatusMessage = "إنشاء طلب شراء جديد...";
        }

        private void AddLine(object _)
        {
            if (!IsEditing && !IsNew) return;
            FormLines.Add(new PurchaseQuotationLineFormItem(this));
            RefreshTotals();
            MarkDirty();
        }

        private void RemoveLine(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is PurchaseQuotationLineFormItem line && FormLines.Count > 1)
            {
                FormLines.Remove(line);
                RefreshTotals();
                MarkDirty();
            }
        }

        private async Task SaveAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var lines = FormLines.Select(l => new CreatePurchaseQuotationLineDto
                {
                    Id = l.Id,
                    ProductId = l.ProductId,
                    UnitId = l.UnitId,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    DiscountPercent = l.DiscountPercent
                }).ToList();

                if (IsNew)
                {
                    var dto = new CreatePurchaseQuotationDto
                    {
                        QuotationDate = FormDate,
                        ValidUntil = FormValidUntil,
                        SupplierId = FormSupplierId ?? 0,
                        WarehouseId = FormWarehouseId ?? 0,
                        Notes = FormNotes?.Trim(),
                        Lines = lines
                    };

                    var result = await _quotationService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء طلب الشراء «{result.Data.QuotationNumber}» بنجاح";
                        CurrentQuotation = result.Data;
                        PopulateFormFromQuotation(result.Data);
                        ResetDirtyTracking();
                    }
                    else ErrorMessage = result.ErrorMessage;
                }
                else
                {
                    var dto = new UpdatePurchaseQuotationDto
                    {
                        Id = CurrentQuotation.Id,
                        QuotationDate = FormDate,
                        ValidUntil = FormValidUntil,
                        SupplierId = FormSupplierId ?? 0,
                        WarehouseId = FormWarehouseId ?? 0,
                        Notes = FormNotes?.Trim(),
                        Lines = lines
                    };

                    var result = await _quotationService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث طلب الشراء «{result.Data.QuotationNumber}» بنجاح";
                        CurrentQuotation = result.Data;
                        PopulateFormFromQuotation(result.Data);
                    }
                    else ErrorMessage = result.ErrorMessage;
                }
            }
            catch (ConcurrencyConflictException)
            {
                ErrorMessage = "حدث تعارض في البيانات. يرجى إعادة تحميل طلب الشراء.";
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الحفظ", ex); }
            finally { IsBusy = false; }
        }

        private async Task SendAsync()
        {
            if (CurrentQuotation == null) return;
            IsBusy = true; ClearError();
            try
            {
                var result = await _quotationService.SendAsync(CurrentQuotation.Id);
                if (result.IsSuccess) { StatusMessage = "تم إرسال طلب الشراء بنجاح"; await LoadQuotationDetailAsync(CurrentQuotation.Id); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الإرسال", ex); }
            finally { IsBusy = false; }
        }

        private async Task AcceptAsync()
        {
            if (CurrentQuotation == null) return;
            IsBusy = true; ClearError();
            try
            {
                var result = await _quotationService.AcceptAsync(CurrentQuotation.Id);
                if (result.IsSuccess) { StatusMessage = "تم قبول طلب الشراء بنجاح"; await LoadQuotationDetailAsync(CurrentQuotation.Id); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("القبول", ex); }
            finally { IsBusy = false; }
        }

        private async Task RejectAsync()
        {
            if (CurrentQuotation == null) return;
            IsBusy = true; ClearError();
            try
            {
                var result = await _quotationService.RejectAsync(CurrentQuotation.Id);
                if (result.IsSuccess) { StatusMessage = "تم رفض طلب الشراء"; await LoadQuotationDetailAsync(CurrentQuotation.Id); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الرفض", ex); }
            finally { IsBusy = false; }
        }

        private async Task ConvertToInvoiceAsync()
        {
            if (CurrentQuotation == null) return;
            if (!_dialog.Confirm($"هل تريد تحويل طلب الشراء «{CurrentQuotation.QuotationNumber}» إلى فاتورة شراء؟", "تأكيد التحويل")) return;

            IsBusy = true; ClearError();
            try
            {
                var result = await _quotationService.ConvertToInvoiceAsync(CurrentQuotation.Id);
                if (result.IsSuccess) { StatusMessage = $"تم تحويل طلب الشراء لفاتورة شراء رقم {result.Data}"; await LoadQuotationDetailAsync(CurrentQuotation.Id); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("التحويل", ex); }
            finally { IsBusy = false; }
        }

        private async Task CancelQuotationAsync()
        {
            if (CurrentQuotation == null) return;
            if (!_dialog.Confirm($"هل تريد إلغاء طلب الشراء «{CurrentQuotation.QuotationNumber}»؟", "تأكيد الإلغاء")) return;

            IsBusy = true; ClearError();
            try
            {
                var result = await _quotationService.CancelAsync(CurrentQuotation.Id);
                if (result.IsSuccess) { StatusMessage = "تم إلغاء طلب الشراء بنجاح"; await LoadQuotationDetailAsync(CurrentQuotation.Id); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الإلغاء", ex); }
            finally { IsBusy = false; }
        }

        private async Task DeleteDraftAsync()
        {
            if (CurrentQuotation == null) return;
            if (!_dialog.Confirm($"هل تريد حذف مسودة طلب الشراء «{CurrentQuotation.QuotationNumber}»؟", "تأكيد الحذف")) return;

            IsBusy = true; ClearError();
            try
            {
                var result = await _quotationService.DeleteDraftAsync(CurrentQuotation.Id);
                if (result.IsSuccess) { StatusMessage = "تم حذف المسودة بنجاح"; NavigateBack(); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الحذف", ex); }
            finally { IsBusy = false; }
        }

        private void StartEditing()
        {
            if (CurrentQuotation == null || !IsDraft) return;
            IsEditing = true;
            IsNew = false;
        }

        private void CancelEditing(object _)
        {
            IsEditing = false;
            IsNew = false;
            ClearError();
            if (CurrentQuotation != null) PopulateFormFromQuotation(CurrentQuotation);
            else NavigateBack();
            StatusMessage = "تم الإلغاء";
        }

        private void NavigateBack()
        {
            _navigationService.NavigateTo("PurchaseQuotations");
        }

        private void PopulateFormFromQuotation(PurchaseQuotationDto quotation)
        {
            FormNumber = quotation.QuotationNumber;
            FormDate = quotation.QuotationDate;
            FormValidUntil = quotation.ValidUntil;
            FormSupplierId = quotation.SupplierId;
            FormWarehouseId = quotation.WarehouseId;
            FormNotes = quotation.Notes;

            FormLines.Clear();
            foreach (var line in quotation.Lines ?? new List<PurchaseQuotationLineDto>())
            {
                FormLines.Add(new PurchaseQuotationLineFormItem(this)
                {
                    Id = line.Id,
                    ProductId = line.ProductId,
                    UnitId = line.UnitId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountPercent = line.DiscountPercent
                });
            }

            IsEditing = false;
            IsNew = false;
            RefreshTotals();
            ResetDirtyTracking();
        }

        private async Task LoadQuotationIdsAsync()
        {
            try
            {
                var result = await _quotationService.GetAllAsync();
                if (result.IsSuccess)
                {
                    var list = result.Data.ToList();
                    _quotationIds = list.Select(q => q.Id).ToList();
                    _quotationNumberToId = list
                        .Where(q => !string.IsNullOrWhiteSpace(q.QuotationNumber))
                        .GroupBy(q => q.QuotationNumber)
                        .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch { /* non-critical */ }
        }

        private async Task GoToNextAsync()
        {
            if (!CanGoNext) return;
            _currentQuotationIndex++;
            await LoadQuotationDetailAsync(_quotationIds[_currentQuotationIndex]);
            UpdateNavigationState();
        }

        private async Task GoToPreviousAsync()
        {
            if (!CanGoPrevious) return;
            _currentQuotationIndex--;
            await LoadQuotationDetailAsync(_quotationIds[_currentQuotationIndex]);
            UpdateNavigationState();
        }

        private async Task JumpToQuotationAsync()
        {
            if (!await DirtyStateGuard.ConfirmContinueAsync(this))
                return;

            if (string.IsNullOrWhiteSpace(JumpQuotationNumber))
                return;

            if (_quotationNumberToId.Count == 0)
                await LoadQuotationIdsAsync();

            if (!_quotationNumberToId.TryGetValue(JumpQuotationNumber.Trim(), out var id))
            {
                _dialog.ShowInfo("رقم طلب الشراء غير موجود.", "تنقل الطلبات");
                return;
            }

            _currentQuotationIndex = _quotationIds.IndexOf(id);
            await LoadQuotationDetailAsync(id);
            UpdateNavigationState();
        }

        private void UpdateNavigationState()
        {
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(NavigationPositionText));
        }
    }

    /// <summary>
    /// Represents a single line in the purchase quotation form.
    /// </summary>
    public sealed class PurchaseQuotationLineFormItem : BaseViewModel
    {
        private readonly IInvoiceLineFormHost _parent;

        public int Id { get; set; }

        public PurchaseQuotationLineFormItem(IInvoiceLineFormHost parent) { _parent = parent; }

        private int _productId;
        public int ProductId
        {
            get => _productId;
            set { if (SetProperty(ref _productId, value)) { OnProductChanged(); _parent?.RefreshTotals(); } }
        }

        private int _unitId;
        public int UnitId
        {
            get => _unitId;
            set { if (SetProperty(ref _unitId, value)) { OnUnitChanged(); _parent?.RefreshTotals(); } }
        }

        private decimal _quantity;
        public decimal Quantity
        {
            get => _quantity;
            set { if (SetProperty(ref _quantity, value)) { RecalcTotals(); _parent?.RefreshTotals(); } }
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set { if (SetProperty(ref _unitPrice, value)) { RecalcTotals(); _parent?.RefreshTotals(); } }
        }

        private decimal _discountPercent;
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set { if (SetProperty(ref _discountPercent, value)) { RecalcTotals(); _parent?.RefreshTotals(); } }
        }

        public ObservableCollection<ProductUnitDto> AvailableUnits { get; } = new();

        private decimal SelectedUnitConversionFactor
        {
            get
            {
                var unit = AvailableUnits.FirstOrDefault(u => u.UnitId == UnitId);
                return unit?.ConversionFactor ?? 1m;
            }
        }

        private decimal _subTotal;
        public decimal SubTotal { get => _subTotal; private set => SetProperty(ref _subTotal, value); }

        private decimal _discountAmount;
        public decimal DiscountAmount { get => _discountAmount; private set => SetProperty(ref _discountAmount, value); }

        private decimal _vatRate;
        public decimal VatRate { get => _vatRate; private set => SetProperty(ref _vatRate, value); }

        private decimal _vatAmount;
        public decimal VatAmount { get => _vatAmount; private set => SetProperty(ref _vatAmount, value); }

        private decimal _totalWithVat;
        public decimal TotalWithVat { get => _totalWithVat; private set => SetProperty(ref _totalWithVat, value); }

        private string _productName;
        public string ProductName { get => _productName; private set => SetProperty(ref _productName, value); }

        private void OnProductChanged()
        {
            AvailableUnits.Clear();
            if (_parent == null || ProductId <= 0) return;
            var product = _parent.Products.FirstOrDefault(p => p.Id == ProductId);
            if (product == null) return;
            ProductName = product.NameAr;
            VatRate = product.VatRate;
            foreach (var unit in product.Units) AvailableUnits.Add(unit);
            var defaultUnit = product.Units.FirstOrDefault(u => u.IsDefault) ?? product.Units.FirstOrDefault();
            if (defaultUnit != null)
            {
                UnitId = defaultUnit.UnitId;
                UnitPrice = defaultUnit.PurchasePrice; // Purchase uses PurchasePrice
            }
        }

        private void OnUnitChanged()
        {
            if (_parent == null || UnitId <= 0) return;
            var unit = AvailableUnits.FirstOrDefault(u => u.UnitId == UnitId);
            if (unit != null) UnitPrice = unit.PurchasePrice;
        }

        private void RecalcTotals()
        {
            if (_parent == null)
                return;

            var result = _parent.CalculateLine(GetCalculationRequest());
            SubTotal = result.SubTotal;
            DiscountAmount = result.DiscountAmount;
            VatAmount = result.VatAmount;
            TotalWithVat = result.TotalWithVat;
        }

        public LineCalculationRequest GetCalculationRequest()
        {
            return new LineCalculationRequest
            {
                Quantity = Quantity,
                UnitPrice = UnitPrice,
                DiscountPercent = DiscountPercent,
                VatRate = VatRate,
                ConversionFactor = SelectedUnitConversionFactor
            };
        }
    }
}
