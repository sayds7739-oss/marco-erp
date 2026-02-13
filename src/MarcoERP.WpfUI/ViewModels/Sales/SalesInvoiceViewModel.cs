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
using MarcoERP.WpfUI.Views.Common;
using MarcoERP.WpfUI.Common;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    /// <summary>
    /// ViewModel for Sales Invoice management screen.
    /// Supports header+lines pattern with draft/post/cancel lifecycle.
    /// Totals displayed are preview-only; authoritative amounts come from Application service.
    /// </summary>
    public sealed class SalesInvoiceViewModel : BaseViewModel, IInvoiceLineFormHost
    {
        private readonly ISalesInvoiceService _invoiceService;
        private readonly IProductService _productService;
        private readonly IWarehouseService _warehouseService;
        private readonly ICustomerService _customerService;
        private readonly ISupplierService _supplierService;
        private readonly ISalesRepresentativeService _salesRepresentativeService;
        private readonly ISmartEntryQueryService _smartEntryQueryService;
        private readonly ILineCalculationService _lineCalculationService;

        // ── Collections ──────────────────────────────────────────
        public ObservableCollection<SalesInvoiceListDto> Invoices { get; } = new();
        public ObservableCollection<Application.DTOs.Sales.CustomerDto> Customers { get; } = new();
        public ObservableCollection<SupplierDto> Suppliers { get; } = new();
        public ObservableCollection<SalesRepresentativeDto> SalesRepresentatives { get; } = new();
        public ObservableCollection<Application.DTOs.Inventory.WarehouseDto> Warehouses { get; } = new();
        public ObservableCollection<ProductDto> Products { get; } = new();
        public ObservableCollection<SalesInvoiceLineFormItem> FormLines { get; } = new();

        // ── State ───────────────────────────────────────────────
        private SalesInvoiceListDto _selectedItem;
        public SalesInvoiceListDto SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value) && value != null && !IsEditing)
                    _ = LoadInvoiceDetailAsync(value.Id);
            }
        }

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
            set => SetProperty(ref _formDate, value);
        }

        private int? _formCustomerId;
        public int? FormCustomerId
        {
            get => _formCustomerId;
            set { SetProperty(ref _formCustomerId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private CounterpartyType _formCounterpartyType = CounterpartyType.Customer;
        public CounterpartyType FormCounterpartyType
        {
            get => _formCounterpartyType;
            set
            {
                if (SetProperty(ref _formCounterpartyType, value))
                {
                    if (value == CounterpartyType.Customer)
                        FormSupplierId = null;
                    else
                        FormCustomerId = null;

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
            set { SetProperty(ref _formSupplierId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private int? _formSalesRepresentativeId;
        public int? FormSalesRepresentativeId
        {
            get => _formSalesRepresentativeId;
            set => SetProperty(ref _formSalesRepresentativeId, value);
        }

        public static IReadOnlyList<KeyValuePair<CounterpartyType, string>> CounterpartyTypes { get; } = new[]
        {
            new KeyValuePair<CounterpartyType, string>(CounterpartyType.Customer, "عميل"),
            new KeyValuePair<CounterpartyType, string>(CounterpartyType.Supplier, "مورد")
        };

        private int? _formWarehouseId;
        public int? FormWarehouseId
        {
            get => _formWarehouseId;
            set { SetProperty(ref _formWarehouseId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private string _formNotes;
        public string FormNotes
        {
            get => _formNotes;
            set => SetProperty(ref _formNotes, value);
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
                       && (FormCounterpartyType != CounterpartyType.Customer || (FormCustomerId.HasValue && FormCustomerId > 0))
                       && (FormCounterpartyType != CounterpartyType.Supplier || (FormSupplierId.HasValue && FormSupplierId > 0))
                       && FormWarehouseId.HasValue && FormWarehouseId > 0
                       && FormLines.Count > 0
                       && FormLines.All(l => l.ProductId > 0 && l.Quantity > 0 && l.UnitPrice > 0);

        public bool CanPost => CurrentInvoice != null && IsDraft && !IsEditing;
        public bool CanCancel => CurrentInvoice != null && IsPosted && !IsEditing;
        public bool CanDeleteDraft => CurrentInvoice != null && IsDraft && !IsEditing;

        // ── Commands ────────────────────────────────────────────
        public ICommand LoadCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand PostCommand { get; }
        public ICommand CancelInvoiceCommand { get; }
        public ICommand DeleteDraftCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand EditSelectedCommand { get; }
        public ICommand OpenPriceHistoryCommand { get; }

        // ── Constructor ─────────────────────────────────────────
        public SalesInvoiceViewModel(
            ISalesInvoiceService invoiceService,
            IProductService productService,
            IWarehouseService warehouseService,
            ICustomerService customerService,
            ISupplierService supplierService,
            ISalesRepresentativeService salesRepresentativeService,
            ISmartEntryQueryService smartEntryQueryService,
            ILineCalculationService lineCalculationService)
        {
            _invoiceService = invoiceService ?? throw new ArgumentNullException(nameof(invoiceService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _salesRepresentativeService = salesRepresentativeService ?? throw new ArgumentNullException(nameof(salesRepresentativeService));
            _smartEntryQueryService = smartEntryQueryService ?? throw new ArgumentNullException(nameof(smartEntryQueryService));
            _lineCalculationService = lineCalculationService ?? throw new ArgumentNullException(nameof(lineCalculationService));

            LoadCommand = new AsyncRelayCommand(LoadInvoicesAsync);
            NewCommand = new AsyncRelayCommand(PrepareNewAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            PostCommand = new AsyncRelayCommand(PostAsync, () => CanPost);
            CancelInvoiceCommand = new AsyncRelayCommand(CancelInvoiceAsync, () => CanCancel);
            DeleteDraftCommand = new AsyncRelayCommand(DeleteDraftAsync, () => CanDeleteDraft);
            CancelEditCommand = new RelayCommand(CancelEditing);
            AddLineCommand = new RelayCommand(AddLine);
            RemoveLineCommand = new RelayCommand(RemoveLine);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());
            OpenPriceHistoryCommand = new AsyncRelayCommand(OpenPriceHistoryAsync);
        }

        private async Task OpenPriceHistoryAsync(object parameter)
        {
            if (parameter is not SalesInvoiceLineFormItem line || line.ProductId <= 0 || line.UnitId <= 0)
                return;

            var owner = System.Windows.Application.Current?.MainWindow;
            var selectedPrice = await PriceHistoryHelper.ShowAsync(
                _smartEntryQueryService,
                PriceHistorySource.Sales,
                FormCounterpartyType,
                FormCustomerId,
                FormSupplierId,
                line.ProductId,
                line.UnitId,
                owner);

            if (selectedPrice.HasValue)
                line.UnitPrice = selectedPrice.Value;
        }

        // ── Load ────────────────────────────────────────────────
        public async Task LoadInvoicesAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                await LoadLookupsAsync();

                var result = await _invoiceService.GetAllAsync();
                Invoices.Clear();
                if (result.IsSuccess)
                    foreach (var inv in result.Data) Invoices.Add(inv);

                StatusMessage = $"تم تحميل {Invoices.Count} فاتورة بيع";
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
            FormCounterpartyType = CounterpartyType.Customer;
            FormCustomerId = null;
            FormSupplierId = null;
            FormSalesRepresentativeId = null;
            FormWarehouseId = null;
            FormNotes = "";
            FormLines.Clear();
            AddLine(null);
            RefreshTotals();
            StatusMessage = "إنشاء فاتورة بيع جديدة...";
        }

        private void AddLine(object _)
        {
            if (!IsEditing && !IsNew) return;
            FormLines.Add(new SalesInvoiceLineFormItem(this));
            RefreshTotals();
        }

        private void RemoveLine(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is SalesInvoiceLineFormItem line && FormLines.Count > 1)
            {
                FormLines.Remove(line);
                RefreshTotals();
            }
        }

        // ── Save ────────────────────────────────────────────────
        private async Task SaveAsync()
        {
            IsBusy = true;
            ClearError();
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
                        IsEditing = false;
                        IsNew = false;
                        await LoadInvoicesAsync();
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
                        IsEditing = false;
                        IsNew = false;
                        await LoadInvoicesAsync();
                    }
                    else ErrorMessage = result.ErrorMessage;
                }
            }
            catch (ConcurrencyConflictException ex)
            {
                await ConcurrencyHelper.ShowConflictAndRefreshAsync(ex, LoadInvoicesAsync);
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الحفظ", ex); }
            finally { IsBusy = false; }
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
                    await LoadInvoicesAsync();
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الترحيل", ex); }
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
                    await LoadInvoicesAsync();
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الإلغاء", ex); }
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
                    CurrentInvoice = null;
                    ClearForm();
                    await LoadInvoicesAsync();
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الحذف", ex); }
            finally { IsBusy = false; }
        }

        public void EditSelected()
        {
            if (CurrentInvoice == null || !IsDraft) return;
            PopulateFormFromInvoice(CurrentInvoice);
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
                ClearForm();
            StatusMessage = "تم الإلغاء";
        }

        private void PopulateFormFromInvoice(SalesInvoiceDto invoice)
        {
            FormNumber = invoice.InvoiceNumber;
            FormDate = invoice.InvoiceDate;
            FormCounterpartyType = invoice.CounterpartyType;
            FormCustomerId = invoice.CounterpartyType == CounterpartyType.Customer ? invoice.CustomerId : null;
            FormSupplierId = invoice.CounterpartyType == CounterpartyType.Supplier ? invoice.SupplierId : null;
            FormSalesRepresentativeId = invoice.SalesRepresentativeId;
            FormWarehouseId = invoice.WarehouseId;
            FormNotes = invoice.Notes;

            FormLines.Clear();
            foreach (var line in invoice.Lines ?? new List<SalesInvoiceLineDto>())
            {
                FormLines.Add(new SalesInvoiceLineFormItem(this)
                {
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
        }

        private void ClearForm()
        {
            FormNumber = "";
            FormDate = DateTime.Today;
            FormCounterpartyType = CounterpartyType.Customer;
            FormCustomerId = null;
            FormSupplierId = null;
            FormSalesRepresentativeId = null;
            FormWarehouseId = null;
            FormNotes = "";
            FormLines.Clear();
            RefreshTotals();
        }
    }

    /// <summary>
    /// Represents a single line in the sales invoice form.
    /// Computed totals are display previews; authoritative values come from the Application service.
    /// </summary>
    public sealed class SalesInvoiceLineFormItem : BaseViewModel
    {
        private readonly IInvoiceLineFormHost _parent;

        public SalesInvoiceLineFormItem(IInvoiceLineFormHost parent) { _parent = parent; }

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
            set
            {
                if (SetProperty(ref _unitId, value))
                {
                    OnUnitChanged();
                    _parent?.RefreshTotals();
                    OnPropertyChanged(nameof(IsSellingBelowCost));
                }
            }
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
            set
            {
                if (SetProperty(ref _unitPrice, value))
                {
                    RecalcTotals();
                    _parent?.RefreshTotals();
                    OnPropertyChanged(nameof(IsPriceDifferentFromLastSale));
                    OnPropertyChanged(nameof(IsSellingBelowCost));
                }
            }
        }

        private decimal _discountPercent;
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (SetProperty(ref _discountPercent, value))
                {
                    RecalcTotals();
                    _parent?.RefreshTotals();
                    OnPropertyChanged(nameof(IsSellingBelowCost));
                }
            }
        }

        public ObservableCollection<ProductUnitDto> AvailableUnits { get; } = new();

        // ── Smart Entry (read-only helpers) ───────────────────
        private decimal? _smartStockBaseQty;
        public decimal? SmartStockBaseQty
        {
            get => _smartStockBaseQty;
            private set
            {
                if (SetProperty(ref _smartStockBaseQty, value))
                    OnPropertyChanged(nameof(SmartStockQty));
            }
        }

        public decimal? SmartStockQty
        {
            get
            {
                if (!SmartStockBaseQty.HasValue)
                    return null;

                var factor = SelectedUnitConversionFactor;
                if (factor <= 0)
                    return SmartStockBaseQty;

                return _parent.ConvertPrice(SmartStockBaseQty.Value, factor);
            }
        }

        private decimal? _smartLastSaleUnitPrice;
        public decimal? SmartLastSaleUnitPrice
        {
            get => _smartLastSaleUnitPrice;
            private set
            {
                if (SetProperty(ref _smartLastSaleUnitPrice, value))
                    OnPropertyChanged(nameof(IsPriceDifferentFromLastSale));
            }
        }

        private decimal? _smartLastPurchaseUnitPrice;
        public decimal? SmartLastPurchaseUnitPrice
        {
            get => _smartLastPurchaseUnitPrice;
            private set => SetProperty(ref _smartLastPurchaseUnitPrice, value);
        }

        private decimal _smartAverageCost;
        public decimal SmartAverageCost
        {
            get => _smartAverageCost;
            private set
            {
                if (SetProperty(ref _smartAverageCost, value))
                    OnPropertyChanged(nameof(IsSellingBelowCost));
            }
        }

        public decimal SmartNetUnitPrice
        {
            get
            {
                // Delegate to service via calculation result
                var result = _parent.CalculateLine(new LineCalculationRequest
                {
                    Quantity = 1,
                    UnitPrice = UnitPrice,
                    DiscountPercent = DiscountPercent,
                    VatRate = 0,
                    ConversionFactor = SelectedUnitConversionFactor,
                    CostPrice = SmartAverageCost
                });
                return result.NetUnitPrice;
            }
        }

        public decimal SmartCostPerSelectedUnit
        {
            get
            {
                var factor = SelectedUnitConversionFactor;
                if (factor <= 0) factor = 1m;
                return _parent.ConvertQuantity(SmartAverageCost, factor);
            }
        }

        public bool IsSellingBelowCost
        {
            get
            {
                if (ProductId <= 0)
                    return false;

                if (SmartAverageCost <= 0)
                    return false;

                return SmartNetUnitPrice + 0.0001m < SmartCostPerSelectedUnit;
            }
        }

        public bool IsPriceDifferentFromLastSale
        {
            get
            {
                if (!SmartLastSaleUnitPrice.HasValue)
                    return false;

                return Math.Abs(UnitPrice - SmartLastSaleUnitPrice.Value) > 0.0001m;
            }
        }

        private decimal SelectedUnitConversionFactor
        {
            get
            {
                var unit = AvailableUnits.FirstOrDefault(u => u.UnitId == UnitId);
                return unit?.ConversionFactor ?? 1m;
            }
        }

        public void SetSmartEntry(decimal stockBaseQty, decimal? lastSaleUnitPrice, decimal? lastPurchaseUnitPrice)
        {
            SmartStockBaseQty = stockBaseQty;
            SmartLastSaleUnitPrice = lastSaleUnitPrice;
            SmartLastPurchaseUnitPrice = lastPurchaseUnitPrice;
        }

        public bool IsUnitPriceAtMasterDefault()
        {
            var unit = AvailableUnits.FirstOrDefault(u => u.UnitId == UnitId);
            if (unit == null)
                return false;

            return Math.Abs(UnitPrice - unit.SalePrice) <= 0.0001m;
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
            SmartAverageCost = product.WeightedAverageCost;

            SmartStockBaseQty = null;
            SmartLastSaleUnitPrice = null;
            SmartLastPurchaseUnitPrice = null;
            foreach (var unit in product.Units) AvailableUnits.Add(unit);
            var defaultUnit = product.Units.FirstOrDefault(u => u.IsDefault) ?? product.Units.FirstOrDefault();
            if (defaultUnit != null)
            {
                UnitId = defaultUnit.UnitId;
                UnitPrice = defaultUnit.SalePrice; // Sales use SalePrice
            }
        }

        private void OnUnitChanged()
        {
            if (_parent == null || UnitId <= 0) return;
            var unit = AvailableUnits.FirstOrDefault(u => u.UnitId == UnitId);
            if (unit != null) UnitPrice = unit.SalePrice;
            OnPropertyChanged(nameof(SmartStockQty));
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
