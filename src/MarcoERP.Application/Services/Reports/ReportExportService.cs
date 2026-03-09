using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Reports;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Reports;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Reports
{
    /// <summary>
    /// Implements report export to PDF (QuestPDF) and Excel (ClosedXML).
    /// </summary>
    [Module(SystemModule.Reporting)]
    public sealed class ReportExportService : IReportExportService
    {
        private readonly IDateTimeProvider _dateTime;
        private readonly IFeatureService _featureService;
        private readonly ILogger<ReportExportService> _logger;

        static ReportExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public ReportExportService(IDateTimeProvider dateTime, IFeatureService featureService = null, ILogger<ReportExportService> logger = null)
        {
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _featureService = featureService;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ReportExportService>.Instance;
        }

        public async Task<ServiceResult<string>> ExportToPdfAsync(ReportExportRequest request, string outputPath, CancellationToken ct)
        {
            // Feature Guard — block operation if Reporting module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<string>(_featureService, FeatureKeys.Reporting, ct);
                if (guard != null) return guard;
            }

            try
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(30);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        // ── Header ──
                        page.Header().Column(col =>
                        {
                            col.Item().AlignCenter().Text(request.Title)
                                .FontSize(18).Bold();

                            if (!string.IsNullOrWhiteSpace(request.Subtitle))
                                col.Item().AlignCenter().Text(request.Subtitle)
                                    .FontSize(12).FontColor(Colors.Grey.Medium);

                            col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        });

                        // ── Content ──
                        page.Content().PaddingVertical(10).Table(table =>
                        {
                            // Define columns
                            table.ColumnsDefinition(columns =>
                            {
                                foreach (var col in request.Columns)
                                    columns.RelativeColumn(col.WidthRatio);
                            });

                            // Header row
                            foreach (var col in request.Columns)
                            {
                                table.Cell().Background(Colors.Blue.Darken2)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text(col.Header)
                                    .FontColor(Colors.White)
                                    .FontSize(10)
                                    .Bold();
                            }

                            // Data rows
                            bool alternate = false;
                            foreach (var row in request.Rows)
                            {
                                var bgColor = alternate ? Colors.Grey.Lighten4 : Colors.White;
                                for (int i = 0; i < row.Count && i < request.Columns.Count; i++)
                                {
                                    var cell = table.Cell().Background(bgColor).Padding(4);
                                    if (request.Columns[i].IsNumeric)
                                        cell.AlignLeft().Text(row[i] ?? "").FontSize(10);
                                    else
                                        cell.AlignRight().Text(row[i] ?? "").FontSize(10);
                                }
                                alternate = !alternate;
                            }
                        });

                        // ── Footer ──
                        page.Footer().Column(col =>
                        {
                            if (!string.IsNullOrWhiteSpace(request.FooterSummary))
                            {
                                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                                col.Item().PaddingTop(5).AlignCenter().Text(request.FooterSummary)
                                    .FontSize(11).Bold();
                            }

                            col.Item().AlignCenter().Text(txt =>
                            {
                                txt.Span($"MarcoERP — {_dateTime.UtcNow:yyyy/MM/dd HH:mm}  —  صفحة ")
                                    .FontSize(8).FontColor(Colors.Grey.Medium);
                                txt.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                                txt.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                                txt.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                            });
                        });
                    });
                }).GeneratePdf(outputPath);

                return ServiceResult<string>.Success(outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExportToPdfAsync failed.");
                return ServiceResult<string>.Failure(ErrorSanitizer.SanitizeGeneric(ex, "تصدير PDF"));
            }
        }

        public async Task<ServiceResult<string>> ExportToExcelAsync(ReportExportRequest request, string outputPath, CancellationToken ct)
        {
            // Feature Guard — block operation if Reporting module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<string>(_featureService, FeatureKeys.Reporting, ct);
                if (guard != null) return guard;
            }

            try
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add(request.Title?.Length > 31
                    ? request.Title.Substring(0, 31) : request.Title ?? "Report");

                // ── Title ──
                int row = 1;
                ws.Cell(row, 1).Value = request.Title ?? "";
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 16;
                ws.Range(row, 1, row, request.Columns.Count).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                if (!string.IsNullOrWhiteSpace(request.Subtitle))
                {
                    row++;
                    ws.Cell(row, 1).Value = request.Subtitle;
                    ws.Cell(row, 1).Style.Font.FontColor = XLColor.Gray;
                    ws.Range(row, 1, row, request.Columns.Count).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                row += 2;

                // ── Header Row ──
                for (int c = 0; c < request.Columns.Count; c++)
                {
                    var cell = ws.Cell(row, c + 1);
                    cell.Value = request.Columns[c].Header;
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                // ── Data Rows ──
                foreach (var dataRow in request.Rows)
                {
                    row++;
                    for (int c = 0; c < dataRow.Count && c < request.Columns.Count; c++)
                    {
                        var cell = ws.Cell(row, c + 1);
                        var val = dataRow[c] ?? "";

                        if (request.Columns[c].IsNumeric && decimal.TryParse(val, out var numVal))
                            cell.Value = numVal;
                        else
                            cell.Value = val;

                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }
                }

                // ── Footer ──
                if (!string.IsNullOrWhiteSpace(request.FooterSummary))
                {
                    row += 2;
                    ws.Cell(row, 1).Value = request.FooterSummary;
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    ws.Range(row, 1, row, request.Columns.Count).Merge();
                }

                // Auto-fit columns
                ws.Columns().AdjustToContents();

                workbook.SaveAs(outputPath);
                return ServiceResult<string>.Success(outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExportToExcelAsync failed.");
                return ServiceResult<string>.Failure(ErrorSanitizer.SanitizeGeneric(ex, "تصدير Excel"));
            }
        }
    }
}
