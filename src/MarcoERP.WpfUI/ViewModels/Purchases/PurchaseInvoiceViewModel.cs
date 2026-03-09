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
    public sealed partial class PurchaseInvoiceViewModel : BaseViewModel, IInvoiceLineFormHost
    {
        private readonly IPurchaseInvoiceService _invoiceService;
        private readonly IProductService _productService;
        private readonly IWarehouseService _warehouseService;
        private readonly ISupplierService _supplierService;
        private readonly ICustomerService _customerService;
        private readonly ISalesRepresentativeService _salesRepresentativeService;
        private readonly ISmartEntryQueryService _smartEntryQueryService;
        private readonly ILineCalculationService _lineCalculationService;
        private readonly IDialogService _dialog;

        // â”€â”€ Collections â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public ObservableCollection<PurchaseInvoiceListDto> Invoices { get; } = new();
        public ObservableCollection<SupplierDto> Suppliers { get; } = new();
        public ObservableCollection<CustomerDto> Customers { get; } = new();
        public ObservableCollection<SalesRepresentativeDto> SalesRepresentatives { get; } = new();
        public ObservableCollection<Application.DTOs.Inventory.WarehouseDto> Warehouses { get; } = new();
        public ObservableCollection<ProductDto> Products { get; } = new();
        public ObservableCollection<PurchaseInvoiceLineFormItem> FormLines { get; } = new();

        // â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // â”€â”€ Status Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public bool IsDraft => CurrentInvoice != null && CurrentInvoice.Status == "Draft";
        public bool IsPosted => CurrentInvoice != null && CurrentInvoice.Status == "Posted";
        public bool IsCancelled => CurrentInvoice != null && CurrentInvoice.Status == "Cancelled";

        // â”€â”€ Form Header Fields â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
            new KeyValuePair<CounterpartyType, string>(CounterpartyType.Supplier, "ظ…ظˆط±ط¯"),
            new KeyValuePair<CounterpartyType, string>(CounterpartyType.Customer, "ط¹ظ…ظٹظ„")
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
            set
            {
                if (SetProperty(ref _formWarehouseId, value))
                {
                    if (value.HasValue && value.Value > 0)
                        SessionSelections.LastWarehouseId = value.Value;
                    OnPropertyChanged(nameof(CanSave));
                }
            }
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

        private InvoiceType _formInvoiceType = InvoiceType.Cash;
        public InvoiceType FormInvoiceType { get => _formInvoiceType; set => SetProperty(ref _formInvoiceType, value); }

        private PaymentMethod _formPaymentMethod = PaymentMethod.Cash;
        public PaymentMethod FormPaymentMethod { get => _formPaymentMethod; set => SetProperty(ref _formPaymentMethod, value); }

        private DateTime? _formDueDate;
        public DateTime? FormDueDate { get => _formDueDate; set => SetProperty(ref _formDueDate, value); }

        // â”€â”€ Totals (preview, computed from lines) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // â”€â”€ CanExecute Guards â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public bool CanSave => IsEditing
                       && FormWarehouseId.HasValue && FormWarehouseId > 0
                       && FormLines.Count > 0
                       && (FormCounterpartyType != CounterpartyType.Supplier || (FormSupplierId.HasValue && FormSupplierId > 0))
                       && (FormCounterpartyType != CounterpartyType.Customer || (FormCounterpartyCustomerId.HasValue && FormCounterpartyCustomerId > 0))
                       && FormLines.All(l => l.ProductId > 0 && l.Quantity > 0 && l.UnitPrice > 0);

        public bool CanPost => CurrentInvoice != null && IsDraft && !IsEditing;
        public bool CanCancel => CurrentInvoice != null && IsPosted && !IsEditing;
        public bool CanDeleteDraft => CurrentInvoice != null && IsDraft && !IsEditing;

        // â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // â”€â”€ Constructor â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public PurchaseInvoiceViewModel(
            IPurchaseInvoiceService invoiceService,
            IProductService productService,
            IWarehouseService warehouseService,
            Application.Interfaces.Purchases.ISupplierService supplierService,
            ICustomerService customerService,
            ISalesRepresentativeService salesRepresentativeService,
            ISmartEntryQueryService smartEntryQueryService,
            ILineCalculationService lineCalculationService,
            IDialogService dialog)
        {
            _invoiceService = invoiceService ?? throw new ArgumentNullException(nameof(invoiceService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _salesRepresentativeService = salesRepresentativeService ?? throw new ArgumentNullException(nameof(salesRepresentativeService));
            _smartEntryQueryService = smartEntryQueryService ?? throw new ArgumentNullException(nameof(smartEntryQueryService));
            _lineCalculationService = lineCalculationService ?? throw new ArgumentNullException(nameof(lineCalculationService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

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

    }
}
