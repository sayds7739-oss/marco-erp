using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.WpfUI.Common;
using MarcoERP.WpfUI.ViewModels;
using MarcoERP.WpfUI.ViewModels.Inventory;
using Microsoft.Extensions.DependencyInjection;

namespace MarcoERP.WpfUI.Views.Common
{
    /// <summary>
    /// Quick-add product dialog — lightweight popup for creating a product without leaving the invoice.
    /// Returns the newly created ProductId on success.
    /// Governance: No business logic — delegates to IProductService.
    /// </summary>
    public partial class QuickAddProductDialog : Window
    {
        /// <summary>The ID of the newly created product, or null if cancelled.</summary>
        public int? CreatedProductId { get; private set; }

        public QuickAddProductDialog()
        {
            InitializeComponent();
            DataContext = new QuickAddProductState();
        }

        /// <summary>
        /// Initializes the dialog with pre-loaded dropdown data.
        /// Call before ShowDialog().
        /// </summary>
        public async Task InitializeAsync()
        {
            if (DataContext is QuickAddProductState state)
                await state.LoadDropdownsAsync();
        }

        private void Field_EnterToNext(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;

            if (sender is TextBox textBox)
            {
                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
            }
            else if (sender is ComboBox comboBox)
            {
                var binding = comboBox.GetBindingExpression(ComboBox.SelectedValueProperty);
                binding?.UpdateSource();
            }

            (sender as UIElement)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not QuickAddProductState state) return;
            if (!state.CanSave) return;

            var productId = await state.SaveAsync();
            if (productId.HasValue)
            {
                CreatedProductId = productId;
                DialogResult = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }

    /// <summary>
    /// Lightweight state for the quick-add product dialog.
    /// Not a full ViewModel — just enough for the popup fields.
    /// </summary>
    public sealed class QuickAddProductState : BaseViewModel
    {
        public ObservableCollection<CategoryDto> Categories { get; } = new();
        public ObservableCollection<UnitDto> Units { get; } = new();

        private string _nameAr;
        public string NameAr
        {
            get => _nameAr;
            set { SetProperty(ref _nameAr, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private string _nameEn;
        public string NameEn { get => _nameEn; set => SetProperty(ref _nameEn, value); }

        private int? _categoryId;
        public int? CategoryId
        {
            get => _categoryId;
            set { SetProperty(ref _categoryId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private int? _baseUnitId;
        public int? BaseUnitId
        {
            get => _baseUnitId;
            set { SetProperty(ref _baseUnitId, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private decimal _costPrice;
        public decimal CostPrice { get => _costPrice; set => SetProperty(ref _costPrice, value); }

        private decimal _salePrice;
        public decimal SalePrice { get => _salePrice; set => SetProperty(ref _salePrice, value); }

        private decimal _vatRate;
        public decimal VatRate { get => _vatRate; set => SetProperty(ref _vatRate, value); }

        private string _barcode;
        public string Barcode { get => _barcode; set => SetProperty(ref _barcode, value); }

        private string _errorMessage;
        public new string ErrorMessage { get => _errorMessage; set { SetProperty(ref _errorMessage, value); OnPropertyChanged(nameof(HasError)); } }
        public new bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public bool CanSave => !string.IsNullOrWhiteSpace(NameAr)
                               && CategoryId.HasValue && CategoryId > 0
                               && BaseUnitId.HasValue && BaseUnitId > 0;

        public async Task LoadDropdownsAsync()
        {
            IsBusy = true;
            try
            {
                using var scope = App.Services.CreateScope();
                var categoryService = scope.ServiceProvider.GetRequiredService<ICategoryService>();
                var unitService = scope.ServiceProvider.GetRequiredService<IUnitService>();

                var catResult = await categoryService.GetAllAsync();
                if (catResult.IsSuccess)
                    foreach (var c in catResult.Data) Categories.Add(c);

                var unitResult = await unitService.GetAllAsync();
                if (unitResult.IsSuccess)
                    foreach (var u in unitResult.Data) Units.Add(u);
            }
            catch (Exception ex) { ErrorMessage = ErrorSanitizer.SanitizeGeneric(ex, "تحميل البيانات"); }
            finally { IsBusy = false; }
        }

        public async Task<int?> SaveAsync()
        {
            ErrorMessage = null;
            IsBusy = true;
            try
            {
                using var scope = App.Services.CreateScope();
                var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

                // Auto-generate code
                var codeResult = await productService.GetNextCodeAsync();
                // TODO: Replace with IDateTimeProvider when refactored — code-behind uses service locator
                var code = codeResult.IsSuccess ? codeResult.Data : $"P-{DateTime.Now:yyMMddHHmmss}";

                var dto = new CreateProductDto
                {
                    Code = code,
                    NameAr = NameAr?.Trim(),
                    NameEn = NameEn?.Trim(),
                    CategoryId = CategoryId ?? 0,
                    BaseUnitId = BaseUnitId ?? 0,
                    CostPrice = CostPrice,
                    DefaultSalePrice = SalePrice,
                    VatRate = VatRate,
                    Barcode = Barcode?.Trim(),
                    Units = new System.Collections.Generic.List<CreateProductUnitDto>()
                };

                var result = await productService.CreateAsync(dto);
                if (result.IsSuccess)
                    return result.Data.Id;

                ErrorMessage = result.ErrorMessage;
                return null;
            }
            catch (Exception ex)
            {
                ErrorMessage = ErrorSanitizer.SanitizeGeneric(ex, "الحفظ");
                return null;
            }
            finally { IsBusy = false; }
        }
    }
}
