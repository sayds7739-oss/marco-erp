using System;
using System.Collections.Generic;
using System.Linq;
using MarcoERP.Application.DTOs.Inventory;

namespace MarcoERP.Application.Services.Inventory
{
    public sealed partial class ProductImportService
    {
        private static string ValidateHeaders(ClosedXML.Excel.IXLWorksheet ws)
        {
            // Check the required headers match expected text (not just non-empty)
            var requiredIndices = new[] { 0, 1, 3, 4 }; // Code, NameAr, Category, Unit
            foreach (var idx in requiredIndices)
            {
                var actual = ws.Cell(1, idx + 1).GetString()?.Trim();
                if (string.IsNullOrEmpty(actual))
                    return $"العمود {idx + 1} يجب أن يكون '{ExpectedHeaders[idx]}'.";

                if (!string.Equals(actual, ExpectedHeaders[idx], StringComparison.OrdinalIgnoreCase))
                    return $"العمود {idx + 1}: المتوقع '{ExpectedHeaders[idx]}' ولكن الموجود '{actual}'.";
            }
            return null;
        }

        private void ValidateRow(
            ProductImportRowDto row,
            Dictionary<string, int> categories,
            Dictionary<string, int> units,
            Dictionary<string, int> suppliers,
            HashSet<string> existingCodes,
            HashSet<string> existingBarcodes,
            HashSet<string> seenCodes,
            HashSet<string> seenBarcodes)
        {
            // Required fields
            if (string.IsNullOrWhiteSpace(row.Code))
            {
                row.IsValid = false;
                row.Errors.Add("كود الصنف مطلوب.");
            }
            else if (row.Code.Length > 20)
            {
                row.IsValid = false;
                row.Errors.Add("كود الصنف يجب أن لا يزيد عن 20 حرف.");
            }
            else if (existingCodes.Contains(row.Code))
            {
                row.IsValid = false;
                row.Errors.Add($"الكود '{row.Code}' موجود مسبقاً في قاعدة البيانات.");
            }
            else if (!seenCodes.Add(row.Code))
            {
                row.IsValid = false;
                row.Errors.Add($"الكود '{row.Code}' مكرر في ملف الاستيراد.");
            }

            if (string.IsNullOrWhiteSpace(row.NameAr))
            {
                row.IsValid = false;
                row.Errors.Add("اسم الصنف (عربي) مطلوب.");
            }
            else if (row.NameAr.Length > 200)
            {
                row.IsValid = false;
                row.Errors.Add("اسم الصنف يجب أن لا يزيد عن 200 حرف.");
            }

            // Category lookup
            if (string.IsNullOrWhiteSpace(row.CategoryName))
            {
                row.IsValid = false;
                row.Errors.Add("التصنيف مطلوب.");
            }
            else if (row.CategoryName.Length > 100)
            {
                row.IsValid = false;
                row.Errors.Add("اسم التصنيف يجب أن لا يزيد عن 100 حرف.");
            }
            else if (TryResolveLookupId(row.CategoryName, categories, out var catId))
            {
                row.ResolvedCategoryId = catId;
            }

            // Unit lookup
            if (string.IsNullOrWhiteSpace(row.BaseUnitName))
            {
                row.IsValid = false;
                row.Errors.Add("الوحدة الأساسية مطلوبة.");
            }
            else if (row.BaseUnitName.Length > 50)
            {
                row.IsValid = false;
                row.Errors.Add("اسم الوحدة الأساسية يجب أن لا يزيد عن 50 حرف.");
            }
            else if (TryResolveLookupId(row.BaseUnitName, units, out var unitId))
            {
                row.ResolvedBaseUnitId = unitId;
            }

            // Minor unit lookup (optional)
            var hasMinorUnit = !string.IsNullOrWhiteSpace(row.MinorUnitName);
            if (hasMinorUnit)
            {
                if (row.MinorUnitName.Length > 50)
                {
                    row.IsValid = false;
                    row.Errors.Add("اسم الوحدة الجزئية يجب أن لا يزيد عن 50 حرف.");
                }
                else if (TryResolveLookupId(row.MinorUnitName, units, out var minorUnitId))
                {
                    row.ResolvedMinorUnitId = minorUnitId;

                    if (row.ResolvedBaseUnitId.HasValue && minorUnitId == row.ResolvedBaseUnitId.Value)
                    {
                        row.IsValid = false;
                        row.Errors.Add("الوحدة الجزئية يجب أن تكون مختلفة عن الوحدة الأساسية.");
                    }
                }

                if (NormalizeLookupKey(row.MinorUnitName)
                    .Equals(NormalizeLookupKey(row.BaseUnitName), StringComparison.OrdinalIgnoreCase))
                {
                    row.IsValid = false;
                    row.Errors.Add("الوحدة الجزئية يجب أن تكون مختلفة عن الوحدة الأساسية.");
                }

                if (row.MinorUnitConversionFactor <= 0)
                {
                    row.IsValid = false;
                    row.Errors.Add("معامل التحويل للوحدة الجزئية يجب أن يكون أكبر من صفر.");
                }
            }
            else if (row.MinorUnitConversionFactor > 0)
            {
                row.IsValid = false;
                row.Errors.Add("تم إدخال معامل التحويل بدون تحديد الوحدة الجزئية.");
            }

            // Numeric validations
            if (row.CostPrice < 0)
            {
                row.IsValid = false;
                row.Errors.Add("سعر التكلفة لا يمكن أن يكون سالباً.");
            }

            if (row.DefaultSalePrice < 0)
            {
                row.IsValid = false;
                row.Errors.Add("سعر البيع لا يمكن أن يكون سالباً.");
            }

            if (row.MinimumStock < 0)
            {
                row.IsValid = false;
                row.Errors.Add("الحد الأدنى لا يمكن أن يكون سالباً.");
            }

            if (row.ReorderLevel < 0)
            {
                row.IsValid = false;
                row.Errors.Add("حد إعادة الطلب لا يمكن أن يكون سالباً.");
            }

            if (row.VatRate < 0 || row.VatRate > 100)
            {
                row.IsValid = false;
                row.Errors.Add("نسبة الضريبة يجب أن تكون بين 0 و 100.");
            }

            if (!string.IsNullOrEmpty(row.Barcode))
            {
                if (row.Barcode.Length > 50)
                {
                    row.IsValid = false;
                    row.Errors.Add("الباركود يجب أن لا يزيد عن 50 حرف.");
                }
                else if (existingBarcodes.Contains(row.Barcode))
                {
                    row.IsValid = false;
                    row.Errors.Add($"الباركود '{row.Barcode}' موجود مسبقاً في قاعدة البيانات.");
                }
                else if (!seenBarcodes.Add(row.Barcode))
                {
                    row.IsValid = false;
                    row.Errors.Add($"الباركود '{row.Barcode}' مكرر في ملف الاستيراد.");
                }
            }

            if (!string.IsNullOrEmpty(row.Description) && row.Description.Length > 500)
            {
                row.IsValid = false;
                row.Errors.Add("الوصف يجب أن لا يزيد عن 500 حرف.");
            }

            // Supplier lookup (optional)
            if (!string.IsNullOrWhiteSpace(row.SupplierName))
            {
                if (row.SupplierName.Length > 200)
                {
                    row.IsValid = false;
                    row.Errors.Add("اسم المورد يجب أن لا يزيد عن 200 حرف.");
                }
                else if (TryResolveLookupId(row.SupplierName, suppliers, out var supId))
                    row.ResolvedSupplierId = supId;
            }
        }
    }
}
