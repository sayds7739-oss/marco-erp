using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Common;

namespace MarcoERP.WpfUI.ViewModels.Inventory
{
    /// <summary>
    /// ViewModel for Product management screen.
    /// Handles CRUD with multi-unit support, category/unit dropdowns, and auto-code generation.
    /// </summary>
    public sealed class ProductViewModel : BaseViewModel
    {
        // ── Services ────────────────────────────────────────────
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IUnitService _unitService;
        private readonly ISupplierService _supplierService;
        private readonly ILineCalculationService _lineCalculationService;
        private readonly IDialogService _dialog;

        // ── Collections ───────────────────────────────────────────
        public ObservableCollection<ProductDto> AllProducts { get; } = new();
        public ObservableCollection<CategoryDto> Categories { get; } = new();
        public ObservableCollection<UnitDto> Units { get; } = new();
        public ObservableCollection<SupplierDto> Suppliers { get; } = new();
        public ObservableCollection<ProductUnitFormItem> FormUnits { get; } = new();

        // ── State ───────────────────────────────────────────────
        private ProductDto _selectedItem;
        public ProductDto SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value) && value != null && !IsEditing)
                {
                    PopulateForm(value);
                }
                OnPropertyChanged(nameof(CanDeactivate));
                OnPropertyChanged(nameof(CanActivate));
                OnPropertyChanged(nameof(CanDelete));
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

        // ── Form Fields (Product header) ────────────────────────
        private string _formCode;
        public string FormCode
        {
            get => _formCode;
            set { SetProperty(ref _formCode, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private string _formNameAr;
        public string FormNameAr
        {
            get => _formNameAr;
            set { SetProperty(ref _formNameAr, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private string _formNameEn;
        public string FormNameEn
        {
            get => _formNameEn;
            set => SetProperty(ref _formNameEn, value);
        }

        private int? _formCategoryId;
        public int? FormCategoryId
        {
            get => _formCategoryId;
            set { SetProperty(ref _formCategoryId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private int? _formBaseUnitId;
        public int? FormBaseUnitId
        {
            get => _formBaseUnitId;
            set { SetProperty(ref _formBaseUnitId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private decimal _formCostPrice;
        public decimal FormCostPrice
        {
            get => _formCostPrice;
            set
            {
                if (SetProperty(ref _formCostPrice, value))
                    RefreshUnitPricesFromBase();
            }
        }

        private decimal _formDefaultSalePrice;
        public decimal FormDefaultSalePrice
        {
            get => _formDefaultSalePrice;
            set
            {
                if (SetProperty(ref _formDefaultSalePrice, value))
                    RefreshUnitPricesFromBase();
            }
        }

        private decimal _formMinimumStock;
        public decimal FormMinimumStock
        {
            get => _formMinimumStock;
            set => SetProperty(ref _formMinimumStock, value);
        }

        private decimal _formReorderLevel;
        public decimal FormReorderLevel
        {
            get => _formReorderLevel;
            set => SetProperty(ref _formReorderLevel, value);
        }

        private decimal _formVatRate;
        public decimal FormVatRate
        {
            get => _formVatRate;
            set => SetProperty(ref _formVatRate, value);
        }

        private string _formBarcode;
        public string FormBarcode
        {
            get => _formBarcode;
            set => SetProperty(ref _formBarcode, value);
        }

        private string _formDescription;
        public string FormDescription
        {
            get => _formDescription;
            set => SetProperty(ref _formDescription, value);
        }

        private int? _formDefaultSupplierId;
        public int? FormDefaultSupplierId
        {
            get => _formDefaultSupplierId;
            set => SetProperty(ref _formDefaultSupplierId, value);
        }

        private decimal _formWholesalePrice;
        public decimal FormWholesalePrice
        {
            get => _formWholesalePrice;
            set => SetProperty(ref _formWholesalePrice, value);
        }

        private decimal _formRetailPrice;
        public decimal FormRetailPrice
        {
            get => _formRetailPrice;
            set => SetProperty(ref _formRetailPrice, value);
        }

        private string _formImagePath;
        public string FormImagePath
        {
            get => _formImagePath;
            set => SetProperty(ref _formImagePath, value);
        }

        private decimal _formMaximumStock;
        public decimal FormMaximumStock
        {
            get => _formMaximumStock;
            set => SetProperty(ref _formMaximumStock, value);
        }

        // ── CanExecute Guards ───────────────────────────────────
        public bool CanSave => !string.IsNullOrWhiteSpace(FormNameAr)
                               && FormCategoryId.HasValue && FormCategoryId > 0
                               && FormBaseUnitId.HasValue && FormBaseUnitId > 0
                               && (!IsNew || !string.IsNullOrWhiteSpace(FormCode));

        public bool CanDeactivate => SelectedItem != null && SelectedItem.Status == "Active";
        public bool CanActivate => SelectedItem != null && SelectedItem.Status != "Active";
        public bool CanDelete => SelectedItem != null;

        // ── Commands ────────────────────────────────────────────
        public AsyncRelayCommand LoadCommand { get; }
        public AsyncRelayCommand NewCommand { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand DeleteCommand { get; }
        public AsyncRelayCommand ActivateCommand { get; }
        public AsyncRelayCommand DeactivateCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand AddUnitCommand { get; }
        public RelayCommand RemoveUnitCommand { get; }
        public RelayCommand EditSelectedCommand { get; }

        // ── Constructor ─────────────────────────────────────────
        public ProductViewModel(
            IProductService productService,
            ICategoryService categoryService,
            IUnitService unitService,
            ISupplierService supplierService,
            ILineCalculationService lineCalculationService,
            IDialogService dialog)
        {
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _unitService = unitService ?? throw new ArgumentNullException(nameof(unitService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _lineCalculationService = lineCalculationService ?? throw new ArgumentNullException(nameof(lineCalculationService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            LoadCommand = new AsyncRelayCommand(LoadProductsAsync);
            NewCommand = new AsyncRelayCommand(PrepareNewAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => CanDelete);
            ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => CanActivate);
            DeactivateCommand = new AsyncRelayCommand(DeactivateAsync, () => CanDeactivate);
            CancelCommand = new RelayCommand(CancelEditing);
            AddUnitCommand = new RelayCommand(AddUnit);
            RemoveUnitCommand = new RelayCommand(RemoveUnit);
            EditSelectedCommand = new RelayCommand(_ => EditSelected());
        }

        // ── Load ────────────────────────────────────────────────
        public async Task LoadProductsAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                // Load products
                var productsResult = await _productService.GetAllAsync();
                AllProducts.Clear();
                if (productsResult.IsSuccess)
                    foreach (var p in productsResult.Data) AllProducts.Add(p);

                // Load active categories for dropdown
                var categoriesResult = await _categoryService.GetAllAsync();
                Categories.Clear();
                if (categoriesResult.IsSuccess)
                    foreach (var c in categoriesResult.Data.Where(x => x.IsActive))
                        Categories.Add(c);

                // Load active units for dropdown
                var unitsResult = await _unitService.GetActiveAsync();
                Units.Clear();
                if (unitsResult.IsSuccess)
                    foreach (var u in unitsResult.Data) Units.Add(u);

                // Load suppliers for dropdown
                var suppliersResult = await _supplierService.GetAllAsync();
                Suppliers.Clear();
                if (suppliersResult.IsSuccess)
                    foreach (var s in suppliersResult.Data.Where(x => x.IsActive))
                        Suppliers.Add(s);

                StatusMessage = $"تم تحميل {AllProducts.Count} صنف";
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تحميل البيانات", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Prepare New ─────────────────────────────────────────
        private async Task PrepareNewAsync()
        {
            IsEditing = true;
            IsNew = true;
            ClearError();

            // Auto-generate code
            try
            {
                var codeResult = await _productService.GetNextCodeAsync();
                FormCode = codeResult.IsSuccess ? codeResult.Data : "";
            }
            catch
            {
                FormCode = "";
            }

            FormNameAr = "";
            FormNameEn = "";
            FormCategoryId = null;
            FormBaseUnitId = null;
            FormCostPrice = 0;
            FormDefaultSalePrice = 0;
            FormMinimumStock = 0;
            FormReorderLevel = 0;
            FormVatRate = 0;
            FormBarcode = "";
            FormDescription = "";
            FormDefaultSupplierId = null;
            FormWholesalePrice = 0;
            FormRetailPrice = 0;
            FormImagePath = "";
            FormMaximumStock = 0;
            FormUnits.Clear();

            StatusMessage = "إنشاء صنف جديد...";
        }

        // ── Save ────────────────────────────────────────────────
        private async Task SaveAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                if (IsNew)
                {
                    var dto = new CreateProductDto
                    {
                        Code = FormCode?.Trim(),
                        NameAr = FormNameAr?.Trim(),
                        NameEn = FormNameEn?.Trim(),
                        CategoryId = FormCategoryId ?? 0,
                        BaseUnitId = FormBaseUnitId ?? 0,
                        CostPrice = FormCostPrice,
                        DefaultSalePrice = FormDefaultSalePrice,
                        WholesalePrice = FormWholesalePrice,
                        RetailPrice = FormRetailPrice,
                        ImagePath = FormImagePath?.Trim(),
                        MaximumStock = FormMaximumStock,
                        MinimumStock = FormMinimumStock,
                        ReorderLevel = FormReorderLevel,
                        VatRate = FormVatRate,
                        Barcode = FormBarcode?.Trim(),
                        Description = FormDescription?.Trim(),
                        DefaultSupplierId = FormDefaultSupplierId,
                        Units = FormUnits.Select(u => new CreateProductUnitDto
                        {
                            UnitId = u.SelectedUnitId,
                            ConversionFactor = u.ConversionFactor,
                            SalePrice = u.SalePrice,
                            PurchasePrice = u.PurchasePrice,
                            Barcode = u.Barcode?.Trim(),
                            IsDefault = u.IsDefault
                        }).ToList()
                    };

                    var result = await _productService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء الصنف «{result.Data.NameAr}» بنجاح";
                        IsEditing = false;
                        IsNew = false;
                        await LoadProductsAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else
                {
                    // Update — send all units (service handles sync)
                    var dto = new UpdateProductDto
                    {
                        Id = SelectedItem.Id,
                        NameAr = FormNameAr?.Trim(),
                        NameEn = FormNameEn?.Trim(),
                        CategoryId = FormCategoryId ?? 0,
                        CostPrice = FormCostPrice,
                        DefaultSalePrice = FormDefaultSalePrice,
                        WholesalePrice = FormWholesalePrice,
                        RetailPrice = FormRetailPrice,
                        ImagePath = FormImagePath?.Trim(),
                        MaximumStock = FormMaximumStock,
                        MinimumStock = FormMinimumStock,
                        ReorderLevel = FormReorderLevel,
                        VatRate = FormVatRate,
                        Barcode = FormBarcode?.Trim(),
                        Description = FormDescription?.Trim(),
                        DefaultSupplierId = FormDefaultSupplierId,
                        Units = FormUnits.Select(u => new CreateProductUnitDto
                        {
                            UnitId = u.SelectedUnitId,
                            ConversionFactor = u.ConversionFactor,
                            SalePrice = u.SalePrice,
                            PurchasePrice = u.PurchasePrice,
                            Barcode = u.Barcode?.Trim(),
                            IsDefault = u.IsDefault
                        }).ToList()
                    };

                    var result = await _productService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث الصنف «{result.Data.NameAr}» بنجاح";
                        IsEditing = false;
                        IsNew = false;
                        await LoadProductsAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
            }
            catch (ConcurrencyConflictException ex)
            {
                await ConcurrencyHelper.ShowConflictAndRefreshAsync(ex, LoadProductsAsync);
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("الحفظ", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Delete (soft) ───────────────────────────────────────
        private async Task DeleteAsync()
        {
            if (SelectedItem == null) return;

            if (!_dialog.Confirm($"هل تريد حذف الصنف «{SelectedItem.NameAr}»؟\nالحذف نهائي ولا يمكن التراجع عنه.", "تأكيد الحذف")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _productService.DeleteAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم حذف الصنف بنجاح";
                    IsEditing = false;
                    IsNew = false;
                    await LoadProductsAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("الحذف", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Activate / Deactivate ───────────────────────────────
        private async Task ActivateAsync()
        {
            if (SelectedItem == null) return;
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _productService.ActivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم تفعيل الصنف «{SelectedItem.NameAr}»";
                    await LoadProductsAsync();
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("تفعيل الصنف", ex); }
            finally { IsBusy = false; }
        }

        private async Task DeactivateAsync()
        {
            if (SelectedItem == null) return;

            if (!_dialog.Confirm($"هل تريد تعطيل الصنف «{SelectedItem.NameAr}»؟", "تأكيد التعطيل")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _productService.DeactivateAsync(SelectedItem.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم تعطيل الصنف «{SelectedItem.NameAr}»";
                    await LoadProductsAsync();
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("تعطيل الصنف", ex); }
            finally { IsBusy = false; }
        }

        // ── Unit Management ─────────────────────────────────────
        private void AddUnit(object _)
        {
            if (!IsEditing) return;
            var item = new ProductUnitFormItem();
            item.SetBasePriceProvider(() => (FormDefaultSalePrice, FormCostPrice));
            item.SetPriceConverter(_lineCalculationService.ConvertPrice);
            FormUnits.Add(item);
        }

        private void RemoveUnit(object parameter)
        {
            if (!IsEditing) return;
            if (parameter is ProductUnitFormItem item)
            {
                // Prevent removing the base unit in edit mode
                if (!IsNew && SelectedItem != null && item.SelectedUnitId == SelectedItem.BaseUnitId)
                {
                    ErrorMessage = "لا يمكن حذف الوحدة الأساسية.";
                    return;
                }
                FormUnits.Remove(item);
            }
        }

        // ── Form Helpers ────────────────────────────────────────
        private void PopulateForm(ProductDto product)
        {
            FormCode = product.Code;
            FormNameAr = product.NameAr;
            FormNameEn = product.NameEn;
            FormCategoryId = product.CategoryId;
            FormBaseUnitId = product.BaseUnitId;
            FormCostPrice = product.CostPrice;
            FormDefaultSalePrice = product.DefaultSalePrice;
            FormMinimumStock = product.MinimumStock;
            FormReorderLevel = product.ReorderLevel;
            FormVatRate = product.VatRate;
            FormBarcode = product.Barcode;
            FormDescription = product.Description;
            FormDefaultSupplierId = product.DefaultSupplierId;
            FormWholesalePrice = product.WholesalePrice;
            FormRetailPrice = product.RetailPrice;
            FormImagePath = product.ImagePath;
            FormMaximumStock = product.MaximumStock;

            // Populate units
            FormUnits.Clear();
            foreach (var u in product.Units ?? new List<ProductUnitDto>())
            {
                var item = new ProductUnitFormItem();
                item.SetBasePriceProvider(() => (FormDefaultSalePrice, FormCostPrice));
                item.SetPriceConverter(_lineCalculationService.ConvertPrice);
                item.LoadWithoutAutoCalc(u.UnitId, u.ConversionFactor, u.SalePrice, u.PurchasePrice, u.Barcode, u.IsDefault);
                FormUnits.Add(item);
            }

            IsEditing = false;
            IsNew = false;
        }

        public void EditSelected()
        {
            if (SelectedItem == null) return;
            PopulateForm(SelectedItem);
            IsEditing = true;
            IsNew = false;
        }

        private void CancelEditing(object _)
        {
            IsEditing = false;
            IsNew = false;
            ClearError();
            if (SelectedItem != null)
                PopulateForm(SelectedItem);
            else
                ClearForm();
            StatusMessage = "تم الإلغاء";
        }

        private void ClearForm()
        {
            FormCode = "";
            FormNameAr = "";
            FormNameEn = "";
            FormCategoryId = null;
            FormBaseUnitId = null;
            FormCostPrice = 0;
            FormDefaultSalePrice = 0;
            FormMinimumStock = 0;
            FormReorderLevel = 0;
            FormVatRate = 0;
            FormBarcode = "";
            FormDescription = "";
            FormDefaultSupplierId = null;
            FormWholesalePrice = 0;
            FormRetailPrice = 0;
            FormImagePath = "";
            FormMaximumStock = 0;
            FormUnits.Clear();
        }

        private void RefreshUnitPricesFromBase()
        {
            foreach (var unit in FormUnits)
                unit.AutoCalcPrices();
        }
    }

    // ════════════════════════════════════════════════════════════
    //  ProductUnit Form Item — represents one row in the units sub-grid
    //  Auto-calculates SalePrice/PurchasePrice when ConversionFactor changes
    //  based on the base unit prices from the parent ViewModel.
    // ════════════════════════════════════════════════════════════
    public sealed class ProductUnitFormItem : BaseViewModel
    {
        private Func<(decimal salePrice, decimal costPrice)> _getBasePrices;
        private Func<decimal, decimal, decimal> _convertPrice;
        private bool _suppressAutoCalc;
        private bool _isAutoCalc;
        private bool _salePriceManual;
        private bool _purchasePriceManual;

        /// <summary>
        /// Sets the base price provider for auto-calculation.
        /// Called by ProductViewModel when adding a unit.
        /// </summary>
        public void SetBasePriceProvider(Func<(decimal salePrice, decimal costPrice)> provider)
        {
            _getBasePrices = provider;
        }

        /// <summary>
        /// Sets the price converter delegate (delegates to ILineCalculationService.ConvertPrice).
        /// </summary>
        public void SetPriceConverter(Func<decimal, decimal, decimal> converter)
        {
            _convertPrice = converter;
        }

        private int _selectedUnitId;
        public int SelectedUnitId
        {
            get => _selectedUnitId;
            set => SetProperty(ref _selectedUnitId, value);
        }

        private decimal _conversionFactor = 1;
        public decimal ConversionFactor
        {
            get => _conversionFactor;
            set
            {
                if (SetProperty(ref _conversionFactor, value) && !_suppressAutoCalc)
                    AutoCalcPrices();
            }
        }

        private decimal _salePrice;
        public decimal SalePrice
        {
            get => _salePrice;
            set
            {
                if (SetProperty(ref _salePrice, value) && !_isAutoCalc)
                    _salePriceManual = value != 0;
            }
        }

        private decimal _purchasePrice;
        public decimal PurchasePrice
        {
            get => _purchasePrice;
            set
            {
                if (SetProperty(ref _purchasePrice, value) && !_isAutoCalc)
                    _purchasePriceManual = value != 0;
            }
        }

        private string _barcode;
        public string Barcode
        {
            get => _barcode;
            set => SetProperty(ref _barcode, value);
        }

        private bool _isDefault;
        public bool IsDefault
        {
            get => _isDefault;
            set => SetProperty(ref _isDefault, value);
        }

        /// <summary>
        /// Auto-calculates SalePrice and PurchasePrice from base unit prices
        /// and the conversion factor. Formula: UnitPrice = BasePrice / Factor.
        /// E.g., Carton(base)=100, Piece factor=12 → PiecePrice = 100/12 ≈ 8.33
        /// If SalePrice or PurchasePrice is zero, it will be auto-calculated.
        /// If user enters a value, it will be preserved.
        /// </summary>
        public void AutoCalcPrices()
        {
            if (_getBasePrices == null || ConversionFactor <= 0) return;
            var (baseSale, baseCost) = _getBasePrices();

            _isAutoCalc = true;

            if (baseSale > 0 && !_salePriceManual)
                SalePrice = _convertPrice != null
                    ? _convertPrice(baseSale, ConversionFactor)
                    : Math.Round(baseSale / ConversionFactor, 4);

            if (baseCost > 0 && !_purchasePriceManual)
                PurchasePrice = _convertPrice != null
                    ? _convertPrice(baseCost, ConversionFactor)
                    : Math.Round(baseCost / ConversionFactor, 4);

            _isAutoCalc = false;
        }

        /// <summary>
        /// Sets all properties without triggering auto-calc (for loading existing data).
        /// </summary>
        public void LoadWithoutAutoCalc(int unitId, decimal factor, decimal sale, decimal purchase, string barcode, bool isDefault)
        {
            _suppressAutoCalc = true;
            SelectedUnitId = unitId;
            ConversionFactor = factor;
            SalePrice = sale;
            PurchasePrice = purchase;
            Barcode = barcode;
            IsDefault = isDefault;
            _salePriceManual = sale != 0;
            _purchasePriceManual = purchase != 0;
            _suppressAutoCalc = false;
        }
    }
}
