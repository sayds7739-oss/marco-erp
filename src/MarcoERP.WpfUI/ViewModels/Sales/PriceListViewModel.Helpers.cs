using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.WpfUI.Services;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    public sealed partial class PriceListViewModel
    {
        // ══════ Bulk Actions ═════════════════════════════════════

        private void SetAllSelection(bool selected)
        {
            foreach (var item in _filteredItems.Cast<PriceListProductItem>())
                item.IsSelected = selected;
            RefreshCounts();
        }

        private void CopyDefaultPrices()
        {
            foreach (var item in AllItems.Where(i => i.IsSelected))
            {
                item.MajorUnitPrice = item.DefaultSalePrice;
                item.PartCount = _lineCalculationService.CalculatePartCount(item.MajorUnitFactor, item.MinorUnitFactor);
                item.MinorUnitPrice = _lineCalculationService.ConvertPrice(item.MajorUnitPrice, item.PartCount);
            }

            StatusMessage = "تم نسخ أسعار البيع الافتراضية للأصناف المحددة";
        }

        private static IEnumerable<CreatePriceTierDto> BuildTiersForItem(PriceListProductItem item)
        {
            var result = new List<CreatePriceTierDto>();

            var minorFactor = item.MinorUnitFactor > 0 ? item.MinorUnitFactor : 1m;
            var partCount = item.PartCount > 0 ? item.PartCount : 1m;
            var majorFactorFromQty = minorFactor * partCount;
            var majorFactor = majorFactorFromQty > 0 ? majorFactorFromQty : 1m;

            result.Add(new CreatePriceTierDto
            {
                ProductId = item.ProductId,
                MinimumQuantity = minorFactor,
                Price = item.MinorUnitPrice
            });

            if (majorFactor != minorFactor)
            {
                result.Add(new CreatePriceTierDto
                {
                    ProductId = item.ProductId,
                    MinimumQuantity = majorFactor,
                    Price = item.MajorUnitPrice
                });
            }

            return result;
        }

        private void SelectVisible()
        {
            var count = 0;
            foreach (var item in _filteredItems.Cast<PriceListProductItem>())
            {
                if (!item.IsSelected)
                {
                    item.IsSelected = true;
                    count++;
                }
            }
            RefreshCounts();
            StatusMessage = $"تم تحديد {count} صنف من الأصناف المعروضة";
        }

        private void ClearFilters()
        {
            _searchText = string.Empty;
            _filterSupplierId = null;
            _filterCategoryName = string.Empty;
            _filterSelectionMode = SelectionFilterMode.All;
            _filterCodeFrom = string.Empty;
            _filterCodeTo = string.Empty;

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(FilterSupplierId));
            OnPropertyChanged(nameof(FilterCategoryName));
            OnPropertyChanged(nameof(FilterSelectionMode));
            OnPropertyChanged(nameof(FilterSelectionModeIndex));
            OnPropertyChanged(nameof(FilterCodeFrom));
            OnPropertyChanged(nameof(FilterCodeTo));

            ApplyFilter();
            StatusMessage = "تم مسح جميع الفلاتر";
        }

        // ══════ Print / PDF ══════════════════════════════════════

        private async Task PrintAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var selectedItems = AllItems.Where(i => i.IsSelected).ToList();
                if (selectedItems.Count == 0)
                {
                    ErrorMessage = "لا توجد أصناف محددة للطباعة. الرجاء تحديد أصناف أولاً.";
                    return;
                }

                var html = BuildPriceListHtml(selectedItems, FormNameAr, FormCode, FormValidFrom, FormValidTo, FormDescription, _dateTimeProvider);
                var request = new InvoicePdfPreviewRequest
                {
                    Title = $"قائمة أسعار — {FormNameAr}",
                    FilePrefix = "price_list",
                    HtmlContent = html,
                    PaperSize = PdfPaperSize.A4
                };

                await _pdfService.ShowHtmlPreviewAsync(request);
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("الطباعة", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string BuildPriceListHtml(
            List<PriceListProductItem> items,
            string nameAr, string code,
            DateTime? validFrom, DateTime? validTo,
            string description,
            IDateTimeProvider dateTimeProvider)
        {
            var culture = CultureInfo.GetCultureInfo("ar-EG");
            var sb = new StringBuilder(4096);

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"ar\" dir=\"rtl\">");
            sb.AppendLine("<head><meta charset=\"utf-8\" />");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:'Segoe UI',Tahoma,Arial;margin:24px;color:#263238;font-size:12px;}");
            sb.AppendLine("h1{font-size:20px;margin:0 0 4px;color:#1565C0;}");
            sb.AppendLine("h2{font-size:14px;margin:20px 0 8px;color:#37474F;border-bottom:2px solid #1565C0;padding-bottom:4px;}");
            sb.AppendLine(".meta{font-size:11px;color:#607D8B;margin-bottom:12px;}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;margin-bottom:0;table-layout:fixed;}");
            sb.AppendLine("th,td{border:1px solid #CFD8DC;padding:6px 8px;word-wrap:break-word;}");
            sb.AppendLine("th{background:#ECEFF1;font-weight:600;text-align:center;}");
            sb.AppendLine("td{text-align:right;}");
            sb.AppendLine("tr:nth-child(even){background:#FAFAFA;}");
            sb.AppendLine(".price{font-weight:600;color:#2E7D32;}");
            sb.AppendLine(".supplier-block{margin:0;break-inside:avoid;page-break-inside:avoid;}");
            sb.AppendLine(".supplier-title{font-size:15px;font-weight:800;text-align:center;color:#1E3A8A;margin:12px 0 6px;}");
            sb.AppendLine(".supplier-page-break{page-break-before:always;break-before:page;margin-top:0;}");
            sb.AppendLine(".col-code{width:88px;}");
            sb.AppendLine(".col-name{width:46%;}");
            sb.AppendLine(".col-price{width:88px;}");
            sb.AppendLine(".col-parts{width:76px;}");
            sb.AppendLine(".footer{margin-top:24px;font-size:10px;color:#90A4AE;text-align:center;border-top:1px solid #ECEFF1;padding-top:8px;}");
            sb.AppendLine("@media print{body{margin:12px;} .supplier-block{break-inside:avoid;page-break-inside:avoid;}}");
            sb.AppendLine("</style></head><body>");

            sb.AppendLine($"<h1>قائمة أسعار — {WebUtility.HtmlEncode(nameAr ?? "")}</h1>");

            var metaParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(code))
                metaParts.Add($"الكود: {WebUtility.HtmlEncode(code)}");
            metaParts.Add($"تاريخ القائمة: {dateTimeProvider.UtcNow:yyyy-MM-dd}");
            if (validFrom.HasValue)
                metaParts.Add($"من: {validFrom.Value:yyyy-MM-dd}");
            if (validTo.HasValue)
                metaParts.Add($"إلى: {validTo.Value:yyyy-MM-dd}");
            metaParts.Add($"عدد الأصناف: {items.Count}");

            sb.AppendLine($"<div class=\"meta\">{string.Join(" | ", metaParts)}</div>");

            if (!string.IsNullOrWhiteSpace(description))
                sb.AppendLine($"<p style=\"font-size:11px;color:#546E7A;\">{WebUtility.HtmlEncode(description)}</p>");

            // Group by supplier
            var groups = items
                .OrderBy(i => i.SupplierName)
                .ThenBy(i => i.ProductCode)
                .GroupBy(i => string.IsNullOrWhiteSpace(i.SupplierName) ? "بدون مورد" : i.SupplierName);

            var groupIndex = 0;
            foreach (var group in groups)
            {
                var groupItems = group.ToList();
                var groupClass = groupIndex > 0 ? "supplier-block supplier-page-break" : "supplier-block";
                sb.AppendLine($"<section class=\"{groupClass}\">");
                sb.AppendLine($"<div class=\"supplier-title\">{WebUtility.HtmlEncode(group.Key)}</div>");
                sb.AppendLine("<table>");
                sb.AppendLine("<thead>");
                sb.AppendLine("<tr>");
                sb.AppendLine($"<th colspan=\"5\" style=\"text-align:center;background:#E8EEF9;color:#1E3A8A;\">عدد الأصناف: {groupItems.Count}</th>");
                sb.AppendLine("</tr>");
                sb.AppendLine("<tr>");
                sb.AppendLine("<th class=\"col-code\">كود الصنف</th>");
                sb.AppendLine("<th class=\"col-name\">اسم الصنف</th>");
                sb.AppendLine("<th class=\"col-price\">السعر الكلي</th>");
                sb.AppendLine("<th class=\"col-price\">السعر الجزئي</th>");
                sb.AppendLine("<th class=\"col-parts\">عدد الجزء</th>");
                sb.AppendLine("</tr>");
                sb.AppendLine("</thead><tbody>");

                foreach (var item in groupItems)
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{WebUtility.HtmlEncode(item.ProductCode)}</td>");
                    sb.AppendLine($"<td>{WebUtility.HtmlEncode(item.ProductName)}</td>");
                    sb.AppendLine($"<td class=\"price\">{item.MajorUnitPrice.ToString("N2", culture)}</td>");
                    sb.AppendLine($"<td class=\"price\">{item.MinorUnitPrice.ToString("N2", culture)}</td>");
                    sb.AppendLine($"<td>{item.PartCount.ToString("N0", culture)}</td>");
                    sb.AppendLine("</tr>");
                }

                sb.AppendLine("</tbody></table>");
                sb.AppendLine("</section>");
                groupIndex++;
            }

            sb.AppendLine($"<div class=\"footer\">MarcoERP — تم الطباعة: {dateTimeProvider.UtcNow:yyyy-MM-dd HH:mm} — " +
                          $"إجمالي الأصناف: {items.Count}</div>");

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }
    }
}
