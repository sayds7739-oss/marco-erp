using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.WpfUI.Services;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    /// <summary>
    /// ViewModel for the Price List management screen (ظ‚ظˆط§ط¦ظ… ط§ظ„ط£ط³ط¹ط§ط±).
    /// Shows ALL products in an Excel-like grid with checkboxes,
    /// inline price editing, supplier/category filtering, and PDF export.
    /// </summary>
    public sealed partial class PriceListViewModel : BaseViewModel
    {
        private readonly IPriceListService _priceListService;
        private readonly IProductService _productService;
        private readonly ISupplierService _supplierService;
        private readonly IInvoicePdfPreviewService _pdfService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILineCalculationService _lineCalculationService;
        private readonly IDialogService _dialog;
        private readonly SemaphoreSlim _loadGate = new(1, 1);

        public PriceListViewModel(
            IPriceListService priceListService,
            IProductService productService,
            ISupplierService supplierService,
            IInvoicePdfPreviewService pdfService,
            IDateTimeProvider dateTimeProvider,
            ILineCalculationService lineCalculationService,
            IDialogService dialog)
        {
            _priceListService = priceListService ?? throw new ArgumentNullException(nameof(priceListService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _pdfService = pdfService ?? throw new ArgumentNullException(nameof(pdfService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _lineCalculationService = lineCalculationService ?? throw new ArgumentNullException(nameof(lineCalculationService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            AllPriceLists = new ObservableCollection<PriceListListDto>();
            AllItems = new ObservableCollection<PriceListProductItem>();
            Suppliers = new ObservableCollection<SupplierDto>();
            Categories = new ObservableCollection<string>();

            _filteredItems = CollectionViewSource.GetDefaultView(AllItems);
            _filteredItems.Filter = FilterPredicate;

            LoadCommand = new AsyncRelayCommand(LoadAsync);
            NewCommand = new AsyncRelayCommand(_ => PrepareNewAsync());
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedPriceList != null && !IsNew);
            CancelCommand = new RelayCommand(CancelEditing);
            PrintCommand = new AsyncRelayCommand(PrintAsync, () => SelectedPriceList != null || IsNew);
            SelectAllCommand = new RelayCommand(_ => SetAllSelection(true));
            DeselectAllCommand = new RelayCommand(_ => SetAllSelection(false));
            CopyDefaultPricesCommand = new RelayCommand(_ => CopyDefaultPrices());
            SelectVisibleCommand = new RelayCommand(_ => SelectVisible());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
        }

        // â•گâ•گâ•گâ•گâ•گâ•گ Collections â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

        public ObservableCollection<PriceListListDto> AllPriceLists { get; }
        public ObservableCollection<PriceListProductItem> AllItems { get; }
        public ObservableCollection<SupplierDto> Suppliers { get; }
        public ObservableCollection<string> Categories { get; }

        private readonly ICollectionView _filteredItems;
        public ICollectionView FilteredItems => _filteredItems;

        // â•گâ•گâ•گâ•گâ•گâ•گ Price List Selection â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

        private PriceListListDto _selectedPriceList;
        public PriceListListDto SelectedPriceList
        {
            get => _selectedPriceList;
            set
            {
                if (SetProperty(ref _selectedPriceList, value))
                {
                    if (value != null)
                    {
                        IsEditing = true;
                        IsNew = false;
                        _ = LoadPriceListDetailAsync(value.Id);
                    }
                }
            }
        }

        // â•گâ•گâ•گâ•گâ•گâ•گ Filter Properties â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
        }

        private int? _filterSupplierId;
        public int? FilterSupplierId
        {
            get => _filterSupplierId;
            set { if (SetProperty(ref _filterSupplierId, value)) ApplyFilter(); }
        }

        private string _filterCategoryName = string.Empty;
        public string FilterCategoryName
        {
            get => _filterCategoryName;
            set { if (SetProperty(ref _filterCategoryName, value)) ApplyFilter(); }
        }

        private SelectionFilterMode _filterSelectionMode = SelectionFilterMode.All;
        public SelectionFilterMode FilterSelectionMode
        {
            get => _filterSelectionMode;
            set { if (SetProperty(ref _filterSelectionMode, value)) ApplyFilter(); }
        }

        /// <summary>
        /// Int wrapper for XAML ComboBox SelectedIndex binding.
        /// 0 = All, 1 = SelectedOnly, 2 = UnselectedOnly.
        /// </summary>
        public int FilterSelectionModeIndex
        {
            get => (int)_filterSelectionMode;
            set
            {
                FilterSelectionMode = (SelectionFilterMode)value;
                OnPropertyChanged(nameof(FilterSelectionModeIndex));
            }
        }

        private string _filterCodeFrom = string.Empty;
        public string FilterCodeFrom
        {
            get => _filterCodeFrom;
            set { if (SetProperty(ref _filterCodeFrom, value)) ApplyFilter(); }
        }

        private string _filterCodeTo = string.Empty;
        public string FilterCodeTo
        {
            get => _filterCodeTo;
            set { if (SetProperty(ref _filterCodeTo, value)) ApplyFilter(); }
        }

        // â•گâ•گâ•گâ•گâ•گâ•گ Form Fields â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

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

        private string _formCode = string.Empty;
        public string FormCode
        {
            get => _formCode;
            set => SetProperty(ref _formCode, value);
        }

        private string _formNameAr = string.Empty;
        public string FormNameAr
        {
            get => _formNameAr;
            set { SetProperty(ref _formNameAr, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private string _formNameEn = string.Empty;
        public string FormNameEn
        {
            get => _formNameEn;
            set => SetProperty(ref _formNameEn, value);
        }

        private string _formDescription = string.Empty;
        public string FormDescription
        {
            get => _formDescription;
            set => SetProperty(ref _formDescription, value);
        }

        private DateTime? _formValidFrom;
        public DateTime? FormValidFrom
        {
            get => _formValidFrom;
            set => SetProperty(ref _formValidFrom, value);
        }

        private DateTime? _formValidTo;
        public DateTime? FormValidTo
        {
            get => _formValidTo;
            set => SetProperty(ref _formValidTo, value);
        }

        private bool _formIsActive = true;
        public bool FormIsActive
        {
            get => _formIsActive;
            set => SetProperty(ref _formIsActive, value);
        }

        // â•گâ•گâ•گâ•گâ•گâ•گ Computed â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

        public bool CanSave => IsEditing && !string.IsNullOrWhiteSpace(FormNameAr);

        public int SelectedCount => AllItems.Count(i => i.IsSelected);
        public int TotalVisible => _filteredItems.Cast<object>().Count();

        private void RefreshCounts()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(TotalVisible));
        }

        // â•گâ•گâ•گâ•گâ•گâ•گ Commands â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

        public ICommand LoadCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand CopyDefaultPricesCommand { get; }
        public ICommand SelectVisibleCommand { get; }
        public ICommand ClearFiltersCommand { get; }

        // â•گâ•گâ•گâ•گâ•گâ•گ Filtering â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

        private void ApplyFilter()
        {
            _filteredItems.Refresh();
            RefreshCounts();
        }

        private bool FilterPredicate(object obj)
        {
            if (obj is not PriceListProductItem item) return false;

            // Selection mode filter
            if (FilterSelectionMode == SelectionFilterMode.SelectedOnly && !item.IsSelected)
                return false;
            if (FilterSelectionMode == SelectionFilterMode.UnselectedOnly && item.IsSelected)
                return false;

            // Text search (name or code)
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.Trim();
                if (!item.ProductName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                    !item.ProductCode.Contains(search, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Code range filter
            if (!string.IsNullOrWhiteSpace(FilterCodeFrom))
            {
                if (string.Compare(item.ProductCode, FilterCodeFrom.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }
            if (!string.IsNullOrWhiteSpace(FilterCodeTo))
            {
                if (string.Compare(item.ProductCode, FilterCodeTo.Trim(), StringComparison.OrdinalIgnoreCase) > 0)
                    return false;
            }

            if (FilterSupplierId.HasValue && FilterSupplierId > 0 && item.SupplierId != FilterSupplierId)
                return false;

            if (!string.IsNullOrWhiteSpace(FilterCategoryName) && item.CategoryName != FilterCategoryName)
                return false;

            return true;
        }
    }
}
