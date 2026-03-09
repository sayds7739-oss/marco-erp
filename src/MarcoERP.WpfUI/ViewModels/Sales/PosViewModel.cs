using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Application.Interfaces.Treasury;
using MarcoERP.Domain.Enums;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    /// <summary>
    /// ViewModel for the POS window. Ultra-fast, keyboard-optimized.
    /// Manages: product lookup/cache, cart, payment, session lifecycle.
    /// </summary>
    public sealed class PosViewModel : BaseViewModel
    {
        private readonly IPosService _posService;
        private readonly ILineCalculationService _lineCalculationService;
        private readonly ICashboxService _cashboxService;
        private readonly IWarehouseService _warehouseService;
        private readonly IDialogService _dialog;

        public event Action RequestBarcodeFocus;

        // ── Product Cache ────────────────────────────────────────
        private List<PosProductLookupDto> _productCache = new();
        public ObservableCollection<PosProductLookupDto> SearchResults { get; } = new();

        // ── Cart ─────────────────────────────────────────────────
        public ObservableCollection<PosCartItemDto> CartItems { get; } = new();

        // ── Session ──────────────────────────────────────────────
        private PosSessionDto _currentSession;
        public PosSessionDto CurrentSession
        {
            get => _currentSession;
            set
            {
                SetProperty(ref _currentSession, value);
                OnPropertyChanged(nameof(HasSession));
                OnPropertyChanged(nameof(SessionInfo));
            }
        }

        public bool HasSession => CurrentSession != null;
        public string SessionInfo => CurrentSession != null
            ? $"جلسة: {CurrentSession.SessionNumber}  |  المعاملات: {CurrentSession.TransactionCount}  |  المبيعات: {CurrentSession.TotalSales:N2}"
            : "لا توجد جلسة مفتوحة";

        // ── Search ─────────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    PerformSearch();
            }
        }

        private bool _hasSearchResults;
        public bool HasSearchResults
        {
            get => _hasSearchResults;
            private set => SetProperty(ref _hasSearchResults, value);
        }

        // ── Customer ────────────────────────────────────────────
        private int? _selectedCustomerId;
        public int? SelectedCustomerId
        {
            get => _selectedCustomerId;
            set => SetProperty(ref _selectedCustomerId, value);
        }

        private string _customerName = "عميل نقدي";
        public string CustomerName
        {
            get => _customerName;
            set => SetProperty(ref _customerName, value);
        }

        // ── Totals ──────────────────────────────────────────────
        public decimal CartSubtotal => CartItems.Sum(i => i.SubTotal);
        public decimal CartDiscount => CartItems.Sum(i => i.DiscountAmount);
        public decimal CartVat => CartItems.Sum(i => i.VatAmount);
        public decimal CartNetTotal => CartItems.Sum(i => i.TotalWithVat);
        public decimal CartProfit => CartItems.Sum(i => i.ProfitAmount);
        public int CartItemCount => CartItems.Count;

        // ── Payment ─────────────────────────────────────────────
        private decimal _cashAmount;
        public decimal CashAmount
        {
            get => _cashAmount;
            set { SetProperty(ref _cashAmount, value); OnPropertyChanged(nameof(ChangeAmount)); }
        }

        private decimal _cardAmount;
        public decimal CardAmount
        {
            get => _cardAmount;
            set { SetProperty(ref _cardAmount, value); OnPropertyChanged(nameof(ChangeAmount)); }
        }

        private decimal _onAccountAmount;
        public decimal OnAccountAmount
        {
            get => _onAccountAmount;
            set => SetProperty(ref _onAccountAmount, value);
        }

        private string _cardReferenceNumber;
        public string CardReferenceNumber
        {
            get => _cardReferenceNumber;
            set => SetProperty(ref _cardReferenceNumber, value);
        }

        public decimal TotalPaid => CashAmount + CardAmount + OnAccountAmount;
        public decimal ChangeAmount => TotalPaid > CartNetTotal ? TotalPaid - CartNetTotal : 0;

        // ── Payment Panel Visibility ────────────────────────────
        private bool _isPaymentPanelVisible;
        public bool IsPaymentPanelVisible
        {
            get => _isPaymentPanelVisible;
            set => SetProperty(ref _isPaymentPanelVisible, value);
        }

        private bool _isCartResetting;
        public bool IsCartResetting
        {
            get => _isCartResetting;
            set => SetProperty(ref _isCartResetting, value);
        }

        // ── Selected Cart Item ──────────────────────────────────
        private PosCartItemDto _selectedCartItem;
        public PosCartItemDto SelectedCartItem
        {
            get => _selectedCartItem;
            set => SetProperty(ref _selectedCartItem, value);
        }

        // ── Commands ─────────────────────────────────────────────
        public ICommand OpenSessionCommand { get; }
        public ICommand CloseSessionCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ScanCommand { get; }
        public ICommand AddToCartCommand { get; }
        public ICommand RemoveFromCartCommand { get; }
        public ICommand ChangeQuantityCommand { get; }
        public ICommand ApplyDiscountCommand { get; }
        public ICommand ShowPaymentCommand { get; }
        public ICommand CompleteSaleCommand { get; }
        public ICommand CancelCartCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand CashFullCommand { get; }
        public ICommand RefreshCacheCommand { get; }
        public ICommand InitializeCommand { get; }

        // ═══════════════════════════════════════════════════════════
        //  Constructor
        // ═══════════════════════════════════════════════════════════

        public PosViewModel(IPosService posService, ILineCalculationService lineCalculationService,
                            ICashboxService cashboxService, IWarehouseService warehouseService,
                            IDialogService dialog)
        {
            _posService = posService ?? throw new ArgumentNullException(nameof(posService));
            _lineCalculationService = lineCalculationService ?? throw new ArgumentNullException(nameof(lineCalculationService));
            _cashboxService = cashboxService ?? throw new ArgumentNullException(nameof(cashboxService));
            _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            OpenSessionCommand = new AsyncRelayCommand(OpenSessionAsync);
            CloseSessionCommand = new AsyncRelayCommand(CloseSessionAsync);
            SearchCommand = new RelayCommand(_ => PerformSearch());
            ScanCommand = new AsyncRelayCommand(p => ScanAndAddAsync(p));
            AddToCartCommand = new AsyncRelayCommand(p => AddToCartAsync(p));
            RemoveFromCartCommand = new RelayCommand(_ => { if (SelectedCartItem != null) { CartItems.Remove(SelectedCartItem); SelectedCartItem = null; RefreshTotals(); } });
            ChangeQuantityCommand = new RelayCommand(p => ChangeQuantity(p));
            ApplyDiscountCommand = new RelayCommand(p => ApplyDiscount(p));
            ShowPaymentCommand = new RelayCommand(_ => ShowPaymentPanel());
            CompleteSaleCommand = new AsyncRelayCommand(CompleteSaleAsync);
            CancelCartCommand = new RelayCommand(_ => CancelCart());
            ResetCommand = new AsyncRelayCommand(ResetSaleAsync);
            CashFullCommand = new RelayCommand(_ => SetCashFull());
            RefreshCacheCommand = new AsyncRelayCommand(LoadProductCacheAsync);
            InitializeCommand = new AsyncRelayCommand(InitializeAsync);
        }

        // ═══════════════════════════════════════════════════════════
        //  Initialization
        // ═══════════════════════════════════════════════════════════

        public async Task InitializeAsync()
        {
            IsBusy = true;
            ClearError();

            try
            {
                // Try to load existing open session
                var sessionResult = await _posService.GetCurrentSessionAsync();
                if (sessionResult.IsSuccess)
                    CurrentSession = sessionResult.Data;

                // Load product cache
                await LoadProductCacheAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("التهيئة", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Session Management
        // ═══════════════════════════════════════════════════════════

        private async Task OpenSessionAsync()
        {
            if (HasSession)
            {
                ErrorMessage = "لديك جلسة مفتوحة بالفعل.";
                return;
            }

            ClearError();

            try
            {
                // Load cashboxes and warehouses for the dialog
                var cashboxResult = await _cashboxService.GetActiveAsync();
                var warehouseResult = await _warehouseService.GetActiveAsync();

                if (!cashboxResult.IsSuccess || cashboxResult.Data == null || cashboxResult.Data.Count == 0)
                {
                    ErrorMessage = "لا توجد خزن مفعّلة. يرجى إضافة خزنة من الإعدادات أولاً.";
                    return;
                }

                if (!warehouseResult.IsSuccess || warehouseResult.Data == null || warehouseResult.Data.Count == 0)
                {
                    ErrorMessage = "لا توجد مستودعات مفعّلة. يرجى إضافة مستودع من الإعدادات أولاً.";
                    return;
                }

                var dialog = new Views.Sales.PosOpenSessionDialog(cashboxResult.Data, warehouseResult.Data, _dialog);
                dialog.Owner = System.Windows.Application.Current.Windows.OfType<Views.Sales.PosWindow>().FirstOrDefault();
                if (dialog.ShowDialog() != true)
                    return;

                IsBusy = true;

                var dto = new OpenPosSessionDto
                {
                    CashboxId = dialog.SelectedCashboxId,
                    WarehouseId = dialog.SelectedWarehouseId,
                    OpeningBalance = dialog.OpeningBalance
                };

                var result = await _posService.OpenSessionAsync(dto);
                if (result.IsSuccess)
                {
                    CurrentSession = result.Data;
                    StatusMessage = $"تم فتح الجلسة: {CurrentSession.SessionNumber}";
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("فتح الجلسة", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CloseSessionAsync()
        {
            if (!HasSession)
            {
                ErrorMessage = "لا توجد جلسة مفتوحة.";
                return;
            }

            ClearError();

            try
            {
                var dialog = new Views.Sales.PosCloseSessionDialog(CurrentSession, _dialog);
                dialog.Owner = System.Windows.Application.Current.Windows.OfType<Views.Sales.PosWindow>().FirstOrDefault();
                if (dialog.ShowDialog() != true)
                    return;

                IsBusy = true;

                var dto = new ClosePosSessionDto
                {
                    SessionId = CurrentSession.Id,
                    ActualClosingBalance = dialog.ActualClosingBalance,
                    Notes = dialog.Notes
                };

                var result = await _posService.CloseSessionAsync(dto);
                if (result.IsSuccess)
                {
                    var variance = result.Data.Variance;
                    var varianceMsg = variance == 0
                        ? "لا يوجد فارق."
                        : $"الفارق: {variance:N2}";
                    StatusMessage = $"تم إغلاق الجلسة بنجاح. {varianceMsg}";
                    CurrentSession = null;
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("إغلاق الجلسة", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Product Search (cached, ultra-fast)
        // ═══════════════════════════════════════════════════════════

        private async Task LoadProductCacheAsync()
        {
            var result = await _posService.LoadProductCacheAsync();
            if (result.IsSuccess)
            {
                _productCache = result.Data.ToList();
                StatusMessage = $"تم تحميل {_productCache.Count} صنف.";
            }
        }

        private void PerformSearch()
        {
            SearchResults.Clear();
            HasSearchResults = false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return;

            var term = SearchText.Trim().ToLowerInvariant();

            var matches = _productCache.Where(p =>
                (p.Code != null && p.Code.ToLowerInvariant().Contains(term)) ||
                (p.NameAr != null && p.NameAr.Contains(term)) ||
                (p.Barcode != null && p.Barcode == term) ||
                (p.Units != null && p.Units.Any(u => u.Barcode == term))
            ).Take(20).ToList();

            foreach (var m in matches)
                SearchResults.Add(m);

            HasSearchResults = SearchResults.Count > 0;

            // If exact barcode match, auto-add
            if (matches.Count == 1 && (matches[0].Barcode == term ||
                matches[0].Units?.Any(u => u.Barcode == term) == true))
            {
                _ = AddProductToCartAsync(matches[0]);
                SearchText = string.Empty;
            }
        }

        private async Task ScanAndAddAsync(object parameter)
        {
            var raw = parameter as string;
            var term = (raw ?? SearchText)?.Trim();
            if (string.IsNullOrWhiteSpace(term))
                return;

            ClearError();

            var unitBarcodeMatch = _productCache
                .Select(p => new
                {
                    Product = p,
                    Unit = p.Units?.FirstOrDefault(u => u.Barcode != null && string.Equals(u.Barcode.Trim(), term, StringComparison.OrdinalIgnoreCase))
                })
                .FirstOrDefault(x => x.Unit != null);

            if (unitBarcodeMatch != null)
            {
                await AddProductToCartAsync(unitBarcodeMatch.Product, unitBarcodeMatch.Unit);
                ResetSearchAfterScan();
                return;
            }

            var productBarcodeMatch = _productCache.FirstOrDefault(p =>
                p.Barcode != null && string.Equals(p.Barcode.Trim(), term, StringComparison.OrdinalIgnoreCase));
            if (productBarcodeMatch != null)
            {
                await AddProductToCartAsync(productBarcodeMatch);
                ResetSearchAfterScan();
                return;
            }

            var productCodeMatch = _productCache.FirstOrDefault(p =>
                p.Code != null && string.Equals(p.Code.Trim(), term, StringComparison.OrdinalIgnoreCase));
            if (productCodeMatch != null)
            {
                await AddProductToCartAsync(productCodeMatch);
                ResetSearchAfterScan();
                return;
            }

            if (SearchResults.Count == 1)
            {
                await AddProductToCartAsync(SearchResults[0]);
                ResetSearchAfterScan();
                return;
            }
        }

        private void ResetSearchAfterScan()
        {
            SearchText = string.Empty;
            SearchResults.Clear();
            HasSearchResults = false;
        }

        // ═══════════════════════════════════════════════════════════
        //  Cart Operations
        // ═══════════════════════════════════════════════════════════

        private async Task AddToCartAsync(object parameter)
        {
            if (parameter is PosProductLookupDto product)
            {
                await AddProductToCartAsync(product);
                SearchText = string.Empty;
            }
        }

        private async Task AddProductToCartAsync(PosProductLookupDto product, PosProductUnitDto unitOverride = null)
        {
            if (!HasSession)
            {
                ErrorMessage = "يجب فتح جلسة أولاً.";
                return;
            }

            ClearError();

            // Get scanned unit (if any) else default unit
            var unit = unitOverride
                       ?? product.Units?.FirstOrDefault(u => u.IsDefault)
                       ?? product.Units?.FirstOrDefault();

            if (unit == null)
            {
                ErrorMessage = $"الصنف ({product.NameAr}) ليس له وحدات قياس.";
                return;
            }

            // Check stock
            var stockResult = await _posService.GetAvailableStockAsync(
                product.Id, CurrentSession.WarehouseId);
            var available = stockResult.IsSuccess ? stockResult.Data : 0;

            if (available < unit.ConversionFactor)
            {
                ErrorMessage = $"المخزون غير كافي للصنف ({product.NameAr}). المتاح: {available:N2}";
                return;
            }

            // Check if already in cart
            var existing = CartItems.FirstOrDefault(c => c.ProductId == product.Id && c.UnitId == unit.UnitId);
            if (existing != null)
            {
                var newQty = existing.Quantity + 1;
                var newBaseQty = _lineCalculationService.ConvertQuantity(newQty, unit.ConversionFactor);
                if (newBaseQty > available)
                {
                    ErrorMessage = $"الكمية المطلوبة تتجاوز المتاح ({available:N2}).";
                    return;
                }
                existing.Quantity = newQty;
                RecalculateCartItem(existing);
                RefreshTotals();
                return;
            }

            var cartItem = new PosCartItemDto
            {
                ProductId = product.Id,
                ProductCode = product.Code,
                ProductNameAr = product.NameAr,
                UnitId = unit.UnitId,
                UnitNameAr = unit.UnitNameAr,
                Quantity = 1,
                UnitPrice = unit.SalePrice > 0 ? unit.SalePrice : product.DefaultSalePrice,
                ConversionFactor = unit.ConversionFactor,
                DiscountPercent = 0,
                VatRate = product.VatRate,
                AvailableStock = available,
                WacPerBaseUnit = product.WeightedAverageCost
            };

            CartItems.Add(cartItem);
            RecalculateCartItem(cartItem);
            RefreshTotals();
        }

private void ChangeQuantity(object parameter)
        {
            if (SelectedCartItem == null) return;

            // For simplicity, increment by 1; in real UI, this opens a numeric input dialog
            if (parameter is string qtyStr && decimal.TryParse(qtyStr, out var newQty) && newQty > 0)
            {
                var baseQty = _lineCalculationService.ConvertQuantity(newQty, SelectedCartItem.ConversionFactor);
                if (baseQty > SelectedCartItem.AvailableStock)
                {
                    ErrorMessage = $"الكمية تتجاوز المتاح ({SelectedCartItem.AvailableStock:N2}).";
                    return;
                }
                SelectedCartItem.Quantity = newQty;
                RecalculateCartItem(SelectedCartItem);
                RefreshTotals();
            }
        }

        private void ApplyDiscount(object parameter)
        {
            if (SelectedCartItem == null) return;

            if (parameter is string discStr && decimal.TryParse(discStr, out var disc) && disc >= 0 && disc <= 100)
            {
                SelectedCartItem.DiscountPercent = disc;
                RecalculateCartItem(SelectedCartItem);
                RefreshTotals();
            }
        }

        private void CancelCart()
        {
            CartItems.Clear();
            SelectedCustomerId = null;
            CustomerName = "عميل نقدي";
            IsPaymentPanelVisible = false;
            ResetPayment();
            RefreshTotals();
            ClearError();
        }

        private async Task ResetSaleAsync()
        {
            if (CartItems.Count == 0)
                return;

            if (!_dialog.Confirm(
                "هل تريد إعادة تعيين عملية البيع الحالية؟",
                "إعادة تعيين")) return;

            ClearError();
            await AnimateCartResetAsync();
            CancelCart();
            RequestBarcodeFocus?.Invoke();
            await ShowTemporaryStatusAsync("تم إعادة التعيين");
        }

        private async Task AnimateCartResetAsync()
        {
            IsCartResetting = true;
            await Task.Delay(160);
            IsCartResetting = false;
        }

        private async Task ShowTemporaryStatusAsync(string message)
        {
            StatusMessage = message;
            await Task.Delay(1500);
            if (StatusMessage == message)
                StatusMessage = null;
        }

        // ═══════════════════════════════════════════════════════════
        //  Payment
        // ═══════════════════════════════════════════════════════════

        private void ShowPaymentPanel()
        {
            if (CartItems.Count == 0)
            {
                ErrorMessage = "السلة فارغة.";
                return;
            }
            ClearError();
            IsPaymentPanelVisible = true;
            CashAmount = CartNetTotal; // Default to full cash
            CardAmount = 0;
            OnAccountAmount = 0;
            OnPropertyChanged(nameof(ChangeAmount));
        }

        private void SetCashFull()
        {
            CashAmount = CartNetTotal;
            CardAmount = 0;
            OnAccountAmount = 0;
        }

        private void ResetPayment()
        {
            CashAmount = 0;
            CardAmount = 0;
            OnAccountAmount = 0;
            CardReferenceNumber = null;
        }

        private async Task CompleteSaleAsync()
        {
            if (!HasSession)
            {
                ErrorMessage = "لا توجد جلسة مفتوحة.";
                return;
            }

            if (CartItems.Count == 0)
            {
                ErrorMessage = "السلة فارغة.";
                return;
            }

            var totalPaid = CashAmount + CardAmount + OnAccountAmount;
            if (totalPaid < CartNetTotal)
            {
                ErrorMessage = $"المبلغ المدفوع ({totalPaid:N2}) أقل من الإجمالي ({CartNetTotal:N2}).";
                return;
            }

            if (OnAccountAmount > 0 && !SelectedCustomerId.HasValue)
            {
                ErrorMessage = "يجب تحديد العميل للبيع الآجل. لا يمكن البيع آجلاً لعميل نقدي.";
                return;
            }

            ClearError();
            IsBusy = true;

            try
            {
                var dto = new CompletePoseSaleDto
                {
                    SessionId = CurrentSession.Id,
                    CustomerId = SelectedCustomerId,
                    Notes = null,
                    Lines = CartItems.Select(c => new PosSaleLineDto
                    {
                        ProductId = c.ProductId,
                        UnitId = c.UnitId,
                        Quantity = c.Quantity,
                        UnitPrice = c.UnitPrice,
                        DiscountPercent = c.DiscountPercent
                    }).ToList(),
                    Payments = BuildPaymentList()
                };

                var result = await _posService.CompleteSaleAsync(dto);
                if (result.IsSuccess)
                {
                    var baseMessage = $"✓ تمت عملية البيع — فاتورة: {result.Data.InvoiceNumber}  الإجمالي: {result.Data.NetTotal:N2}";
                    StatusMessage = string.IsNullOrWhiteSpace(result.Data.WarningMessage)
                        ? baseMessage
                        : $"{baseMessage}  |  ⚠ {result.Data.WarningMessage}";

                    // Refresh session totals
                    var sessionResult = await _posService.GetCurrentSessionAsync();
                    if (sessionResult.IsSuccess)
                        CurrentSession = sessionResult.Data;

                    CancelCart(); // Reset for next customer
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("إتمام البيع", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private List<PosPaymentDto> BuildPaymentList()
        {
            var payments = new List<PosPaymentDto>();

            // Cash amount should exclude change returned to customer — delegated to service
            var netCash = _lineCalculationService.CalculateNetCash(CashAmount, CartNetTotal, CardAmount, OnAccountAmount);
            if (netCash > 0)
                payments.Add(new PosPaymentDto { PaymentMethod = "Cash", Amount = netCash });

            if (CardAmount > 0)
                payments.Add(new PosPaymentDto
                {
                    PaymentMethod = "Card",
                    Amount = CardAmount,
                    ReferenceNumber = CardReferenceNumber
                });

            if (OnAccountAmount > 0)
                payments.Add(new PosPaymentDto { PaymentMethod = "OnAccount", Amount = OnAccountAmount });

            return payments;
        }

        // ═══════════════════════════════════════════════════════════
        //  Refresh Totals
        // ═══════════════════════════════════════════════════════════

        private void RefreshTotals()
        {
            OnPropertyChanged(nameof(CartSubtotal));
            OnPropertyChanged(nameof(CartDiscount));
            OnPropertyChanged(nameof(CartVat));
            OnPropertyChanged(nameof(CartNetTotal));
            OnPropertyChanged(nameof(CartProfit));
            OnPropertyChanged(nameof(CartItemCount));
            OnPropertyChanged(nameof(ChangeAmount));
        }

        /// <summary>Populates the calculated fields of a cart item via the shared calculation service.</summary>
        private void RecalculateCartItem(PosCartItemDto item)
        {
            item.BaseQuantity = _lineCalculationService.ConvertQuantity(item.Quantity, item.ConversionFactor);

            var result = _lineCalculationService.CalculateLine(new LineCalculationRequest
            {
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountPercent = item.DiscountPercent,
                VatRate = item.VatRate,
                ConversionFactor = item.ConversionFactor,
                CostPrice = item.WacPerBaseUnit
            });

            item.SubTotal = result.SubTotal;
            item.DiscountAmount = result.DiscountAmount;
            item.NetTotal = result.NetTotal;
            item.VatAmount = result.VatAmount;
            item.TotalWithVat = result.TotalWithVat;
            item.CostTotal = result.CostTotal;
            item.ProfitAmount = result.TotalProfit;
            item.ProfitMarginPercent = result.ProfitMarginPercent;
        }
    }
}
