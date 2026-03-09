using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.WpfUI.Common;
using MarcoERP.WpfUI.Services;
using MarcoERP.WpfUI.ViewModels.Common;
using MarcoERP.WpfUI.Views.Common;
using MarcoERP.WpfUI.Views.Sales;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    public sealed partial class SalesInvoiceDetailViewModel
    {
        // ── Line Management ─────────────────────────────────────

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
            else if (parameter is SalesInvoiceLineFormItem line)
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

        private void AddLine(object _)
        {
            if (!IsEditing && !IsNew) return;
            OpenAddLinePopup();
        }

        private void RemoveLine(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is SalesInvoiceLineFormItem line)
            {
                UnhookLine(line);
                FormLines.Remove(line);
                RefreshTotals();
                MarkDirty();
            }
        }

        private void HookLine(SalesInvoiceLineFormItem line)
        {
            if (line == null) return;
            if (_smartRefreshVersions.ContainsKey(line))
                return;

            _smartRefreshVersions[line] = 0;
            line.PropertyChanged += LineOnPropertyChanged;
        }

        private void UnhookLine(SalesInvoiceLineFormItem line)
        {
            if (line == null) return;
            if (_smartRefreshVersions.ContainsKey(line))
                _smartRefreshVersions.Remove(line);
            line.PropertyChanged -= LineOnPropertyChanged;
        }

        private void UnhookAllLines()
        {
            foreach (var line in FormLines)
                UnhookLine(line);
        }

        private void LineOnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is not SalesInvoiceLineFormItem line)
                return;

            if (IsUserEditableLineProperty(e.PropertyName))
                MarkDirty();

            if (e.PropertyName == nameof(SalesInvoiceLineFormItem.ProductId)
                || e.PropertyName == nameof(SalesInvoiceLineFormItem.UnitId)
                || e.PropertyName == nameof(SalesInvoiceLineFormItem.Quantity))
                EnqueueDbWork(() => RefreshSmartEntryForLineAsync(line));
        }

        private static bool IsUserEditableLineProperty(string propertyName)
        {
            return propertyName == nameof(SalesInvoiceLineFormItem.ProductId)
                   || propertyName == nameof(SalesInvoiceLineFormItem.UnitId)
                   || propertyName == nameof(SalesInvoiceLineFormItem.Quantity)
                   || propertyName == nameof(SalesInvoiceLineFormItem.UnitPrice)
                   || propertyName == nameof(SalesInvoiceLineFormItem.DiscountPercent);
        }

        private async Task RefreshSmartEntryForAllLinesAsync()
        {
            foreach (var line in FormLines.ToList())
                await RefreshSmartEntryForLineAsync(line);
        }

        private async Task RefreshSmartEntryForLineAsync(SalesInvoiceLineFormItem line)
        {
            if (line == null) return;
            if (line.ProductId <= 0 || line.UnitId <= 0) return;
            if (!FormWarehouseId.HasValue || FormWarehouseId <= 0) return;

            if (!_smartRefreshVersions.TryGetValue(line, out var version))
                _smartRefreshVersions[line] = 0;
            version = ++_smartRefreshVersions[line];

            var warehouseId = FormWarehouseId.Value;
            var customerId = FormCustomerId ?? 0;

            try
            {
                var stockBase = await _smartEntryQueryService.GetStockBaseQtyAsync(warehouseId, line.ProductId);
                var lastSale = customerId > 0
                    ? await _smartEntryQueryService.GetLastSalesUnitPriceAsync(customerId, line.ProductId, line.UnitId)
                    : null;
                var lastPurchase = await _smartEntryQueryService.GetLastPurchaseUnitPriceAsync(line.ProductId, line.UnitId);

                decimal? tierBaseUnitPrice = null;
                decimal? tierUnitPrice = null;
                if (customerId > 0)
                {
                    var selectedUnit = line.AvailableUnits.FirstOrDefault(u => u.UnitId == line.UnitId);
                    var factor = selectedUnit?.ConversionFactor ?? 1m;
                    if (factor <= 0) factor = 1m;

                    var baseQty = _lineCalculationService.ConvertQuantity(line.Quantity, factor);
                    tierBaseUnitPrice = await _smartEntryQueryService
                        .GetBestTierSaleBaseUnitPriceForCustomerAsync(customerId, line.ProductId, baseQty);

                    if (tierBaseUnitPrice.HasValue)
                        tierUnitPrice = _lineCalculationService.ConvertQuantity(tierBaseUnitPrice.Value, factor);
                }

                if (!_smartRefreshVersions.TryGetValue(line, out var current) || current != version)
                    return;

                line.SetSmartEntry(stockBase, lastSale, lastPurchase);

                // Pricing priority (only when user is still on master default):
                // Tier price > Last sale price > Master default.
                if (line.IsUnitPriceAtMasterDefault())
                {
                    if (tierUnitPrice.HasValue)
                        line.UnitPrice = tierUnitPrice.Value;
                    else if (lastSale.HasValue)
                        line.UnitPrice = lastSale.Value;
                }
            }
            catch
            {
                // Smart entry data is non-critical; ignore failures.
            }
        }

        // ── Add/Edit Line Popup ──────────────────────────────────

        /// <summary>Opens the add-line popup for a new line.</summary>
        private void OpenAddLinePopup()
        {
            if (!IsEditing && !IsNew) return;
            var state = new InvoiceLinePopupState(this, InvoicePopupMode.Sale, _lineCalculationService);
            state.IsVatInclusive = App.IsVatInclusive;
            LinePopup = state;
            state.PropertyChanged += PopupState_PropertyChanged;
            ShowPopupLoop(state);
            state.PropertyChanged -= PopupState_PropertyChanged;
            LinePopup = null;
        }

        /// <summary>Opens the add-line popup to edit an existing line.</summary>
        private void EditLinePopup(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is not SalesInvoiceLineFormItem line) return;

            var index = FormLines.IndexOf(line);
            if (index < 0) return;

            var state = new InvoiceLinePopupState(this, InvoicePopupMode.Sale, _lineCalculationService);
            state.IsVatInclusive = App.IsVatInclusive;
            LinePopup = state;
            state.PropertyChanged += PopupState_PropertyChanged;
            state.LoadFromLine(line.ProductId, line.UnitId, line.Quantity, line.UnitPrice,
                line.DiscountPercent, 0, 0, 0, null, index);
            EnqueueDbWork(() => RefreshSmartEntryForPopupAsync(state));

            var parentWindow = System.Windows.Application.Current.MainWindow;
            var popup = new InvoiceAddLineWindow
            {
                Owner = parentWindow,
                DataContext = this
            };

            if (popup.ShowDialog() == true && popup.LineAdded && state.IsValid)
            {
                ApplyPopupStateToLine(state, index);
            }

            state.PropertyChanged -= PopupState_PropertyChanged;
            LinePopup = null;
        }

        /// <summary>Shows the popup in a loop for "Add & Next" workflow.</summary>
        private void ShowPopupLoop(InvoiceLinePopupState state)
        {
            var parentWindow = System.Windows.Application.Current.MainWindow;
            var keepAdding = true;

            while (keepAdding)
            {
                state.Reset();
                var popup = new InvoiceAddLineWindow
                {
                    Owner = parentWindow,
                    DataContext = this
                };

                if (popup.ShowDialog() == true && popup.LineAdded && state.IsValid)
                {
                    ApplyPopupStateToLine(state, editIndex: null);
                    keepAdding = popup.AddAnother;
                }
                else
                {
                    keepAdding = false;
                }
            }
        }

        /// <summary>Creates or updates a form line from the popup state.</summary>
        private void ApplyPopupStateToLine(InvoiceLinePopupState state, int? editIndex)
        {
            // ── Duplicate product check — ask the user ──
            var existingIndex = editIndex ?? -1;
            var isDuplicate = FormLines
                .Where((l, i) => i != existingIndex && l.ProductId == state.ProductId && l.ProductId > 0)
                .Any();

            if (isDuplicate)
            {
                if (!_dialog.Confirm(
                    "هذا الصنف موجود بالفعل في الفاتورة.\nهل تريد إضافته مرة أخرى؟",
                    "صنف مكرر"))
                    return;
            }

            // ── Stock validation (sales only — warn if selling more than available) ──
            if (state.StockQty >= 0)
            {
                var selectedUnit = state.AvailableUnits.FirstOrDefault(u => u.UnitId == state.SelectedUnitId);
                var factor = selectedUnit?.ConversionFactor ?? 1m;
                if (factor <= 0) factor = 1m;
                var baseQtyNeeded = _lineCalculationService.ConvertQuantity(state.SelectedQty, factor);

                if (baseQtyNeeded > state.StockQty && state.StockQty >= 0)
                {
                    if (!_dialog.Confirm(
                        $"الكمية المطلوبة ({state.SelectedQty:N2}) أكبر من المخزون المتاح ({state.StockQty:N2} بالوحدة الأساسية).\nهل تريد المتابعة؟",
                        "تحذير المخزون"))
                        return;
                }
            }

            if (editIndex.HasValue && editIndex.Value >= 0 && editIndex.Value < FormLines.Count)
            {
                // Update existing line
                var existingLine = FormLines[editIndex.Value];
                existingLine.ProductId = state.ProductId;
                existingLine.UnitId = state.SelectedUnitId;
                existingLine.Quantity = state.SelectedQty;
                existingLine.UnitPrice = state.SelectedUnitPrice;
                existingLine.DiscountPercent = state.DiscountPercent;
            }
            else
            {
                // Add new line
                var line = new SalesInvoiceLineFormItem(this)
                {
                    ProductId = state.ProductId,
                    UnitId = state.SelectedUnitId,
                    Quantity = state.SelectedQty,
                    UnitPrice = state.SelectedUnitPrice,
                    DiscountPercent = state.DiscountPercent
                };
                HookLine(line);
                FormLines.Add(line);
            }

            RefreshTotals();
            MarkDirty();
        }

        /// <summary>Watches popup product changes to fetch smart entry data.</summary>
        private void PopupState_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is not InvoiceLinePopupState state) return;
            if (e.PropertyName == nameof(InvoiceLinePopupState.ProductId))
                EnqueueDbWork(() => RefreshSmartEntryForPopupAsync(state));
        }

        /// <summary>Fetches stock, last prices, and tier prices for the popup.</summary>
        private async Task RefreshSmartEntryForPopupAsync(InvoiceLinePopupState state)
        {
            if (state == null || state.ProductId <= 0) return;
            if (!FormWarehouseId.HasValue || FormWarehouseId <= 0) return;

            var warehouseId = FormWarehouseId.Value;
            var customerId = FormCustomerId ?? 0;
            var unitId = state.SelectedUnitId > 0 ? state.SelectedUnitId : (state.SecondaryUnit?.UnitId ?? 0);

            try
            {
                var stockBase = await _smartEntryQueryService.GetStockBaseQtyAsync(warehouseId, state.ProductId);
                state.StockQty = stockBase;

                var lastPurchase = await _smartEntryQueryService.GetLastPurchaseUnitPriceAsync(state.ProductId, unitId);
                state.LastPurchasePrice = lastPurchase ?? 0m;

                if (customerId > 0)
                {
                    var lastSale = await _smartEntryQueryService.GetLastSalesUnitPriceAsync(customerId, state.ProductId, unitId);
                    state.LastSalePrice = lastSale;
                }
            }
            catch
            {
                // Smart entry is non-critical; ignore failures.
            }
        }
    }
}
