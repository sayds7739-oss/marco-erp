using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MarcoERP.WpfUI.Common;
using MarcoERP.WpfUI.Navigation;
using MarcoERP.WpfUI.ViewModels.Common;
using MarcoERP.WpfUI.Views.Common;

namespace MarcoERP.WpfUI.ViewModels.Purchases
{
    public sealed partial class PurchaseInvoiceDetailViewModel
    {
        // ── Invoice Navigation ───────────────────────────────
        private async Task LoadInvoiceIdsAsync()
        {
            try
            {
                var result = await _invoiceService.GetAllAsync();
                if (result.IsSuccess)
                {
                    var list = result.Data.ToList();
                    _invoiceIds = list.Select(i => i.Id).ToList();
                    _invoiceNumberToId = list
                        .Where(i => !string.IsNullOrWhiteSpace(i.InvoiceNumber))
                        .GroupBy(i => i.InvoiceNumber)
                        .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch { /* non-critical */ }
        }

        private async Task GoToNextAsync()
        {
            if (!await DirtyStateGuard.ConfirmContinueAsync(this))
                return;
            if (!CanGoNext) return;
            _currentInvoiceIndex++;
            await LoadDetailAsync(_invoiceIds[_currentInvoiceIndex]);
            UpdateNavigationState();
        }

        private async Task GoToPreviousAsync()
        {
            if (!await DirtyStateGuard.ConfirmContinueAsync(this))
                return;
            if (!CanGoPrevious) return;
            _currentInvoiceIndex--;
            await LoadDetailAsync(_invoiceIds[_currentInvoiceIndex]);
            UpdateNavigationState();
        }

        private async Task JumpToInvoiceAsync()
        {
            if (!await DirtyStateGuard.ConfirmContinueAsync(this))
                return;

            if (string.IsNullOrWhiteSpace(JumpInvoiceNumber))
                return;

            if (!_invoiceNumberToId.TryGetValue(JumpInvoiceNumber.Trim(), out var id))
            {
                _dialog.ShowInfo("رقم الفاتورة غير موجود.", "تنقل الفواتير");
                return;
            }

            _currentInvoiceIndex = _invoiceIds.IndexOf(id);
            await LoadDetailAsync(id);
            UpdateNavigationState();
        }

        private void UpdateNavigationState()
        {
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(NavigationPositionText));
            RelayCommand.RaiseCanExecuteChanged();
        }

        private async Task ViewPdfAsync()
        {
            if (CurrentInvoice == null) return;
            await _invoicePdfPreviewService.ShowPurchaseInvoiceAsync(CurrentInvoice);
        }

        // ── Add/Edit Line Popup ──────────────────────────────────

        private void OpenAddLinePopup()
        {
            if (!IsEditing && !IsNew) return;
            var state = new InvoiceLinePopupState(this, InvoicePopupMode.Purchase, _lineCalculationService);
            LinePopup = state;
            state.PropertyChanged += PopupState_PropertyChanged;
            ShowPopupLoop(state);
            state.PropertyChanged -= PopupState_PropertyChanged;
            LinePopup = null;
        }

        private void EditLinePopup(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is not PurchaseInvoiceLineFormItem line) return;
            var index = FormLines.IndexOf(line);
            if (index < 0) return;

            var state = new InvoiceLinePopupState(this, InvoicePopupMode.Purchase, _lineCalculationService);
            LinePopup = state;
            state.PropertyChanged += PopupState_PropertyChanged;
            state.LoadFromLine(line.ProductId, line.UnitId, line.Quantity, line.UnitPrice,
                line.DiscountPercent, line.SmartStockQty ?? 0, line.SmartLastPurchaseUnitPrice ?? 0,
                line.SmartAverageCost, null, index);
            EnqueueDbWork(() => RefreshSmartEntryForPopupAsync(state));

            var popup = new InvoiceAddLineWindow { Owner = System.Windows.Application.Current.MainWindow, DataContext = this };
            if (popup.ShowDialog() == true && popup.LineAdded && state.IsValid)
                ApplyPopupStateToLine(state, index);

            state.PropertyChanged -= PopupState_PropertyChanged;
            LinePopup = null;
        }

        private void ShowPopupLoop(InvoiceLinePopupState state)
        {
            var parent = System.Windows.Application.Current.MainWindow;
            var keepAdding = true;
            while (keepAdding)
            {
                state.Reset();
                var popup = new InvoiceAddLineWindow { Owner = parent, DataContext = this };
                if (popup.ShowDialog() == true && popup.LineAdded && state.IsValid)
                {
                    ApplyPopupStateToLine(state, editIndex: null);
                    keepAdding = popup.AddAnother;
                }
                else keepAdding = false;
            }
        }

        private void ApplyPopupStateToLine(InvoiceLinePopupState state, int? editIndex)
        {
            // ── Duplicate product check ──
            var existingIndex = editIndex ?? -1;
            var isDuplicate = FormLines
                .Where((l, i) => i != existingIndex && l.ProductId == state.ProductId && l.ProductId > 0)
                .Any();
            if (isDuplicate)
            {
                if (!_dialog.Confirm("هذا الصنف موجود بالفعل في الفاتورة.\nهل تريد إضافته مرة أخرى؟", "صنف مكرر"))
                    return;
            }

            if (editIndex.HasValue && editIndex.Value >= 0 && editIndex.Value < FormLines.Count)
            {
                var existing = FormLines[editIndex.Value];
                existing.ProductId = state.ProductId;
                existing.UnitId = state.SelectedUnitId;
                existing.Quantity = state.SelectedQty;
                existing.UnitPrice = state.SelectedUnitPrice;
                existing.DiscountPercent = state.DiscountPercent;
            }
            else
            {
                var line = new PurchaseInvoiceLineFormItem(this)
                { ProductId = state.ProductId, UnitId = state.SelectedUnitId, Quantity = state.SelectedQty, UnitPrice = state.SelectedUnitPrice, DiscountPercent = state.DiscountPercent };
                HookLine(line);
                FormLines.Add(line);
            }
            RefreshTotals();
            MarkDirty();
        }

        private void PopupState_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is InvoiceLinePopupState state && e.PropertyName == nameof(InvoiceLinePopupState.ProductId))
                EnqueueDbWork(() => RefreshSmartEntryForPopupAsync(state));
        }

        private async Task RefreshSmartEntryForPopupAsync(InvoiceLinePopupState state)
        {
            if (state == null || state.ProductId <= 0) return;
            if (!FormWarehouseId.HasValue || FormWarehouseId <= 0) return;
            var warehouseId = FormWarehouseId.Value;
            var supplierId = FormSupplierId ?? 0;
            var unitId = state.SelectedUnitId > 0 ? state.SelectedUnitId : (state.SecondaryUnit?.UnitId ?? 0);
            try
            {
                var stock = await _smartEntryQueryService.GetStockBaseQtyAsync(warehouseId, state.ProductId);
                state.StockQty = stock;
                var lastPurch = supplierId > 0
                    ? await _smartEntryQueryService.GetLastPurchaseUnitPriceForSupplierAsync(supplierId, state.ProductId, unitId)
                    : await _smartEntryQueryService.GetLastPurchaseUnitPriceAsync(state.ProductId, unitId);
                state.LastPurchasePrice = lastPurch ?? 0m;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PurchaseInvoiceDetail] Failed to load stock/price hints: {ex.Message}");
            }
        }

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
            else if (parameter is PurchaseInvoiceLineFormItem line)
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
                PriceHistorySource.Purchase,
                FormCounterpartyType,
                FormCounterpartyCustomerId,
                FormSupplierId,
                productId,
                unitId,
                owner);

            if (selectedPrice.HasValue)
                applyPrice(selectedPrice.Value);
        }
    }
}
