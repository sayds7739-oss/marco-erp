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
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Application.Interfaces.SmartEntry;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Common;
using MarcoERP.WpfUI.Navigation;
using MarcoERP.WpfUI.Services;
using MarcoERP.WpfUI.ViewModels.Common;
using MarcoERP.WpfUI.Views.Common;

namespace MarcoERP.WpfUI.ViewModels.Purchases
{
    /// <summary>
    /// Full-screen ViewModel for Purchase Invoice detail — create, edit, post, cancel.
    /// </summary>
    public sealed class PurchaseInvoiceDetailViewModel : BaseViewModel, INavigationAware, IInvoiceLineFormHost, IDirtyStateAware
    {
        private readonly IPurchaseInvoiceService _invoiceService;
        private readonly IProductService _productService;
        private readonly IWarehouseService _warehouseService;
        private readonly ISupplierService _supplierService;
        private readonly ICustomerService _customerService;
        private readonly ISalesRepresentativeService _salesRepresentativeService;
        private readonly INavigationService _navigationService;
        private readonly IInvoiceTreasuryIntegrationService _invoiceTreasuryIntegrationService;
        private readonly ISmartEntryQueryService _smartEntryQueryService;
        private readonly IInvoicePdfPreviewService _invoicePdfPreviewService;
        private readonly ILineCalculationService _lineCalculationService;

        private readonly Dictionary<PurchaseInvoiceLineFormItem, int> _smartRefreshVersions = new();

        public ObservableCollection<SupplierDto> Suppliers { get; } = new();
        public ObservableCollection<CustomerDto> Customers { get; } = new();
        public ObservableCollection<SalesRepresentativeDto> SalesRepresentatives { get; } = new();
        public ObservableCollection<Application.DTOs.Inventory.WarehouseDto> Warehouses { get; } = new();
        public ObservableCollection<ProductDto> Products { get; } = new();
        public ObservableCollection<PurchaseInvoiceLineFormItem> FormLines { get; } = new();

        // ── Invoice Navigation ───────────────────────────────────
        private List<int> _invoiceIds = new();
        private int _currentInvoiceIndex = -1;
        private Dictionary<string, int> _invoiceNumberToId = new(StringComparer.OrdinalIgnoreCase);

        public bool CanGoNext => _currentInvoiceIndex >= 0 && _currentInvoiceIndex < _invoiceIds.Count - 1;
        public bool CanGoPrevious => _currentInvoiceIndex > 0;
        public string NavigationPositionText => _invoiceIds.Count > 0
            ? $"{_currentInvoiceIndex + 1} / {_invoiceIds.Count}"
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

        private PurchaseInvoiceDto _currentInvoice;
        public PurchaseInvoiceDto CurrentInvoice
        {
            get => _currentInvoice;
            set
            {
                SetProperty(ref _currentInvoice, value);
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

        public bool IsDraft => CurrentInvoice != null && CurrentInvoice.Status == "Draft";
        public bool IsPosted => CurrentInvoice != null && CurrentInvoice.Status == "Posted";
        public bool IsCancelled => CurrentInvoice != null && CurrentInvoice.Status == "Cancelled";

        private string _formNumber;
        public string FormNumber { get => _formNumber; set => SetProperty(ref _formNumber, value); }

        private DateTime _formDate = DateTime.Today;
        public DateTime FormDate { get => _formDate; set { if (SetProperty(ref _formDate, value)) MarkDirty(); } }

        private int? _formSupplierId;
        public int? FormSupplierId
        {
            get => _formSupplierId;
            set
            {
                if (SetProperty(ref _formSupplierId, value))
                {
                    MarkDirty();
                    OnPropertyChanged(nameof(CanSave));
                    EnqueueDbWork(RefreshSmartEntryForAllLinesAsync);
                }
            }
        }

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
        public int? FormCounterpartyCustomerId
        {
            get => _formCounterpartyCustomerId;
            set
            {
                if (SetProperty(ref _formCounterpartyCustomerId, value))
                {
                    MarkDirty();
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        private int? _formWarehouseId;
        public int? FormWarehouseId
        {
            get => _formWarehouseId;
            set
            {
                if (SetProperty(ref _formWarehouseId, value))
                {
                    MarkDirty();
                    OnPropertyChanged(nameof(CanSave));
                    EnqueueDbWork(RefreshSmartEntryForAllLinesAsync);
                }
            }
        }

        private int? _formSalesRepresentativeId;
        public int? FormSalesRepresentativeId
        {
            get => _formSalesRepresentativeId;
            set
            {
                if (SetProperty(ref _formSalesRepresentativeId, value))
                    MarkDirty();
            }
        }

        private string _formNotes;
        public string FormNotes { get => _formNotes; set { if (SetProperty(ref _formNotes, value)) MarkDirty(); } }

        private string _jumpInvoiceNumber;
        public string JumpInvoiceNumber
        {
            get => _jumpInvoiceNumber;
            set => SetProperty(ref _jumpInvoiceNumber, value);
        }

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

        private decimal _paidAmount;
        public decimal PaidAmount
        {
            get => _paidAmount;
            private set
            {
                if (SetProperty(ref _paidAmount, value))
                    OnPropertyChanged(nameof(RemainingAmount));
            }
        }

        public decimal BalanceAmount => CurrentInvoice?.NetTotal ?? TotalNet;
        public decimal RemainingAmount => BalanceAmount - PaidAmount;

        public void RefreshTotals()
        {
            var totals = CalculateTotals(FormLines.Select(l => l.GetCalculationRequest()));
            TotalSubtotal = totals.Subtotal;
            TotalDiscount = totals.DiscountTotal;
            TotalVat = totals.VatTotal;
            TotalNet = totals.NetTotal;
            OnPropertyChanged(nameof(BalanceAmount));
            OnPropertyChanged(nameof(RemainingAmount));
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

        public bool CanPost => CurrentInvoice != null && IsDraft && !IsEditing;
        public bool CanCancel => CurrentInvoice != null && IsPosted && !IsEditing;
        public bool CanDeleteDraft => CurrentInvoice != null && IsDraft && !IsEditing;
        public bool CanEdit => CurrentInvoice != null && IsDraft && !IsEditing;

        public ICommand SaveCommand { get; }
        public ICommand PostCommand { get; }
        public ICommand CancelInvoiceCommand { get; }
        public ICommand DeleteDraftCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand GoToNextCommand { get; }
        public ICommand GoToPreviousCommand { get; }
        public ICommand JumpToInvoiceCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand OpenAddLinePopupCommand { get; }
        public ICommand EditLineCommand { get; }
        public ICommand OpenPriceHistoryCommand { get; }

        private InvoiceLinePopupState _linePopup;
        public InvoiceLinePopupState LinePopup { get => _linePopup; private set => SetProperty(ref _linePopup, value); }

        public PurchaseInvoiceDetailViewModel(
            IPurchaseInvoiceService invoiceService,
            IProductService productService,
            IWarehouseService warehouseService,
            ISupplierService supplierService,
            ICustomerService customerService,
            ISalesRepresentativeService salesRepresentativeService,
            INavigationService navigationService,
            IInvoiceTreasuryIntegrationService invoiceTreasuryIntegrationService,
            ISmartEntryQueryService smartEntryQueryService,
            IInvoicePdfPreviewService invoicePdfPreviewService,
            ILineCalculationService lineCalculationService)
        {
            _invoiceService = invoiceService ?? throw new ArgumentNullException(nameof(invoiceService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _salesRepresentativeService = salesRepresentativeService ?? throw new ArgumentNullException(nameof(salesRepresentativeService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _invoiceTreasuryIntegrationService = invoiceTreasuryIntegrationService ?? throw new ArgumentNullException(nameof(invoiceTreasuryIntegrationService));
            _smartEntryQueryService = smartEntryQueryService ?? throw new ArgumentNullException(nameof(smartEntryQueryService));
            _invoicePdfPreviewService = invoicePdfPreviewService ?? throw new ArgumentNullException(nameof(invoicePdfPreviewService));
            _lineCalculationService = lineCalculationService ?? throw new ArgumentNullException(nameof(lineCalculationService));

            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            PostCommand = new AsyncRelayCommand(PostAsync, () => CanPost);
            CancelInvoiceCommand = new AsyncRelayCommand(CancelInvoiceAsync, () => CanCancel);
            DeleteDraftCommand = new AsyncRelayCommand(DeleteDraftAsync, () => CanDeleteDraft);
            CancelEditCommand = new RelayCommand(CancelEditing);
            AddLineCommand = new RelayCommand(AddLine);
            RemoveLineCommand = new RelayCommand(RemoveLine);
            EditCommand = new RelayCommand(_ => StartEditing());
            BackCommand = new RelayCommand(_ => NavigateBack());
            GoToNextCommand = new AsyncRelayCommand(GoToNextAsync, () => CanGoNext);
            GoToPreviousCommand = new AsyncRelayCommand(GoToPreviousAsync, () => CanGoPrevious);
            JumpToInvoiceCommand = new AsyncRelayCommand(JumpToInvoiceAsync);
            PrintCommand = new AsyncRelayCommand(ViewPdfAsync);
            OpenAddLinePopupCommand = new RelayCommand(_ => OpenAddLinePopup());
            EditLineCommand = new RelayCommand(EditLinePopup);
            OpenPriceHistoryCommand = new AsyncRelayCommand(OpenPriceHistoryAsync);
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
            else if (parameter is PurchaseInvoiceLineFormItem line)
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
                await LoadInvoiceIdsAsync();
                if (parameter is int id && id > 0)
                {
                    _currentInvoiceIndex = _invoiceIds.IndexOf(id);
                    await LoadDetailAsync(id);
                }
                else
                {
                    await PrepareNewAsync();
                }

                UpdateNavigationState();
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
        }

        private async Task LoadDetailAsync(int id)
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _invoiceService.GetByIdAsync(id);
                if (result.IsSuccess) { CurrentInvoice = result.Data; PopulateForm(result.Data); StatusMessage = $"فاتورة شراء «{result.Data.InvoiceNumber}»"; }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("تحميل الفاتورة", ex); }
            finally { IsBusy = false; }
        }

        private async Task PrepareNewAsync()
        {
            IsEditing = true; IsNew = true; CurrentInvoice = null; ClearError();
            try { var r = await _invoiceService.GetNextNumberAsync(); FormNumber = r.IsSuccess ? r.Data : ""; }
            catch { FormNumber = ""; }
            FormDate = DateTime.Today;
            FormCounterpartyType = CounterpartyType.Supplier;
            FormSupplierId = null;
            FormCounterpartyCustomerId = null;
            FormSalesRepresentativeId = null;
            FormWarehouseId = null;
            FormNotes = "";
            UnhookAllLines();
            FormLines.Clear(); RefreshTotals();
            StatusMessage = "إنشاء فاتورة شراء جديدة...";
            ResetDirtyTracking();
        }

        private void AddLine(object _)
        {
            if (!IsEditing && !IsNew) return;
            OpenAddLinePopup();
        }

        private void RemoveLine(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is PurchaseInvoiceLineFormItem line)
            {
                UnhookLine(line);
                FormLines.Remove(line);
                RefreshTotals();
                MarkDirty();
            }
        }

        private void HookLine(PurchaseInvoiceLineFormItem line)
        {
            if (line == null) return;
            if (_smartRefreshVersions.ContainsKey(line))
                return;

            _smartRefreshVersions[line] = 0;
            line.PropertyChanged += LineOnPropertyChanged;
        }

        private void UnhookLine(PurchaseInvoiceLineFormItem line)
        {
            if (line == null) return;
            if (_smartRefreshVersions.ContainsKey(line))
                _smartRefreshVersions.Remove(line);
            line.PropertyChanged -= LineOnPropertyChanged;
        }

        private void UnhookAllLines()
        {
            foreach (var line in FormLines)
                UnhookLine(line);
        }

        private void LineOnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is not PurchaseInvoiceLineFormItem line)
                return;

            if (IsUserEditableLineProperty(e.PropertyName))
                MarkDirty();

            if (e.PropertyName == nameof(PurchaseInvoiceLineFormItem.ProductId) || e.PropertyName == nameof(PurchaseInvoiceLineFormItem.UnitId))
                EnqueueDbWork(() => RefreshSmartEntryForLineAsync(line));
        }

        private static bool IsUserEditableLineProperty(string propertyName)
        {
            return propertyName == nameof(PurchaseInvoiceLineFormItem.ProductId)
                   || propertyName == nameof(PurchaseInvoiceLineFormItem.UnitId)
                   || propertyName == nameof(PurchaseInvoiceLineFormItem.Quantity)
                   || propertyName == nameof(PurchaseInvoiceLineFormItem.UnitPrice)
                   || propertyName == nameof(PurchaseInvoiceLineFormItem.DiscountPercent);
        }

        private async Task RefreshSmartEntryForAllLinesAsync()
        {
            foreach (var line in FormLines.ToList())
                await RefreshSmartEntryForLineAsync(line);
        }

        private async Task RefreshSmartEntryForLineAsync(PurchaseInvoiceLineFormItem line)
        {
            if (line == null) return;
            if (line.ProductId <= 0 || line.UnitId <= 0) return;
            if (!FormWarehouseId.HasValue || FormWarehouseId <= 0) return;

            if (!_smartRefreshVersions.TryGetValue(line, out var version))
                _smartRefreshVersions[line] = 0;
            version = ++_smartRefreshVersions[line];

            var warehouseId = FormWarehouseId.Value;
            var supplierId = FormSupplierId ?? 0;

            try
            {
                var stockBase = await _smartEntryQueryService.GetStockBaseQtyAsync(warehouseId, line.ProductId);
                var lastPurchase = supplierId > 0
                    ? await _smartEntryQueryService.GetLastPurchaseUnitPriceForSupplierAsync(supplierId, line.ProductId, line.UnitId)
                    : await _smartEntryQueryService.GetLastPurchaseUnitPriceAsync(line.ProductId, line.UnitId);

                if (!_smartRefreshVersions.TryGetValue(line, out var current) || current != version)
                    return;

                line.SetSmartEntry(stockBase, lastPurchase);

                if (lastPurchase.HasValue && line.IsUnitPriceAtMasterDefault())
                    line.UnitPrice = lastPurchase.Value;
            }
            catch
            {
                // Non-critical.
            }
        }

        private async Task SaveAsync()
        {
            await RunDbGuardedAsync(async () =>
            {
                IsBusy = true; ClearError();

                // ── Pre-save validation ──
                if (FormLines.Count == 0)
                {
                    ErrorMessage = "لا يمكن حفظ فاتورة بدون بنود. أضف صنف واحد على الأقل.";
                    IsBusy = false;
                    return;
                }
                var invalidLines = FormLines.Where(l => l.ProductId <= 0 || l.Quantity <= 0 || l.UnitPrice < 0).ToList();
                if (invalidLines.Any())
                {
                    ErrorMessage = "يوجد بنود غير مكتملة (صنف أو كمية أو سعر = صفر). يرجى مراجعة البنود.";
                    IsBusy = false;
                    return;
                }
                // Duplicate products are allowed (user confirmed during add)

                try
                {
                    var lines = FormLines.Select(l => new CreatePurchaseInvoiceLineDto
                    { ProductId = l.ProductId, UnitId = l.UnitId, Quantity = l.Quantity, UnitPrice = l.UnitPrice, DiscountPercent = l.DiscountPercent }).ToList();

                    if (IsNew)
                    {
                        var dto = new CreatePurchaseInvoiceDto
                        {
                            InvoiceDate = FormDate,
                            SupplierId = FormSupplierId,
                            WarehouseId = FormWarehouseId ?? 0,
                            Notes = FormNotes?.Trim(),
                            SalesRepresentativeId = FormSalesRepresentativeId,
                            CounterpartyType = FormCounterpartyType,
                            CounterpartyCustomerId = FormCounterpartyCustomerId,
                            Lines = lines
                        };
                        var result = await _invoiceService.CreateAsync(dto);
                        if (result.IsSuccess)
                        {
                            StatusMessage = $"تم إنشاء فاتورة الشراء «{result.Data.InvoiceNumber}» بنجاح";
                            CurrentInvoice = result.Data;
                            PopulateForm(result.Data);

                            await RefreshInvoicePaymentsAsync();
                        }
                        else ErrorMessage = result.ErrorMessage;
                    }
                    else
                    {
                        var dto = new UpdatePurchaseInvoiceDto
                        {
                            Id = CurrentInvoice.Id,
                            InvoiceDate = FormDate,
                            SupplierId = FormSupplierId,
                            WarehouseId = FormWarehouseId ?? 0,
                            Notes = FormNotes?.Trim(),
                            SalesRepresentativeId = FormSalesRepresentativeId,
                            CounterpartyType = FormCounterpartyType,
                            CounterpartyCustomerId = FormCounterpartyCustomerId,
                            Lines = lines
                        };
                        var result = await _invoiceService.UpdateAsync(dto);
                        if (result.IsSuccess) { StatusMessage = $"تم تحديث فاتورة الشراء «{result.Data.InvoiceNumber}» بنجاح"; CurrentInvoice = result.Data; PopulateForm(result.Data); }
                        else ErrorMessage = result.ErrorMessage;

                        await RefreshInvoicePaymentsAsync();
                    }
                }
                catch (ConcurrencyConflictException) { ErrorMessage = "حدث تعارض في البيانات. يرجى إعادة تحميل الفاتورة."; }
                catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("حفظ الفاتورة", ex); }
                finally { IsBusy = false; }
            });
        }

        private async Task RefreshInvoicePaymentsAsync()
        {
            if (CurrentInvoice?.Id > 0)
            {
                PaidAmount = await _invoiceTreasuryIntegrationService.GetPostedPaidForPurchaseInvoiceAsync(CurrentInvoice.Id);
            }
            else
            {
                PaidAmount = 0m;
            }

            OnPropertyChanged(nameof(BalanceAmount));
            OnPropertyChanged(nameof(RemainingAmount));
        }

        private async Task PromptCreatePaymentFromInvoiceAsync(PurchaseInvoiceDto invoice)
        {
            if (invoice == null) return;

            if (!invoice.SupplierId.HasValue || invoice.SupplierId <= 0)
            {
                MessageBox.Show(
                    "لا يمكن إنشاء سند صرف تلقائياً لأن المورد غير محدد للفاتورة.",
                    "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var supplier = Suppliers.FirstOrDefault(s => s.Id == invoice.SupplierId.Value);
            if (supplier?.AccountId is not int supplierAccountId || supplierAccountId <= 0)
            {
                MessageBox.Show(
                    "لا يمكن إنشاء سند صرف تلقائياً لأن حساب المورد غير محدد.\nيمكنك إنشاء السند يدوياً من شاشة سندات الصرف.",
                    "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);

                _navigationService.NavigateTo(
                    "CashPayments",
                    new CashPaymentNavigationParams
                    {
                        PurchaseInvoiceId = invoice.Id,
                        SupplierId = invoice.SupplierId,
                        Date = invoice.InvoiceDate,
                        Amount = invoice.NetTotal,
                        Description = $"سداد فاتورة شراء {invoice.InvoiceNumber}",
                        Notes = invoice.Notes
                    });
                return;
            }

            var createResult = await _invoiceTreasuryIntegrationService.PromptAndCreatePurchasePaymentAsync(invoice, supplierAccountId);
            if (createResult.Created)
            {
                StatusMessage = $"تم إنشاء سند صرف مرتبط بالفاتورة «{invoice.InvoiceNumber}» بنجاح";
                await RefreshInvoicePaymentsAsync();
            }
            else if (!string.IsNullOrWhiteSpace(createResult.ErrorMessage))
            {
                ErrorMessage = createResult.ErrorMessage;
            }
        }

        private async Task PostAsync()
        {
            if (CurrentInvoice == null) return;
            if (MessageBox.Show($"هل تريد ترحيل فاتورة الشراء «{CurrentInvoice.InvoiceNumber}»؟\nبعد الترحيل لا يمكن التعديل.", "تأكيد الترحيل", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            IsBusy = true; ClearError();
            try { var r = await _invoiceService.PostAsync(CurrentInvoice.Id); if (r.IsSuccess) { StatusMessage = $"تم ترحيل فاتورة الشراء «{r.Data.InvoiceNumber}»"; CurrentInvoice = r.Data; PopulateForm(r.Data); await PromptCreatePaymentFromInvoiceAsync(r.Data); } else ErrorMessage = r.ErrorMessage; }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("ترحيل الفاتورة", ex); }
            finally { IsBusy = false; }
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

        private async Task CancelInvoiceAsync()
        {
            if (CurrentInvoice == null) return;
            if (MessageBox.Show($"هل تريد إلغاء فاتورة الشراء «{CurrentInvoice.InvoiceNumber}»؟", "تأكيد الإلغاء", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            IsBusy = true; ClearError();
            try { var r = await _invoiceService.CancelAsync(CurrentInvoice.Id); if (r.IsSuccess) { StatusMessage = "تم إلغاء الفاتورة"; await LoadDetailAsync(CurrentInvoice.Id); } else ErrorMessage = r.ErrorMessage; }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("إلغاء الفاتورة", ex); }
            finally { IsBusy = false; }
        }

        private async Task DeleteDraftAsync()
        {
            if (CurrentInvoice == null) return;
            if (MessageBox.Show($"هل تريد حذف مسودة الفاتورة «{CurrentInvoice.InvoiceNumber}»؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            IsBusy = true; ClearError();
            try { var r = await _invoiceService.DeleteDraftAsync(CurrentInvoice.Id); if (r.IsSuccess) { StatusMessage = "تم حذف المسودة"; NavigateBack(); } else ErrorMessage = r.ErrorMessage; }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("حذف المسودة", ex); }
            finally { IsBusy = false; }
        }

        private void StartEditing()
        {
            if (CurrentInvoice != null && IsDraft)
            {
                IsEditing = true;
                IsNew = false;
            }
        }

        private void CancelEditing(object _)
        {
            IsEditing = false;
            IsNew = false;
            ClearError();

            if (CurrentInvoice != null)
                PopulateForm(CurrentInvoice);
            else
                NavigateBack();

            ResetDirtyTracking();
        }
        private void NavigateBack() => _navigationService.NavigateTo("PurchaseInvoices");

        private void PopulateForm(PurchaseInvoiceDto inv)
        {
            FormNumber = inv.InvoiceNumber;
            FormDate = inv.InvoiceDate;
            FormCounterpartyType = inv.CounterpartyType;
            FormSupplierId = inv.SupplierId;
            FormCounterpartyCustomerId = inv.CounterpartyCustomerId;
            FormSalesRepresentativeId = inv.SalesRepresentativeId;
            FormWarehouseId = inv.WarehouseId;
            FormNotes = inv.Notes;
            UnhookAllLines();
            FormLines.Clear();
            foreach (var line in inv.Lines ?? new List<PurchaseInvoiceLineDto>())
            {
                var formLine = new PurchaseInvoiceLineFormItem(this) { ProductId = line.ProductId, UnitId = line.UnitId, Quantity = line.Quantity, UnitPrice = line.UnitPrice, DiscountPercent = line.DiscountPercent };
                HookLine(formLine);
                FormLines.Add(formLine);
            }
            IsEditing = false; IsNew = false; RefreshTotals();
            ResetDirtyTracking();

            EnqueueDbWork(RefreshSmartEntryForAllLinesAsync);
        }

        // ── Invoice Navigation ───────────────────────────────
        private async Task LoadInvoiceIdsAsync()
        {
            try
            {
                var result = await _invoiceService.GetAllAsync();
                if (result.IsSuccess)
                {
                    var list = result.Data.ToList();
                    _invoiceIds = list.Select(i => i.Id).ToList();
                    _invoiceNumberToId = list
                        .Where(i => !string.IsNullOrWhiteSpace(i.InvoiceNumber))
                        .GroupBy(i => i.InvoiceNumber)
                        .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch { /* non-critical */ }
        }

        private async Task GoToNextAsync()
        {
            if (!await DirtyStateGuard.ConfirmContinueAsync(this))
                return;
            if (!CanGoNext) return;
            _currentInvoiceIndex++;
            await LoadDetailAsync(_invoiceIds[_currentInvoiceIndex]);
            UpdateNavigationState();
        }

        private async Task GoToPreviousAsync()
        {
            if (!await DirtyStateGuard.ConfirmContinueAsync(this))
                return;
            if (!CanGoPrevious) return;
            _currentInvoiceIndex--;
            await LoadDetailAsync(_invoiceIds[_currentInvoiceIndex]);
            UpdateNavigationState();
        }

        private async Task JumpToInvoiceAsync()
        {
            if (!await DirtyStateGuard.ConfirmContinueAsync(this))
                return;

            if (string.IsNullOrWhiteSpace(JumpInvoiceNumber))
                return;

            if (!_invoiceNumberToId.TryGetValue(JumpInvoiceNumber.Trim(), out var id))
            {
                MessageBox.Show("رقم الفاتورة غير موجود.", "تنقل الفواتير", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _currentInvoiceIndex = _invoiceIds.IndexOf(id);
            await LoadDetailAsync(id);
            UpdateNavigationState();
        }

        private void UpdateNavigationState()
        {
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(NavigationPositionText));
            RelayCommand.RaiseCanExecuteChanged();
        }

        private async Task ViewPdfAsync()
        {
            if (CurrentInvoice == null) return;
            await _invoicePdfPreviewService.ShowPurchaseInvoiceAsync(CurrentInvoice);
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
            if (parameter is not PurchaseInvoiceLineFormItem line) return;
            var index = FormLines.IndexOf(line);
            if (index < 0) return;

            var state = new InvoiceLinePopupState(this, InvoicePopupMode.Purchase, _lineCalculationService);
            LinePopup = state;
            state.PropertyChanged += PopupState_PropertyChanged;
            state.LoadFromLine(line.ProductId, line.UnitId, line.Quantity, line.UnitPrice,
                line.DiscountPercent, line.SmartStockQty ?? 0, line.SmartLastPurchaseUnitPrice ?? 0,
                line.SmartAverageCost, null, index);
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
                var confirm = MessageBox.Show(
                    "هذا الصنف موجود بالفعل في الفاتورة.\nهل تريد إضافته مرة أخرى؟",
                    "صنف مكرر", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (confirm != MessageBoxResult.Yes)
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
                var line = new PurchaseInvoiceLineFormItem(this)
                { ProductId = state.ProductId, UnitId = state.SelectedUnitId, Quantity = state.SelectedQty, UnitPrice = state.SelectedUnitPrice, DiscountPercent = state.DiscountPercent };
                HookLine(line);
                FormLines.Add(line);
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
            catch { }
        }

    }
}
