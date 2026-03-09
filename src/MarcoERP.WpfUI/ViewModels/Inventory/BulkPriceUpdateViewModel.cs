using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.WpfUI.Common;

namespace MarcoERP.WpfUI.ViewModels.Inventory
{
    /// <summary>
    /// ViewModel for Bulk Price Update screen.
    /// </summary>
    public sealed class BulkPriceUpdateViewModel : BaseViewModel
    {
        private const string PercentageMode = "Percentage";
        private const string DirectMode = "Direct";
        private const string SalePriceTarget = "SalePrice";
        private const string MajorUnitLevel = "MajorUnit";
        private const string MinorUnitLevel = "MinorUnit";

        private readonly IProductService _productService;
        private readonly IBulkPriceUpdateService _bulkService;
        private readonly ILineCalculationService _lineCalculationService;
        private readonly IDialogService _dialog;

        public BulkPriceUpdateViewModel(IProductService productService, IBulkPriceUpdateService bulkService, ILineCalculationService lineCalculationService, IDialogService dialog)
        {
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _bulkService = bulkService ?? throw new ArgumentNullException(nameof(bulkService));
            _lineCalculationService = lineCalculationService ?? throw new ArgumentNullException(nameof(lineCalculationService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            AllProducts = new ObservableCollection<SelectableProduct>();
            PreviewItems = new ObservableCollection<BulkPricePreviewItemDto>();

            LoadCommand = new AsyncRelayCommand(LoadProductsAsync);
            SelectAllCommand = new RelayCommand(_ => SelectAll(true));
            DeselectAllCommand = new RelayCommand(_ => SelectAll(false));
            PreviewCommand = new AsyncRelayCommand(PreviewAsync, () => CanPreview);
            ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => CanApply);
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<SelectableProduct> AllProducts { get; }
        public ObservableCollection<BulkPricePreviewItemDto> PreviewItems { get; }

        // ── Form Fields ─────────────────────────────────────────

        private string _selectedMode = PercentageMode;
        public string SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (SetProperty(ref _selectedMode, value))
                {
                    OnPropertyChanged(nameof(IsPercentageMode));
                    OnPropertyChanged(nameof(IsDirectMode));
                    OnPropertyChanged(nameof(CanPreview));
                }
            }
        }

        public bool IsPercentageMode
        {
            get => SelectedMode == PercentageMode;
            set
            {
                if (value && SelectedMode != PercentageMode)
                    SelectedMode = PercentageMode;
            }
        }

        public bool IsDirectMode
        {
            get => SelectedMode == DirectMode;
            set
            {
                if (value && SelectedMode != DirectMode)
                    SelectedMode = DirectMode;
            }
        }

        private string _selectedPriceTarget = SalePriceTarget;
        public string SelectedPriceTarget
        {
            get => _selectedPriceTarget;
            set => SetProperty(ref _selectedPriceTarget, value);
        }

        private string _selectedUnitLevel = MajorUnitLevel;
        public string SelectedUnitLevel
        {
            get => _selectedUnitLevel;
            set
            {
                if (SetProperty(ref _selectedUnitLevel, value))
                {
                    OnPropertyChanged(nameof(IsMajorUnitLevel));
                    OnPropertyChanged(nameof(IsMinorUnitLevel));
                    OnPropertyChanged(nameof(ShowMajorColumns));
                    OnPropertyChanged(nameof(ShowMinorColumns));
                    OnPropertyChanged(nameof(ShowMajorPreviewColumns));
                    OnPropertyChanged(nameof(ShowMinorPreviewColumns));
                    OnPropertyChanged(nameof(CanPreview));
                }
            }
        }

        public bool IsMajorUnitLevel
        {
            get => SelectedUnitLevel == MajorUnitLevel;
            set
            {
                if (value && SelectedUnitLevel != MajorUnitLevel)
                    SelectedUnitLevel = MajorUnitLevel;
            }
        }

        public bool IsMinorUnitLevel
        {
            get => SelectedUnitLevel == MinorUnitLevel;
            set
            {
                if (value && SelectedUnitLevel != MinorUnitLevel)
                    SelectedUnitLevel = MinorUnitLevel;
            }
        }

        public bool ShowMajorColumns => SelectedUnitLevel == MajorUnitLevel;
        public bool ShowMinorColumns => SelectedUnitLevel == MinorUnitLevel;
        public bool ShowMajorPreviewColumns => ShowPreview && ShowMajorColumns;
        public bool ShowMinorPreviewColumns => ShowPreview && ShowMinorColumns;

        private bool _syncByConversion = true;
        public bool SyncByConversion
        {
            get => _syncByConversion;
            set => SetProperty(ref _syncByConversion, value);
        }

        private decimal _percentageChange;
        public decimal PercentageChange
        {
            get => _percentageChange;
            set { SetProperty(ref _percentageChange, value); OnPropertyChanged(nameof(CanPreview)); }
        }

        private decimal _directPrice;
        public decimal DirectPrice
        {
            get => _directPrice;
            set { SetProperty(ref _directPrice, value); OnPropertyChanged(nameof(CanPreview)); }
        }

        private bool _showPreview;
        public bool ShowPreview
        {
            get => _showPreview;
            set
            {
                if (SetProperty(ref _showPreview, value) && !value)
                {
                    // Clear inline preview when hiding
                    foreach (var p in AllProducts)
                        p.ClearPreview();
                }

                OnPropertyChanged(nameof(ShowMajorPreviewColumns));
                OnPropertyChanged(nameof(ShowMinorPreviewColumns));
            }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    FilterProducts();
            }
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand LoadCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand PreviewCommand { get; }
        public ICommand ApplyCommand { get; }

        // ── Can Execute ──────────────────────────────────────────

        public bool CanPreview
        {
            get
            {
                var anySelected = AllProducts.Any(p => p.IsSelected);
                if (!anySelected) return false;
                if (SelectedUnitLevel == MinorUnitLevel && !AllProducts.Any(p => p.IsSelected && p.HasMinorUnit)) return false;
                if (SelectedMode == PercentageMode && PercentageChange == 0) return false;
                if (SelectedMode == DirectMode && DirectPrice < 0) return false;
                return true;
            }
        }

        public bool CanApply => ShowPreview && PreviewItems.Count > 0;

        public int SelectedCount => AllProducts.Count(p => p.IsSelected);

        // ── Load ─────────────────────────────────────────────────

        private readonly ObservableCollection<SelectableProduct> _allProductsBackup = new();

        public async Task LoadProductsAsync()
        {
            IsBusy = true;
            ClearError();
            ShowPreview = false;
            try
            {
                var result = await _productService.GetAllAsync();
                if (result.IsSuccess)
                {
                    AllProducts.Clear();
                    _allProductsBackup.Clear();
                    foreach (var p in result.Data)
                    {
                        var minorUnit = p.Units?
                            .Where(u => u.UnitId != p.BaseUnitId && u.ConversionFactor > 0)
                            .OrderByDescending(u => u.ConversionFactor)
                            .FirstOrDefault();

                        var minorSalePrice = minorUnit?.SalePrice ?? (minorUnit != null
                            ? _lineCalculationService.ConvertPrice(p.DefaultSalePrice, minorUnit.ConversionFactor)
                            : 0);

                        var minorCostPrice = minorUnit?.PurchasePrice ?? (minorUnit != null
                            ? _lineCalculationService.ConvertPrice(p.CostPrice, minorUnit.ConversionFactor)
                            : 0);

                        var sp = new SelectableProduct
                        {
                            Id = p.Id,
                            Code = p.Code,
                            NameAr = p.NameAr,
                            BaseUnitName = p.BaseUnitName,
                            DefaultSalePrice = p.DefaultSalePrice,
                            CostPrice = p.CostPrice,
                            MinorUnitName = minorUnit?.UnitNameAr,
                            MinorUnitFactor = minorUnit?.ConversionFactor ?? 0,
                            MinorSalePrice = minorSalePrice,
                            MinorCostPrice = minorCostPrice,
                            HasMinorUnit = minorUnit != null,
                            IsSelected = false
                        };
                        AllProducts.Add(sp);
                        _allProductsBackup.Add(sp);
                    }
                    StatusMessage = $"تم تحميل {AllProducts.Count} صنف";
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
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

        // ── Select All / Deselect All ───────────────────────────

        private void SelectAll(bool selected)
        {
            foreach (var p in AllProducts)
                p.IsSelected = selected;
            OnPropertyChanged(nameof(CanPreview));
            OnPropertyChanged(nameof(SelectedCount));
        }

        // ── Preview ──────────────────────────────────────────────

        private async Task PreviewAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var request = BuildRequest();
                var result = await _bulkService.PreviewAsync(request);
                if (result.IsSuccess)
                {
                    PreviewItems.Clear();
                    // Populate inline preview on each product row
                    var lookup = result.Data.ToDictionary(x => x.ProductId);
                    foreach (var p in AllProducts)
                    {
                        if (lookup.TryGetValue(p.Id, out var preview))
                        {
                            p.NewPrice = preview.NewPrice;
                            p.Difference = preview.Difference;
                            p.PercentageChange = preview.PercentageChange;
                            p.PreviewMajorPrice = preview.NewMajorPrice;
                            p.PreviewMinorPrice = preview.NewMinorPrice;
                        }
                        else
                        {
                            p.ClearPreview();
                        }
                    }
                    foreach (var item in result.Data)
                        PreviewItems.Add(item);
                    ShowPreview = true;
                    OnPropertyChanged(nameof(CanApply));
                    StatusMessage = $"معاينة {PreviewItems.Count} صنف — راجع التغييرات ثم اضغط تطبيق";
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("المعاينة", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Apply ────────────────────────────────────────────────

        private async Task ApplyAsync()
        {
            if (!_dialog.Confirm($"سيتم تحديث أسعار {PreviewItems.Count} صنف.\nهل أنت متأكد؟", "تأكيد التحديث")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var request = BuildRequest();
                var result = await _bulkService.ApplyAsync(request);
                if (result.IsSuccess)
                {
                    var data = result.Data;
                    StatusMessage = $"تم تحديث {data.UpdatedCount} صنف بنجاح";
                    if (data.FailedCount > 0)
                        ErrorMessage = $"فشل تحديث {data.FailedCount} صنف: {string.Join(" | ", data.Errors)}";

                    ShowPreview = false;
                    PreviewItems.Clear();
                    await LoadProductsAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("التطبيق", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Helpers ──────────────────────────────────────────────

        private BulkPriceUpdateRequestDto BuildRequest()
        {
            var selectedIds = AllProducts
                .Where(p => p.IsSelected && (SelectedUnitLevel != MinorUnitLevel || p.HasMinorUnit))
                .Select(p => p.Id)
                .ToList();

            return new BulkPriceUpdateRequestDto
            {
                ProductIds = selectedIds,
                Mode = SelectedMode,
                PercentageChange = PercentageChange,
                DirectPrice = DirectPrice,
                PriceTarget = SelectedPriceTarget,
                UnitLevel = SelectedUnitLevel,
                SyncByConversion = SyncByConversion
            };
        }

        private void FilterProducts()
        {
            AllProducts.Clear();
            var term = SearchText?.Trim().ToLower() ?? "";
            foreach (var p in _allProductsBackup)
            {
                if (string.IsNullOrEmpty(term) ||
                    (p.Code?.ToLower().Contains(term) ?? false) ||
                    (p.NameAr?.ToLower().Contains(term) ?? false))
                {
                    AllProducts.Add(p);
                }
            }
        }
    }

    /// <summary>
    /// Wrapper for products with selection state and inline preview.
    /// </summary>
    public sealed class SelectableProduct : BaseViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string NameAr { get; set; }
        public string BaseUnitName { get; set; }
        public decimal DefaultSalePrice { get; set; }
        public decimal CostPrice { get; set; }
        public string MinorUnitName { get; set; }
        public decimal MinorUnitFactor { get; set; }
        public decimal MinorSalePrice { get; set; }
        public decimal MinorCostPrice { get; set; }
        public bool HasMinorUnit { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // ── Preview fields (populated after PreviewAsync) ──

        private decimal _newPrice;
        public decimal NewPrice
        {
            get => _newPrice;
            set => SetProperty(ref _newPrice, value);
        }

        private decimal _difference;
        public decimal Difference
        {
            get => _difference;
            set => SetProperty(ref _difference, value);
        }

        private decimal _percentageChange;
        public decimal PercentageChange
        {
            get => _percentageChange;
            set => SetProperty(ref _percentageChange, value);
        }

        private decimal _previewMajorPrice;
        public decimal PreviewMajorPrice
        {
            get => _previewMajorPrice;
            set => SetProperty(ref _previewMajorPrice, value);
        }

        private decimal _previewMinorPrice;
        public decimal PreviewMinorPrice
        {
            get => _previewMinorPrice;
            set => SetProperty(ref _previewMinorPrice, value);
        }

        public void ClearPreview()
        {
            NewPrice = 0;
            Difference = 0;
            PercentageChange = 0;
            PreviewMajorPrice = 0;
            PreviewMinorPrice = 0;
        }
    }
}
