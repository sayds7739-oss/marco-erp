using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Inventory
{
    /// <summary>
    /// Imports products from Excel files with full validation and lookup resolution.
    /// </summary>
    public sealed partial class ProductImportService : IProductImportService
    {
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IUnitService _unitService;
        private readonly ISupplierService _supplierService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFeatureService _featureService;
        private readonly ILogger<ProductImportService> _logger;

        // â”€â”€ Expected column order (Arabic headers) â”€â”€
        private static readonly string[] ExpectedHeaders = new[]
        {
            "ظƒظˆط¯ ط§ظ„طµظ†ظپ",        // 0 - Code (required)
            "ط§ط³ظ… ط§ظ„طµظ†ظپ (ط¹ط±ط¨ظٹ)", // 1 - NameAr (required)
            "ط§ط³ظ… ط§ظ„طµظ†ظپ (ط¥ظ†ط¬ظ„ظٹط²ظٹ)", // 2 - NameEn
            "ط§ظ„طھطµظ†ظٹظپ",          // 3 - CategoryName (required)
            "ط§ظ„ظˆط­ط¯ط© ط§ظ„ط£ط³ط§ط³ظٹط©",  // 4 - BaseUnitName (required)
            "ط§ظ„ظˆط­ط¯ط© ط§ظ„ط¬ط²ط¦ظٹط©",   // 5 - MinorUnitName (optional)
            "ظ…ط¹ط§ظ…ظ„ ط§ظ„طھط­ظˆظٹظ„",    // 6 - MinorUnitConversionFactor (optional)
            "ط³ط¹ط± ط§ظ„طھظƒظ„ظپط©",      // 7 - CostPrice
            "ط³ط¹ط± ط§ظ„ط¨ظٹط¹",       // 8 - DefaultSalePrice
            "ط§ظ„ط­ط¯ ط§ظ„ط£ط¯ظ†ظ‰",      // 9 - MinimumStock
            "ط­ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ط·ظ„ط¨",   // 10 - ReorderLevel
            "ظ†ط³ط¨ط© ط§ظ„ط¶ط±ظٹط¨ط© %",   // 11 - VatRate
            "ط§ظ„ط¨ط§ط±ظƒظˆط¯",         // 12 - Barcode
            "ط§ظ„ظˆطµظپ",           // 13 - Description
            "ط§ظ„ظ…ظˆط±ط¯ ط§ظ„ط§ظپطھط±ط§ط¶ظٹ", // 14 - SupplierName
        };

        public ProductImportService(
            IProductService productService,
            ICategoryService categoryService,
            IUnitService unitService,
            ISupplierService supplierService,
            IUnitOfWork unitOfWork,
            IFeatureService featureService,
            ILogger<ProductImportService> logger = null)
        {
            _productService = productService;
            _categoryService = categoryService;
            _unitService = unitService;
            _supplierService = supplierService;
            _unitOfWork = unitOfWork;
            _featureService = featureService;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductImportService>.Instance;
        }

        /// <inheritdoc />
        public async Task<ServiceResult<IReadOnlyList<ProductImportRowDto>>> ParseExcelAsync(
            string filePath, CancellationToken ct = default)
        {
            try
            {
                // H-22 fix: Check file size before processing
                var fileInfo = new System.IO.FileInfo(filePath);
                if (!fileInfo.Exists)
                    return ServiceResult<IReadOnlyList<ProductImportRowDto>>.Failure(
                        "ط§ظ„ظ…ظ„ظپ ط؛ظٹط± ظ…ظˆط¬ظˆط¯.");
                const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
                if (fileInfo.Length > MaxFileSizeBytes)
                    return ServiceResult<IReadOnlyList<ProductImportRowDto>>.Failure(
                        $"ط­ط¬ظ… ط§ظ„ظ…ظ„ظپ ({fileInfo.Length / (1024 * 1024):N1} MB) ظٹطھط¬ط§ظˆط² ط§ظ„ط­ط¯ ط§ظ„ط£ظ‚طµظ‰ ط§ظ„ظ…ط³ظ…ظˆط­ (10 MB).");

                using var workbook = new XLWorkbook(filePath);
                var ws = workbook.Worksheets.FirstOrDefault();
                if (ws == null)
                    return ServiceResult<IReadOnlyList<ProductImportRowDto>>.Failure(
                        "ط§ظ„ظ…ظ„ظپ ظ„ط§ ظٹط­طھظˆظٹ ط¹ظ„ظ‰ ط£ظٹ ظˆط±ظ‚ط© ط¹ظ…ظ„.");

                // Validate headers
                var headerErrors = ValidateHeaders(ws);
                if (headerErrors != null)
                    return ServiceResult<IReadOnlyList<ProductImportRowDto>>.Failure(headerErrors);

                // H-22 fix: Check row count limit
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                const int MaxRowCount = 10_000;
                if (lastRow - 1 > MaxRowCount)
                    return ServiceResult<IReadOnlyList<ProductImportRowDto>>.Failure(
                        $"ط¹ط¯ط¯ ط§ظ„طµظپظˆظپ ({lastRow - 1}) ظٹطھط¬ط§ظˆط² ط§ظ„ط­ط¯ ط§ظ„ط£ظ‚طµظ‰ ط§ظ„ظ…ط³ظ…ظˆط­ ({MaxRowCount:N0}).");

                // Load lookup data
                var categories = await LoadCategoriesAsync(ct);
                var units = await LoadUnitsAsync(ct);
                var suppliers = await LoadSuppliersAsync(ct);
                var (existingCodes, existingBarcodes) = await LoadExistingProductDataAsync(ct);
                var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Parse rows
                var rows = new List<ProductImportRowDto>();

                for (var row = 2; row <= lastRow; row++)
                {
                    ct.ThrowIfCancellationRequested();

                    var code = ws.Cell(row, 1).GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(code)) continue; // Skip empty rows

                    var importRow = new ProductImportRowDto
                    {
                        RowNumber = row - 1,
                        Code = code,
                        NameAr = ws.Cell(row, 2).GetString()?.Trim(),
                        NameEn = ws.Cell(row, 3).GetString()?.Trim(),
                        CategoryName = ws.Cell(row, 4).GetString()?.Trim(),
                        BaseUnitName = ws.Cell(row, 5).GetString()?.Trim(),
                        MinorUnitName = ws.Cell(row, 6).GetString()?.Trim(),
                        MinorUnitConversionFactor = GetDecimal(ws.Cell(row, 7)),
                        CostPrice = GetDecimal(ws.Cell(row, 8)),
                        DefaultSalePrice = GetDecimal(ws.Cell(row, 9)),
                        MinimumStock = GetDecimal(ws.Cell(row, 10)),
                        ReorderLevel = GetDecimal(ws.Cell(row, 11)),
                        VatRate = GetDecimal(ws.Cell(row, 12)),
                        Barcode = ws.Cell(row, 13).GetString()?.Trim(),
                        Description = ws.Cell(row, 14).GetString()?.Trim(),
                        SupplierName = ws.Cell(row, 15).GetString()?.Trim(),
                    };

                    ValidateRow(importRow, categories, units, suppliers, existingCodes, existingBarcodes, seenCodes, seenBarcodes);
                    rows.Add(importRow);
                }

                if (rows.Count == 0)
                    return ServiceResult<IReadOnlyList<ProductImportRowDto>>.Failure(
                        "ط§ظ„ظ…ظ„ظپ ظ„ط§ ظٹط­طھظˆظٹ ط¹ظ„ظ‰ ط£ظٹ ط¨ظٹط§ظ†ط§طھ ط£طµظ†ط§ظپ.");

                return ServiceResult<IReadOnlyList<ProductImportRowDto>>.Success(rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ParseAsync failed for ProductImport.");
                return ServiceResult<IReadOnlyList<ProductImportRowDto>>.Failure(
                    ErrorSanitizer.SanitizeGeneric(ex, "قراءة ملف الاستيراد"));
            }
        }

        /// <inheritdoc />
        public async Task<ServiceResult<ProductImportResultDto>> ImportAsync(
            IReadOnlyList<ProductImportRowDto> rows, CancellationToken ct = default)
        {
            // Feature guard: Inventory must be enabled
            var guard = await FeatureGuard.CheckAsync<ProductImportResultDto>(_featureService, FeatureKeys.Inventory, ct);
            if (guard != null) return guard;

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ImportAsync", "ProductImport", 0);
            var result = new ProductImportResultDto { TotalRows = rows.Count };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await EnsureLookupEntitiesAsync(rows, ct);

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
                        var dto = new CreateProductDto
                        {
                            Code = row.Code,
                            NameAr = row.NameAr,
                            NameEn = row.NameEn,
                            CategoryId = row.ResolvedCategoryId!.Value,
                            BaseUnitId = row.ResolvedBaseUnitId!.Value,
                            CostPrice = row.CostPrice,
                            DefaultSalePrice = row.DefaultSalePrice,
                            MinimumStock = row.MinimumStock,
                            ReorderLevel = row.ReorderLevel,
                            VatRate = row.VatRate,
                            Barcode = row.Barcode,
                            Description = row.Description,
                            DefaultSupplierId = row.ResolvedSupplierId,
                            Units = new List<CreateProductUnitDto>()
                        };

                        if (row.ResolvedMinorUnitId.HasValue &&
                            row.ResolvedMinorUnitId.Value != row.ResolvedBaseUnitId!.Value &&
                            row.MinorUnitConversionFactor > 0)
                        {
                            dto.Units.Add(new CreateProductUnitDto
                            {
                                UnitId = row.ResolvedMinorUnitId.Value,
                                ConversionFactor = row.MinorUnitConversionFactor,
                                SalePrice = row.DefaultSalePrice > 0
                                    ? Math.Round(row.DefaultSalePrice / row.MinorUnitConversionFactor, 4)
                                    : 0,
                                PurchasePrice = row.CostPrice > 0
                                    ? Math.Round(row.CostPrice / row.MinorUnitConversionFactor, 4)
                                    : 0,
                                Barcode = null,
                                IsDefault = false
                            });
                        }

                        var createResult = await _productService.CreateAsync(dto, ct);

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
                        _logger.LogError(ex, "ImportAsync failed for row {RowNumber}.", row.RowNumber);
                        row.IsValid = false;
                        row.Errors.Add(ErrorSanitizer.SanitizeGeneric(ex, "استيراد الصنف"));
                        result.FailedCount++;
                        result.FailedRows.Add(row);
                    }
                }
            }, cancellationToken: ct);

            return ServiceResult<ProductImportResultDto>.Success(result);
        }

        /// <inheritdoc />
        public Task<ServiceResult<string>> GenerateTemplateAsync(
            string outputPath, CancellationToken ct = default)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.AddWorksheet("ط£طµظ†ط§ظپ");
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
                ws.Cell(2, 1).Value = "P001";
                ws.Cell(2, 2).Value = "طµظ†ظپ طھط¬ط±ظٹط¨ظٹ";
                ws.Cell(2, 3).Value = "Sample Product";
                ws.Cell(2, 4).Value = "طھطµظ†ظٹظپ ط¹ط§ظ…";
                ws.Cell(2, 5).Value = "ظ‚ط·ط¹ط©";
                ws.Cell(2, 6).Value = "ط­ط¨ط©";
                ws.Cell(2, 7).Value = 12;
                ws.Cell(2, 8).Value = 10.00;
                ws.Cell(2, 9).Value = 15.00;
                ws.Cell(2, 10).Value = 5;
                ws.Cell(2, 11).Value = 10;
                ws.Cell(2, 12).Value = 15;
                ws.Cell(2, 13).Value = "123456789";
                ws.Cell(2, 14).Value = "طµظ†ظپ طھط¬ط±ظٹط¨ظٹ ظ„ظ„طھظˆط¶ظٹط­";
                ws.Cell(2, 15).Value = "";

                // Column widths
                ws.Column(1).Width = 15;
                ws.Column(2).Width = 25;
                ws.Column(3).Width = 25;
                ws.Column(4).Width = 18;
                ws.Column(5).Width = 15;
                ws.Column(6).Width = 14;
                ws.Column(7).Width = 14;
                ws.Column(8).Width = 14;
                ws.Column(9).Width = 14;
                ws.Column(10).Width = 12;
                ws.Column(11).Width = 14;
                ws.Column(12).Width = 12;
                ws.Column(13).Width = 18;
                ws.Column(14).Width = 30;
                ws.Column(15).Width = 20;

                // Instruction row
                var noteRow = ws.Cell(4, 1);
                noteRow.Value = "ظ…ظ„ط§ط­ط¸ط©: ط§ظ„ط£ط¹ظ…ط¯ط© ط§ظ„ظ…ط·ظ„ظˆط¨ط© ظ‡ظٹ: ظƒظˆط¯ ط§ظ„طµظ†ظپطŒ ط§ط³ظ… ط§ظ„طµظ†ظپ (ط¹ط±ط¨ظٹ)طŒ ط§ظ„طھطµظ†ظٹظپطŒ ط§ظ„ظˆط­ط¯ط© ط§ظ„ط£ط³ط§ط³ظٹط©. ط¥ط°ط§ طھظ… ط¥ط¯ط®ط§ظ„ ط§ظ„ظˆط­ط¯ط© ط§ظ„ط¬ط²ط¦ظٹط© ظپظٹط¬ط¨ ط¥ط¯ط®ط§ظ„ ظ…ط¹ط§ظ…ظ„ ط§ظ„طھط­ظˆظٹظ„ (> 0).";
                noteRow.Style.Font.FontColor = XLColor.Red;
                noteRow.Style.Font.Italic = true;

                workbook.SaveAs(outputPath);
                return Task.FromResult(ServiceResult<string>.Success(outputPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateTemplateAsync failed for ProductImport.");
                return Task.FromResult(ServiceResult<string>.Failure(
                    ErrorSanitizer.SanitizeGeneric(ex, "إنشاء قالب الاستيراد")));
            }
        }
    }
}
