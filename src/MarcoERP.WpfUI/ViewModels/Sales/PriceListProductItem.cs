using System;
using MarcoERP.Application.Interfaces;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    // ═══════════════════════════════════════════════════════════
    //  SelectionFilterMode — what rows to show by selection state
    // ═══════════════════════════════════════════════════════════

    public enum SelectionFilterMode
    {
        All,           // الكل
        SelectedOnly,  // المحددة فقط
        UnselectedOnly // غير المحددة
    }

    // ═══════════════════════════════════════════════════════════
    //  PriceListProductItem — row model for the Excel-like grid
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Represents a single product row inside the price-list grid.
    /// Each row carries the product master data (read-only) plus
    /// editable price-list fields (selection + سعر الوحدة الكلية + سعر الوحدة الجزئية).
    /// </summary>
    public sealed class PriceListProductItem : BaseViewModel
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public int? SupplierId { get; set; }
        public decimal DefaultSalePrice { get; set; }
        public string MajorUnitName { get; set; } = string.Empty;
        public string MinorUnitName { get; set; } = string.Empty;
        public decimal MajorUnitFactor { get; set; } = 1m;
        public decimal MinorUnitFactor { get; set; } = 1m;
        private bool _isSyncingPrices;

        /// <summary>Delegates to ILineCalculationService.ConvertPrice (price / factor).</summary>
        internal Func<decimal, decimal, decimal> ConvertPrice { get; set; }
        /// <summary>Delegates to ILineCalculationService.ConvertQuantity (price * factor).</summary>
        internal Func<decimal, decimal, decimal> ConvertQuantity { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private decimal _majorUnitPrice;
        public decimal MajorUnitPrice
        {
            get => _majorUnitPrice;
            set
            {
                var normalized = value < 0 ? 0 : value;
                if (SetProperty(ref _majorUnitPrice, normalized) && !_isSyncingPrices)
                    SyncMinorFromMajor();
            }
        }

        private decimal _minorUnitPrice;
        public decimal MinorUnitPrice
        {
            get => _minorUnitPrice;
            set
            {
                var normalized = value < 0 ? 0 : value;
                if (SetProperty(ref _minorUnitPrice, normalized) && !_isSyncingPrices)
                    SyncMajorFromMinor();
            }
        }

        private decimal _partCount = 1;
        public decimal PartCount
        {
            get => _partCount;
            set
            {
                var normalized = value <= 0 ? 1 : value;
                if (SetProperty(ref _partCount, normalized))
                {
                    OnPropertyChanged(nameof(MinQuantity));
                    if (!_isSyncingPrices)
                        SyncMinorFromMajor();
                }
            }
        }

        // Backward-compatible alias
        public decimal MinQuantity
        {
            get => PartCount;
            set => PartCount = value;
        }

        private void SyncMinorFromMajor()
        {
            var divisor = PartCount <= 0 ? 1 : PartCount;
            var calculated = ConvertPrice != null
                ? ConvertPrice(MajorUnitPrice, divisor)
                : Math.Round(MajorUnitPrice / divisor, 4);

            _isSyncingPrices = true;
            try
            {
                if (_minorUnitPrice != calculated)
                {
                    _minorUnitPrice = calculated;
                    OnPropertyChanged(nameof(MinorUnitPrice));
                }
            }
            finally
            {
                _isSyncingPrices = false;
            }
        }

        private void SyncMajorFromMinor()
        {
            var multiplier = PartCount <= 0 ? 1 : PartCount;
            var calculated = ConvertQuantity != null
                ? ConvertQuantity(MinorUnitPrice, multiplier)
                : Math.Round(MinorUnitPrice * multiplier, 4);

            _isSyncingPrices = true;
            try
            {
                if (_majorUnitPrice != calculated)
                {
                    _majorUnitPrice = calculated;
                    OnPropertyChanged(nameof(MajorUnitPrice));
                }
            }
            finally
            {
                _isSyncingPrices = false;
            }
        }
    }
}
