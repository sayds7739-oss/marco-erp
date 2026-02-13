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

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    /// <summary>
    /// ViewModel for Sales Return management screen.
    /// Supports header+lines with optional original invoice reference.
    /// </summary>
    public sealed class SalesReturnViewModel : BaseViewModel, IInvoiceLineFormHost
    {
        private readonly ISalesReturnService _returnService;
        private readonly ISalesInvoiceService _invoiceService;
        private readonly IProductService _productService;
        private readonly IWarehouseService _warehouseService;
        private readonly ICustomerService _customerService;
        private readonly ISupplierService _supplierService;
        private readonly ISalesRepresentativeService _salesRepresentativeService;
        private readonly ISmartEntryQueryService _smartEntryQueryService;
        private readonly ILineCalculationService _lineCalculationService;

        public ObservableCollection<SalesReturnListDto> Returns { get; } = new();
        public ObservableCollection<CustomerDto> Customers { get; } = new();
        public ObservableCollection<SupplierDto> Suppliers { get; } = new();
        public ObservableCollection<SalesRepresentativeDto> SalesRepresentatives { get; } = new();
        public ObservableCollection<WarehouseDto> Warehouses { get; } = new();
        public ObservableCollection<ProductDto> Products { get; } = new();
        public ObservableCollection<SalesInvoiceListDto> PostedInvoices { get; } = new();
        public ObservableCollection<SalesReturnLineFormItem> FormLines { get; } = new();

        private SalesReturnListDto _selectedItem;
        public SalesReturnListDto SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value) && value != null && !IsEditing)
                    _ = LoadReturnDetailAsync(value.Id);
            }
        }

        private SalesReturnDto _currentReturn;
        public SalesReturnDto CurrentReturn
        {
            get => _currentReturn;
            set
            {
                SetProperty(ref _currentReturn, value);
                OnPropertyChanged(nameof(IsPosted)); OnPropertyChanged(nameof(IsCancelled));
                OnPropertyChanged(nameof(IsDraft)); OnPropertyChanged(nameof(CanPost));
                OnPropertyChanged(nameof(CanCancel)); OnPropertyChanged(nameof(CanDeleteDraft));
            }
        }

        private bool _isEditing;
        public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }

        private bool _isNew;
        public bool IsNew { get => _isNew; set => SetProperty(ref _isNew, value); }

        public bool IsDraft => CurrentReturn != null && CurrentReturn.Status == "Draft";
        public bool IsPosted => CurrentReturn != null && CurrentReturn.Status == "Posted";
        public bool IsCancelled => CurrentReturn != null && CurrentReturn.Status == "Cancelled";

        private Dictionary<string, int> _returnNumberToId = new(StringComparer.OrdinalIgnoreCase);

        private string _jumpReturnNumber;
        public string JumpReturnNumber
        {
            get => _jumpReturnNumber;
            set => SetProperty(ref _jumpReturnNumber, value);
        }

        // ── Form Fields ─────────────────────────────────────────
        private string _formNumber;
        public string FormNumber { get => _formNumber; set => SetProperty(ref _formNumber, value); }

        private DateTime _formDate = DateTime.Today;
        public DateTime FormDate { get => _formDate; set => SetProperty(ref _formDate, value); }

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

        public static IReadOnlyList<KeyValuePair<CounterpartyType, string>> CounterpartyTypes { get; } = new[]
        {
            new KeyValuePair<CounterpartyType, string>(CounterpartyType.Customer, "عميل"),
            new KeyValuePair<CounterpartyType, string>(CounterpartyType.Supplier, "مورد")
        };

        private int? _formSupplierId;
        public int? FormSupplierId
        {
            get => _formSupplierId;
            set { SetProperty(ref _formSupplierId, value); OnPropertyChanged(nameof(CanSave)); }
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

        private int? _formOriginalInvoiceId;
        public int? FormOriginalInvoiceId
        {
            get => _formOriginalInvoiceId;
            set => SetProperty(ref _formOriginalInvoiceId, value);
        }

        private string _formNotes;
        public string FormNotes { get => _formNotes; set => SetProperty(ref _formNotes, value); }

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

        public bool CanSave => IsEditing && FormWarehouseId > 0
                       && FormLines.Count > 0
                       && (FormCounterpartyType != CounterpartyType.Customer || (FormCustomerId.HasValue && FormCustomerId > 0))
                       && (FormCounterpartyType != CounterpartyType.Supplier || (FormSupplierId.HasValue && FormSupplierId > 0))
                       && FormLines.All(l => l.ProductId > 0 && l.Quantity > 0 && l.UnitPrice > 0);
        public bool CanPost => CurrentReturn != null && IsDraft && !IsEditing;
        public bool CanCancel => CurrentReturn != null && IsPosted && !IsEditing;
        public bool CanDeleteDraft => CurrentReturn != null && IsDraft && !IsEditing;

        public ICommand LoadCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand PostCommand { get; }
        public ICommand CancelReturnCommand { get; }
        public ICommand DeleteDraftCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand EditSelectedCommand { get; }
        public ICommand JumpToReturnCommand { get; }
        public ICommand OpenPriceHistoryCommand { get; }

        public SalesReturnViewModel(
            ISalesReturnService returnService,
            ISalesInvoiceService invoiceService,
            IProductService productService,
            IWarehouseService warehouseService,
            ICustomerService customerService,
            ISupplierService supplierService,
            ISalesRepresentativeService salesRepresentativeService,
            ISmartEntryQueryService smartEntryQueryService,
            ILineCalculationService lineCalculationService)
        {
            _returnService = returnService ?? throw new ArgumentNullException(nameof(returnService));
            _invoiceService = invoiceService ?? throw new ArgumentNullException(nameof(invoiceService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _salesRepresentativeService = salesRepresentativeService ?? throw new ArgumentNullException(nameof(salesRepresentativeService));
            _smartEntryQueryService = smartEntryQueryService ?? throw new ArgumentNullException(nameof(smartEntryQueryService));
            _lineCalculationService = lineCalculationService ?? throw new ArgumentNullException(nameof(lineCalculationService));

            LoadCommand = new AsyncRelayCommand(LoadReturnsAsync);
            NewCommand = new AsyncRelayCommand(PrepareNewAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            PostCommand = new AsyncRelayCommand(PostAsync, () => CanPost);
            CancelReturnCommand = new AsyncRelayCommand(CancelReturnAsync, () => CanCancel);
            DeleteDraftCommand = new AsyncRelayCommand(DeleteDraftAsync, () => CanDeleteDraft);
            CancelEditCommand = new RelayCommand(CancelEditing);
            AddLineCommand = new RelayCommand(AddLine);
            RemoveLineCommand = new RelayCommand(RemoveLine);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());
            JumpToReturnCommand = new AsyncRelayCommand(JumpToReturnAsync);
            OpenPriceHistoryCommand = new AsyncRelayCommand(OpenPriceHistoryAsync);
        }

        private async Task OpenPriceHistoryAsync(object parameter)
        {
            if (parameter is not SalesReturnLineFormItem line || line.ProductId <= 0 || line.UnitId <= 0)
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

        public async Task LoadReturnsAsync()
        {
            IsBusy = true; ClearError();
            try
            {
                await LoadLookupsAsync();
                var result = await _returnService.GetAllAsync();
                Returns.Clear();
                _returnNumberToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (result.IsSuccess)
                {
                    var list = result.Data.ToList();
                    foreach (var r in list) Returns.Add(r);
                    _returnNumberToId = list
                        .Where(r => !string.IsNullOrWhiteSpace(r.ReturnNumber))
                        .GroupBy(r => r.ReturnNumber)
                        .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
                }
                StatusMessage = $"تم تحميل {Returns.Count} مرتجع بيع";
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("التحميل", ex); }
            finally { IsBusy = false; }
        }

        private async Task LoadLookupsAsync()
        {
            var custResult = await _customerService.GetAllAsync();
            Customers.Clear();
            if (custResult.IsSuccess)
                foreach (var c in custResult.Data.Where(x => x.IsActive)) Customers.Add(c);

            var suppResult = await _supplierService.GetAllAsync();
            Suppliers.Clear();
            if (suppResult.IsSuccess)
                foreach (var s in suppResult.Data.Where(x => x.IsActive)) Suppliers.Add(s);

            var repResult = await _salesRepresentativeService.GetActiveAsync();
            SalesRepresentatives.Clear();
            if (repResult.IsSuccess)
                foreach (var rep in repResult.Data)
                    SalesRepresentatives.Add(rep);

            var whResult = await _warehouseService.GetAllAsync();
            Warehouses.Clear();
            if (whResult.IsSuccess)
                foreach (var w in whResult.Data.Where(x => x.IsActive)) Warehouses.Add(w);

            var prodResult = await _productService.GetAllAsync();
            Products.Clear();
            if (prodResult.IsSuccess)
                foreach (var p in prodResult.Data.Where(x => x.Status == "Active")) Products.Add(p);

            var invResult = await _invoiceService.GetAllAsync();
            PostedInvoices.Clear();
            if (invResult.IsSuccess)
                foreach (var inv in invResult.Data.Where(x => x.Status == "Posted")) PostedInvoices.Add(inv);
        }

        private async Task LoadReturnDetailAsync(int id)
        {
            IsBusy = true; ClearError();
            try
            {
                var result = await _returnService.GetByIdAsync(id);
                if (result.IsSuccess)
                {
                    CurrentReturn = result.Data;
                    PopulateForm(result.Data);
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("تحميل التفاصيل", ex); }
            finally { IsBusy = false; }
        }

        private async Task PrepareNewAsync()
        {
            IsEditing = true; IsNew = true; CurrentReturn = null; ClearError();
            try
            {
                var numResult = await _returnService.GetNextNumberAsync();
                FormNumber = numResult.IsSuccess ? numResult.Data : "";
            }
            catch { FormNumber = ""; }

            FormDate = DateTime.Today;
            FormCounterpartyType = CounterpartyType.Customer;
            FormCustomerId = null;
            FormSupplierId = null;
            FormSalesRepresentativeId = null;
            FormWarehouseId = null;
            FormOriginalInvoiceId = null; FormNotes = "";
            FormLines.Clear(); AddLine(null); RefreshTotals();
            StatusMessage = "إنشاء مرتجع بيع جديد...";
        }

        private void AddLine(object _)
        {
            if (!IsEditing && !IsNew) return;
            FormLines.Add(new SalesReturnLineFormItem(this));
            RefreshTotals();
        }

        private void RemoveLine(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is SalesReturnLineFormItem line && FormLines.Count > 1)
            { FormLines.Remove(line); RefreshTotals(); }
        }

        private async Task SaveAsync()
        {
            IsBusy = true; ClearError();
            try
            {
                var lines = FormLines.Select(l => new CreateSalesReturnLineDto
                {
                    ProductId = l.ProductId, UnitId = l.UnitId, Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice, DiscountPercent = l.DiscountPercent
                }).ToList();

                if (IsNew)
                {
                    var dto = new CreateSalesReturnDto
                    {
                        ReturnDate = FormDate,
                        CustomerId = FormCustomerId,
                        CounterpartyType = FormCounterpartyType,
                        SupplierId = FormSupplierId,
                        SalesRepresentativeId = FormSalesRepresentativeId,
                        WarehouseId = FormWarehouseId ?? 0,
                        OriginalInvoiceId = FormOriginalInvoiceId,
                        Notes = FormNotes?.Trim(),
                        Lines = lines
                    };
                    var result = await _returnService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء مرتجع البيع «{result.Data.ReturnNumber}» بنجاح";
                        IsEditing = false; IsNew = false; await LoadReturnsAsync();
                    }
                    else ErrorMessage = result.ErrorMessage;
                }
                else
                {
                    var dto = new UpdateSalesReturnDto
                    {
                        Id = CurrentReturn.Id,
                        ReturnDate = FormDate,
                        CustomerId = FormCustomerId,
                        CounterpartyType = FormCounterpartyType,
                        SupplierId = FormSupplierId,
                        SalesRepresentativeId = FormSalesRepresentativeId,
                        WarehouseId = FormWarehouseId ?? 0,
                        OriginalInvoiceId = FormOriginalInvoiceId,
                        Notes = FormNotes?.Trim(),
                        Lines = lines
                    };
                    var result = await _returnService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث مرتجع البيع «{result.Data.ReturnNumber}» بنجاح";
                        IsEditing = false; IsNew = false; await LoadReturnsAsync();
                    }
                    else ErrorMessage = result.ErrorMessage;
                }
            }
            catch (ConcurrencyConflictException ex)
            {
                await ConcurrencyHelper.ShowConflictAndRefreshAsync(ex, LoadReturnsAsync);
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الحفظ", ex); }
            finally { IsBusy = false; }
        }

        private async Task PostAsync()
        {
            if (CurrentReturn == null) return;
            var confirm = MessageBox.Show(
                $"هل تريد ترحيل مرتجع البيع «{CurrentReturn.ReturnNumber}»؟\nبعد الترحيل لا يمكن التعديل.",
                "تأكيد الترحيل", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;
            IsBusy = true; ClearError();
            try
            {
                var result = await _returnService.PostAsync(CurrentReturn.Id);
                if (result.IsSuccess)
                { StatusMessage = $"تم ترحيل مرتجع البيع «{result.Data.ReturnNumber}» بنجاح"; await LoadReturnsAsync(); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الترحيل", ex); }
            finally { IsBusy = false; }
        }

        private async Task CancelReturnAsync()
        {
            if (CurrentReturn == null) return;
            var confirm = MessageBox.Show(
                $"هل تريد إلغاء مرتجع البيع «{CurrentReturn.ReturnNumber}»؟",
                "تأكيد الإلغاء", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;
            IsBusy = true; ClearError();
            try
            {
                var result = await _returnService.CancelAsync(CurrentReturn.Id);
                if (result.IsSuccess) { StatusMessage = "تم إلغاء المرتجع بنجاح"; await LoadReturnsAsync(); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الإلغاء", ex); }
            finally { IsBusy = false; }
        }

        private async Task DeleteDraftAsync()
        {
            if (CurrentReturn == null) return;
            var confirm = MessageBox.Show(
                $"هل تريد حذف مسودة المرتجع «{CurrentReturn.ReturnNumber}»؟",
                "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;
            IsBusy = true; ClearError();
            try
            {
                var result = await _returnService.DeleteDraftAsync(CurrentReturn.Id);
                if (result.IsSuccess)
                { StatusMessage = "تم حذف المسودة بنجاح"; CurrentReturn = null; ClearForm(); await LoadReturnsAsync(); }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الحذف", ex); }
            finally { IsBusy = false; }
        }

        public void EditSelected()
        {
            if (CurrentReturn == null || !IsDraft) return;
            PopulateForm(CurrentReturn); IsEditing = true; IsNew = false;
        }

        private async Task JumpToReturnAsync()
        {
            if (IsEditing)
            {
                MessageBox.Show("يرجى إنهاء التعديل قبل التنقل.", "تنقل المرتجعات", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(JumpReturnNumber))
                return;

            if (_returnNumberToId.Count == 0)
                await LoadReturnsAsync();

            if (!_returnNumberToId.TryGetValue(JumpReturnNumber.Trim(), out var id))
            {
                MessageBox.Show("رقم المرتجع غير موجود.", "تنقل المرتجعات", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var item = Returns.FirstOrDefault(r => r.Id == id);
            if (item != null)
                SelectedItem = item;
            else
                await LoadReturnDetailAsync(id);
        }

        private void CancelEditing(object _)
        {
            IsEditing = false; IsNew = false; ClearError();
            if (CurrentReturn != null) PopulateForm(CurrentReturn); else ClearForm();
            StatusMessage = "تم الإلغاء";
        }

        private void PopulateForm(SalesReturnDto ret)
        {
            FormNumber = ret.ReturnNumber; FormDate = ret.ReturnDate;
            FormCounterpartyType = ret.CounterpartyType;
            FormCustomerId = ret.CustomerId;
            FormSupplierId = ret.SupplierId;
            FormSalesRepresentativeId = ret.SalesRepresentativeId;
            FormWarehouseId = ret.WarehouseId;
            FormOriginalInvoiceId = ret.OriginalInvoiceId; FormNotes = ret.Notes;
            FormLines.Clear();
            foreach (var line in ret.Lines ?? new List<SalesReturnLineDto>())
            {
                FormLines.Add(new SalesReturnLineFormItem(this)
                {
                    ProductId = line.ProductId, UnitId = line.UnitId,
                    Quantity = line.Quantity, UnitPrice = line.UnitPrice, DiscountPercent = line.DiscountPercent
                });
            }
            IsEditing = false; IsNew = false; RefreshTotals();
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
            FormOriginalInvoiceId = null;
            FormNotes = "";
            FormLines.Clear(); RefreshTotals();
        }
    }

    /// <summary>Line form item for sales return.</summary>
    public sealed class SalesReturnLineFormItem : BaseViewModel
    {
        private readonly IInvoiceLineFormHost _parent;
        public SalesReturnLineFormItem(IInvoiceLineFormHost parent) { _parent = parent; }

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

        private string _productName;
        public string ProductName { get => _productName; private set => SetProperty(ref _productName, value); }

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

        private void OnProductChanged()
        {
            AvailableUnits.Clear();
            if (_parent == null || ProductId <= 0) return;
            var product = _parent.Products.FirstOrDefault(p => p.Id == ProductId);
            if (product == null) return;
            ProductName = product.NameAr;
            VatRate = product.VatRate;
            foreach (var unit in product.Units) AvailableUnits.Add(unit);
            var defUnit = product.Units.FirstOrDefault(u => u.IsDefault) ?? product.Units.FirstOrDefault();
            if (defUnit != null) { UnitId = defUnit.UnitId; UnitPrice = defUnit.SalePrice; }
        }

        private void OnUnitChanged()
        {
            if (UnitId <= 0) return;
            var unit = AvailableUnits.FirstOrDefault(u => u.UnitId == UnitId);
            if (unit != null) UnitPrice = unit.SalePrice;
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
