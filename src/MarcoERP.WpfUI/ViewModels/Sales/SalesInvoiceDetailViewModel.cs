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
    public sealed partial class SalesInvoiceDetailViewModel : BaseViewModel, INavigationAware, IInvoiceLineFormHost, IDirtyStateAware
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
        private readonly IDialogService _dialog;
        private readonly IAttachmentService _attachmentService;
        private readonly IEmailService _emailService;

        private readonly Dictionary<SalesInvoiceLineFormItem, int> _smartRefreshVersions = new();

        // ── Collections ──────────────────────────────────────────
        public ObservableCollection<Application.DTOs.Sales.CustomerDto> Customers { get; } = new();
        public ObservableCollection<Application.DTOs.Purchases.SupplierDto> Suppliers { get; } = new();
        public ObservableCollection<SalesRepresentativeDto> SalesRepresentatives { get; } = new();
        public ObservableCollection<Application.DTOs.Inventory.WarehouseDto> Warehouses { get; } = new();
        public ObservableCollection<ProductDto> Products { get; } = new();
        public ObservableCollection<SalesInvoiceLineFormItem> FormLines { get; } = new();

        // ── Attachments ────────────────────────────────────────────
        public ObservableCollection<AttachmentDto> Attachments { get; } = new();

        private AttachmentDto _selectedAttachment;
        public AttachmentDto SelectedAttachment
        {
            get => _selectedAttachment;
            set => SetProperty(ref _selectedAttachment, value);
        }

        public bool HasNoAttachments => Attachments.Count == 0;

        public ICommand UploadAttachmentCommand { get; private set; }
        public ICommand DeleteAttachmentCommand { get; private set; }
        public ICommand OpenAttachmentCommand { get; private set; }

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

        /// <summary>Static list for InvoiceType ComboBox binding.</summary>
        public static IReadOnlyList<KeyValuePair<InvoiceType, string>> InvoiceTypes { get; } = new[]
        {
            new KeyValuePair<InvoiceType, string>(InvoiceType.Cash, "نقدي"),
            new KeyValuePair<InvoiceType, string>(InvoiceType.Credit, "آجل")
        };

        /// <summary>Static list for PaymentMethod ComboBox binding.</summary>
        public static IReadOnlyList<KeyValuePair<PaymentMethod, string>> PaymentMethods { get; } = new[]
        {
            new KeyValuePair<PaymentMethod, string>(PaymentMethod.Cash, "نقدي"),
            new KeyValuePair<PaymentMethod, string>(PaymentMethod.Card, "بطاقة"),
            new KeyValuePair<PaymentMethod, string>(PaymentMethod.OnAccount, "آجل"),
            new KeyValuePair<PaymentMethod, string>(PaymentMethod.Check, "شيك"),
            new KeyValuePair<PaymentMethod, string>(PaymentMethod.BankTransfer, "تحويل بنكي"),
            new KeyValuePair<PaymentMethod, string>(PaymentMethod.EWallet, "محفظة إلكترونية")
        };

        private string _formNotes;
        public string FormNotes
        {
            get => _formNotes;
            set
            {
                var old = _formNotes;
                if (SetProperty(ref _formNotes, value))
                {
                    MarkDirty();
                    if (UndoManager is { IsSuppressed: false })
                        UndoManager.RecordChange(nameof(FormNotes), old, value, v => FormNotes = (string)v);
                }
            }
        }

        private decimal _formHeaderDiscountPercent;
        public decimal FormHeaderDiscountPercent
        {
            get => _formHeaderDiscountPercent;
            set
            {
                var old = _formHeaderDiscountPercent;
                if (SetProperty(ref _formHeaderDiscountPercent, value))
                {
                    MarkDirty();
                    RefreshTotals();
                    if (UndoManager is { IsSuppressed: false })
                        UndoManager.RecordChange(nameof(FormHeaderDiscountPercent), old, value, v => FormHeaderDiscountPercent = (decimal)v);
                }
            }
        }

        private decimal _formHeaderDiscountAmount;
        public decimal FormHeaderDiscountAmount
        {
            get => _formHeaderDiscountAmount;
            set
            {
                var old = _formHeaderDiscountAmount;
                if (SetProperty(ref _formHeaderDiscountAmount, value))
                {
                    MarkDirty();
                    RefreshTotals();
                    if (UndoManager is { IsSuppressed: false })
                        UndoManager.RecordChange(nameof(FormHeaderDiscountAmount), old, value, v => FormHeaderDiscountAmount = (decimal)v);
                }
            }
        }

        private decimal _formDeliveryFee;
        public decimal FormDeliveryFee
        {
            get => _formDeliveryFee;
            set
            {
                var old = _formDeliveryFee;
                if (SetProperty(ref _formDeliveryFee, value))
                {
                    MarkDirty();
                    RefreshTotals();
                    if (UndoManager is { IsSuppressed: false })
                        UndoManager.RecordChange(nameof(FormDeliveryFee), old, value, v => FormDeliveryFee = (decimal)v);
                }
            }
        }

        private InvoiceType _formInvoiceType = InvoiceType.Cash;
        public InvoiceType FormInvoiceType { get => _formInvoiceType; set => SetProperty(ref _formInvoiceType, value); }

        private PaymentMethod _formPaymentMethod = PaymentMethod.Cash;
        public PaymentMethod FormPaymentMethod { get => _formPaymentMethod; set => SetProperty(ref _formPaymentMethod, value); }

        private DateTime? _formDueDate;
        public DateTime? FormDueDate { get => _formDueDate; set => SetProperty(ref _formDueDate, value); }

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
        public ICommand SendByEmailCommand { get; private set; }
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
            ILineCalculationService lineCalculationService,
            IDialogService dialog,
            IAttachmentService attachmentService,
            IEmailService emailService = null)
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
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _attachmentService = attachmentService ?? throw new ArgumentNullException(nameof(attachmentService));
            _emailService = emailService;

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
            SendByEmailCommand = CreateCommand(SendByEmailAsync, () => CurrentInvoice != null && IsPosted && _emailService != null);
            JumpToInvoiceCommand = new AsyncRelayCommand(JumpToInvoiceAsync);
            OpenPriceHistoryCommand = new AsyncRelayCommand(OpenPriceHistoryAsync);

            // Attachment commands
            UploadAttachmentCommand = CreateCommand(UploadAttachmentAsync, () => CurrentInvoice != null);
            DeleteAttachmentCommand = CreateCommand(DeleteAttachmentAsync, () => SelectedAttachment != null);
            OpenAttachmentCommand = CreateCommand(OpenAttachmentAsync, () => SelectedAttachment != null);
            Attachments.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoAttachments));
        }

        private async Task UploadAttachmentAsync()
        {
            if (CurrentInvoice == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "اختر ملف مرفق",
                Filter = "كل الملفات|*.*|PDF|*.pdf|صور|*.png;*.jpg;*.jpeg|Excel|*.xlsx;*.xls"
            };
            if (dlg.ShowDialog() != true) return;

            var result = await _attachmentService.UploadAsync("SalesInvoice", CurrentInvoice.Id, dlg.FileName);
            if (result.IsSuccess)
            {
                Attachments.Add(result.Data);
                StatusMessage = "تم إضافة المرفق بنجاح.";
            }
            else
                ErrorMessage = result.ErrorMessage;
        }

        private async Task DeleteAttachmentAsync()
        {
            if (SelectedAttachment == null) return;
            var confirm = MessageBox.Show("هل تريد حذف هذا المرفق؟", "تأكيد الحذف",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            var result = await _attachmentService.DeleteAsync(SelectedAttachment.Id);
            if (result.IsSuccess)
            {
                Attachments.Remove(SelectedAttachment);
                StatusMessage = "تم حذف المرفق.";
            }
            else
                ErrorMessage = result.ErrorMessage;
        }

        private async Task OpenAttachmentAsync()
        {
            if (SelectedAttachment == null) return;
            var result = await _attachmentService.OpenAsync(SelectedAttachment.Id);
            if (!result.IsSuccess)
                ErrorMessage = result.ErrorMessage;
        }

        internal async Task LoadAttachmentsAsync()
        {
            if (CurrentInvoice == null) return;
            var result = await _attachmentService.GetAttachmentsAsync("SalesInvoice", CurrentInvoice.Id);
            Attachments.Clear();
            if (result.IsSuccess)
                foreach (var a in result.Data)
                    Attachments.Add(a);
        }
    }
}
