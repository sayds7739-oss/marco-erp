using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.DTOs.Sales;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    public sealed partial class PriceListViewModel
    {
        // ══════ Load ═════════════════════════════════════════════

        public async Task LoadAsync()
        {
            await _loadGate.WaitAsync();
            IsBusy = true;
            ClearError();
            try
            {
                var selectedIdToRestore = SelectedPriceList?.Id;

                // Load sequentially to avoid concurrent use of a shared DbContext instance
                var plResult = await _priceListService.GetAllAsync();
                var prodResult = await _productService.GetAllAsync();
                var supResult = await _supplierService.GetAllAsync();

                // Price lists
                if (plResult.IsSuccess)
                {
                    AllPriceLists.Clear();
                    foreach (var item in plResult.Data)
                        AllPriceLists.Add(item);
                }
                else
                {
                    ErrorMessage = plResult.ErrorMessage;
                    return;
                }

                // Suppliers (for filter dropdown)
                if (supResult.IsSuccess)
                {
                    Suppliers.Clear();
                    foreach (var s in supResult.Data.Where(s => s.IsActive).OrderBy(s => s.NameAr))
                        Suppliers.Add(s);
                }

                // Products → AllItems
                if (prodResult.IsSuccess)
                {
                    AllItems.Clear();
                    Categories.Clear();

                    var cats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var p in prodResult.Data.OrderBy(p => p.Code))
                    {
                        var units = p.Units ?? new List<ProductUnitDto>();
                        var majorUnit = units.OrderByDescending(u => u.ConversionFactor).FirstOrDefault();
                        var minorUnit = units.OrderBy(u => u.ConversionFactor).FirstOrDefault();

                        var majorFactor = majorUnit?.ConversionFactor > 0 ? majorUnit.ConversionFactor : 1m;
                        var minorFactor = minorUnit?.ConversionFactor > 0 ? minorUnit.ConversionFactor : 1m;

                        var majorUnitName = majorUnit?.UnitNameAr ?? p.BaseUnitName ?? "-";
                        var minorUnitName = minorUnit?.UnitNameAr ?? p.BaseUnitName ?? "-";

                        var majorDefaultPrice = majorUnit?.SalePrice ?? p.DefaultSalePrice;
                        var partCount = _lineCalculationService.CalculatePartCount(majorFactor, minorFactor);
                        if (partCount <= 0)
                            partCount = 1m;
                        var minorDefaultPrice = _lineCalculationService.ConvertPrice(majorDefaultPrice, partCount);

                        AllItems.Add(new PriceListProductItem
                        {
                            ProductId = p.Id,
                            ProductCode = p.Code ?? string.Empty,
                            ProductName = p.NameAr ?? string.Empty,
                            CategoryName = p.CategoryName ?? string.Empty,
                            SupplierId = p.DefaultSupplierId,
                            SupplierName = p.DefaultSupplierName ?? string.Empty,
                            DefaultSalePrice = p.DefaultSalePrice,
                            MajorUnitName = majorUnitName,
                            MinorUnitName = minorUnitName,
                            MajorUnitFactor = majorFactor,
                            MinorUnitFactor = minorFactor,
                            PartCount = partCount,
                            MajorUnitPrice = majorDefaultPrice,
                            MinorUnitPrice = minorDefaultPrice,
                            IsSelected = false,
                            ConvertPrice = _lineCalculationService.ConvertPrice,
                            ConvertQuantity = _lineCalculationService.ConvertQuantity
                        });

                        if (!string.IsNullOrWhiteSpace(p.CategoryName))
                            cats.Add(p.CategoryName);
                    }

                    foreach (var c in cats.OrderBy(c => c))
                        Categories.Add(c);
                }
                else
                {
                    ErrorMessage = prodResult.ErrorMessage;
                    return;
                }

                StatusMessage = $"تم تحميل {AllItems.Count} صنف و {AllPriceLists.Count} قائمة أسعار";
                RefreshCounts();

                if (selectedIdToRestore.HasValue)
                {
                    var restored = AllPriceLists.FirstOrDefault(p => p.Id == selectedIdToRestore.Value);
                    if (restored != null)
                        SelectedPriceList = restored;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("التحميل", ex);
            }
            finally
            {
                IsBusy = false;
                _loadGate.Release();
            }
        }

        private async Task LoadPriceListDetailAsync(int id)
        {
            await _loadGate.WaitAsync();
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _priceListService.GetByIdAsync(id);
                if (result.IsSuccess)
                {
                    var dto = result.Data;
                    FormCode = dto.Code;
                    FormNameAr = dto.NameAr;
                    FormNameEn = dto.NameEn;
                    FormDescription = dto.Description;
                    FormValidFrom = dto.ValidFrom;
                    FormValidTo = dto.ValidTo;
                    FormIsActive = dto.IsActive;

                    // Merge tiers into product grid (supports multiple tiers per product)
                    var tierMap = dto.Tiers
                        .GroupBy(t => t.ProductId)
                        .ToDictionary(g => g.Key, g => g.OrderBy(t => t.MinimumQuantity).ToList());

                    foreach (var item in AllItems)
                    {
                        if (tierMap.TryGetValue(item.ProductId, out var tiers) && tiers.Count > 0)
                        {
                            var minorTier = tiers.First();
                            var majorTier = tiers.Last();
                            var partCount = minorTier.MinimumQuantity > 0
                                ? _lineCalculationService.ConvertPrice(majorTier.MinimumQuantity, minorTier.MinimumQuantity)
                                : 1m;
                            if (partCount <= 0)
                                partCount = _lineCalculationService.CalculatePartCount(item.MajorUnitFactor, item.MinorUnitFactor);

                            var majorPrice = majorTier.Price;
                            var minorPrice = _lineCalculationService.ConvertPrice(majorPrice, partCount);

                            item.IsSelected = true;
                            item.PartCount = partCount;
                            item.MajorUnitPrice = majorPrice;
                            item.MinorUnitPrice = minorPrice;
                        }
                        else
                        {
                            item.IsSelected = false;
                            item.MajorUnitPrice = item.DefaultSalePrice;
                            item.PartCount = _lineCalculationService.CalculatePartCount(item.MajorUnitFactor, item.MinorUnitFactor);
                            item.MinorUnitPrice = _lineCalculationService.ConvertPrice(item.MajorUnitPrice, item.PartCount);
                        }
                    }

                    StatusMessage = $"تم تحميل قائمة الأسعار: {dto.NameAr} ({dto.Tiers.Count} شريحة)";
                    RefreshCounts();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تحميل التفاصيل", ex);
            }
            finally
            {
                IsBusy = false;
                _loadGate.Release();
            }
        }
    }
}
