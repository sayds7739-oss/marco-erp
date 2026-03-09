using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.DTOs.Purchases;

namespace MarcoERP.Application.Services.Inventory
{
    public sealed partial class ProductImportService
    {
        private async Task EnsureLookupEntitiesAsync(IReadOnlyList<ProductImportRowDto> rows, CancellationToken ct)
        {
            var categories = await LoadCategoriesAsync(ct);
            var units = await LoadUnitsAsync(ct);
            var suppliers = await LoadSuppliersAsync(ct);

            foreach (var row in rows.Where(r => r.IsValid))
            {
                ct.ThrowIfCancellationRequested();

                if (!TryResolveLookupId(row.CategoryName, categories, out var categoryId))
                {
                    var createCategoryResult = await _categoryService.CreateAsync(new CreateCategoryDto
                    {
                        NameAr = NormalizeLookupKey(row.CategoryName),
                        NameEn = null,
                        ParentCategoryId = null,
                        Level = 1,
                        Description = "تم الإنشاء تلقائياً من استيراد الأصناف"
                    }, ct);

                    if (!createCategoryResult.IsSuccess || createCategoryResult.Data == null)
                    {
                        categories = await LoadCategoriesAsync(ct);
                        if (!TryResolveLookupId(row.CategoryName, categories, out categoryId))
                        {
                            row.IsValid = false;
                            row.Errors.Add($"تعذر إنشاء التصنيف '{row.CategoryName}': {createCategoryResult.ErrorMessage}");
                            continue;
                        }
                    }
                    else
                    {
                        categoryId = createCategoryResult.Data.Id;
                        categories[NormalizeLookupKey(row.CategoryName)] = categoryId;
                    }
                }

                row.ResolvedCategoryId = categoryId;

                if (!TryResolveLookupId(row.BaseUnitName, units, out var baseUnitId))
                {
                    var normalizedBaseUnit = NormalizeLookupKey(row.BaseUnitName);
                    var createBaseUnitResult = await _unitService.CreateAsync(new CreateUnitDto
                    {
                        NameAr = normalizedBaseUnit,
                        NameEn = null,
                        AbbreviationAr = CreateUnitAbbreviation(normalizedBaseUnit),
                        AbbreviationEn = null
                    }, ct);

                    if (!createBaseUnitResult.IsSuccess || createBaseUnitResult.Data == null)
                    {
                        units = await LoadUnitsAsync(ct);
                        if (!TryResolveLookupId(row.BaseUnitName, units, out baseUnitId))
                        {
                            row.IsValid = false;
                            row.Errors.Add($"تعذر إنشاء الوحدة الأساسية '{row.BaseUnitName}': {createBaseUnitResult.ErrorMessage}");
                            continue;
                        }
                    }
                    else
                    {
                        baseUnitId = createBaseUnitResult.Data.Id;
                        units[NormalizeLookupKey(row.BaseUnitName)] = baseUnitId;
                    }
                }

                row.ResolvedBaseUnitId = baseUnitId;

                if (!string.IsNullOrWhiteSpace(row.MinorUnitName))
                {
                    if (!TryResolveLookupId(row.MinorUnitName, units, out var minorUnitId))
                    {
                        var normalizedMinorUnit = NormalizeLookupKey(row.MinorUnitName);
                        var createMinorUnitResult = await _unitService.CreateAsync(new CreateUnitDto
                        {
                            NameAr = normalizedMinorUnit,
                            NameEn = null,
                            AbbreviationAr = CreateUnitAbbreviation(normalizedMinorUnit),
                            AbbreviationEn = null
                        }, ct);

                        if (!createMinorUnitResult.IsSuccess || createMinorUnitResult.Data == null)
                        {
                            units = await LoadUnitsAsync(ct);
                            if (!TryResolveLookupId(row.MinorUnitName, units, out minorUnitId))
                            {
                                row.IsValid = false;
                                row.Errors.Add($"تعذر إنشاء الوحدة الجزئية '{row.MinorUnitName}': {createMinorUnitResult.ErrorMessage}");
                                continue;
                            }
                        }
                        else
                        {
                            minorUnitId = createMinorUnitResult.Data.Id;
                            units[NormalizeLookupKey(row.MinorUnitName)] = minorUnitId;
                        }
                    }

                    if (minorUnitId == baseUnitId)
                    {
                        row.IsValid = false;
                        row.Errors.Add("الوحدة الجزئية يجب أن تكون مختلفة عن الوحدة الأساسية.");
                        continue;
                    }

                    row.ResolvedMinorUnitId = minorUnitId;
                }

                if (!string.IsNullOrWhiteSpace(row.SupplierName) &&
                    !TryResolveLookupId(row.SupplierName, suppliers, out var supplierId))
                {
                    var nextCodeResult = await _supplierService.GetNextCodeAsync(ct);
                    if (nextCodeResult.IsSuccess && !string.IsNullOrWhiteSpace(nextCodeResult.Data))
                    {
                        var createSupplierResult = await _supplierService.CreateAsync(new CreateSupplierDto
                        {
                            Code = nextCodeResult.Data,
                            NameAr = NormalizeLookupKey(row.SupplierName),
                            NameEn = null,
                            Phone = null,
                            Mobile = null,
                            Address = null,
                            City = null,
                            TaxNumber = null,
                            PreviousBalance = 0,
                            Notes = "تم الإنشاء تلقائياً من استيراد الأصناف"
                        }, ct);

                        if (createSupplierResult.IsSuccess && createSupplierResult.Data != null)
                        {
                            supplierId = createSupplierResult.Data.Id;
                            suppliers[NormalizeLookupKey(row.SupplierName)] = supplierId;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(row.SupplierName) &&
                    TryResolveLookupId(row.SupplierName, suppliers, out var resolvedSupplierId))
                {
                    row.ResolvedSupplierId = resolvedSupplierId;
                }
            }
        }

        private static bool TryResolveLookupId(string rawName, Dictionary<string, int> source, out int id)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                id = default;
                return false;
            }

            var normalized = NormalizeLookupKey(rawName);
            return source.TryGetValue(normalized, out id);
        }

        private static string CreateUnitAbbreviation(string unitName)
        {
            var cleaned = new string((unitName ?? string.Empty)
                .Where(ch => !char.IsWhiteSpace(ch))
                .ToArray());

            if (string.IsNullOrWhiteSpace(cleaned))
                return "وحدة";

            return cleaned.Length <= 10
                ? cleaned
                : cleaned.Substring(0, 10);
        }

        private async Task<Dictionary<string, int>> LoadCategoriesAsync(CancellationToken ct)
        {
            var result = await _categoryService.GetAllAsync(ct);
            if (!result.IsSuccess || result.Data == null) return new Dictionary<string, int>();
            // H-20 fix: use GroupBy+First to avoid ArgumentException on duplicate NameAr
            return result.Data
                .GroupBy(c => NormalizeLookupKey(c.NameAr), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<Dictionary<string, int>> LoadUnitsAsync(CancellationToken ct)
        {
            var result = await _unitService.GetAllAsync(ct);
            if (!result.IsSuccess || result.Data == null) return new Dictionary<string, int>();
            return result.Data
                .GroupBy(u => NormalizeLookupKey(u.NameAr), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<Dictionary<string, int>> LoadSuppliersAsync(CancellationToken ct)
        {
            var result = await _supplierService.GetAllAsync(ct);
            if (!result.IsSuccess || result.Data == null) return new Dictionary<string, int>();
            return result.Data
                .GroupBy(s => NormalizeLookupKey(s.NameAr), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// C-13 fix: Single call to GetAllAsync instead of two separate calls.
        /// Returns both existing codes and barcodes in one pass.
        /// </summary>
        private async Task<(HashSet<string> Codes, HashSet<string> Barcodes)> LoadExistingProductDataAsync(CancellationToken ct)
        {
            var result = await _productService.GetAllAsync(ct);
            if (!result.IsSuccess || result.Data == null)
                return (new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            var codes = new HashSet<string>(result.Data.Select(p => p.Code), StringComparer.OrdinalIgnoreCase);
            var barcodes = new HashSet<string>(
                result.Data.Where(p => !string.IsNullOrEmpty(p.Barcode)).Select(p => p.Barcode),
                StringComparer.OrdinalIgnoreCase);

            return (codes, barcodes);
        }

        private static decimal GetDecimal(IXLCell cell)
        {
            if (cell.IsEmpty()) return 0;
            if (cell.DataType == XLDataType.Number)
                return (decimal)cell.GetDouble();

            var text = cell.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            var normalized = NormalizeNumericText(text);

            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.CurrentCulture, out var currentVal))
                return currentVal;

            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariantVal))
                return invariantVal;

            if (decimal.TryParse(normalized, NumberStyles.Any, new CultureInfo("ar-EG"), out var arVal))
                return arVal;

            if (decimal.TryParse(normalized, NumberStyles.Any, new CultureInfo("en-US"), out var enVal))
                return enVal;

            return 0;
        }

        private static string NormalizeLookupKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value
                .Replace('\u00A0', ' ')
                .Replace('\u200F', ' ')
                .Replace('\u200E', ' ')
                .Replace("ـ", string.Empty)
                .Trim();

            return string.Join(" ", normalized.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string NormalizeNumericText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalizedChars = value
                .Select(ch => ch switch
                {
                    '٠' => '0',
                    '١' => '1',
                    '٢' => '2',
                    '٣' => '3',
                    '٤' => '4',
                    '٥' => '5',
                    '٦' => '6',
                    '٧' => '7',
                    '٨' => '8',
                    '٩' => '9',
                    '۰' => '0',
                    '۱' => '1',
                    '۲' => '2',
                    '۳' => '3',
                    '۴' => '4',
                    '۵' => '5',
                    '۶' => '6',
                    '۷' => '7',
                    '۸' => '8',
                    '۹' => '9',
                    '٫' => '.',
                    '٬' => ',',
                    '\u00A0' => ' ',
                    '\u200F' => ' ',
                    '\u200E' => ' ',
                    _ => ch
                })
                .ToArray();

            return new string(normalizedChars).Trim();
        }
    }
}
