using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Sales
{
    /// <summary>
    /// Imports customers from Excel files with full validation.
    /// Follows the same pattern as ProductImportService.
    /// </summary>
    public sealed class CustomerImportService : ICustomerImportService
    {
        private readonly ICustomerService _customerService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFeatureService _featureService;
        private readonly ILogger<CustomerImportService> _logger;

        // ── Expected column order (Arabic headers) ──
        private static readonly string[] ExpectedHeaders = new[]
        {
            "الكود",           // 0 - Code (required)
            "الاسم بالعربي",    // 1 - NameAr (required)
            "الاسم بالإنجليزي",  // 2 - NameEn
            "الهاتف",          // 3 - Phone
            "الجوال",          // 4 - Mobile
            "البريد",          // 5 - Email
            "الرقم الضريبي",    // 6 - TaxNumber
            "العنوان",          // 7 - Address
            "المدينة",          // 8 - City
            "حد الائتمان",      // 9 - CreditLimit
            "نسبة الخصم",      // 10 - DefaultDiscountPercent
            "نوع العميل",      // 11 - CustomerType
            "ملاحظات",         // 12 - Notes
        };

        public CustomerImportService(
            ICustomerService customerService,
            IUnitOfWork unitOfWork,
            IFeatureService featureService,
            ILogger<CustomerImportService> logger = null)
        {
            _customerService = customerService;
            _unitOfWork = unitOfWork;
            _featureService = featureService;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CustomerImportService>.Instance;
        }

        /// <inheritdoc />
        public async Task<ServiceResult<IReadOnlyList<CustomerImportRowDto>>> ParseExcelAsync(
            string filePath, CancellationToken ct = default)
        {
            try
            {
                // Check file size before processing
                var fileInfo = new System.IO.FileInfo(filePath);
                if (!fileInfo.Exists)
                    return ServiceResult<IReadOnlyList<CustomerImportRowDto>>.Failure(
                        "الملف غير موجود.");
                const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
                if (fileInfo.Length > MaxFileSizeBytes)
                    return ServiceResult<IReadOnlyList<CustomerImportRowDto>>.Failure(
                        $"حجم الملف ({fileInfo.Length / (1024 * 1024):N1} MB) يتجاوز الحد الأقصى المسموح (10 MB).");

                using var workbook = new XLWorkbook(filePath);
                var ws = workbook.Worksheets.FirstOrDefault();
                if (ws == null)
                    return ServiceResult<IReadOnlyList<CustomerImportRowDto>>.Failure(
                        "الملف لا يحتوي على أي ورقة عمل.");

                // Validate headers
                var headerErrors = ValidateHeaders(ws);
                if (headerErrors != null)
                    return ServiceResult<IReadOnlyList<CustomerImportRowDto>>.Failure(headerErrors);

                // Check row count limit
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                const int MaxRowCount = 10_000;
                if (lastRow - 1 > MaxRowCount)
                    return ServiceResult<IReadOnlyList<CustomerImportRowDto>>.Failure(
                        $"عدد الصفوف ({lastRow - 1}) يتجاوز الحد الأقصى المسموح ({MaxRowCount:N0}).");

                // Load existing customer codes for duplicate detection
                var existingCodes = await LoadExistingCustomerCodesAsync(ct);
                var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Parse rows
                var rows = new List<CustomerImportRowDto>();

                for (var row = 2; row <= lastRow; row++)
                {
                    ct.ThrowIfCancellationRequested();

                    var code = ws.Cell(row, 1).GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(code)) continue; // Skip empty rows

                    var importRow = new CustomerImportRowDto
                    {
                        RowNumber = row - 1,
                        Code = code,
                        NameAr = ws.Cell(row, 2).GetString()?.Trim(),
                        NameEn = ws.Cell(row, 3).GetString()?.Trim(),
                        Phone = ws.Cell(row, 4).GetString()?.Trim(),
                        Mobile = ws.Cell(row, 5).GetString()?.Trim(),
                        Email = ws.Cell(row, 6).GetString()?.Trim(),
                        TaxNumber = ws.Cell(row, 7).GetString()?.Trim(),
                        Address = ws.Cell(row, 8).GetString()?.Trim(),
                        City = ws.Cell(row, 9).GetString()?.Trim(),
                        CreditLimit = GetDecimal(ws.Cell(row, 10)),
                        DefaultDiscountPercent = GetDecimal(ws.Cell(row, 11)),
                        CustomerTypeName = ws.Cell(row, 12).GetString()?.Trim(),
                        Notes = ws.Cell(row, 13).GetString()?.Trim(),
                    };

                    ValidateRow(importRow, existingCodes, seenCodes);
                    rows.Add(importRow);
                }

                if (rows.Count == 0)
                    return ServiceResult<IReadOnlyList<CustomerImportRowDto>>.Failure(
                        "الملف لا يحتوي على أي بيانات عملاء.");

                return ServiceResult<IReadOnlyList<CustomerImportRowDto>>.Success(rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ParseAsync failed for CustomerImport.");
                return ServiceResult<IReadOnlyList<CustomerImportRowDto>>.Failure(
                    ErrorSanitizer.SanitizeGeneric(ex, "قراءة ملف استيراد العملاء"));
            }
        }

        /// <inheritdoc />
        public async Task<ServiceResult<CustomerImportResultDto>> ImportAsync(
            IReadOnlyList<CustomerImportRowDto> rows, CancellationToken ct = default)
        {
            // Feature guard: Sales must be enabled
            var guard = await FeatureGuard.CheckAsync<CustomerImportResultDto>(_featureService, FeatureKeys.Sales, ct);
            if (guard != null) return guard;

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ImportAsync", "CustomerImport", 0);
            var result = new CustomerImportResultDto { TotalRows = rows.Count };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                foreach (var row in rows)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!row.IsValid)
                    {
                        result.SkippedCount++;
                        result.FailedRows.Add(row);
                        continue;
                    }

                    try
                    {
                        var customerType = ResolveCustomerType(row.CustomerTypeName);

                        var dto = new CreateCustomerDto
                        {
                            Code = row.Code,
                            NameAr = row.NameAr,
                            NameEn = row.NameEn,
                            Phone = row.Phone,
                            Mobile = row.Mobile,
                            Email = row.Email,
                            TaxNumber = row.TaxNumber,
                            Address = row.Address,
                            City = row.City,
                            CreditLimit = row.CreditLimit,
                            DefaultDiscountPercent = row.DefaultDiscountPercent,
                            CustomerType = customerType,
                            Notes = row.Notes,
                            PreviousBalance = 0
                        };

                        var createResult = await _customerService.CreateAsync(dto, ct);

                        if (createResult.IsSuccess)
                        {
                            result.SuccessCount++;
                        }
                        else
                        {
                            row.IsValid = false;
                            row.Errors.Add(createResult.ErrorMessage);
                            result.FailedCount++;
                            result.FailedRows.Add(row);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "ImportAsync failed for customer row {RowNumber}.", row.RowNumber);
                        row.IsValid = false;
                        row.Errors.Add(ErrorSanitizer.SanitizeGeneric(ex, "استيراد العميل"));
                        result.FailedCount++;
                        result.FailedRows.Add(row);
                    }
                }
            }, cancellationToken: ct);

            return ServiceResult<CustomerImportResultDto>.Success(result);
        }

        /// <inheritdoc />
        public Task<ServiceResult<string>> GenerateTemplateAsync(
            string outputPath, CancellationToken ct = default)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.AddWorksheet("عملاء");
                ws.RightToLeft = true;

                // Headers
                for (var i = 0; i < ExpectedHeaders.Length; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = ExpectedHeaders[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(33, 150, 243);
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // Sample row
                ws.Cell(2, 1).Value = "C001";
                ws.Cell(2, 2).Value = "عميل تجريبي";
                ws.Cell(2, 3).Value = "Sample Customer";
                ws.Cell(2, 4).Value = "0112345678";
                ws.Cell(2, 5).Value = "0501234567";
                ws.Cell(2, 6).Value = "customer@example.com";
                ws.Cell(2, 7).Value = "300000000000003";
                ws.Cell(2, 8).Value = "شارع الملك فهد";
                ws.Cell(2, 9).Value = "الرياض";
                ws.Cell(2, 10).Value = 50000;
                ws.Cell(2, 11).Value = 5;
                ws.Cell(2, 12).Value = "فرد";
                ws.Cell(2, 13).Value = "عميل تجريبي للتوضيح";

                // Column widths
                ws.Column(1).Width = 15;   // Code
                ws.Column(2).Width = 25;   // NameAr
                ws.Column(3).Width = 25;   // NameEn
                ws.Column(4).Width = 18;   // Phone
                ws.Column(5).Width = 18;   // Mobile
                ws.Column(6).Width = 25;   // Email
                ws.Column(7).Width = 20;   // TaxNumber
                ws.Column(8).Width = 25;   // Address
                ws.Column(9).Width = 15;   // City
                ws.Column(10).Width = 15;  // CreditLimit
                ws.Column(11).Width = 14;  // DiscountPercent
                ws.Column(12).Width = 14;  // CustomerType
                ws.Column(13).Width = 30;  // Notes

                // Instruction row
                var noteRow = ws.Cell(4, 1);
                noteRow.Value = "ملاحظة: الأعمدة المطلوبة هي: الكود، الاسم بالعربي. نوع العميل: فرد أو شركة (الافتراضي: فرد). نسبة الخصم بين 0 و 100.";
                noteRow.Style.Font.FontColor = XLColor.Red;
                noteRow.Style.Font.Italic = true;

                workbook.SaveAs(outputPath);
                return Task.FromResult(ServiceResult<string>.Success(outputPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateTemplateAsync failed for CustomerImport.");
                return Task.FromResult(ServiceResult<string>.Failure(
                    ErrorSanitizer.SanitizeGeneric(ex, "إنشاء قالب استيراد العملاء")));
            }
        }

        // ══════════════════════════════════════════════════════════
        // VALIDATION
        // ══════════════════════════════════════════════════════════

        private static string ValidateHeaders(IXLWorksheet ws)
        {
            // Check the required headers match expected text
            var requiredIndices = new[] { 0, 1 }; // Code, NameAr
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
            CustomerImportRowDto row,
            HashSet<string> existingCodes,
            HashSet<string> seenCodes)
        {
            // ── Code (required, max 20) ──
            if (string.IsNullOrWhiteSpace(row.Code))
            {
                row.IsValid = false;
                row.Errors.Add("كود العميل مطلوب.");
            }
            else if (row.Code.Length > 20)
            {
                row.IsValid = false;
                row.Errors.Add("كود العميل يجب أن لا يزيد عن 20 حرف.");
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

            // ── NameAr (required, max 200) ──
            if (string.IsNullOrWhiteSpace(row.NameAr))
            {
                row.IsValid = false;
                row.Errors.Add("اسم العميل بالعربي مطلوب.");
            }
            else if (row.NameAr.Length > 200)
            {
                row.IsValid = false;
                row.Errors.Add("اسم العميل يجب أن لا يزيد عن 200 حرف.");
            }

            // ── NameEn (optional, max 200) ──
            if (!string.IsNullOrEmpty(row.NameEn) && row.NameEn.Length > 200)
            {
                row.IsValid = false;
                row.Errors.Add("اسم العميل بالإنجليزي يجب أن لا يزيد عن 200 حرف.");
            }

            // ── Phone (optional, max 30) ──
            if (!string.IsNullOrEmpty(row.Phone) && row.Phone.Length > 30)
            {
                row.IsValid = false;
                row.Errors.Add("رقم الهاتف يجب أن لا يزيد عن 30 حرف.");
            }

            // ── Mobile (optional, max 30) ──
            if (!string.IsNullOrEmpty(row.Mobile) && row.Mobile.Length > 30)
            {
                row.IsValid = false;
                row.Errors.Add("رقم الجوال يجب أن لا يزيد عن 30 حرف.");
            }

            // ── Email (optional, max 100, basic format check) ──
            if (!string.IsNullOrEmpty(row.Email))
            {
                if (row.Email.Length > 100)
                {
                    row.IsValid = false;
                    row.Errors.Add("البريد الإلكتروني يجب أن لا يزيد عن 100 حرف.");
                }
                else if (!Regex.IsMatch(row.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    row.IsValid = false;
                    row.Errors.Add("صيغة البريد الإلكتروني غير صحيحة.");
                }
            }

            // ── TaxNumber (optional, max 30) ──
            if (!string.IsNullOrEmpty(row.TaxNumber) && row.TaxNumber.Length > 30)
            {
                row.IsValid = false;
                row.Errors.Add("الرقم الضريبي يجب أن لا يزيد عن 30 حرف.");
            }

            // ── Address (optional, max 500) ──
            if (!string.IsNullOrEmpty(row.Address) && row.Address.Length > 500)
            {
                row.IsValid = false;
                row.Errors.Add("العنوان يجب أن لا يزيد عن 500 حرف.");
            }

            // ── City (optional, max 100) ──
            if (!string.IsNullOrEmpty(row.City) && row.City.Length > 100)
            {
                row.IsValid = false;
                row.Errors.Add("المدينة يجب أن لا يزيد عن 100 حرف.");
            }

            // ── CreditLimit (>= 0) ──
            if (row.CreditLimit < 0)
            {
                row.IsValid = false;
                row.Errors.Add("حد الائتمان لا يمكن أن يكون سالباً.");
            }

            // ── DefaultDiscountPercent (0-100) ──
            if (row.DefaultDiscountPercent < 0 || row.DefaultDiscountPercent > 100)
            {
                row.IsValid = false;
                row.Errors.Add("نسبة الخصم يجب أن تكون بين 0 و 100.");
            }

            // ── CustomerType (optional: فرد or شركة, default فرد) ──
            if (!string.IsNullOrWhiteSpace(row.CustomerTypeName))
            {
                var normalized = NormalizeLookupKey(row.CustomerTypeName);
                if (normalized != "فرد" && normalized != "شركة")
                {
                    row.IsValid = false;
                    row.Errors.Add("نوع العميل يجب أن يكون 'فرد' أو 'شركة'.");
                }
            }

            // ── Notes (optional, max 500) ──
            if (!string.IsNullOrEmpty(row.Notes) && row.Notes.Length > 500)
            {
                row.IsValid = false;
                row.Errors.Add("الملاحظات يجب أن لا تزيد عن 500 حرف.");
            }
        }

        // ══════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════

        private static CustomerType ResolveCustomerType(string customerTypeName)
        {
            if (string.IsNullOrWhiteSpace(customerTypeName))
                return CustomerType.Individual;

            var normalized = NormalizeLookupKey(customerTypeName);
            return normalized == "شركة" ? CustomerType.Company : CustomerType.Individual;
        }

        private async Task<HashSet<string>> LoadExistingCustomerCodesAsync(CancellationToken ct)
        {
            var result = await _customerService.GetAllAsync(ct);
            if (!result.IsSuccess || result.Data == null)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return new HashSet<string>(
                result.Data.Select(c => c.Code),
                StringComparer.OrdinalIgnoreCase);
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
                .Replace("\u0640", string.Empty) // Arabic tatweel
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
                    '\u0660' => '0', // ٠
                    '\u0661' => '1', // ١
                    '\u0662' => '2', // ٢
                    '\u0663' => '3', // ٣
                    '\u0664' => '4', // ٤
                    '\u0665' => '5', // ٥
                    '\u0666' => '6', // ٦
                    '\u0667' => '7', // ٧
                    '\u0668' => '8', // ٨
                    '\u0669' => '9', // ٩
                    '\u06F0' => '0', // ۰
                    '\u06F1' => '1', // ۱
                    '\u06F2' => '2', // ۲
                    '\u06F3' => '3', // ۳
                    '\u06F4' => '4', // ۴
                    '\u06F5' => '5', // ۵
                    '\u06F6' => '6', // ۶
                    '\u06F7' => '7', // ۷
                    '\u06F8' => '8', // ۸
                    '\u06F9' => '9', // ۹
                    '\u066B' => '.', // ٫
                    '\u066C' => ',', // ٬
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
