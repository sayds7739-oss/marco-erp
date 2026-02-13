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
using MarcoERP.Application.DTOs.Treasury;
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
using MarcoERP.WpfUI.Views.Sales;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    /// <summary>
    /// Full-screen ViewModel for Sales Invoice detail — create, edit, post, cancel.
    /// Implements INavigationAware to receive invoice ID from list view.
    /// </summary>
    public sealed class SalesInvoiceDetailViewModel : BaseViewModel, INavigationAware, IInvoiceLineFormHost, IDirtyStateAware
    {
        private readonly ISalesInvoiceService _invoiceService;
        private readonly IProductService _productService;
        private readonly IWarehouseService _warehouseService;
        private readonly ICustomerService _customerService;
        private readonly ISupplierService _supplierService;
        private readonly ISalesRepresentativeService _salesRepresentativeService;
        private readonly INavigationService _navigationService;
        private readonly IInvoiceTreasuryIntegrationService _invoiceTreasuryIntegrationService;
        private readonly ISmartEntryQueryService _smartEntryQueryService;
        private readonly IInvoicePdfPreviewService _invoicePdfPreviewService;
        private readonly ILineCalculationService _lineCalculationService;

        private readonly Dictionary<SalesInvoiceLineFormItem, int> _smartRefreshVersions = new();

        // ── Collections ──────────────────────────────────────────
        public ObservableCollection<Application.DTOs.Sales.CustomerDto> Customers { get; } = new();
        public ObservableCollection<Application.DTOs.Purchases.SupplierDto> Suppliers { get; } = new();
        public ObservableCollection<SalesRepresentativeDto> SalesRepresentatives { get; } = new();
        public ObservableCollection<Application.DTOs.Inventory.WarehouseDto> Warehouses { get; } = new();
        public ObservableCollection<ProductDto> Products { get; } = new();
        public ObservableCollection<SalesInvoiceLineFormItem> FormLines { get; } = new();

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

        // ── State ───────────────────────────────────────────────
        private SalesInvoiceDto _currentInvoice;
        public SalesInvoiceDto CurrentInvoice
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
        public bool IsDraft => CurrentInvoice != null && CurrentInvoice.Status == "Draft";
        public bool IsPosted => CurrentInvoice != null && CurrentInvoice.Status == "Posted";
        public bool IsCancelled => CurrentInvoice != null && CurrentInvoice.Status == "Cancelled";

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

        private int? _formCustomerId;
        public int? FormCustomerId
        {
            get => _formCustomerId;
            set
            {
                if (SetProperty(ref _formCustomerId, value))
                {
                    MarkDirty();
                    OnPropertyChanged(nameof(CanSave));
                    EnqueueDbWork(async () => 
                    {
                        await RefreshCustomerFinancialStatusAsync();
                        await RefreshSmartEntryForAllLinesAsync();
                    });
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

        private CounterpartyType _formCounterpartyType;
        public CounterpartyType FormCounterpartyType
        {
            get => _formCounterpartyType;
            set
            {
                if (SetProperty(ref _formCounterpartyType, value))
                {
                    MarkDirty();
                    OnPropertyChanged(nameof(IsCustomerMode));
                    OnPropertyChanged(nameof(IsSupplierMode));
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        public bool IsCustomerMode => FormCounterpartyType == CounterpartyType.Customer;
        public bool IsSupplierMode => FormCounterpartyType == CounterpartyType.Supplier;

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

        /// <summary>Static list for ComboBox binding.</summary>
        public static IReadOnlyList<KeyValuePair<CounterpartyType, string>> CounterpartyTypes { get; } = new[]
        {
            new KeyValuePair<CounterpartyType, string>(CounterpartyType.Customer, "عميل"),
            new KeyValuePair<CounterpartyType, string>(CounterpartyType.Supplier, "مورد")
        };

        private string _formNotes;
        public string FormNotes
        {
            get => _formNotes;
            set { if (SetProperty(ref _formNotes, value)) MarkDirty(); }
        }

        private string _jumpInvoiceNumber;
        public string JumpInvoiceNumber
        {
            get => _jumpInvoiceNumber;
            set => SetProperty(ref _jumpInvoiceNumber, value);
        }

        // ── Totals (preview) ────────────────────────────────────
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

        private decimal _customerPreviousBalance;
        public decimal CustomerPreviousBalance
        {
            get => _customerPreviousBalance;
            private set => SetProperty(ref _customerPreviousBalance, value);
        }

        private decimal _customerOutstandingAmount;
        public decimal CustomerOutstandingAmount
        {
            get => _customerOutstandingAmount;
            private set => SetProperty(ref _customerOutstandingAmount, value);
        }

        private bool _customerHasOverdue;
        public bool CustomerHasOverdue
        {
            get => _customerHasOverdue;
            private set => SetProperty(ref _customerHasOverdue, value);
        }

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

        // ── CanExecute Guards ───────────────────────────────────
        public bool CanSave => IsEditing
                               && (FormCounterpartyType != CounterpartyType.Customer || (FormCustomerId.HasValue && FormCustomerId > 0))
                               && (FormCounterpartyType != CounterpartyType.Supplier || (FormSupplierId.HasValue && FormSupplierId > 0))
                               && FormWarehouseId.HasValue && FormWarehouseId > 0
                               && FormLines.Count > 0
                               && FormLines.All(l => l.ProductId > 0 && l.Quantity > 0 && l.UnitPrice >= 0);

        public bool CanPost => CurrentInvoice != null && IsDraft && !IsEditing;
        public bool CanCancel => CurrentInvoice != null && IsPosted && !IsEditing;
        public bool CanDeleteDraft => CurrentInvoice != null && IsDraft && !IsEditing;
        public bool CanEdit => CurrentInvoice != null && IsDraft && !IsEditing;

        // ── Popup State ──────────────────────────────────────────
        private InvoiceLinePopupState _linePopup;
        /// <summary>State for the add/edit line popup window.</summary>
        public InvoiceLinePopupState LinePopup
        {
            get => _linePopup;
            private set => SetProperty(ref _linePopup, value);
        }

        // ── Commands ────────────────────────────────────────────
        public ICommand SaveCommand { get; }
        public ICommand PostCommand { get; }
        public ICommand CancelInvoiceCommand { get; }
        public ICommand DeleteDraftCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand EditLineCommand { get; }
        public ICommand OpenAddLinePopupCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand GoToNextCommand { get; }
        public ICommand GoToPreviousCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand ExportToExcelCommand { get; }
        public ICommand JumpToInvoiceCommand { get; }
        public ICommand OpenPriceHistoryCommand { get; }

        // ── Constructor ─────────────────────────────────────────
        public SalesInvoiceDetailViewModel(
            ISalesInvoiceService invoiceService,
            IProductService productService,
            IWarehouseService warehouseService,
            ICustomerService customerService,
            ISupplierService supplierService,
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
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
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
            OpenAddLinePopupCommand = new RelayCommand(_ => OpenAddLinePopup());
            EditLineCommand = new RelayCommand(EditLinePopup);
            EditCommand = new RelayCommand(_ => StartEditing());
            BackCommand = new RelayCommand(_ => NavigateBack());
            GoToNextCommand = new AsyncRelayCommand(GoToNextAsync, () => CanGoNext);
            GoToPreviousCommand = new AsyncRelayCommand(GoToPreviousAsync, () => CanGoPrevious);
            PrintCommand = new AsyncRelayCommand(ViewPdfAsync);
            ExportToExcelCommand = new AsyncRelayCommand(ExportToExcelAsync);
            JumpToInvoiceCommand = new AsyncRelayCommand(JumpToInvoiceAsync);
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
            else if (parameter is SalesInvoiceLineFormItem line)
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
                PriceHistorySource.Sales,
                FormCounterpartyType,
                FormCustomerId,
                FormSupplierId,
                productId,
                unitId,
                owner);

            if (selectedPrice.HasValue)
                applyPrice(selectedPrice.Value);
        }

        // ── INavigationAware ────────────────────────────────────
        public async Task OnNavigatedToAsync(object parameter)
        {
            await RunDbGuardedAsync(async () =>
            {
                await LoadLookupsAsync();
                await LoadInvoiceIdsAsync();

                if (parameter is int invoiceId && invoiceId > 0)
                {
                    _currentInvoiceIndex = _invoiceIds.IndexOf(invoiceId);
                    await LoadInvoiceDetailAsync(invoiceId);
                }
                else
                {
                    await PrepareNewAsync();
                }

                UpdateNavigationState();
            });
        }

        // ── Load ────────────────────────────────────────────────
        private async Task LoadLookupsAsync()
        {
            var custResult = await _customerService.GetAllAsync();
            Customers.Clear();
            if (custResult.IsSuccess)
                foreach (var c in custResult.Data.Where(x => x.IsActive))
                    Customers.Add(c);

            var suppResult = await _supplierService.GetAllAsync();
            Suppliers.Clear();
            if (suppResult.IsSuccess)
                foreach (var s in suppResult.Data.Where(x => x.IsActive))
                    Suppliers.Add(s);

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

            await RefreshCustomerFinancialStatusAsync();
        }

        private async Task LoadInvoiceDetailAsync(int id)
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _invoiceService.GetByIdAsync(id);
                if (result.IsSuccess)
                {
                    CurrentInvoice = result.Data;
                    PopulateFormFromInvoice(result.Data);
                    await RefreshInvoicePaymentsAsync();
                    StatusMessage = $"فاتورة بيع «{result.Data.InvoiceNumber}»";
                    UpdateTabTitle(result.Data.InvoiceNumber, GetStatusText(result.Data.Status));
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تحميل الفاتورة", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Prepare New ─────────────────────────────────────────
        private async Task PrepareNewAsync()
        {
            IsEditing = true;
            IsNew = true;
            CurrentInvoice = null;
            ClearError();

            try
            {
                var numResult = await _invoiceService.GetNextNumberAsync();
                FormNumber = numResult.IsSuccess ? numResult.Data : "";
            }
            catch { FormNumber = ""; }

            FormDate = DateTime.Today;
            FormCustomerId = null;
            FormSalesRepresentativeId = null;
            FormWarehouseId = null;
            FormNotes = "";
            UnhookAllLines();
            FormLines.Clear();
            RefreshTotals();
            ResetDirtyTracking();
            StatusMessage = "إنشاء فاتورة بيع جديدة...";
        }

        private void AddLine(object _)
        {
            if (!IsEditing && !IsNew) return;
            OpenAddLinePopup();
        }

        private void RemoveLine(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is SalesInvoiceLineFormItem line)
            {
                UnhookLine(line);
                FormLines.Remove(line);
                RefreshTotals();
                MarkDirty();
            }
        }

        private void HookLine(SalesInvoiceLineFormItem line)
        {
            if (line == null) return;
            if (_smartRefreshVersions.ContainsKey(line))
                return;

            _smartRefreshVersions[line] = 0;
            line.PropertyChanged += LineOnPropertyChanged;
        }

        private void UnhookLine(SalesInvoiceLineFormItem line)
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
            if (sender is not SalesInvoiceLineFormItem line)
                return;

            if (IsUserEditableLineProperty(e.PropertyName))
                MarkDirty();

            if (e.PropertyName == nameof(SalesInvoiceLineFormItem.ProductId)
                || e.PropertyName == nameof(SalesInvoiceLineFormItem.UnitId)
                || e.PropertyName == nameof(SalesInvoiceLineFormItem.Quantity))
                EnqueueDbWork(() => RefreshSmartEntryForLineAsync(line));
        }

        private static bool IsUserEditableLineProperty(string propertyName)
        {
            return propertyName == nameof(SalesInvoiceLineFormItem.ProductId)
                   || propertyName == nameof(SalesInvoiceLineFormItem.UnitId)
                   || propertyName == nameof(SalesInvoiceLineFormItem.Quantity)
                   || propertyName == nameof(SalesInvoiceLineFormItem.UnitPrice)
                   || propertyName == nameof(SalesInvoiceLineFormItem.DiscountPercent);
        }

        private async Task RefreshSmartEntryForAllLinesAsync()
        {
            foreach (var line in FormLines.ToList())
                await RefreshSmartEntryForLineAsync(line);
        }

        private async Task RefreshSmartEntryForLineAsync(SalesInvoiceLineFormItem line)
        {
            if (line == null) return;
            if (line.ProductId <= 0 || line.UnitId <= 0) return;
            if (!FormWarehouseId.HasValue || FormWarehouseId <= 0) return;

            if (!_smartRefreshVersions.TryGetValue(line, out var version))
                _smartRefreshVersions[line] = 0;
            version = ++_smartRefreshVersions[line];

            var warehouseId = FormWarehouseId.Value;
            var customerId = FormCustomerId ?? 0;

            try
            {
                var stockBase = await _smartEntryQueryService.GetStockBaseQtyAsync(warehouseId, line.ProductId);
                var lastSale = customerId > 0
                    ? await _smartEntryQueryService.GetLastSalesUnitPriceAsync(customerId, line.ProductId, line.UnitId)
                    : null;
                var lastPurchase = await _smartEntryQueryService.GetLastPurchaseUnitPriceAsync(line.ProductId, line.UnitId);

                decimal? tierBaseUnitPrice = null;
                decimal? tierUnitPrice = null;
                if (customerId > 0)
                {
                    var selectedUnit = line.AvailableUnits.FirstOrDefault(u => u.UnitId == line.UnitId);
                    var factor = selectedUnit?.ConversionFactor ?? 1m;
                    if (factor <= 0) factor = 1m;

                    var baseQty = _lineCalculationService.ConvertQuantity(line.Quantity, factor);
                    tierBaseUnitPrice = await _smartEntryQueryService
                        .GetBestTierSaleBaseUnitPriceForCustomerAsync(customerId, line.ProductId, baseQty);

                    if (tierBaseUnitPrice.HasValue)
                        tierUnitPrice = _lineCalculationService.ConvertQuantity(tierBaseUnitPrice.Value, factor);
                }

                if (!_smartRefreshVersions.TryGetValue(line, out var current) || current != version)
                    return;

                line.SetSmartEntry(stockBase, lastSale, lastPurchase);

                // Pricing priority (only when user is still on master default):
                // Tier price > Last sale price > Master default.
                if (line.IsUnitPriceAtMasterDefault())
                {
                    if (tierUnitPrice.HasValue)
                        line.UnitPrice = tierUnitPrice.Value;
                    else if (lastSale.HasValue)
                        line.UnitPrice = lastSale.Value;
                }
            }
            catch
            {
                // Smart entry data is non-critical; ignore failures.
            }
        }

        private async Task RefreshCustomerFinancialStatusAsync()
        {
            if (!FormCustomerId.HasValue || FormCustomerId <= 0)
            {
                CustomerPreviousBalance = 0m;
                CustomerOutstandingAmount = 0m;
                CustomerHasOverdue = false;
                return;
            }

            var customer = Customers.FirstOrDefault(c => c.Id == FormCustomerId.Value);
            CustomerPreviousBalance = customer?.PreviousBalance ?? 0m;

            try
            {
                CustomerOutstandingAmount = await _smartEntryQueryService.GetCustomerOutstandingSalesBalanceAsync(FormCustomerId.Value);
            }
            catch
            {
                CustomerOutstandingAmount = 0m;
            }

            CustomerHasOverdue = false;
            if (customer?.DaysAllowed is int daysAllowed && daysAllowed > 0)
            {
                var cutoff = DateTime.Today.AddDays(-daysAllowed);
                try
                {
                    CustomerHasOverdue = await _smartEntryQueryService.HasOverduePostedSalesInvoicesAsync(FormCustomerId.Value, cutoff);
                }
                catch
                {
                    CustomerHasOverdue = false;
                }
            }
        }

        // ── Save ────────────────────────────────────────────────
        private async Task SaveAsync()
        {
            await RunDbGuardedAsync(async () =>
            {
                IsBusy = true;
                ClearError();

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

                var wasNew = IsNew;
                try
                {
                    var lines = FormLines.Select(l => new CreateSalesInvoiceLineDto
                    {
                        ProductId = l.ProductId,
                        UnitId = l.UnitId,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        DiscountPercent = l.DiscountPercent
                    }).ToList();

                    if (IsNew)
                    {
                        var dto = new CreateSalesInvoiceDto
                        {
                            InvoiceDate = FormDate,
                            CustomerId = FormCustomerId ?? 0,
                            WarehouseId = FormWarehouseId ?? 0,
                            Notes = FormNotes?.Trim(),
                            SalesRepresentativeId = FormSalesRepresentativeId,
                            CounterpartyType = FormCounterpartyType,
                            SupplierId = FormSupplierId,
                            Lines = lines
                        };

                        var result = await _invoiceService.CreateAsync(dto);
                        if (result.IsSuccess)
                        {
                            StatusMessage = $"تم إنشاء فاتورة البيع «{result.Data.InvoiceNumber}» بنجاح";
                            CurrentInvoice = result.Data;
                            PopulateFormFromInvoice(result.Data);
                            UpdateTabTitle(result.Data.InvoiceNumber, GetStatusText(result.Data.Status));
                            ResetDirtyTracking();

                            await RefreshInvoicePaymentsAsync();
                        }
                        else ErrorMessage = result.ErrorMessage;
                    }
                    else
                    {
                        var dto = new UpdateSalesInvoiceDto
                        {
                            Id = CurrentInvoice.Id,
                            InvoiceDate = FormDate,
                            CustomerId = FormCustomerId ?? 0,
                            WarehouseId = FormWarehouseId ?? 0,
                            Notes = FormNotes?.Trim(),
                            SalesRepresentativeId = FormSalesRepresentativeId,
                            CounterpartyType = FormCounterpartyType,
                            SupplierId = FormSupplierId,
                            Lines = lines
                        };

                        var result = await _invoiceService.UpdateAsync(dto);
                        if (result.IsSuccess)
                        {
                            StatusMessage = $"تم تحديث فاتورة البيع «{result.Data.InvoiceNumber}» بنجاح";
                            CurrentInvoice = result.Data;
                            PopulateFormFromInvoice(result.Data);
                            UpdateTabTitle(result.Data.InvoiceNumber, GetStatusText(result.Data.Status));

                            await RefreshInvoicePaymentsAsync();
                        }
                        else ErrorMessage = result.ErrorMessage;
                    }
                }
                catch (ConcurrencyConflictException)
                {
                    ErrorMessage = "حدث تعارض في البيانات. يرجى إعادة تحميل الفاتورة.";
                }
                catch (Exception ex)
                {
                    ErrorMessage = FriendlyErrorMessage("حفظ الفاتورة", ex);
                }
                finally { IsBusy = false; }
            });
        }

        private async Task RefreshInvoicePaymentsAsync()
        {
            if (CurrentInvoice?.Id > 0)
            {
                PaidAmount = await _invoiceTreasuryIntegrationService.GetPostedPaidForSalesInvoiceAsync(CurrentInvoice.Id);
            }
            else
            {
                PaidAmount = 0m;
            }

            OnPropertyChanged(nameof(BalanceAmount));
            OnPropertyChanged(nameof(RemainingAmount));
        }

        private async Task PromptCreateReceiptFromInvoiceAsync(SalesInvoiceDto invoice)
        {
            if (invoice == null) return;

            var customer = Customers.FirstOrDefault(c => c.Id == invoice.CustomerId);
            if (customer?.AccountId is not int customerAccountId || customerAccountId <= 0)
            {
                MessageBox.Show(
                    "لا يمكن إنشاء سند قبض تلقائياً لأن حساب العميل غير محدد.\nيمكنك إنشاء السند يدوياً من شاشة سندات القبض.",
                    "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);

                _navigationService.NavigateTo(
                    "CashReceipts",
                    new CashReceiptNavigationParams
                    {
                        SalesInvoiceId = invoice.Id,
                        CustomerId = invoice.CustomerId,
                        Date = invoice.InvoiceDate,
                        Amount = invoice.NetTotal,
                        Description = $"تحصيل فاتورة بيع {invoice.InvoiceNumber}",
                        Notes = invoice.Notes
                    });
                return;
            }

            var createResult = await _invoiceTreasuryIntegrationService.PromptAndCreateSalesReceiptAsync(invoice, customerAccountId);
            if (createResult.Created)
            {
                StatusMessage = $"تم إنشاء سند قبض مرتبط بالفاتورة «{invoice.InvoiceNumber}» بنجاح";
                await RefreshInvoicePaymentsAsync();
            }
            else if (!string.IsNullOrWhiteSpace(createResult.ErrorMessage))
            {
                ErrorMessage = createResult.ErrorMessage;
            }
            // Note: PromptAndCreateSalesReceiptAsync already handles the user prompt internally.
            // No secondary prompt needed — removed to prevent double-prompting.
        }

        private async Task PostAsync()
        {
            if (CurrentInvoice == null) return;
            var confirm = MessageBox.Show(
                $"هل تريد ترحيل فاتورة البيع «{CurrentInvoice.InvoiceNumber}»؟\nبعد الترحيل لا يمكن التعديل وسيتم إنشاء قيود محاسبية تلقائية.",
                "تأكيد الترحيل", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _invoiceService.PostAsync(CurrentInvoice.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم ترحيل فاتورة البيع «{result.Data.InvoiceNumber}» بنجاح";
                    CurrentInvoice = result.Data;
                    PopulateFormFromInvoice(result.Data);
                    await PromptCreateReceiptFromInvoiceAsync(result.Data);
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("ترحيل الفاتورة", ex); }
            finally { IsBusy = false; }
        }

        private async Task CancelInvoiceAsync()
        {
            if (CurrentInvoice == null) return;
            var confirm = MessageBox.Show(
                $"هل تريد إلغاء فاتورة البيع «{CurrentInvoice.InvoiceNumber}»؟\nسيتم إنشاء قيد عكسي وإعادة الكميات.",
                "تأكيد الإلغاء", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _invoiceService.CancelAsync(CurrentInvoice.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم إلغاء الفاتورة بنجاح";
                    await LoadInvoiceDetailAsync(CurrentInvoice.Id);
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("إلغاء الفاتورة", ex); }
            finally { IsBusy = false; }
        }

        private async Task DeleteDraftAsync()
        {
            if (CurrentInvoice == null) return;
            var confirm = MessageBox.Show(
                $"هل تريد حذف مسودة الفاتورة «{CurrentInvoice.InvoiceNumber}»؟",
                "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _invoiceService.DeleteDraftAsync(CurrentInvoice.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم حذف المسودة بنجاح";
                    NavigateBack();
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("حذف المسودة", ex); }
            finally { IsBusy = false; }
        }

        private void StartEditing()
        {
            if (CurrentInvoice == null || !IsDraft) return;
            IsEditing = true;
            IsNew = false;
        }

        private void CancelEditing(object _)
        {
            IsEditing = false;
            IsNew = false;
            ClearError();
            if (CurrentInvoice != null)
                PopulateFormFromInvoice(CurrentInvoice);
            else
                NavigateBack();
            StatusMessage = "تم الإلغاء";
            ResetDirtyTracking();
        }

        private void NavigateBack()
        {
            _navigationService.NavigateTo("SalesInvoices");
        }

        private void PopulateFormFromInvoice(SalesInvoiceDto invoice)
        {
            FormNumber = invoice.InvoiceNumber;
            FormDate = invoice.InvoiceDate;
            FormCounterpartyType = invoice.CounterpartyType;
            FormCustomerId = invoice.CustomerId;
            FormSupplierId = invoice.SupplierId;
            FormSalesRepresentativeId = invoice.SalesRepresentativeId;
            FormWarehouseId = invoice.WarehouseId;
            FormNotes = invoice.Notes;

            UnhookAllLines();
            FormLines.Clear();
            foreach (var line in invoice.Lines ?? new List<SalesInvoiceLineDto>())
            {
                var formLine = new SalesInvoiceLineFormItem(this)
                {
                    ProductId = line.ProductId,
                    UnitId = line.UnitId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountPercent = line.DiscountPercent
                };
                HookLine(formLine);
                FormLines.Add(formLine);
            }

            IsEditing = false;
            IsNew = false;
            RefreshTotals();
            ResetDirtyTracking();

            EnqueueDbWork(async () =>
            {
                await RefreshCustomerFinancialStatusAsync();
                await RefreshSmartEntryForAllLinesAsync();
            });
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
            await LoadInvoiceDetailAsync(_invoiceIds[_currentInvoiceIndex]);
            UpdateNavigationState();
        }

        private async Task GoToPreviousAsync()
        {
            if (!await DirtyStateGuard.ConfirmContinueAsync(this))
                return;
            if (!CanGoPrevious) return;
            _currentInvoiceIndex--;
            await LoadInvoiceDetailAsync(_invoiceIds[_currentInvoiceIndex]);
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
            await LoadInvoiceDetailAsync(id);
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
            await _invoicePdfPreviewService.ShowSalesInvoiceAsync(CurrentInvoice);
        }

        private async Task ExportToExcelAsync()
        {
            if (CurrentInvoice == null) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"فاتورة_{CurrentInvoice.InvoiceNumber}_{CurrentInvoice.InvoiceDate:yyyy-MM-dd}.xlsx",
                Title = "تصدير الفاتورة إلى Excel"
            };

            if (dlg.ShowDialog() != true) return;

            await Task.Run(() =>
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var ws = workbook.Worksheets.Add("فاتورة مبيعات");
                ws.RightToLeft = true;

                // ── Header Section ──
                var headerFont = ws.Style.Font;

                ws.Cell(1, 1).Value = "فاتورة مبيعات";
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(1, 1).Style.Font.FontSize = 16;
                ws.Range(1, 1, 1, 8).Merge();
                ws.Cell(1, 1).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;

                ws.Cell(3, 1).Value = "رقم الفاتورة:";
                ws.Cell(3, 1).Style.Font.Bold = true;
                ws.Cell(3, 2).Value = CurrentInvoice.InvoiceNumber;

                ws.Cell(3, 4).Value = "التاريخ:";
                ws.Cell(3, 4).Style.Font.Bold = true;
                ws.Cell(3, 5).Value = CurrentInvoice.InvoiceDate.ToString("yyyy-MM-dd");

                ws.Cell(4, 1).Value = "العميل:";
                ws.Cell(4, 1).Style.Font.Bold = true;
                ws.Cell(4, 2).Value = CurrentInvoice.CustomerNameAr ?? CurrentInvoice.SupplierNameAr ?? "";

                ws.Cell(4, 4).Value = "المستودع:";
                ws.Cell(4, 4).Style.Font.Bold = true;
                ws.Cell(4, 5).Value = CurrentInvoice.WarehouseNameAr ?? "";

                ws.Cell(5, 1).Value = "الحالة:";
                ws.Cell(5, 1).Style.Font.Bold = true;
                ws.Cell(5, 2).Value = CurrentInvoice.Status;

                // ── Lines Table Header ──
                int headerRow = 7;
                var headers = new[] { "#", "كود الصنف", "اسم الصنف", "الوحدة", "الكمية", "سعر الوحدة", "خصم %", "مبلغ الخصم", "الصافي", "نسبة الضريبة %", "مبلغ الضريبة", "الإجمالي" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(headerRow, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(0x1565C0);
                    cell.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                    cell.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                    cell.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                }

                // ── Lines Data ──
                int dataRow = headerRow + 1;
                int lineNum = 1;
                foreach (var line in CurrentInvoice.Lines)
                {
                    ws.Cell(dataRow, 1).Value = lineNum;
                    ws.Cell(dataRow, 2).Value = line.ProductCode ?? "";
                    ws.Cell(dataRow, 3).Value = line.ProductNameAr ?? "";
                    ws.Cell(dataRow, 4).Value = line.UnitNameAr ?? "";
                    ws.Cell(dataRow, 5).Value = line.Quantity;
                    ws.Cell(dataRow, 6).Value = line.UnitPrice;
                    ws.Cell(dataRow, 7).Value = line.DiscountPercent;
                    ws.Cell(dataRow, 8).Value = line.DiscountAmount;
                    ws.Cell(dataRow, 9).Value = line.NetTotal;
                    ws.Cell(dataRow, 10).Value = line.VatRate;
                    ws.Cell(dataRow, 11).Value = line.VatAmount;
                    ws.Cell(dataRow, 12).Value = line.TotalWithVat;

                    // Alternate row color
                    if (lineNum % 2 == 0)
                    {
                        ws.Range(dataRow, 1, dataRow, 12).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(0xF5F5F5);
                    }

                    // Borders
                    ws.Range(dataRow, 1, dataRow, 12).Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                    ws.Range(dataRow, 1, dataRow, 12).Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;

                    // Number formatting
                    ws.Cell(dataRow, 5).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(dataRow, 6).Style.NumberFormat.Format = "#,##0.0000";
                    ws.Cell(dataRow, 7).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(dataRow, 8).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(dataRow, 9).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(dataRow, 10).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(dataRow, 11).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(dataRow, 12).Style.NumberFormat.Format = "#,##0.00";

                    dataRow++;
                    lineNum++;
                }

                // ── Totals Row ──
                int totalsRow = dataRow + 1;
                ws.Cell(totalsRow, 1).Value = "الإجماليات";
                ws.Cell(totalsRow, 1).Style.Font.Bold = true;
                ws.Range(totalsRow, 1, totalsRow, 4).Merge();

                void SetTotalCell(int col, string label, decimal value)
                {
                    ws.Cell(totalsRow, col).Value = label;
                    ws.Cell(totalsRow, col).Style.Font.Bold = true;
                    ws.Cell(totalsRow, col + 1).Value = value;
                    ws.Cell(totalsRow, col + 1).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(totalsRow, col + 1).Style.Font.Bold = true;
                }

                SetTotalCell(5, "الإجمالي:", CurrentInvoice.Subtotal);
                SetTotalCell(7, "الخصم:", CurrentInvoice.DiscountTotal);
                SetTotalCell(9, "الضريبة:", CurrentInvoice.VatTotal);
                ws.Cell(totalsRow, 11).Value = "الصافي:";
                ws.Cell(totalsRow, 11).Style.Font.Bold = true;
                ws.Cell(totalsRow, 12).Value = CurrentInvoice.NetTotal;
                ws.Cell(totalsRow, 12).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(totalsRow, 12).Style.Font.Bold = true;
                ws.Cell(totalsRow, 12).Style.Font.FontSize = 13;
                ws.Cell(totalsRow, 12).Style.Font.FontColor = ClosedXML.Excel.XLColor.FromArgb(0x1565C0);

                // Totals border
                ws.Range(totalsRow, 1, totalsRow, 12).Style.Border.TopBorder = ClosedXML.Excel.XLBorderStyleValues.Double;
                ws.Range(totalsRow, 1, totalsRow, 12).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(0xE3F2FD);

                // ── Notes ──
                if (!string.IsNullOrWhiteSpace(CurrentInvoice.Notes))
                {
                    int notesRow = totalsRow + 2;
                    ws.Cell(notesRow, 1).Value = "ملاحظات:";
                    ws.Cell(notesRow, 1).Style.Font.Bold = true;
                    ws.Cell(notesRow, 2).Value = CurrentInvoice.Notes;
                    ws.Range(notesRow, 2, notesRow, 6).Merge();
                }

                // ── Auto-fit columns ──
                ws.Columns(1, 12).AdjustToContents();

                workbook.SaveAs(dlg.FileName);
            });

            StatusMessage = "تم تصدير الفاتورة إلى Excel بنجاح";
        }

        // ── Add/Edit Line Popup ──────────────────────────────────

        /// <summary>Opens the add-line popup for a new line.</summary>
        private void OpenAddLinePopup()
        {
            if (!IsEditing && !IsNew) return;
            var state = new InvoiceLinePopupState(this, InvoicePopupMode.Sale, _lineCalculationService);
            state.IsVatInclusive = App.IsVatInclusive;
            LinePopup = state;
            state.PropertyChanged += PopupState_PropertyChanged;
            ShowPopupLoop(state);
            state.PropertyChanged -= PopupState_PropertyChanged;
            LinePopup = null;
        }

        /// <summary>Opens the add-line popup to edit an existing line.</summary>
        private void EditLinePopup(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is not SalesInvoiceLineFormItem line) return;

            var index = FormLines.IndexOf(line);
            if (index < 0) return;

            var state = new InvoiceLinePopupState(this, InvoicePopupMode.Sale, _lineCalculationService);
            state.IsVatInclusive = App.IsVatInclusive;
            LinePopup = state;
            state.PropertyChanged += PopupState_PropertyChanged;
            state.LoadFromLine(line.ProductId, line.UnitId, line.Quantity, line.UnitPrice,
                line.DiscountPercent, 0, 0, 0, null, index);
            EnqueueDbWork(() => RefreshSmartEntryForPopupAsync(state));

            var parentWindow = System.Windows.Application.Current.MainWindow;
            var popup = new InvoiceAddLineWindow
            {
                Owner = parentWindow,
                DataContext = this
            };

            if (popup.ShowDialog() == true && popup.LineAdded && state.IsValid)
            {
                ApplyPopupStateToLine(state, index);
            }

            state.PropertyChanged -= PopupState_PropertyChanged;
            LinePopup = null;
        }

        /// <summary>Shows the popup in a loop for "Add & Next" workflow.</summary>
        private void ShowPopupLoop(InvoiceLinePopupState state)
        {
            var parentWindow = System.Windows.Application.Current.MainWindow;
            var keepAdding = true;

            while (keepAdding)
            {
                state.Reset();
                var popup = new InvoiceAddLineWindow
                {
                    Owner = parentWindow,
                    DataContext = this
                };

                if (popup.ShowDialog() == true && popup.LineAdded && state.IsValid)
                {
                    ApplyPopupStateToLine(state, editIndex: null);
                    keepAdding = popup.AddAnother;
                }
                else
                {
                    keepAdding = false;
                }
            }
        }

        /// <summary>Creates or updates a form line from the popup state.</summary>
        private void ApplyPopupStateToLine(InvoiceLinePopupState state, int? editIndex)
        {
            // ── Duplicate product check — ask the user ──
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

            // ── Stock validation (sales only — warn if selling more than available) ──
            if (state.StockQty >= 0)
            {
                var selectedUnit = state.AvailableUnits.FirstOrDefault(u => u.UnitId == state.SelectedUnitId);
                var factor = selectedUnit?.ConversionFactor ?? 1m;
                if (factor <= 0) factor = 1m;
                var baseQtyNeeded = state.SelectedQty * factor;

                if (baseQtyNeeded > state.StockQty && state.StockQty >= 0)
                {
                    var confirm = MessageBox.Show(
                        $"الكمية المطلوبة ({state.SelectedQty:N2}) أكبر من المخزون المتاح ({state.StockQty:N2} بالوحدة الأساسية).\nهل تريد المتابعة؟",
                        "تحذير المخزون", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                    if (confirm != MessageBoxResult.Yes)
                        return;
                }
            }

            if (editIndex.HasValue && editIndex.Value >= 0 && editIndex.Value < FormLines.Count)
            {
                // Update existing line
                var existingLine = FormLines[editIndex.Value];
                existingLine.ProductId = state.ProductId;
                existingLine.UnitId = state.SelectedUnitId;
                existingLine.Quantity = state.SelectedQty;
                existingLine.UnitPrice = state.SelectedUnitPrice;
                existingLine.DiscountPercent = state.DiscountPercent;
            }
            else
            {
                // Add new line
                var line = new SalesInvoiceLineFormItem(this)
                {
                    ProductId = state.ProductId,
                    UnitId = state.SelectedUnitId,
                    Quantity = state.SelectedQty,
                    UnitPrice = state.SelectedUnitPrice,
                    DiscountPercent = state.DiscountPercent
                };
                HookLine(line);
                FormLines.Add(line);
            }

            RefreshTotals();
            MarkDirty();
        }

        /// <summary>Watches popup product changes to fetch smart entry data.</summary>
        private void PopupState_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is not InvoiceLinePopupState state) return;
            if (e.PropertyName == nameof(InvoiceLinePopupState.ProductId))
                EnqueueDbWork(() => RefreshSmartEntryForPopupAsync(state));
        }

        /// <summary>Fetches stock, last prices, and tier prices for the popup.</summary>
        private async Task RefreshSmartEntryForPopupAsync(InvoiceLinePopupState state)
        {
            if (state == null || state.ProductId <= 0) return;
            if (!FormWarehouseId.HasValue || FormWarehouseId <= 0) return;

            var warehouseId = FormWarehouseId.Value;
            var customerId = FormCustomerId ?? 0;
            var unitId = state.SelectedUnitId > 0 ? state.SelectedUnitId : (state.SecondaryUnit?.UnitId ?? 0);

            try
            {
                var stockBase = await _smartEntryQueryService.GetStockBaseQtyAsync(warehouseId, state.ProductId);
                state.StockQty = stockBase;

                var lastPurchase = await _smartEntryQueryService.GetLastPurchaseUnitPriceAsync(state.ProductId, unitId);
                state.LastPurchasePrice = lastPurchase ?? 0m;

                if (customerId > 0)
                {
                    var lastSale = await _smartEntryQueryService.GetLastSalesUnitPriceAsync(customerId, state.ProductId, unitId);
                    state.LastSalePrice = lastSale;
                }
            }
            catch
            {
                // Smart entry is non-critical; ignore failures.
            }
        }

        // ── Tab Title Update ────────────────────────────────────
        /// <summary>Updates the tab title to show invoice number and status.</summary>
        private void UpdateTabTitle(string invoiceNumber, string statusText)
        {
            try
            {
                // Find current tab through navigation service
                var mainViewModel = System.Windows.Application.Current.MainWindow?.DataContext as Shell.MainWindowViewModel;
                if (mainViewModel == null) return;

                var currentTab = mainViewModel.ActiveTab;
                if (currentTab != null)
                {
                    currentTab.Title = $"فاتورة بيع - {invoiceNumber}";
                    currentTab.StatusText = statusText;
                }
            }
            catch
            {
                // Tab update is non-critical; ignore failures
            }
        }

        /// <summary>Returns Arabic status text for display.</summary>
        private static string GetStatusText(string status)
        {
            return status switch
            {
                "Draft" => "مسودة",
                "Posted" => "مرحّلة",
                "Cancelled" => "ملغاة",
                _ => status
            };
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

    }
}

