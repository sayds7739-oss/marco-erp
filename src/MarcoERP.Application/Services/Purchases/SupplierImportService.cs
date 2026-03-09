using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Purchases
{
    /// <summary>
    /// Imports suppliers from Excel files with full validation.
    /// </summary>
    public sealed class SupplierImportService : ISupplierImportService
    {
        private readonly ISupplierService _supplierService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFeatureService _featureService;
        private readonly ILogger<SupplierImportService> _logger;

        // ── Expected column order (Arabic headers) ──
        private static readonly string[] ExpectedHeaders = new[]
        {
            "الكود",              // 0  - Code (required)
            "الاسم بالعربي",       // 1  - NameAr (required)
            "الاسم بالإنجليزي",    // 2  - NameEn
            "الهاتف",             // 3  - Phone
            "الجوال",             // 4  - Mobile
            "البريد",             // 5  - Email
            "الرقم الضريبي",      // 6  - TaxNumber
            "العنوان",            // 7  - Address
            "المدينة",            // 8  - City
            "مدة السداد بالأيام",  // 9  - PaymentTermDays
            "ملاحظات",            // 10 - Notes
        };

        // Basic email validation pattern
        private static readonly Regex EmailRegex = new(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public SupplierImportService(
            ISupplierService supplierService,
            IUnitOfWork unitOfWork,
            IFeatureService featureService,
            ILogger<SupplierImportService> logger = null)
        {
            _supplierService = supplierService;
            _unitOfWork = unitOfWork;
            _featureService = featureService;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SupplierImportService>.Instance;
        }

        /// <inheritdoc />
        public async Task<ServiceResult<IReadOnlyList<SupplierImportRowDto>>> ParseExcelAsync(
            string filePath, CancellationToken ct = default)
        {
            try
            {
                // Check file existence and size
                var fileInfo = new System.IO.FileInfo(filePath);
                if (!fileInfo.Exists)
                    return ServiceResult<IReadOnlyList<SupplierImportRowDto>>.Failure(
                        "الملف غير موجود.");
                const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
                if (fileInfo.Length > MaxFileSizeBytes)
                    return ServiceResult<IReadOnlyList<SupplierImportRowDto>>.Failure(
                        $"حجم الملف ({fileInfo.Length / (1024 * 1024):N1} MB) يتجاوز الحد الأقصى المسموح (10 MB).");

                using var workbook = new XLWorkbook(filePath);
                var ws = workbook.Worksheets.FirstOrDefault();
                if (ws == null)
                    return ServiceResult<IReadOnlyList<SupplierImportRowDto>>.Failure(
                        "الملف لا يحتوي على أي ورقة عمل.");

                // Validate headers
                var headerErrors = ValidateHeaders(ws);
                if (headerErrors != null)
                    return ServiceResult<IReadOnlyList<SupplierImportRowDto>>.Failure(headerErrors);

                // Check row count limit
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                const int MaxRowCount = 10_000;
                if (lastRow - 1 > MaxRowCount)
                    return ServiceResult<IReadOnlyList<SupplierImportRowDto>>.Failure(
                        $"عدد الصفوف ({lastRow - 1}) يتجاوز الحد الأقصى المسموح ({MaxRowCount:N0}).");

                // Load existing supplier codes for duplicate detection
                var existingCodes = await LoadExistingSupplierCodesAsync(ct);
                var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Parse rows
                var rows = new List<SupplierImportRowDto>();

                for (var row = 2; row <= lastRow; row++)
                {
                    ct.ThrowIfCancellationRequested();

                    var code = ws.Cell(row, 1).GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(code)) continue; // Skip empty rows

                    var importRow = new SupplierImportRowDto
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
                        PaymentTermDays = GetInt(ws.Cell(row, 10), defaultValue: 30),
                        Notes = ws.Cell(row, 11).GetString()?.Trim(),
                    };

                    ValidateRow(importRow, existingCodes, seenCodes);
                    rows.Add(importRow);
                }

                if (rows.Count == 0)
                    return ServiceResult<IReadOnlyList<SupplierImportRowDto>>.Failure(
                        "الملف لا يحتوي على أي بيانات موردين.");

                return ServiceResult<IReadOnlyList<SupplierImportRowDto>>.Success(rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ParseAsync failed for SupplierImport.");
                return ServiceResult<IReadOnlyList<SupplierImportRowDto>>.Failure(
                    ErrorSanitizer.SanitizeGeneric(ex, "قراءة ملف استيراد الموردين"));
            }
        }

        /// <inheritdoc />
        public async Task<ServiceResult<SupplierImportResultDto>> ImportAsync(
            IReadOnlyList<SupplierImportRowDto> rows, CancellationToken ct = default)
        {
            // Feature guard: Purchases must be enabled
            var guard = await FeatureGuard.CheckAsync<SupplierImportResultDto>(_featureService, FeatureKeys.Purchases, ct);
            if (guard != null) return guard;

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ImportAsync", "SupplierImport", 0);
            var result = new SupplierImportResultDto { TotalRows = rows.Count };

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
                        var dto = new CreateSupplierDto
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
                            DaysAllowed = row.PaymentTermDays,
                            Notes = row.Notes,
                            PreviousBalance = 0,
                        };

                        var createResult = await _supplierService.CreateAsync(dto, ct);

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
                        _logger.LogError(ex, "ImportAsync failed for supplier row {RowNumber}.", row.RowNumber);
                        row.IsValid = false;
                        row.Errors.Add(ErrorSanitizer.SanitizeGeneric(ex, "استيراد المورد"));
                        result.FailedCount++;
                        result.FailedRows.Add(row);
                    }
                }
            }, cancellationToken: ct);

            return ServiceResult<SupplierImportResultDto>.Success(result);
        }

        /// <inheritdoc />
        public Task<ServiceResult<string>> GenerateTemplateAsync(
            string outputPath, CancellationToken ct = default)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.AddWorksheet("موردين");
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
                ws.Cell(2, 1).Value = "SUP001";
                ws.Cell(2, 2).Value = "مورد تجريبي";
                ws.Cell(2, 3).Value = "Sample Supplier";
                ws.Cell(2, 4).Value = "0112345678";
                ws.Cell(2, 5).Value = "0501234567";
                ws.Cell(2, 6).Value = "supplier@example.com";
                ws.Cell(2, 7).Value = "300000000000003";
                ws.Cell(2, 8).Value = "شارع الملك فهد";
                ws.Cell(2, 9).Value = "الرياض";
                ws.Cell(2, 10).Value = 30;
                ws.Cell(2, 11).Value = "مورد تجريبي للتوضيح";

                // Column widths
                ws.Column(1).Width = 15;
                ws.Column(2).Width = 25;
                ws.Column(3).Width = 25;
                ws.Column(4).Width = 18;
                ws.Column(5).Width = 18;
                ws.Column(6).Width = 25;
                ws.Column(7).Width = 20;
                ws.Column(8).Width = 25;
                ws.Column(9).Width = 15;
                ws.Column(10).Width = 18;
                ws.Column(11).Width = 30;

                // Instruction row
                var noteRow = ws.Cell(4, 1);
                noteRow.Value = "ملاحظة: الأعمدة المطلوبة هي: الكود، الاسم بالعربي. مدة السداد الافتراضية 30 يوم إذا لم يتم إدخالها.";
                noteRow.Style.Font.FontColor = XLColor.Red;
                noteRow.Style.Font.Italic = true;

                workbook.SaveAs(outputPath);
                return Task.FromResult(ServiceResult<string>.Success(outputPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateTemplateAsync failed for SupplierImport.");
                return Task.FromResult(ServiceResult<string>.Failure(
                    ErrorSanitizer.SanitizeGeneric(ex, "إنشاء قالب استيراد الموردين")));
            }
        }

        // ══════════════════════════════════════════════════════════
        // PRIVATE HELPERS
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
            SupplierImportRowDto row,
            HashSet<string> existingCodes,
            HashSet<string> seenCodes)
        {
            // ── Code: required, max 20 ──
            if (string.IsNullOrWhiteSpace(row.Code))
            {
                row.IsValid = false;
                row.Errors.Add("كود المورد مطلوب.");
            }
            else if (row.Code.Length > 20)
            {
                row.IsValid = false;
                row.Errors.Add("كود المورد يجب أن لا يزيد عن 20 حرف.");
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

            // ── NameAr: required, max 200 ──
            if (string.IsNullOrWhiteSpace(row.NameAr))
            {
                row.IsValid = false;
                row.Errors.Add("اسم المورد (عربي) مطلوب.");
            }
            else if (row.NameAr.Length > 200)
            {
                row.IsValid = false;
                row.Errors.Add("اسم المورد يجب أن لا يزيد عن 200 حرف.");
            }

            // ── NameEn: optional, max 200 ──
            if (!string.IsNullOrEmpty(row.NameEn) && row.NameEn.Length > 200)
            {
                row.IsValid = false;
                row.Errors.Add("اسم المورد (إنجليزي) يجب أن لا يزيد عن 200 حرف.");
            }

            // ── Phone: optional, max 30 ──
            if (!string.IsNullOrEmpty(row.Phone) && row.Phone.Length > 30)
            {
                row.IsValid = false;
                row.Errors.Add("رقم الهاتف يجب أن لا يزيد عن 30 حرف.");
            }

            // ── Mobile: optional, max 30 ──
            if (!string.IsNullOrEmpty(row.Mobile) && row.Mobile.Length > 30)
            {
                row.IsValid = false;
                row.Errors.Add("رقم الجوال يجب أن لا يزيد عن 30 حرف.");
            }

            // ── Email: optional, max 100, basic format ──
            if (!string.IsNullOrEmpty(row.Email))
            {
                if (row.Email.Length > 100)
                {
                    row.IsValid = false;
                    row.Errors.Add("البريد الإلكتروني يجب أن لا يزيد عن 100 حرف.");
                }
                else if (!EmailRegex.IsMatch(row.Email))
                {
                    row.IsValid = false;
                    row.Errors.Add("صيغة البريد الإلكتروني غير صحيحة.");
                }
            }

            // ── TaxNumber: optional, max 30 ──
            if (!string.IsNullOrEmpty(row.TaxNumber) && row.TaxNumber.Length > 30)
            {
                row.IsValid = false;
                row.Errors.Add("الرقم الضريبي يجب أن لا يزيد عن 30 حرف.");
            }

            // ── Address: optional, max 500 ──
            if (!string.IsNullOrEmpty(row.Address) && row.Address.Length > 500)
            {
                row.IsValid = false;
                row.Errors.Add("العنوان يجب أن لا يزيد عن 500 حرف.");
            }

            // ── City: optional, max 100 ──
            if (!string.IsNullOrEmpty(row.City) && row.City.Length > 100)
            {
                row.IsValid = false;
                row.Errors.Add("المدينة يجب أن لا يزيد عن 100 حرف.");
            }

            // ── PaymentTermDays: >= 0 ──
            if (row.PaymentTermDays < 0)
            {
                row.IsValid = false;
                row.Errors.Add("مدة السداد بالأيام لا يمكن أن تكون سالبة.");
            }

            // ── Notes: optional, max 500 ──
            if (!string.IsNullOrEmpty(row.Notes) && row.Notes.Length > 500)
            {
                row.IsValid = false;
                row.Errors.Add("الملاحظات يجب أن لا تزيد عن 500 حرف.");
            }
        }

        private async Task<HashSet<string>> LoadExistingSupplierCodesAsync(CancellationToken ct)
        {
            var result = await _supplierService.GetAllAsync(ct);
            if (!result.IsSuccess || result.Data == null)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return new HashSet<string>(
                result.Data.Select(s => s.Code),
                StringComparer.OrdinalIgnoreCase);
        }

        private static int GetInt(IXLCell cell, int defaultValue = 0)
        {
            if (cell.IsEmpty()) return defaultValue;
            if (cell.DataType == XLDataType.Number)
                return (int)cell.GetDouble();

            var text = cell.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return defaultValue;

            if (int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
                return val;

            if (int.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var val2))
                return val2;

            return defaultValue;
        }
    }
}
