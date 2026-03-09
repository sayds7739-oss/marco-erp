using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MarcoERP.Application.DTOs.Sales;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    public sealed partial class PriceListViewModel
    {
        // ══════ New ══════════════════════════════════════════════

        private async Task PrepareNewAsync()
        {
            IsEditing = true;
            IsNew = true;
            ClearError();

            _selectedPriceList = null;
            OnPropertyChanged(nameof(SelectedPriceList));

            FormNameAr = "";
            FormNameEn = "";
            FormDescription = "";
            FormValidFrom = null;
            FormValidTo = null;
            FormIsActive = true;

            // Reset all product items
            foreach (var item in AllItems)
            {
                item.IsSelected = false;
                item.MajorUnitPrice = item.DefaultSalePrice;
                item.PartCount = _lineCalculationService.CalculatePartCount(item.MajorUnitFactor, item.MinorUnitFactor);
                item.MinorUnitPrice = _lineCalculationService.ConvertPrice(item.MajorUnitPrice, item.PartCount);
            }

            try
            {
                var codeResult = await _priceListService.GetNextCodeAsync();
                FormCode = codeResult.IsSuccess ? codeResult.Data : "";
            }
            catch
            {
                FormCode = "";
            }

            RefreshCounts();
            StatusMessage = "إدخال قائمة أسعار جديدة...";
        }

        // ══════ Save ═════════════════════════════════════════════

        private async Task SaveAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var selectedTiers = AllItems
                    .Where(i => i.IsSelected)
                    .SelectMany(BuildTiersForItem)
                    .ToList();

                if (IsNew)
                {
                    var dto = new CreatePriceListDto
                    {
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        Description = FormDescription,
                        ValidFrom = FormValidFrom,
                        ValidTo = FormValidTo,
                        Tiers = selectedTiers
                    };
                    var result = await _priceListService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء قائمة الأسعار: {result.Data.NameAr} ({selectedTiers.Count} صنف)";
                        IsEditing = false;
                        IsNew = false;
                        await LoadAsync();
                        await RestorePriceListSelectionAsync(result.Data.Id);
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else
                {
                    if (SelectedPriceList == null) return;

                    var dto = new UpdatePriceListDto
                    {
                        Id = SelectedPriceList.Id,
                        NameAr = FormNameAr,
                        NameEn = FormNameEn,
                        Description = FormDescription,
                        ValidFrom = FormValidFrom,
                        ValidTo = FormValidTo,
                        Tiers = selectedTiers
                    };
                    var result = await _priceListService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث قائمة الأسعار: {result.Data.NameAr} ({selectedTiers.Count} صنف)";
                        IsEditing = false;
                        await LoadAsync();
                        await RestorePriceListSelectionAsync(result.Data.Id);
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
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

        private async Task RestorePriceListSelectionAsync(int priceListId)
        {
            var selected = AllPriceLists.FirstOrDefault(p => p.Id == priceListId);
            if (selected == null)
                return;

            SelectedPriceList = selected;
            await Task.CompletedTask;
        }

        // ══════ Delete ═══════════════════════════════════════════

        private async Task DeleteAsync()
        {
            if (SelectedPriceList == null) return;

            if (!_dialog.Confirm(
                $"هل أنت متأكد من حذف قائمة الأسعار «{SelectedPriceList.NameAr}»؟",
                "تأكيد الحذف")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _priceListService.DeleteAsync(SelectedPriceList.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم حذف قائمة الأسعار";
                    IsEditing = false;
                    await LoadAsync();
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

        // ══════ Cancel ═══════════════════════════════════════════

        private void CancelEditing(object parameter)
        {
            IsEditing = false;
            IsNew = false;
            ClearError();

            // Reset grid items
            foreach (var item in AllItems)
            {
                item.IsSelected = false;
                item.MajorUnitPrice = item.DefaultSalePrice;
                item.PartCount = _lineCalculationService.CalculatePartCount(item.MajorUnitFactor, item.MinorUnitFactor);
                item.MinorUnitPrice = _lineCalculationService.ConvertPrice(item.MajorUnitPrice, item.PartCount);
            }

            _selectedPriceList = null;
            OnPropertyChanged(nameof(SelectedPriceList));

            FormCode = "";
            FormNameAr = "";
            FormNameEn = "";
            FormDescription = "";
            FormValidFrom = null;
            FormValidTo = null;
            FormIsActive = true;

            RefreshCounts();
            StatusMessage = "تم الإلغاء";
        }
    }
}
