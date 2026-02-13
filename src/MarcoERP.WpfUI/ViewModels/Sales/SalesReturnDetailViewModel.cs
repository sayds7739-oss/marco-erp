using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Common;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Application.Interfaces.SmartEntry;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Common;
using MarcoERP.WpfUI.Navigation;
using MarcoERP.WpfUI.ViewModels.Common;
using MarcoERP.WpfUI.Views.Common;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    /// <summary>
    /// Full-screen ViewModel for Sales Return detail — create, edit, post, cancel.
    /// Includes optional OriginalInvoiceId reference.
    /// </summary>
    public sealed class SalesReturnDetailViewModel : BaseViewModel, INavigationAware, IInvoiceLineFormHost, IDirtyStateAware
    {
        private readonly ISalesReturnService _returnService;
        private readonly ISalesInvoiceService _invoiceService;
        private readonly IProductService _productService;
        private readonly IWarehouseService _warehouseService;
        private readonly ICustomerService _customerService;
        private readonly ISupplierService _supplierService;
        private readonly ISalesRepresentativeService _salesRepresentativeService;
        private readonly INavigationService _navigationService;
        private readonly ILineCalculationService _lineCalculationService;
        private readonly ISmartEntryQueryService _smartEntryQueryService;

        public ObservableCollection<CustomerDto> Customers { get; } = new();
        public ObservableCollection<SupplierDto> Suppliers { get; } = new();
        public ObservableCollection<SalesRepresentativeDto> SalesRepresentatives { get; } = new();
        public ObservableCollection<Application.DTOs.Inventory.WarehouseDto> Warehouses { get; } = new();
        public ObservableCollection<ProductDto> Products { get; } = new();
        public ObservableCollection<SalesInvoiceListDto> PostedInvoices { get; } = new();
        public ObservableCollection<SalesReturnLineFormItem> FormLines { get; } = new();

        private SalesReturnDto _currentReturn;
        public SalesReturnDto CurrentReturn
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

        private int? _formCustomerId;
        public int? FormCustomerId { get => _formCustomerId; set { if (SetProperty(ref _formCustomerId, value)) { MarkDirty(); OnPropertyChanged(nameof(CanSave)); } } }

        private CounterpartyType _formCounterpartyType = CounterpartyType.Customer;
        public CounterpartyType FormCounterpartyType
        {
            get => _formCounterpartyType;
            set
            {
                if (SetProperty(ref _formCounterpartyType, value))
                {
                    MarkDirty();
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
        public int? FormSupplierId { get => _formSupplierId; set { if (SetProperty(ref _formSupplierId, value)) { MarkDirty(); OnPropertyChanged(nameof(CanSave)); } } }

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
                       && (FormCounterpartyType != CounterpartyType.Customer || (FormCustomerId.HasValue && FormCustomerId > 0))
                       && (FormCounterpartyType != CounterpartyType.Supplier || (FormSupplierId.HasValue && FormSupplierId > 0))
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

        public SalesReturnDetailViewModel(
            ISalesReturnService returnService,
            ISalesInvoiceService invoiceService,
            IProductService productService,
            IWarehouseService warehouseService,
            ICustomerService customerService,
            ISupplierService supplierService,
            ISalesRepresentativeService salesRepresentativeService,
            INavigationService navigationService,
            ILineCalculationService lineCalculationService,
            ISmartEntryQueryService smartEntryQueryService)
        {
            _returnService = returnService ?? throw new ArgumentNullException(nameof(returnService));
            _invoiceService = invoiceService ?? throw new ArgumentNullException(nameof(invoiceService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _salesRepresentativeService = salesRepresentativeService ?? throw new ArgumentNullException(nameof(salesRepresentativeService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _lineCalculationService = lineCalculationService ?? throw new ArgumentNullException(nameof(lineCalculationService));
            _smartEntryQueryService = smartEntryQueryService ?? throw new ArgumentNullException(nameof(smartEntryQueryService));

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
            else if (parameter is SalesReturnLineFormItem line)
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
                if (result.IsSuccess) { CurrentReturn = result.Data; PopulateForm(result.Data); StatusMessage = $"مرتجع بيع «{result.Data.ReturnNumber}»"; }
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
            FormCounterpartyType = CounterpartyType.Customer;
            FormCustomerId = null;
            FormSupplierId = null;
            FormSalesRepresentativeId = null;
            FormWarehouseId = null;
            FormOriginalInvoiceId = null; FormNotes = "";
            FormLines.Clear(); RefreshTotals();
            StatusMessage = "إنشاء مرتجع بيع جديد...";
        }

        private void AddLine(object _)
        {
            if (!IsEditing && !IsNew) return;
            OpenAddLinePopup();
        }

        private void RemoveLine(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is SalesReturnLineFormItem line)
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
                    var lines = FormLines.Select(l => new CreateSalesReturnLineDto
                    { ProductId = l.ProductId, UnitId = l.UnitId, Quantity = l.Quantity, UnitPrice = l.UnitPrice, DiscountPercent = l.DiscountPercent }).ToList();

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
                        if (result.IsSuccess) { StatusMessage = $"تم إنشاء مرتجع البيع «{result.Data.ReturnNumber}» بنجاح"; CurrentReturn = result.Data; PopulateForm(result.Data); }
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
                        if (result.IsSuccess) { StatusMessage = $"تم تحديث مرتجع البيع «{result.Data.ReturnNumber}» بنجاح"; CurrentReturn = result.Data; PopulateForm(result.Data); }
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
            if (MessageBox.Show($"هل تريد ترحيل مرتجع البيع «{CurrentReturn.ReturnNumber}»؟\nبعد الترحيل لا يمكن التعديل.", "تأكيد الترحيل", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            IsBusy = true; ClearError();
            try { var r = await _returnService.PostAsync(CurrentReturn.Id); if (r.IsSuccess) { StatusMessage = $"تم ترحيل مرتجع البيع «{r.Data.ReturnNumber}»"; CurrentReturn = r.Data; PopulateForm(r.Data); } else ErrorMessage = r.ErrorMessage; }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("ترحيل المرتجع", ex); }
            finally { IsBusy = false; }
        }

        private async Task CancelReturnAsync()
        {
            if (CurrentReturn == null) return;
            if (MessageBox.Show($"هل تريد إلغاء مرتجع البيع «{CurrentReturn.ReturnNumber}»؟", "تأكيد الإلغاء", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            IsBusy = true; ClearError();
            try { var r = await _returnService.CancelAsync(CurrentReturn.Id); if (r.IsSuccess) { StatusMessage = "تم إلغاء المرتجع"; await LoadDetailAsync(CurrentReturn.Id); } else ErrorMessage = r.ErrorMessage; }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("إلغاء المرتجع", ex); }
            finally { IsBusy = false; }
        }

        private async Task DeleteDraftAsync()
        {
            if (CurrentReturn == null) return;
            if (MessageBox.Show($"هل تريد حذف مسودة المرتجع «{CurrentReturn.ReturnNumber}»؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            IsBusy = true; ClearError();
            try { var r = await _returnService.DeleteDraftAsync(CurrentReturn.Id); if (r.IsSuccess) { StatusMessage = "تم حذف المسودة"; NavigateBack(); } else ErrorMessage = r.ErrorMessage; }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("حذف المسودة", ex); }
            finally { IsBusy = false; }
        }

        private void StartEditing() { if (CurrentReturn != null && IsDraft) { IsEditing = true; IsNew = false; } }
        private void CancelEditing(object _) { IsEditing = false; IsNew = false; ClearError(); if (CurrentReturn != null) PopulateForm(CurrentReturn); else NavigateBack(); ResetDirtyTracking(); }
        private void NavigateBack() => _navigationService.NavigateTo("SalesReturns");

        private void PopulateForm(SalesReturnDto ret)
        {
            FormNumber = ret.ReturnNumber;
            FormDate = ret.ReturnDate;
            FormCounterpartyType = ret.CounterpartyType;
            FormCustomerId = ret.CustomerId;
            FormSupplierId = ret.SupplierId;
            FormSalesRepresentativeId = ret.SalesRepresentativeId;
            FormWarehouseId = ret.WarehouseId;
            FormOriginalInvoiceId = ret.OriginalInvoiceId;
            FormNotes = ret.Notes;
            FormLines.Clear();
            foreach (var line in ret.Lines ?? new List<SalesReturnLineDto>())
                FormLines.Add(new SalesReturnLineFormItem(this) { ProductId = line.ProductId, UnitId = line.UnitId, Quantity = line.Quantity, UnitPrice = line.UnitPrice, DiscountPercent = line.DiscountPercent });
            IsEditing = false; IsNew = false; RefreshTotals();
            ResetDirtyTracking();
        }

        // ── Add/Edit Line Popup ──────────────────────────────────
        private void OpenAddLinePopup()
        {
            if (!IsEditing && !IsNew) return;
            var state = new InvoiceLinePopupState(this, InvoicePopupMode.Sale, _lineCalculationService);
            LinePopup = state;
            state.PropertyChanged += PopupState_PropertyChanged;
            ShowPopupLoop(state);
            state.PropertyChanged -= PopupState_PropertyChanged;
            LinePopup = null;
        }

        private void EditLinePopup(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is not SalesReturnLineFormItem line) return;
            var index = FormLines.IndexOf(line);
            if (index < 0) return;

            var state = new InvoiceLinePopupState(this, InvoicePopupMode.Sale, _lineCalculationService);
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
                var confirm = MessageBox.Show(
                    "هذا الصنف موجود بالفعل في المرتجع.\nهل تريد إضافته مرة أخرى؟",
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
                FormLines.Add(new SalesReturnLineFormItem(this)
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
            var customerId = FormCustomerId ?? 0;
            var unitId = state.SelectedUnitId > 0 ? state.SelectedUnitId : (state.SecondaryUnit?.UnitId ?? 0);
            try
            {
                var stock = await _smartEntryQueryService.GetStockBaseQtyAsync(warehouseId, state.ProductId);
                state.StockQty = stock;

                var lastPurchase = await _smartEntryQueryService.GetLastPurchaseUnitPriceAsync(state.ProductId, unitId);
                state.LastPurchasePrice = lastPurchase ?? 0m;

                if (customerId > 0)
                {
                    var lastSale = await _smartEntryQueryService.GetLastSalesUnitPriceAsync(customerId, state.ProductId, unitId);
                    state.LastSalePrice = lastSale;
                }
            }
            catch { /* Smart entry is non-critical */ }
        }
    }
}
