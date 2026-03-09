using System;
using System.Collections.ObjectModel;
using System.Linq;
using MarcoERP.Application.DTOs.Common;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces;

namespace MarcoERP.WpfUI.ViewModels.Purchases
{
    /// <summary>
    /// Represents a single line in the purchase invoice form.
    /// Computed totals are display previews; authoritative values come from the Application service.
    /// </summary>
    public sealed class PurchaseInvoiceLineFormItem : BaseViewModel
    {
        private readonly IInvoiceLineFormHost _parent;

        public int Id { get; set; }

        public PurchaseInvoiceLineFormItem(IInvoiceLineFormHost parent)
        {
            _parent = parent;
            _quantity = 1m;
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
