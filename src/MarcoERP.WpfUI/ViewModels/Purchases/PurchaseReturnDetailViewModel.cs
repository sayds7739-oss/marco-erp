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
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Application.Interfaces.SmartEntry;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Application.DTOs.Printing;
using MarcoERP.Application.Interfaces.Printing;
using MarcoERP.WpfUI.Common;
using MarcoERP.WpfUI.Navigation;
using MarcoERP.WpfUI.Services;
using MarcoERP.WpfUI.ViewModels.Common;
using MarcoERP.WpfUI.Views.Common;

namespace MarcoERP.WpfUI.ViewModels.Purchases
{
    /// <summary>
    /// Full-screen ViewModel for Purchase Return detail — create, edit, post, cancel.
    /// Includes optional OriginalInvoiceId reference.
    /// </summary>
    public sealed class PurchaseReturnDetailViewModel : BaseViewModel, INavigationAware, IInvoiceLineFormHost, IDirtyStateAware
    {
        private readonly IPurchaseReturnService _returnService;
        private readonly IPurchaseInvoiceService _invoiceService;
        private readonly IProductService _productService;
        private readonly IWarehouseService _warehouseService;
        private readonly ISupplierService _supplierService;
        private readonly ICustomerService _customerService;
        private readonly ISalesRepresentativeService _salesRepresentativeService;
        private readonly INavigationService _navigationService;
        private readonly ILineCalculationService _lineCalculationService;
        private readonly ISmartEntryQueryService _smartEntryQueryService;
        private readonly IDialogService _dialog;
        private readonly IInvoicePdfPreviewService _previewService;
        private readonly IDocumentHtmlBuilder _htmlBuilder;

        public ObservableCollection<SupplierDto> Suppliers { get; } = new();
        public ObservableCollection<CustomerDto> Customers { get; } = new();
        public ObservableCollection<SalesRepresentativeDto> SalesRepresentatives { get; } = new();
        public ObservableCollection<Application.DTOs.Inventory.WarehouseDto> Warehouses { get; } = new();
        public ObservableCollection<ProductDto> Products { get; } = new();
        public ObservableCollection<PurchaseInvoiceListDto> PostedInvoices { get; } = new();
        public ObservableCollection<PurchaseReturnLineFormItem> FormLines { get; } = new();

        private PurchaseReturnDto _currentReturn;
        public PurchaseReturnDto CurrentReturn
        {
            get => _currentReturn;
            set
            {
                SetProperty(ref _currentReturn, value);
                OnPropertyChanged(nameof(IsPosted));
                OnPropertyChanged(nameof(IsCancelled));
                OnPropertyChanged(nameof(IsDraft));
                OnPropertyChanged(nameof(CanPost));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(CanDeleteDraft));
                OnPropertyChanged(nameof(CanEdit));
            }
        }

        private bool _isEditing;
        public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }

        private bool _isNew;
        public bool IsNew { get => _isNew; set => SetProperty(ref _isNew, value); }

        public bool IsDraft => CurrentReturn != null && CurrentReturn.Status == "Draft";
        public bool IsPosted => CurrentReturn != null && CurrentReturn.Status == "Posted";
        public bool IsCancelled => CurrentReturn != null && CurrentReturn.Status == "Cancelled";

        private string _formNumber;
        public string FormNumber { get => _formNumber; set => SetProperty(ref _formNumber, value); }

        private DateTime _formDate = DateTime.Today;
        public DateTime FormDate { get => _formDate; set { if (SetProperty(ref _formDate, value)) MarkDirty(); } }

        private int? _formSupplierId;
        public int? FormSupplierId { get => _formSupplierId; set { if (SetProperty(ref _formSupplierId, value)) { MarkDirty(); OnPropertyChanged(nameof(CanSave)); } } }

        private CounterpartyType _formCounterpartyType = CounterpartyType.Supplier;
        public CounterpartyType FormCounterpartyType
        {
            get => _formCounterpartyType;
            set
            {
                if (SetProperty(ref _formCounterpartyType, value))
                {
                    MarkDirty();
                    if (value == CounterpartyType.Supplier)
                        FormCounterpartyCustomerId = null;
                    else
                        FormSupplierId = null;
                    OnPropertyChanged(nameof(IsSupplierMode));
                    OnPropertyChanged(nameof(IsCustomerMode));
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        public bool IsSupplierMode => FormCounterpartyType == CounterpartyType.Supplier;
        public bool IsCustomerMode => FormCounterpartyType == CounterpartyType.Customer;

        public static IReadOnlyList<KeyValuePair<CounterpartyType, string>> CounterpartyTypes { get; } = new[]
        {
            new KeyValuePair<CounterpartyType, string>(CounterpartyType.Supplier, "مورد"),
            new KeyValuePair<CounterpartyType, string>(CounterpartyType.Customer, "عميل")
        };

        private int? _formCounterpartyCustomerId;
        public int? FormCounterpartyCustomerId { get => _formCounterpartyCustomerId; set { if (SetProperty(ref _formCounterpartyCustomerId, value)) { MarkDirty(); OnPropertyChanged(nameof(CanSave)); } } }

        private int? _formWarehouseId;
        public int? FormWarehouseId { get => _formWarehouseId; set { if (SetProperty(ref _formWarehouseId, value)) { MarkDirty(); OnPropertyChanged(nameof(CanSave)); } } }

        private int? _formSalesRepresentativeId;
        public int? FormSalesRepresentativeId { get => _formSalesRepresentativeId; set { if (SetProperty(ref _formSalesRepresentativeId, value)) MarkDirty(); } }

        private int? _formOriginalInvoiceId;
        public int? FormOriginalInvoiceId { get => _formOriginalInvoiceId; set { if (SetProperty(ref _formOriginalInvoiceId, value)) MarkDirty(); } }

        private string _formNotes;
        public string FormNotes { get => _formNotes; set { if (SetProperty(ref _formNotes, value)) MarkDirty(); } }

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

        public bool CanSave => IsEditing
                       && FormWarehouseId.HasValue && FormWarehouseId > 0
                       && FormLines.Count > 0
                       && (FormCounterpartyType != CounterpartyType.Supplier || (FormSupplierId.HasValue && FormSupplierId > 0))
                       && (FormCounterpartyType != CounterpartyType.Customer || (FormCounterpartyCustomerId.HasValue && FormCounterpartyCustomerId > 0))
                       && FormLines.All(l => l.ProductId > 0 && l.Quantity > 0 && l.UnitPrice >= 0);

        public bool CanPost => CurrentReturn != null && IsDraft && !IsEditing;
        public bool CanCancel => CurrentReturn != null && IsPosted && !IsEditing;
        public bool CanDeleteDraft => CurrentReturn != null && IsDraft && !IsEditing;
        public bool CanEdit => CurrentReturn != null && IsDraft && !IsEditing;

        public ICommand SaveCommand { get; }
        public ICommand PostCommand { get; }
        public ICommand CancelReturnCommand { get; }
        public ICommand DeleteDraftCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand OpenAddLinePopupCommand { get; }
        public ICommand EditLineCommand { get; }
        public ICommand OpenPriceHistoryCommand { get; }
        public ICommand PrintCommand { get; }

        // ── IDirtyStateAware ─────────────────────────────────────
        public void ResetDirtyState() => ResetDirtyTracking();
        public async Task<bool> SaveChangesAsync()
        {
            if (!CanSave) return false;
            await SaveAsync();
            return !IsDirty && !HasError;
        }

        private InvoiceLinePopupState _linePopup;
        public InvoiceLinePopupState LinePopup { get => _linePopup; private set => SetProperty(ref _linePopup, value); }

        public PurchaseReturnDetailViewModel(
            IPurchaseReturnService returnService,
            IPurchaseInvoiceService invoiceService,
            IProductService productService,
            IWarehouseService warehouseService,
            ISupplierService supplierService,
            ICustomerService customerService,
            ISalesRepresentativeService salesRepresentativeService,
            INavigationService navigationService,
            ILineCalculationService lineCalculationService,
            ISmartEntryQueryService smartEntryQueryService,
            IDialogService dialog,
            IInvoicePdfPreviewService previewService,
            IDocumentHtmlBuilder htmlBuilder)
        {
            _returnService = returnService ?? throw new ArgumentNullException(nameof(returnService));
            _invoiceService = invoiceService ?? throw new ArgumentNullException(nameof(invoiceService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _salesRepresentativeService = salesRepresentativeService ?? throw new ArgumentNullException(nameof(salesRepresentativeService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _lineCalculationService = lineCalculationService ?? throw new ArgumentNullException(nameof(lineCalculationService));
            _smartEntryQueryService = smartEntryQueryService ?? throw new ArgumentNullException(nameof(smartEntryQueryService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
            _htmlBuilder = htmlBuilder ?? throw new ArgumentNullException(nameof(htmlBuilder));

            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            PostCommand = new AsyncRelayCommand(PostAsync, () => CanPost);
            CancelReturnCommand = new AsyncRelayCommand(CancelReturnAsync, () => CanCancel);
            DeleteDraftCommand = new AsyncRelayCommand(DeleteDraftAsync, () => CanDeleteDraft);
            CancelEditCommand = new RelayCommand(CancelEditing);
            AddLineCommand = new RelayCommand(AddLine);
            RemoveLineCommand = new RelayCommand(RemoveLine);
            EditCommand = new RelayCommand(_ => StartEditing());
            BackCommand = new RelayCommand(_ => NavigateBack());
            OpenAddLinePopupCommand = new RelayCommand(_ => OpenAddLinePopup());
            EditLineCommand = new RelayCommand(EditLinePopup);
            OpenPriceHistoryCommand = new AsyncRelayCommand(OpenPriceHistoryAsync);
            PrintCommand = new AsyncRelayCommand(PrintAsync);
        }

        private async Task PrintAsync(object _)
        {
            if (CurrentReturn == null) return;
            IsBusy = true;
            ClearError();
            try
            {
                var data = new DocumentData
                {
                    Title = $"مرتجع مشتريات رقم {CurrentReturn.ReturnNumber}",
                    DocumentType = PrintableDocumentType.PurchaseReturn,
                    MetaFields = new()
                    {
                        new("رقم المرتجع", CurrentReturn.ReturnNumber),
                        new("التاريخ", CurrentReturn.ReturnDate.ToString("yyyy-MM-dd")),
                        new("المورد", CurrentReturn.SupplierNameAr ?? "—"),
                        new("المستودع", CurrentReturn.WarehouseNameAr ?? "—"),
                        new("الحالة", CurrentReturn.Status, true),
                        new("فاتورة المرجع", CurrentReturn.OriginalInvoiceNumber ?? "—")
                    },
                    Columns = new()
                    {
                        new("#"), new("كود الصنف"), new("اسم الصنف"),
                        new("الوحدة"), new("الكمية", true), new("السعر", true),
                        new("الخصم", true), new("الضريبة", true), new("الصافي", true)
                    },
                    Notes = CurrentReturn.Notes
                };
                int row = 1;
                foreach (var l in CurrentReturn.Lines)
                    data.Rows.Add(new() { (row++).ToString(), l.ProductCode, l.ProductNameAr, l.UnitNameAr,
                        l.Quantity.ToString("N2"), l.UnitPrice.ToString("N2"), l.DiscountAmount.ToString("N2"),
                        l.VatAmount.ToString("N2"), l.NetTotal.ToString("N2") });
                data.SummaryFields = new()
                {
                    new("الإجمالي", CurrentReturn.Subtotal.ToString("N2")),
                    new("الخصم", CurrentReturn.DiscountTotal.ToString("N2")),
                    new("الضريبة", CurrentReturn.VatTotal.ToString("N2")),
                    new("الصافي", CurrentReturn.NetTotal.ToString("N2"), true)
                };
                var html = await _htmlBuilder.BuildAsync(data);
                await _previewService.ShowHtmlPreviewAsync(new InvoicePdfPreviewRequest
                {
                    Title = data.Title, FilePrefix = "purchase_return", HtmlContent = html
                });
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الطباعة", ex); }
            finally { IsBusy = false; }
        }

        private async Task OpenPriceHistoryAsync(object parameter)
        {
            int productId = 0;
            int unitId = 0;
            Action<decimal> applyPrice = null;

            if (parameter is InvoiceLinePopupState popup)
            {
                productId = popup.ProductId;
                unitId = popup.SelectedUnitId;
                applyPrice = popup.ApplyUnitPrice;
            }
            else if (parameter is PurchaseReturnLineFormItem line)
            {
                productId = line.ProductId;
                unitId = line.UnitId;
                applyPrice = price => line.UnitPrice = price;
            }

            if (productId <= 0 || unitId <= 0 || applyPrice == null)
                return;

            var owner = System.Windows.Application.Current?.MainWindow;
            var selectedPrice = await PriceHistoryHelper.ShowAsync(
                _smartEntryQueryService,
                PriceHistorySource.Purchase,
                FormCounterpartyType,
                FormCounterpartyCustomerId,
                FormSupplierId,
                productId,
                unitId,
                owner);

            if (selectedPrice.HasValue)
                applyPrice(selectedPrice.Value);
        }

        public async Task OnNavigatedToAsync(object parameter)
        {
            await RunDbGuardedAsync(async () =>
            {
                await LoadLookupsAsync();
                if (parameter is int id && id > 0)
                    await LoadDetailAsync(id);
                else
                    await PrepareNewAsync();
            });
        }

        private async Task LoadLookupsAsync()
        {
            var suppResult = await _supplierService.GetAllAsync();
            Suppliers.Clear();
            if (suppResult.IsSuccess)
                foreach (var s in suppResult.Data.Where(x => x.IsActive))
                    Suppliers.Add(s);

            var custResult = await _customerService.GetAllAsync();
            Customers.Clear();
            if (custResult.IsSuccess)
                foreach (var c in custResult.Data.Where(x => x.IsActive))
                    Customers.Add(c);

            var repResult = await _salesRepresentativeService.GetActiveAsync();
            SalesRepresentatives.Clear();
            if (repResult.IsSuccess)
                foreach (var rep in repResult.Data)
                    SalesRepresentatives.Add(rep);

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

            var invResult = await _invoiceService.GetAllAsync();
            PostedInvoices.Clear();
            if (invResult.IsSuccess)
                foreach (var inv in invResult.Data.Where(x => x.Status == "Posted"))
                    PostedInvoices.Add(inv);
        }

        private async Task LoadDetailAsync(int id)
        {
            IsBusy = true; ClearError();
            try
            {
                var result = await _returnService.GetByIdAsync(id);
                if (result.IsSuccess) { CurrentReturn = result.Data; PopulateForm(result.Data); StatusMessage = $"مرتجع شراء «{result.Data.ReturnNumber}»"; }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("تحميل المرتجع", ex); }
            finally { IsBusy = false; }
        }

        private async Task PrepareNewAsync()
        {
            IsEditing = true; IsNew = true; CurrentReturn = null; ClearError();
            try { var r = await _returnService.GetNextNumberAsync(); FormNumber = r.IsSuccess ? r.Data : ""; }
            catch { FormNumber = ""; }
            FormDate = DateTime.Today;
            FormCounterpartyType = CounterpartyType.Supplier;
            FormSupplierId = null;
            FormCounterpartyCustomerId = null;
            FormSalesRepresentativeId = null;
            FormWarehouseId = null;
            FormOriginalInvoiceId = null; FormNotes = "";
            FormLines.Clear(); RefreshTotals();
            StatusMessage = "إنشاء مرتجع شراء جديد...";
        }

        private void AddLine(object _)
        {
            if (!IsEditing && !IsNew) return;
            OpenAddLinePopup();
        }

        private void RemoveLine(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is PurchaseReturnLineFormItem line)
            { FormLines.Remove(line); RefreshTotals(); }
        }

        private async Task SaveAsync()
        {
            await RunDbGuardedAsync(async () =>
            {
                IsBusy = true; ClearError();

                // ── Pre-save validation ──
                if (FormLines.Count == 0)
                {
                    ErrorMessage = "لا يمكن حفظ مرتجع بدون بنود. أضف صنف واحد على الأقل.";
                    IsBusy = false;
                    return;
                }

                var invalidLines = FormLines.Where(l => l.ProductId <= 0 || l.Quantity <= 0 || l.UnitPrice < 0).ToList();
                if (invalidLines.Any())
                {
                    ErrorMessage = "يوجد بنود غير مكتملة (صنف أو كمية = صفر). يرجى مراجعة البنود.";
                    IsBusy = false;
                    return;
                }

                // Duplicate products are allowed (user confirmed during add)

                try
                {
                    var lines = FormLines.Select(l => new CreatePurchaseReturnLineDto
                    { Id = l.Id, ProductId = l.ProductId, UnitId = l.UnitId, Quantity = l.Quantity, UnitPrice = l.UnitPrice, DiscountPercent = l.DiscountPercent }).ToList();

                    if (IsNew)
                    {
                        var dto = new CreatePurchaseReturnDto
                        {
                            ReturnDate = FormDate,
                            SupplierId = FormSupplierId,
                            CounterpartyType = FormCounterpartyType,
                            CounterpartyCustomerId = FormCounterpartyCustomerId,
                            SalesRepresentativeId = FormSalesRepresentativeId,
                            WarehouseId = FormWarehouseId ?? 0,
                            OriginalInvoiceId = FormOriginalInvoiceId,
                            Notes = FormNotes?.Trim(),
                            Lines = lines
                        };
                        var result = await _returnService.CreateAsync(dto);
                        if (result.IsSuccess) { StatusMessage = $"تم إنشاء مرتجع الشراء «{result.Data.ReturnNumber}» بنجاح"; CurrentReturn = result.Data; PopulateForm(result.Data); }
                        else ErrorMessage = result.ErrorMessage;
                    }
                    else
                    {
                        var dto = new UpdatePurchaseReturnDto
                        {
                            Id = CurrentReturn.Id,
                            ReturnDate = FormDate,
                            SupplierId = FormSupplierId,
                            CounterpartyType = FormCounterpartyType,
                            CounterpartyCustomerId = FormCounterpartyCustomerId,
                            SalesRepresentativeId = FormSalesRepresentativeId,
                            WarehouseId = FormWarehouseId ?? 0,
                            OriginalInvoiceId = FormOriginalInvoiceId,
                            Notes = FormNotes?.Trim(),
                            Lines = lines
                        };
                        var result = await _returnService.UpdateAsync(dto);
                        if (result.IsSuccess) { StatusMessage = $"تم تحديث مرتجع الشراء «{result.Data.ReturnNumber}» بنجاح"; CurrentReturn = result.Data; PopulateForm(result.Data); }
                        else ErrorMessage = result.ErrorMessage;
                    }
                }
                catch (ConcurrencyConflictException) { ErrorMessage = "حدث تعارض في البيانات. يرجى إعادة تحميل المرتجع."; }
                catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("حفظ المرتجع", ex); }
                finally { IsBusy = false; }
            });
        }

        /// <summary>Serializes DB access within this ViewModel.</summary>
        private async Task RunDbGuardedAsync(Func<Task> work)
        {
            await DbGuard.WaitAsync().ConfigureAwait(false);
            try
            {
                await work().ConfigureAwait(false);
            }
            finally
            {
                DbGuard.Release();
            }
        }

        private async Task PostAsync()
        {
            if (CurrentReturn == null) return;
            if (!_dialog.Confirm($"هل تريد ترحيل مرتجع الشراء «{CurrentReturn.ReturnNumber}»؟\nبعد الترحيل لا يمكن التعديل.", "تأكيد الترحيل")) return;
            IsBusy = true; ClearError();
            try { var r = await _returnService.PostAsync(CurrentReturn.Id); if (r.IsSuccess) { StatusMessage = $"تم ترحيل مرتجع الشراء «{r.Data.ReturnNumber}»"; CurrentReturn = r.Data; PopulateForm(r.Data); } else ErrorMessage = r.ErrorMessage; }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("ترحيل المرتجع", ex); }
            finally { IsBusy = false; }
        }

        private async Task CancelReturnAsync()
        {
            if (CurrentReturn == null) return;
            if (!_dialog.Confirm($"هل تريد إلغاء مرتجع الشراء «{CurrentReturn.ReturnNumber}»؟", "تأكيد الإلغاء")) return;
            IsBusy = true; ClearError();
            try { var r = await _returnService.CancelAsync(CurrentReturn.Id); if (r.IsSuccess) { StatusMessage = "تم إلغاء المرتجع"; await LoadDetailAsync(CurrentReturn.Id); } else ErrorMessage = r.ErrorMessage; }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("إلغاء المرتجع", ex); }
            finally { IsBusy = false; }
        }

        private async Task DeleteDraftAsync()
        {
            if (CurrentReturn == null) return;
            if (!_dialog.Confirm($"هل تريد حذف مسودة المرتجع «{CurrentReturn.ReturnNumber}»؟", "تأكيد الحذف")) return;
            IsBusy = true; ClearError();
            try { var r = await _returnService.DeleteDraftAsync(CurrentReturn.Id); if (r.IsSuccess) { StatusMessage = "تم حذف المسودة"; NavigateBack(); } else ErrorMessage = r.ErrorMessage; }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("حذف المسودة", ex); }
            finally { IsBusy = false; }
        }

        private void StartEditing() { if (CurrentReturn != null && IsDraft) { IsEditing = true; IsNew = false; } }
        private void CancelEditing(object _) { IsEditing = false; IsNew = false; ClearError(); if (CurrentReturn != null) PopulateForm(CurrentReturn); else NavigateBack(); ResetDirtyTracking(); }
        private void NavigateBack() => _navigationService.NavigateTo("PurchaseReturns");

        private void PopulateForm(PurchaseReturnDto ret)
        {
            FormNumber = ret.ReturnNumber;
            FormDate = ret.ReturnDate;
            FormCounterpartyType = ret.CounterpartyType;
            FormSupplierId = ret.SupplierId;
            FormCounterpartyCustomerId = ret.CounterpartyCustomerId;
            FormSalesRepresentativeId = ret.SalesRepresentativeId;
            FormWarehouseId = ret.WarehouseId;
            FormOriginalInvoiceId = ret.OriginalInvoiceId;
            FormNotes = ret.Notes;
            FormLines.Clear();
            foreach (var line in ret.Lines ?? new List<PurchaseReturnLineDto>())
                FormLines.Add(new PurchaseReturnLineFormItem(this) { Id = line.Id, ProductId = line.ProductId, UnitId = line.UnitId, Quantity = line.Quantity, UnitPrice = line.UnitPrice, DiscountPercent = line.DiscountPercent });
            IsEditing = false; IsNew = false; RefreshTotals();
            ResetDirtyTracking();
        }

        // ── Add/Edit Line Popup ──────────────────────────────────
        private void OpenAddLinePopup()
        {
            if (!IsEditing && !IsNew) return;
            var state = new InvoiceLinePopupState(this, InvoicePopupMode.Purchase, _lineCalculationService);
            LinePopup = state;
            state.PropertyChanged += PopupState_PropertyChanged;
            ShowPopupLoop(state);
            state.PropertyChanged -= PopupState_PropertyChanged;
            LinePopup = null;
        }

        private void EditLinePopup(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is not PurchaseReturnLineFormItem line) return;
            var index = FormLines.IndexOf(line);
            if (index < 0) return;

            var state = new InvoiceLinePopupState(this, InvoicePopupMode.Purchase, _lineCalculationService);
            LinePopup = state;
            state.PropertyChanged += PopupState_PropertyChanged;
            state.LoadFromLine(line.ProductId, line.UnitId, line.Quantity, line.UnitPrice,
                line.DiscountPercent, 0, 0, 0, null, index);
            EnqueueDbWork(() => RefreshSmartEntryForPopupAsync(state));

            var popup = new InvoiceAddLineWindow { Owner = System.Windows.Application.Current.MainWindow, DataContext = this };
            if (popup.ShowDialog() == true && popup.LineAdded && state.IsValid)
                ApplyPopupStateToLine(state, index);

            state.PropertyChanged -= PopupState_PropertyChanged;
            LinePopup = null;
        }

        private void ShowPopupLoop(InvoiceLinePopupState state)
        {
            var parent = System.Windows.Application.Current.MainWindow;
            var keepAdding = true;
            while (keepAdding)
            {
                state.Reset();
                var popup = new InvoiceAddLineWindow { Owner = parent, DataContext = this };
                if (popup.ShowDialog() == true && popup.LineAdded && state.IsValid)
                {
                    ApplyPopupStateToLine(state, editIndex: null);
                    keepAdding = popup.AddAnother;
                }
                else keepAdding = false;
            }
        }

        private void ApplyPopupStateToLine(InvoiceLinePopupState state, int? editIndex)
        {
            // ── Duplicate product check ──
            var existingIndex = editIndex ?? -1;
            var isDuplicate = FormLines
                .Where((l, i) => i != existingIndex && l.ProductId == state.ProductId && l.ProductId > 0)
                .Any();
            if (isDuplicate)
            {
                if (!_dialog.Confirm("هذا الصنف موجود بالفعل في المرتجع.\nهل تريد إضافته مرة أخرى؟", "صنف مكرر"))
                    return;
            }

            if (editIndex.HasValue && editIndex.Value >= 0 && editIndex.Value < FormLines.Count)
            {
                var existing = FormLines[editIndex.Value];
                existing.ProductId = state.ProductId;
                existing.UnitId = state.SelectedUnitId;
                existing.Quantity = state.SelectedQty;
                existing.UnitPrice = state.SelectedUnitPrice;
                existing.DiscountPercent = state.DiscountPercent;
            }
            else
            {
                FormLines.Add(new PurchaseReturnLineFormItem(this)
                { ProductId = state.ProductId, UnitId = state.SelectedUnitId, Quantity = state.SelectedQty, UnitPrice = state.SelectedUnitPrice, DiscountPercent = state.DiscountPercent });
            }
            RefreshTotals();
            MarkDirty();
        }

        private void PopupState_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is InvoiceLinePopupState state && e.PropertyName == nameof(InvoiceLinePopupState.ProductId))
                EnqueueDbWork(() => RefreshSmartEntryForPopupAsync(state));
        }

        private async Task RefreshSmartEntryForPopupAsync(InvoiceLinePopupState state)
        {
            if (state == null || state.ProductId <= 0) return;
            if (!FormWarehouseId.HasValue || FormWarehouseId <= 0) return;
            var warehouseId = FormWarehouseId.Value;
            var supplierId = FormSupplierId ?? 0;
            var unitId = state.SelectedUnitId > 0 ? state.SelectedUnitId : (state.SecondaryUnit?.UnitId ?? 0);
            try
            {
                var stock = await _smartEntryQueryService.GetStockBaseQtyAsync(warehouseId, state.ProductId);
                state.StockQty = stock;
                var lastPurch = supplierId > 0
                    ? await _smartEntryQueryService.GetLastPurchaseUnitPriceForSupplierAsync(supplierId, state.ProductId, unitId)
                    : await _smartEntryQueryService.GetLastPurchaseUnitPriceAsync(state.ProductId, unitId);
                state.LastPurchasePrice = lastPurch ?? 0m;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PurchaseReturnDetail] Failed to load stock/price hints: {ex.Message}");
            }
        }
    }
}
