using System;
using System.Collections.ObjectModel;
using System.Linq;
using MarcoERP.Application.DTOs.Common;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces;

namespace MarcoERP.WpfUI.ViewModels.Common
{
    /// <summary>
    /// Determines which prices are used as defaults when a product is selected.
    /// </summary>
    public enum InvoicePopupMode
    {
        /// <summary>Uses SalePrice from product units.</summary>
        Sale,
        /// <summary>Uses PurchasePrice from product units.</summary>
        Purchase
    }

    /// <summary>
    /// Shared state object for the invoice add/edit line popup.
    /// Supports dual-unit entry (primary/secondary) with automatic quantity and price conversion.
    /// Profit fields are read-only and calculated dynamically (per governance — never stored).
    /// Used by Sales Invoice, Purchase Invoice, Sales Return, Purchase Return.
    /// Phase 9C: All arithmetic delegated to ILineCalculationService.
    /// </summary>
    public sealed class InvoiceLinePopupState : BaseViewModel
    {
        private readonly IInvoiceLineFormHost _host;
        private readonly ILineCalculationService _calc;
        private bool _isUpdatingQty;
        private bool _isUpdatingPrice;

        private decimal PrimaryToSecondaryRatio
        {
            get
            {
                if (!HasPrimaryUnit || !HasSecondaryUnit) return 1m;
                var primaryFactor = PrimaryUnit?.ConversionFactor ?? 0m;
                var secondaryFactor = SecondaryUnit?.ConversionFactor ?? 0m;
                if (primaryFactor <= 0m || secondaryFactor <= 0m) return 1m;
                return secondaryFactor / primaryFactor;
            }
        }

        /// <summary>Sale or Purchase — determines which price column is used as default.</summary>
        public InvoicePopupMode Mode { get; }

        /// <summary>
        /// When true, UnitPrice is treated as VAT-inclusive.
        /// Governance: ACCOUNTING_PRINCIPLES VAT-03.
        /// </summary>
        public bool IsVatInclusive { get; set; }

        public InvoiceLinePopupState(IInvoiceLineFormHost host, InvoicePopupMode mode, ILineCalculationService lineCalculationService)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _calc = lineCalculationService ?? throw new ArgumentNullException(nameof(lineCalculationService));
            Mode = mode;
        }

        // ── Product ──────────────────────────────────────────────

        private int _productId;
        public int ProductId
        {
            get => _productId;
            set { if (SetProperty(ref _productId, value)) OnProductChanged(); }
        }

        private string _productName;
        public string ProductName
        {
            get => _productName;
            private set => SetProperty(ref _productName, value);
        }

        public ObservableCollection<ProductUnitDto> AvailableUnits { get; } = new();

        // ── Primary Unit (registered/main unit — e.g., carton) ────

        private ProductUnitDto _primaryUnit;
        public ProductUnitDto PrimaryUnit
        {
            get => _primaryUnit;
            private set
            {
                SetProperty(ref _primaryUnit, value);
                OnPropertyChanged(nameof(HasPrimaryUnit));
                OnPropertyChanged(nameof(PrimaryUnitName));
            }
        }

        public bool HasPrimaryUnit => PrimaryUnit != null;
        public string PrimaryUnitName => PrimaryUnit?.UnitNameAr ?? "";

        private decimal _primaryQty;
        public decimal PrimaryQty
        {
            get => _primaryQty;
            set
            {
                if (!SetProperty(ref _primaryQty, value) || _isUpdatingQty || !HasPrimaryUnit) return;
                _isUpdatingQty = true;
                _lastEditedIsPrimary = true;
                if (HasSecondaryUnit)
                    SecondaryQty = _calc.ConvertQuantity(value, PrimaryToSecondaryRatio);
                _isUpdatingQty = false;
                RecalcComputed();
            }
        }

        private decimal _primaryPrice;
        public decimal PrimaryPrice
        {
            get => _primaryPrice;
            set
            {
                if (!SetProperty(ref _primaryPrice, value)) return;
                // Auto-sync: primary price / factor = secondary price
                if (!_isUpdatingPrice && HasPrimaryUnit && SecondaryUnit != null)
                {
                    _isUpdatingPrice = true;
                    var ratio = PrimaryToSecondaryRatio;
                    if (ratio > 0)
                        SecondaryPrice = _calc.ConvertPrice(value, ratio);
                    _isUpdatingPrice = false;
                    RecalcComputed();
                }
                else if (!_isUpdatingPrice)
                {
                    RecalcComputed();
                }
            }
        }

        // ── Secondary Unit (base/smallest — e.g., piece) ─────────

        private ProductUnitDto _secondaryUnit;
        public ProductUnitDto SecondaryUnit
        {
            get => _secondaryUnit;
            private set
            {
                SetProperty(ref _secondaryUnit, value);
                OnPropertyChanged(nameof(HasSecondaryUnit));
                OnPropertyChanged(nameof(SecondaryUnitName));
            }
        }

        public bool HasSecondaryUnit => SecondaryUnit != null;
        public string SecondaryUnitName => SecondaryUnit?.UnitNameAr ?? "";

        private decimal _secondaryQty;
        public decimal SecondaryQty
        {
            get => _secondaryQty;
            set
            {
                if (!SetProperty(ref _secondaryQty, value) || _isUpdatingQty) return;
                _isUpdatingQty = true;
                _lastEditedIsPrimary = false;
                var ratio = PrimaryToSecondaryRatio;
                if (HasPrimaryUnit && ratio > 0)
                    PrimaryQty = _calc.ConvertPrice(value, ratio);
                _isUpdatingQty = false;
                RecalcComputed();
            }
        }

        private decimal _secondaryPrice;
        public decimal SecondaryPrice
        {
            get => _secondaryPrice;
            set
            {
                if (!SetProperty(ref _secondaryPrice, value)) return;
                // Auto-sync: secondary price × factor = primary price
                if (!_isUpdatingPrice && HasPrimaryUnit)
                {
                    _isUpdatingPrice = true;
                    var ratio = PrimaryToSecondaryRatio;
                    PrimaryPrice = _calc.ConvertQuantity(value, ratio);
                    _isUpdatingPrice = false;
                    RecalcComputed();
                }
                else if (!_isUpdatingPrice)
                {
                    RecalcComputed();
                }
            }
        }

        // ── Discount ─────────────────────────────────────────────

        private decimal _discountPercent;
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set { if (SetProperty(ref _discountPercent, value)) RecalcComputed(); }
        }

        // ── Smart Entry Data (read-only, populated by host) ─────

        private decimal _stockQty;
        public decimal StockQty { get => _stockQty; set => SetProperty(ref _stockQty, value); }

        private decimal _lastPurchasePrice;
        public decimal LastPurchasePrice { get => _lastPurchasePrice; set => SetProperty(ref _lastPurchasePrice, value); }

        private decimal _averageCost;
        public decimal AverageCost
        {
            get => _averageCost;
            set { if (SetProperty(ref _averageCost, value)) RecalcComputed(); }
        }

        private decimal? _lastSalePrice;
        public decimal? LastSalePrice { get => _lastSalePrice; set => SetProperty(ref _lastSalePrice, value); }

        // ── VAT ──────────────────────────────────────────────────

        private decimal _vatRate;
        public decimal VatRate { get => _vatRate; private set => SetProperty(ref _vatRate, value); }

        // ── Selection State ──────────────────────────────────────

        private bool _lastEditedIsPrimary;

        /// <summary>Unit ID to save (based on last-edited unit field).</summary>
        public int SelectedUnitId => _lastEditedIsPrimary && HasPrimaryUnit
            ? PrimaryUnit.UnitId
            : SecondaryUnit?.UnitId ?? 0;

        /// <summary>Quantity to save in the selected unit.</summary>
        public decimal SelectedQty => _lastEditedIsPrimary && HasPrimaryUnit
            ? PrimaryQty
            : SecondaryQty;

        /// <summary>Price to save for the selected unit.</summary>
        public decimal SelectedUnitPrice => _lastEditedIsPrimary && HasPrimaryUnit
            ? PrimaryPrice
            : SecondaryPrice;

        // ── Computed Read-Only Fields ────────────────────────────

        private decimal _unitProfit;
        public decimal UnitProfit { get => _unitProfit; private set => SetProperty(ref _unitProfit, value); }

        private decimal _totalProfit;
        public decimal TotalProfit { get => _totalProfit; private set => SetProperty(ref _totalProfit, value); }

        private decimal _lineSubtotal;
        public decimal LineSubtotal { get => _lineSubtotal; private set => SetProperty(ref _lineSubtotal, value); }

        private decimal _lineDiscount;
        public decimal LineDiscount { get => _lineDiscount; private set => SetProperty(ref _lineDiscount, value); }

        private decimal _lineVat;
        public decimal LineVat { get => _lineVat; private set => SetProperty(ref _lineVat, value); }

        private decimal _lineTotal;
        public decimal LineTotal { get => _lineTotal; private set => SetProperty(ref _lineTotal, value); }

        /// <summary>True for sale mode (show profit section).</summary>
        public bool ShowProfit => Mode == InvoicePopupMode.Sale;

        public bool IsValid => ProductId > 0
                               && SelectedUnitId > 0
                               && SelectedQty > 0
                               && SelectedUnitPrice >= 0;

        // ── Editing Support ──────────────────────────────────────

        private int? _editingLineIndex;
        public int? EditingLineIndex { get => _editingLineIndex; set => SetProperty(ref _editingLineIndex, value); }
        public bool IsEditMode => EditingLineIndex.HasValue;

        /// <summary>Price hint label based on mode.</summary>
        public string PriceHint => Mode == InvoicePopupMode.Sale ? "سعر البيع" : "سعر الشراء";

        // ── Methods ──────────────────────────────────────────────

        private void OnProductChanged()
        {
            AvailableUnits.Clear();
            PrimaryUnit = null;
            SecondaryUnit = null;

            _isUpdatingQty = true;
            _primaryQty = 0; OnPropertyChanged(nameof(PrimaryQty));
            _secondaryQty = 0; OnPropertyChanged(nameof(SecondaryQty));
            _isUpdatingQty = false;

            _isUpdatingPrice = true;
            _primaryPrice = 0; OnPropertyChanged(nameof(PrimaryPrice));
            _secondaryPrice = 0; OnPropertyChanged(nameof(SecondaryPrice));
            _isUpdatingPrice = false;

            VatRate = 0;
            AverageCost = 0;
            StockQty = 0;
            LastPurchasePrice = 0;
            LastSalePrice = null;

            if (ProductId <= 0 || _host?.Products == null) return;

            var product = _host.Products.FirstOrDefault(p => p.Id == ProductId);
            if (product == null) return;

            ProductName = product.NameAr;
            VatRate = product.VatRate;
            AverageCost = product.WeightedAverageCost;

            foreach (var unit in product.Units)
                AvailableUnits.Add(unit);

            // Primary = product default unit (business main unit)
            var primaryUnit = product.Units
                              .Where(u => u.IsDefault)
                              .OrderBy(u => u.ConversionFactor)
                              .FirstOrDefault()
                              ?? product.Units.FirstOrDefault();

            // Secondary = first non-primary unit (prefer higher conversion for piece/carton scenarios)
            var secondaryUnit = product.Units
                .Where(u => u.UnitId != primaryUnit?.UnitId)
                .OrderByDescending(u => u.ConversionFactor)
                .FirstOrDefault();

            PrimaryUnit = primaryUnit;
            SecondaryUnit = secondaryUnit;

            // Set prices based on mode (use _isUpdatingPrice to avoid cascade)
            _isUpdatingPrice = true;
            if (Mode == InvoicePopupMode.Sale)
            {
                _primaryPrice = primaryUnit?.SalePrice ?? 0;
                OnPropertyChanged(nameof(PrimaryPrice));
                _secondaryPrice = secondaryUnit?.SalePrice ?? (PrimaryToSecondaryRatio > 0 ? _calc.ConvertPrice(_primaryPrice, PrimaryToSecondaryRatio) : 0m);
                OnPropertyChanged(nameof(SecondaryPrice));
            }
            else
            {
                _primaryPrice = primaryUnit?.PurchasePrice ?? 0;
                OnPropertyChanged(nameof(PrimaryPrice));
                _secondaryPrice = secondaryUnit?.PurchasePrice ?? (PrimaryToSecondaryRatio > 0 ? _calc.ConvertPrice(_primaryPrice, PrimaryToSecondaryRatio) : 0m);
                OnPropertyChanged(nameof(SecondaryPrice));
            }
            _isUpdatingPrice = false;

            // Default to primary (bulk) unit if available, otherwise secondary
            _lastEditedIsPrimary = primaryUnit != null;
            RecalcComputed();
        }

        private void RecalcComputed()
        {
            var selectedFactor = 1m;
            if (!_lastEditedIsPrimary && HasSecondaryUnit)
            {
                var ratio = PrimaryToSecondaryRatio;
                selectedFactor = ratio > 0m ? (1m / ratio) : 1m;
            }

            var result = _calc.CalculateLine(new LineCalculationRequest
            {
                Quantity = SelectedQty,
                UnitPrice = SelectedUnitPrice,
                DiscountPercent = DiscountPercent,
                VatRate = VatRate,
                ConversionFactor = selectedFactor,
                CostPrice = AverageCost,
                IsVatInclusive = IsVatInclusive
            });

            LineSubtotal = result.SubTotal;
            LineDiscount = result.DiscountAmount;
            LineVat = result.VatAmount;
            LineTotal = result.TotalWithVat;
            UnitProfit = result.UnitProfit;
            TotalProfit = result.TotalProfit;

            OnPropertyChanged(nameof(SelectedUnitId));
            OnPropertyChanged(nameof(SelectedQty));
            OnPropertyChanged(nameof(SelectedUnitPrice));
            OnPropertyChanged(nameof(IsValid));
        }

        public void ApplyUnitPrice(decimal price)
        {
            if (_lastEditedIsPrimary && HasPrimaryUnit)
                PrimaryPrice = price;
            else
                SecondaryPrice = price;

            RecalcComputed();
        }

        /// <summary>Populates the popup state from an existing line for editing (generic).</summary>
        public void LoadFromLine(int productId, int unitId, decimal quantity, decimal unitPrice,
            decimal discountPercent, decimal stockQty, decimal lastPurchasePrice,
            decimal averageCost, decimal? lastSalePrice, int lineIndex)
        {
            EditingLineIndex = lineIndex;
            ProductId = productId; // triggers OnProductChanged

            var matchedUnit = AvailableUnits.FirstOrDefault(u => u.UnitId == unitId);
            if (matchedUnit != null)
            {
                _isUpdatingQty = true;
                _isUpdatingPrice = true;
                if (PrimaryUnit != null && matchedUnit.UnitId == PrimaryUnit.UnitId)
                {
                    _lastEditedIsPrimary = true;
                    _primaryQty = quantity; OnPropertyChanged(nameof(PrimaryQty));
                    _primaryPrice = unitPrice; OnPropertyChanged(nameof(PrimaryPrice));
                    var ratio = PrimaryToSecondaryRatio;
                    if (HasSecondaryUnit && ratio > 0)
                    {
                        _secondaryQty = _calc.ConvertQuantity(quantity, ratio);
                        OnPropertyChanged(nameof(SecondaryQty));
                        _secondaryPrice = _calc.ConvertPrice(unitPrice, ratio);
                        OnPropertyChanged(nameof(SecondaryPrice));
                    }
                }
                else
                {
                    _lastEditedIsPrimary = false;
                    _secondaryQty = quantity; OnPropertyChanged(nameof(SecondaryQty));
                    _secondaryPrice = unitPrice; OnPropertyChanged(nameof(SecondaryPrice));
                    var ratio = PrimaryToSecondaryRatio;
                    if (HasPrimaryUnit && ratio > 0)
                    {
                        _primaryQty = _calc.ConvertPrice(quantity, ratio);
                        OnPropertyChanged(nameof(PrimaryQty));
                        _primaryPrice = _calc.ConvertQuantity(unitPrice, ratio);
                        OnPropertyChanged(nameof(PrimaryPrice));
                    }
                }
                _isUpdatingQty = false;
                _isUpdatingPrice = false;
            }

            DiscountPercent = discountPercent;
            StockQty = stockQty;
            LastPurchasePrice = lastPurchasePrice;
            AverageCost = averageCost;
            LastSalePrice = lastSalePrice;

            RecalcComputed();
        }

        /// <summary>Resets all fields for a new line entry.</summary>
        public void Reset()
        {
            EditingLineIndex = null;
            ProductId = 0;
            ProductName = null;
            AvailableUnits.Clear();
            PrimaryUnit = null;
            SecondaryUnit = null;

            _isUpdatingQty = true;
            _primaryQty = 0; OnPropertyChanged(nameof(PrimaryQty));
            _secondaryQty = 0; OnPropertyChanged(nameof(SecondaryQty));
            _isUpdatingQty = false;

            _isUpdatingPrice = true;
            _primaryPrice = 0; OnPropertyChanged(nameof(PrimaryPrice));
            _secondaryPrice = 0; OnPropertyChanged(nameof(SecondaryPrice));
            _isUpdatingPrice = false;

            DiscountPercent = 0;
            StockQty = 0;
            LastPurchasePrice = 0;
            AverageCost = 0;
            LastSalePrice = null;
            VatRate = 0;
            _lastEditedIsPrimary = false;
            RecalcComputed();
        }
    }
}
