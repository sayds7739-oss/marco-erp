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
using MarcoERP.WpfUI.Common;

namespace MarcoERP.WpfUI.ViewModels.Purchases
{
    /// <summary>
    /// ViewModel for Purchase Invoice management screen.
    /// Supports header+lines pattern with draft/post/cancel lifecycle.
    /// Totals displayed in ViewModel are preview-only; authoritative amounts come from Application service.
    /// </summary>
    public sealed class PurchaseInvoiceViewModel : BaseViewModel, IInvoiceLineFormHost
    {
        private readonly IPurchaseInvoiceService _invoiceService;
        private readonly IProductService _productService;
        private readonly IWarehouseService _warehouseService;
        private readonly ISupplierService _supplierService;
        private readonly ICustomerService _customerService;
        private readonly ISalesRepresentativeService _salesRepresentativeService;
        private readonly ISmartEntryQueryService _smartEntryQueryService;
        private readonly ILineCalculationService _lineCalculationService;

        // ── Collections ──────────────────────────────────────────
        public ObservableCollection<PurchaseInvoiceListDto> Invoices { get; } = new();
        public ObservableCollection<SupplierDto> Suppliers { get; } = new();
        public ObservableCollection<CustomerDto> Customers { get; } = new();
        public ObservableCollection<SalesRepresentativeDto> SalesRepresentatives { get; } = new();
        public ObservableCollection<Application.DTOs.Inventory.WarehouseDto> Warehouses { get; } = new();
        public ObservableCollection<ProductDto> Products { get; } = new();
        public ObservableCollection<PurchaseInvoiceLineFormItem> FormLines { get; } = new();

        // ── State ───────────────────────────────────────────────
        private PurchaseInvoiceListDto _selectedItem;
        public PurchaseInvoiceListDto SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value) && value != null && !IsEditing)
                    _ = LoadInvoiceDetailAsync(value.Id);
            }
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

        private int? _formSupplierId;
        public int? FormSupplierId
        {
            get => _formSupplierId;
            set { SetProperty(ref _formSupplierId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private CounterpartyType _formCounterpartyType = CounterpartyType.Supplier;
        public CounterpartyType FormCounterpartyType
        {
            get => _formCounterpartyType;
            set
            {
                if (SetProperty(ref _formCounterpartyType, value))
                {
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
            set { SetProperty(ref _formCounterpartyCustomerId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private int? _formWarehouseId;
        public int? FormWarehouseId
        {
            get => _formWarehouseId;
            set { SetProperty(ref _formWarehouseId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private int? _formSalesRepresentativeId;
        public int? FormSalesRepresentativeId
        {
            get => _formSalesRepresentativeId;
            set => SetProperty(ref _formSalesRepresentativeId, value);
        }

        private string _formNotes;
        public string FormNotes
        {
            get => _formNotes;
            set => SetProperty(ref _formNotes, value);
        }

        // ── Totals (preview, computed from lines) ───────────────
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

        /// <summary>Refreshes all total properties for UI binding.</summary>
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
                       && FormWarehouseId.HasValue && FormWarehouseId > 0
                       && FormLines.Count > 0
                       && (FormCounterpartyType != CounterpartyType.Supplier || (FormSupplierId.HasValue && FormSupplierId > 0))
                       && (FormCounterpartyType != CounterpartyType.Customer || (FormCounterpartyCustomerId.HasValue && FormCounterpartyCustomerId > 0))
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
        public PurchaseInvoiceViewModel(
            IPurchaseInvoiceService invoiceService,
            IProductService productService,
            IWarehouseService warehouseService,
            Application.Interfaces.Purchases.ISupplierService supplierService,
            ICustomerService customerService,
            ISalesRepresentativeService salesRepresentativeService,
            ISmartEntryQueryService smartEntryQueryService,
            ILineCalculationService lineCalculationService)
        {
            _invoiceService = invoiceService ?? throw new ArgumentNullException(nameof(invoiceService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
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
            if (parameter is not PurchaseInvoiceLineFormItem line || line.ProductId <= 0 || line.UnitId <= 0)
                return;

            var owner = System.Windows.Application.Current?.MainWindow;
            var selectedPrice = await PriceHistoryHelper.ShowAsync(
                _smartEntryQueryService,
                PriceHistorySource.Purchase,
                FormCounterpartyType,
                FormCounterpartyCustomerId,
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
                // Load lookup data
                await LoadLookupsAsync();

                // Load invoices list
                var result = await _invoiceService.GetAllAsync();
                Invoices.Clear();
                if (result.IsSuccess)
                    foreach (var inv in result.Data) Invoices.Add(inv);

                StatusMessage = $"تم تحميل {Invoices.Count} فاتورة شراء";
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
            // Suppliers
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

            // Warehouses
            var whResult = await _warehouseService.GetAllAsync();
            Warehouses.Clear();
            if (whResult.IsSuccess)
                foreach (var w in whResult.Data.Where(x => x.IsActive))
                    Warehouses.Add(w);

            // Products
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
            catch
            {
                FormNumber = "";
            }

            FormDate = DateTime.Today;
            FormCounterpartyType = CounterpartyType.Supplier;
            FormSupplierId = null;
            FormCounterpartyCustomerId = null;
            FormSalesRepresentativeId = null;
            FormWarehouseId = null;
            FormNotes = "";
            FormLines.Clear();
            AddLine(null);
            RefreshTotals();

            StatusMessage = "إنشاء فاتورة شراء جديدة...";
        }

        // ── Add / Remove Lines ──────────────────────────────────
        private void AddLine(object _)
        {
            if (!IsEditing && !IsNew) return;
            FormLines.Add(new PurchaseInvoiceLineFormItem(this));
            RefreshTotals();
        }

        private void RemoveLine(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is PurchaseInvoiceLineFormItem line && FormLines.Count > 1)
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
                var lines = FormLines.Select(l => new CreatePurchaseInvoiceLineDto
                {
                    ProductId = l.ProductId,
                    UnitId = l.UnitId,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    DiscountPercent = l.DiscountPercent
                }).ToList();

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
                        IsEditing = false;
                        IsNew = false;
                        await LoadInvoicesAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
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
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث فاتورة الشراء «{result.Data.InvoiceNumber}» بنجاح";
                        IsEditing = false;
                        IsNew = false;
                        await LoadInvoicesAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
            }
            catch (ConcurrencyConflictException ex)
            {
                await ConcurrencyHelper.ShowConflictAndRefreshAsync(ex, LoadInvoicesAsync);
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

        // ── Post ────────────────────────────────────────────────
        private async Task PostAsync()
        {
            if (CurrentInvoice == null) return;

            var confirm = MessageBox.Show(
                $"هل تريد ترحيل فاتورة الشراء «{CurrentInvoice.InvoiceNumber}»؟\nبعد الترحيل لا يمكن التعديل وسيتم إنشاء قيد محاسبي تلقائي.",
                "تأكيد الترحيل",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _invoiceService.PostAsync(CurrentInvoice.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم ترحيل فاتورة الشراء «{result.Data.InvoiceNumber}» — قيد رقم: {result.Data.JournalEntryId}";
                    await LoadInvoicesAsync();
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

        // ── Cancel Invoice ──────────────────────────────────────
        private async Task CancelInvoiceAsync()
        {
            if (CurrentInvoice == null) return;

            var confirm = MessageBox.Show(
                $"هل تريد إلغاء فاتورة الشراء «{CurrentInvoice.InvoiceNumber}»؟\nسيتم إنشاء قيد عكسي وإعادة الكميات.",
                "تأكيد الإلغاء",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
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

        // ── Delete Draft ────────────────────────────────────────
        private async Task DeleteDraftAsync()
        {
            if (CurrentInvoice == null) return;

            var confirm = MessageBox.Show(
                $"هل تريد حذف مسودة الفاتورة «{CurrentInvoice.InvoiceNumber}»؟",
                "تأكيد الحذف",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
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

        // ── Edit Selected ───────────────────────────────────────
        public void EditSelected()
        {
            if (CurrentInvoice == null || !IsDraft) return;
            PopulateFormFromInvoice(CurrentInvoice);
            IsEditing = true;
            IsNew = false;
        }

        // ── Cancel Editing ──────────────────────────────────────
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

        // ── Helpers ─────────────────────────────────────────────
        private void PopulateFormFromInvoice(PurchaseInvoiceDto invoice)
        {
            FormNumber = invoice.InvoiceNumber;
            FormDate = invoice.InvoiceDate;
            FormCounterpartyType = invoice.CounterpartyType;
            FormSupplierId = invoice.SupplierId;
            FormCounterpartyCustomerId = invoice.CounterpartyCustomerId;
            FormSalesRepresentativeId = invoice.SalesRepresentativeId;
            FormWarehouseId = invoice.WarehouseId;
            FormNotes = invoice.Notes;

            FormLines.Clear();
            foreach (var line in invoice.Lines ?? new List<PurchaseInvoiceLineDto>())
            {
                FormLines.Add(new PurchaseInvoiceLineFormItem(this)
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
            FormCounterpartyType = CounterpartyType.Supplier;
            FormSupplierId = null;
            FormCounterpartyCustomerId = null;
            FormSalesRepresentativeId = null;
            FormWarehouseId = null;
            FormNotes = "";
            FormLines.Clear();
            RefreshTotals();
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Line form item for purchase invoice
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Represents a single line in the purchase invoice form.
    /// Computed totals are display previews; authoritative values come from the Application service.
    /// </summary>
    public sealed class PurchaseInvoiceLineFormItem : BaseViewModel
    {
        private readonly IInvoiceLineFormHost _parent;

        public PurchaseInvoiceLineFormItem(IInvoiceLineFormHost parent)
        {
            _parent = parent;
        }

        private int _productId;
        public int ProductId
        {
            get => _productId;
            set
            {
                if (SetProperty(ref _productId, value))
                {
                    OnProductChanged();
                    _parent?.RefreshTotals();
                }
            }
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
                }
            }
        }

        private decimal _quantity;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                {
                    RecalcTotals();
                    _parent?.RefreshTotals();
                }
            }
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
                    OnPropertyChanged(nameof(IsPriceDifferentFromLastPurchase));
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
                }
            }
        }

        // ── Available units for the selected product ────────────
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

        private decimal? _smartLastPurchaseUnitPrice;
        public decimal? SmartLastPurchaseUnitPrice
        {
            get => _smartLastPurchaseUnitPrice;
            private set
            {
                if (SetProperty(ref _smartLastPurchaseUnitPrice, value))
                    OnPropertyChanged(nameof(IsPriceDifferentFromLastPurchase));
            }
        }

        private decimal _smartAverageCost;
        public decimal SmartAverageCost
        {
            get => _smartAverageCost;
            private set => SetProperty(ref _smartAverageCost, value);
        }

        public bool IsPriceDifferentFromLastPurchase
        {
            get
            {
                if (!SmartLastPurchaseUnitPrice.HasValue)
                    return false;

                return Math.Abs(UnitPrice - SmartLastPurchaseUnitPrice.Value) > 0.0001m;
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

        public void SetSmartEntry(decimal stockBaseQty, decimal? lastPurchaseUnitPrice)
        {
            SmartStockBaseQty = stockBaseQty;
            SmartLastPurchaseUnitPrice = lastPurchaseUnitPrice;
        }

        public bool IsUnitPriceAtMasterDefault()
        {
            var unit = AvailableUnits.FirstOrDefault(u => u.UnitId == UnitId);
            if (unit == null)
                return false;

            return Math.Abs(UnitPrice - unit.PurchasePrice) <= 0.0001m;
        }

        // ── Computed preview fields (display only) ──────────────
        private decimal _subTotal;
        public decimal SubTotal
        {
            get => _subTotal;
            private set => SetProperty(ref _subTotal, value);
        }

        private decimal _discountAmount;
        public decimal DiscountAmount
        {
            get => _discountAmount;
            private set => SetProperty(ref _discountAmount, value);
        }

        private decimal _vatRate;
        public decimal VatRate
        {
            get => _vatRate;
            private set => SetProperty(ref _vatRate, value);
        }

        private decimal _vatAmount;
        public decimal VatAmount
        {
            get => _vatAmount;
            private set => SetProperty(ref _vatAmount, value);
        }

        private decimal _totalWithVat;
        public decimal TotalWithVat
        {
            get => _totalWithVat;
            private set => SetProperty(ref _totalWithVat, value);
        }

        private string _productName;
        public string ProductName
        {
            get => _productName;
            private set => SetProperty(ref _productName, value);
        }

        // ── Product changed: populate available units ───────────
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
            SmartLastPurchaseUnitPrice = null;

            foreach (var unit in product.Units)
                AvailableUnits.Add(unit);

            // Auto-select default unit
            var defaultUnit = product.Units.FirstOrDefault(u => u.IsDefault)
                              ?? product.Units.FirstOrDefault();
            if (defaultUnit != null)
            {
                UnitId = defaultUnit.UnitId;
                UnitPrice = defaultUnit.PurchasePrice;
            }
        }

        // ── Unit changed: update price ──────────────────────────
        private void OnUnitChanged()
        {
            if (_parent == null || UnitId <= 0) return;
            var unit = AvailableUnits.FirstOrDefault(u => u.UnitId == UnitId);
            if (unit != null)
                UnitPrice = unit.PurchasePrice;
            OnPropertyChanged(nameof(SmartStockQty));
        }

        // ── Recalculate preview totals ──────────────────────────
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
