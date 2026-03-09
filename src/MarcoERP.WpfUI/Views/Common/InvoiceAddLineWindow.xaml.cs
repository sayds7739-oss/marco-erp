using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.WpfUI.ViewModels;

namespace MarcoERP.WpfUI.Views.Common
{
    /// <summary>
    /// Shared modal popup for adding/editing an invoice line item.
    /// Uses a dedicated search TextBox with auto-suggest popup.
    /// Detects barcode scanner input (rapid keystrokes) for instant product selection.
    /// Search priority: exact barcode → code prefix → name/code substring.
    /// </summary>
    public partial class InvoiceAddLineWindow : Window
    {
        /// <summary>Debounce timer: waits before filtering to avoid per-keystroke lag.</summary>
        private readonly DispatcherTimer _debounceTimer;
        private const int DebounceMilliseconds = 200;

        /// <summary>Tracks timestamps of recent keystrokes for barcode scanner detection.</summary>
        private readonly List<DateTime> _keystrokeTimestamps = new();
        private const int BarcodeScanThresholdMs = 50;
        private const int BarcodeScanMinLength = 6;

        /// <summary>True when the user confirmed adding a line (vs cancelling).</summary>
        public bool LineAdded { get; private set; }

        /// <summary>True when the user wants to add another line after this one.</summary>
        public bool AddAnother { get; private set; }

        public InvoiceAddLineWindow()
        {
            InitializeComponent();

            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DebounceMilliseconds)
            };
            _debounceTimer.Tick += DebounceTimer_Tick;

            Loaded += OnLoaded;
        }

        protected override void OnClosed(EventArgs e)
        {
            _debounceTimer?.Stop();
            base.OnClosed(e);
        }

        /// <summary>
        /// Pre-populate the search TextBox when editing an existing line (ProductId already set).
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (cmbProduct.SelectedValue is int selectedId && selectedId > 0)
            {
                var products = GetProducts();
                var product = products?.FirstOrDefault(p => p.Id == selectedId);
                if (product != null)
                {
                    txtProductSearch.TextChanged -= SearchTextBox_TextChanged;
                    txtProductSearch.Text = $"{product.Code} - {product.NameAr}";
                    txtProductSearch.TextChanged += SearchTextBox_TextChanged;
                    // Focus the quantity field instead of search
                    Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                    {
                        var target = FindName("txtPrimaryQty") as TextBox ?? FindName("txtSecondaryQty") as TextBox;
                        target?.Focus();
                        target?.SelectAll();
                    }));
                }
            }
        }

        // ────────────── Search TextBox Events ──────────────

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = txtProductSearch.Text?.Trim() ?? string.Empty;

            // Track keystroke timing for barcode scanner detection
            _keystrokeTimestamps.Add(DateTime.UtcNow);

            // Reset debounce on every keystroke
            _debounceTimer.Stop();

            if (string.IsNullOrWhiteSpace(text))
            {
                lstSearchResults.ItemsSource = null;
                searchPopup.IsOpen = false;
                return;
            }

            // Check if this looks like a barcode scan (rapid sequential input)
            if (IsBarcodeScanning(text))
            {
                // Don't debounce — wait for scanner to finish, then auto-select
                _debounceTimer.Interval = TimeSpan.FromMilliseconds(80);
                _debounceTimer.Start();
                return;
            }

            _debounceTimer.Interval = TimeSpan.FromMilliseconds(DebounceMilliseconds);
            _debounceTimer.Start();
        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                // Move focus to the results list
                if (searchPopup.IsOpen && lstSearchResults.Items.Count > 0)
                {
                    lstSearchResults.SelectedIndex = 0;
                    lstSearchResults.Focus();
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                // If popup is open and has a selected item, select it
                if (searchPopup.IsOpen && lstSearchResults.SelectedItem is ProductDto selected)
                {
                    SelectProduct(selected);
                    return;
                }

                // If popup is open and has results, select the first one
                if (searchPopup.IsOpen && lstSearchResults.Items.Count > 0)
                {
                    SelectProduct(lstSearchResults.Items[0] as ProductDto);
                    return;
                }

                // If product is already selected, move to next field
                if (cmbProduct.SelectedValue is int pid && pid > 0)
                {
                    txtProductSearch.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (searchPopup.IsOpen)
                {
                    searchPopup.IsOpen = false;
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.F1)
            {
                // Open full product search (legacy F1 behavior)
                return;
            }
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Show popup if there's already text and results
            var text = txtProductSearch.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text) && lstSearchResults.Items.Count > 0)
                searchPopup.IsOpen = true;
        }

        // ────────────── Search Results List Events ──────────────

        private void SearchResults_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && lstSearchResults.SelectedItem is ProductDto selected)
            {
                SelectProduct(selected);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up && lstSearchResults.SelectedIndex == 0)
            {
                // Move focus back to search box
                txtProductSearch.Focus();
                txtProductSearch.CaretIndex = txtProductSearch.Text?.Length ?? 0;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                searchPopup.IsOpen = false;
                txtProductSearch.Focus();
                e.Handled = true;
            }
        }

        private void SearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstSearchResults.SelectedItem is ProductDto selected)
                SelectProduct(selected);
        }

        private void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ensure selected item is visible
            if (lstSearchResults.SelectedItem != null)
                lstSearchResults.ScrollIntoView(lstSearchResults.SelectedItem);
        }

        // ────────────── Core Search Logic ──────────────

        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();

            var text = txtProductSearch.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                lstSearchResults.ItemsSource = null;
                searchPopup.IsOpen = false;
                return;
            }

            // Check if this was a barcode scan (rapid input of sufficient length)
            bool wasBarcodeScan = text.Length >= BarcodeScanMinLength && IsBarcodeScanning(text);

            // Get products from the bound collection
            var products = GetProducts();
            if (products == null)
                return;

            // Prioritized search
            var results = SmartSearch(products, text);

            // Barcode scanner auto-select: if we found exactly 1 exact barcode match, select it immediately
            if (wasBarcodeScan)
            {
                var exactBarcodeMatch = products.FirstOrDefault(p =>
                    string.Equals(p.Barcode, text, StringComparison.OrdinalIgnoreCase)
                    || (p.Units?.Any(u => string.Equals(u.Barcode, text, StringComparison.OrdinalIgnoreCase)) == true));

                if (exactBarcodeMatch != null)
                {
                    SelectProduct(exactBarcodeMatch);
                    _keystrokeTimestamps.Clear();
                    return;
                }
            }

            lstSearchResults.ItemsSource = results;
            lblResultCount.Text = results.Count > 0
                ? $"{results.Count} نتيجة"
                : "لا توجد نتائج";

            searchPopup.IsOpen = results.Count > 0;
            _keystrokeTimestamps.Clear();
        }

        /// <summary>
        /// Performs a prioritized search across the product collection.
        /// Priority: exact barcode → code prefix → name/code substring → unit barcodes → numeric code match.
        /// Results are ordered by match quality (best matches first).
        /// </summary>
        private static List<ProductDto> SmartSearch(IEnumerable<ProductDto> products, string term)
        {
            var exactBarcode = new List<ProductDto>();
            var codePrefix = new List<ProductDto>();
            var nameOrCodeContains = new List<ProductDto>();
            var unitBarcodeMatch = new List<ProductDto>();
            var numericCodeMatch = new List<ProductDto>();

            string termLower = term.ToLowerInvariant();
            bool isNumeric = IsAllDigits(term);
            string trimmedSearchNum = isNumeric ? term.TrimStart('0') : null;

            foreach (var p in products)
            {
                // 1. Exact barcode match (product level)
                if (!string.IsNullOrEmpty(p.Barcode)
                    && string.Equals(p.Barcode, term, StringComparison.OrdinalIgnoreCase))
                {
                    exactBarcode.Add(p);
                    continue;
                }

                // 2. Code prefix match
                if (!string.IsNullOrEmpty(p.Code)
                    && p.Code.StartsWith(term, StringComparison.OrdinalIgnoreCase))
                {
                    codePrefix.Add(p);
                    continue;
                }

                // 3. Name or code contains
                if (ContainsIgnoreCase(p.NameAr, term)
                    || ContainsIgnoreCase(p.NameEn, term)
                    || ContainsIgnoreCase(p.Code, term)
                    || ContainsIgnoreCase(p.Barcode, term))
                {
                    nameOrCodeContains.Add(p);
                    continue;
                }

                // 4. Unit-level barcode/name match
                if (p.Units != null)
                {
                    bool unitMatch = false;
                    foreach (var unit in p.Units)
                    {
                        if (ContainsIgnoreCase(unit.Barcode, term)
                            || ContainsIgnoreCase(unit.UnitNameAr, term))
                        {
                            unitMatch = true;
                            break;
                        }
                    }
                    if (unitMatch)
                    {
                        unitBarcodeMatch.Add(p);
                        continue;
                    }
                }

                // 5. Smart numeric matching (e.g., "5" matches code "PRD-0005")
                if (isNumeric && !string.IsNullOrEmpty(p.Code) && trimmedSearchNum.Length > 0)
                {
                    var codeDigits = ExtractDigits(p.Code).TrimStart('0');
                    if (codeDigits.Length > 0
                        && (codeDigits == trimmedSearchNum || codeDigits.EndsWith(trimmedSearchNum)))
                    {
                        numericCodeMatch.Add(p);
                    }
                }
            }

            // Combine in priority order, cap at 50 results
            var results = new List<ProductDto>(exactBarcode.Count + codePrefix.Count + nameOrCodeContains.Count + unitBarcodeMatch.Count + numericCodeMatch.Count);
            results.AddRange(exactBarcode);
            results.AddRange(codePrefix);
            results.AddRange(nameOrCodeContains);
            results.AddRange(unitBarcodeMatch);
            results.AddRange(numericCodeMatch);

            if (results.Count > 50)
                return results.GetRange(0, 50);

            return results;
        }

        /// <summary>
        /// Detects barcode scanner input by checking if recent keystrokes arrived very rapidly.
        /// Scanners typically input an entire barcode in &lt; 100ms total.
        /// </summary>
        private bool IsBarcodeScanning(string currentText)
        {
            if (_keystrokeTimestamps.Count < BarcodeScanMinLength)
                return false;

            // Check average interval between last N keystrokes
            int checkCount = Math.Min(_keystrokeTimestamps.Count, currentText.Length);
            if (checkCount < BarcodeScanMinLength)
                return false;

            var recent = _keystrokeTimestamps.Skip(_keystrokeTimestamps.Count - checkCount).ToList();
            double totalMs = (recent.Last() - recent.First()).TotalMilliseconds;
            double avgInterval = totalMs / (recent.Count - 1);

            return avgInterval < BarcodeScanThresholdMs;
        }

        // ────────────── Product Selection ──────────────

        private void SelectProduct(ProductDto product)
        {
            if (product == null) return;

            // Set the hidden ComboBox's selected value (triggers binding to LinePopup.ProductId)
            cmbProduct.SelectedValue = product.Id;

            // Update search box to show the selected product name
            txtProductSearch.TextChanged -= SearchTextBox_TextChanged;
            txtProductSearch.Text = $"{product.Code} - {product.NameAr}";
            txtProductSearch.TextChanged += SearchTextBox_TextChanged;

            // Close popup and move to next field
            searchPopup.IsOpen = false;
            _keystrokeTimestamps.Clear();

            // Move focus to the appropriate quantity field
            txtProductSearch.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        /// <summary>Clears the search box and selection for the next line entry.</summary>
        public void ResetSearch()
        {
            txtProductSearch.TextChanged -= SearchTextBox_TextChanged;
            txtProductSearch.Text = string.Empty;
            txtProductSearch.TextChanged += SearchTextBox_TextChanged;
            lstSearchResults.ItemsSource = null;
            searchPopup.IsOpen = false;
            cmbProduct.SelectedValue = null;
            _keystrokeTimestamps.Clear();
            txtProductSearch.Focus();
        }

        private IEnumerable<ProductDto> GetProducts()
        {
            return cmbProduct.ItemsSource as IEnumerable<ProductDto>;
        }

        // ────────────── Field Navigation ──────────────

        private void Field_EnterToNext(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            e.Handled = true;

            if (sender is TextBox textBox)
            {
                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
            }

            (sender as UIElement)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        // ────────────── Button Handlers ──────────────

        private void AddAndNext_Click(object sender, RoutedEventArgs e)
        {
            LineAdded = true;
            AddAnother = true;
            DialogResult = true;
        }

        private void AddAndClose_Click(object sender, RoutedEventArgs e)
        {
            LineAdded = true;
            AddAnother = false;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            LineAdded = false;
            DialogResult = false;
        }

        private async void QuickAddProduct_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new QuickAddProductDialog { Owner = this };
            await dialog.InitializeAsync();

            if (dialog.ShowDialog() == true && dialog.CreatedProductId.HasValue)
            {
                // Refresh products list from host ViewModel
                if (DataContext is IInvoiceLineFormHost host)
                {
                    await host.RefreshProductsAsync();
                    // Select the newly created product
                    var products = GetProducts();
                    var newProduct = products?.FirstOrDefault(p => p.Id == dialog.CreatedProductId.Value);
                    if (newProduct != null)
                        SelectProduct(newProduct);
                }
            }
        }

        // ────────────── String Helpers ──────────────

        private static bool IsAllDigits(string s)
        {
            foreach (var c in s)
                if (c < '0' || c > '9') return false;
            return s.Length > 0;
        }

        private static string ExtractDigits(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var c in s)
                if (c >= '0' && c <= '9') sb.Append(c);
            return sb.ToString();
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
                return false;
            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
