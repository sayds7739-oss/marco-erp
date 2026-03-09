using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastReport;
using FastReport.Export.PdfSimple;
using FastReport.Utils;
using Microsoft.Extensions.Logging;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Reports;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Reports;
using MarcoERP.Application.Interfaces.Settings;

namespace MarcoERP.Application.Services.Reports
{
    /// <summary>
    /// FastReport-based implementation of IReportExportService.
    /// Uses FastReport OpenSource engine to generate PDF and Excel files
    /// from the universal ReportExportRequest DTO.
    /// </summary>
    [Module(Domain.Enums.SystemModule.Reporting)]
    public sealed class FastReportExportService : IReportExportService
    {
        private readonly IDateTimeProvider _dateTime;
        private readonly IFeatureService _featureService;
        private readonly ILogger<FastReportExportService> _logger;

        public FastReportExportService(
            IDateTimeProvider dateTime,
            IFeatureService featureService = null,
            ILogger<FastReportExportService> logger = null)
        {
            _dateTime = dateTime;
            _featureService = featureService;
            _logger = logger;
        }

        public async Task<ServiceResult<string>> ExportToPdfAsync(
            ReportExportRequest request, string outputPath, CancellationToken ct = default)
        {
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<string>(_featureService, FeatureKeys.Reporting, ct);
                if (guard != null) return guard;
            }

            if (request == null)
                return ServiceResult<string>.Failure("بيانات التقرير مطلوبة.");
            if (string.IsNullOrWhiteSpace(outputPath))
                return ServiceResult<string>.Failure("مسار الملف مطلوب.");

            try
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using var report = BuildReport(request);
                report.Prepare();

                using var pdfExport = new PDFSimpleExport();
                report.Export(pdfExport, outputPath);

                return ServiceResult<string>.Success(outputPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "FastReport PDF export failed for '{Title}'", request.Title);
                return ServiceResult<string>.Failure(ErrorSanitizer.SanitizeGeneric(ex, "تصدير PDF"));
            }
        }

        public async Task<ServiceResult<string>> ExportToExcelAsync(
            ReportExportRequest request, string outputPath, CancellationToken ct = default)
        {
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<string>(_featureService, FeatureKeys.Reporting, ct);
                if (guard != null) return guard;
            }

            if (request == null)
                return ServiceResult<string>.Failure("بيانات التقرير مطلوبة.");
            if (string.IsNullOrWhiteSpace(outputPath))
                return ServiceResult<string>.Failure("مسار الملف مطلوب.");

            try
            {
                // FastReport OpenSource doesn't include Excel export.
                // Fall back to ClosedXML for Excel generation.
                return await ExportToExcelViaClosedXmlAsync(request, outputPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "FastReport Excel export failed for '{Title}'", request.Title);
                return ServiceResult<string>.Failure(ErrorSanitizer.SanitizeGeneric(ex, "تصدير Excel"));
            }
        }

        /// <summary>
        /// Builds a FastReport Report object programmatically from ReportExportRequest.
        /// Uses the FastReport object model to create a report in memory (no .frx file needed).
        /// </summary>
        private Report BuildReport(ReportExportRequest request)
        {
            var report = new Report();

            // Create DataTable from request data
            var dt = new DataTable("ReportData");
            foreach (var col in request.Columns)
                dt.Columns.Add(col.Header, typeof(string));

            foreach (var row in request.Rows)
            {
                var dr = dt.NewRow();
                for (int i = 0; i < Math.Min(row.Count, request.Columns.Count); i++)
                    dr[i] = row[i] ?? "";
                dt.Rows.Add(dr);
            }

            report.RegisterData(dt, "ReportData");
            report.GetDataSource("ReportData").Enabled = true;

            // ── Page Setup ──
            var page = new ReportPage();
            report.Pages.Add(page);
            page.Landscape = true;
            page.PaperWidth = 297; // A4 Landscape
            page.PaperHeight = 210;
            page.LeftMargin = 10;
            page.RightMargin = 10;
            page.TopMargin = 10;
            page.BottomMargin = 10;
            float pageWidth = (page.PaperWidth - page.LeftMargin - page.RightMargin) * Units.Millimeters;

            // ── Report Title Band ──
            var titleBand = new ReportTitleBand();
            page.ReportTitle = titleBand;
            titleBand.Height = Units.Centimeters * 1.5f;

            var titleText = new TextObject();
            titleText.Name = "txtTitle";
            titleText.Bounds = new System.Drawing.RectangleF(0, 0, pageWidth, Units.Centimeters * 0.8f);
            titleText.Text = request.Title ?? "تقرير";
            titleText.Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold);
            titleText.HorzAlign = HorzAlign.Center;
            titleText.TextColor = System.Drawing.Color.FromArgb(33, 33, 33);
            titleBand.Objects.Add(titleText);

            if (!string.IsNullOrWhiteSpace(request.Subtitle))
            {
                titleBand.Height = Units.Centimeters * 2.2f;
                var subtitleText = new TextObject();
                subtitleText.Name = "txtSubtitle";
                subtitleText.Bounds = new System.Drawing.RectangleF(0, Units.Centimeters * 0.9f, pageWidth, Units.Centimeters * 0.6f);
                subtitleText.Text = request.Subtitle;
                subtitleText.Font = new System.Drawing.Font("Segoe UI", 10);
                subtitleText.HorzAlign = HorzAlign.Center;
                subtitleText.TextColor = System.Drawing.Color.FromArgb(117, 117, 117);
                titleBand.Objects.Add(subtitleText);
            }

            // ── Column Header Band ──
            var headerBand = new DataHeaderBand();
            page.Bands.Add(headerBand);
            headerBand.Height = Units.Centimeters * 0.8f;

            float totalRatio = request.Columns.Sum(c => c.WidthRatio);
            float xPos = 0;

            foreach (var col in request.Columns)
            {
                float colWidth = (col.WidthRatio / totalRatio) * pageWidth;
                var headerCell = new TextObject();
                headerCell.Bounds = new System.Drawing.RectangleF(xPos, 0, colWidth, Units.Centimeters * 0.8f);
                headerCell.Text = col.Header;
                headerCell.Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
                headerCell.FillColor = System.Drawing.Color.FromArgb(38, 50, 56);
                headerCell.TextColor = System.Drawing.Color.White;
                headerCell.HorzAlign = HorzAlign.Center;
                headerCell.VertAlign = VertAlign.Center;
                headerCell.Border.Lines = BorderLines.All;
                headerCell.Border.Color = System.Drawing.Color.FromArgb(69, 90, 100);
                headerBand.Objects.Add(headerCell);
                xPos += colWidth;
            }

            // ── Data Band ──
            var dataBand = new DataBand();
            dataBand.Name = "DataBand1";
            dataBand.Height = Units.Centimeters * 0.7f;
            dataBand.DataSource = report.GetDataSource("ReportData");
            page.Bands.Add(dataBand);

            // Alternating row colors
            dataBand.EvenStyle = "EvenRows";

            xPos = 0;
            for (int i = 0; i < request.Columns.Count; i++)
            {
                var col = request.Columns[i];
                float colWidth = (col.WidthRatio / totalRatio) * pageWidth;

                var dataCell = new TextObject();
                dataCell.Bounds = new System.Drawing.RectangleF(xPos, 0, colWidth, Units.Centimeters * 0.7f);
                dataCell.Text = $"[ReportData.{col.Header}]";
                dataCell.Font = new System.Drawing.Font("Segoe UI", 8);
                dataCell.HorzAlign = col.IsNumeric ? HorzAlign.Left : HorzAlign.Right;
                dataCell.VertAlign = VertAlign.Center;
                dataCell.Border.Lines = BorderLines.Left | BorderLines.Right | BorderLines.Bottom;
                dataCell.Border.Color = System.Drawing.Color.FromArgb(189, 189, 189);
                dataBand.Objects.Add(dataCell);
                xPos += colWidth;
            }

            // ── Footer Band ──
            if (!string.IsNullOrWhiteSpace(request.FooterSummary))
            {
                var footerBand = new DataFooterBand();
                page.Bands.Add(footerBand);
                footerBand.Height = Units.Centimeters * 0.8f;

                var footerText = new TextObject();
                footerText.Bounds = new System.Drawing.RectangleF(0, 0, pageWidth, Units.Centimeters * 0.8f);
                footerText.Text = request.FooterSummary;
                footerText.Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
                footerText.HorzAlign = HorzAlign.Right;
                footerText.FillColor = System.Drawing.Color.FromArgb(245, 245, 245);
                footerText.Border.Lines = BorderLines.All;
                footerBand.Objects.Add(footerText);
            }

            // ── Page Footer ──
            var pageFooterBand = new PageFooterBand();
            page.PageFooter = pageFooterBand;
            pageFooterBand.Height = Units.Centimeters * 0.6f;

            var pageFooterText = new TextObject();
            pageFooterText.Bounds = new System.Drawing.RectangleF(0, 0, pageWidth, Units.Centimeters * 0.6f);
            pageFooterText.Text = $"MarcoERP — {_dateTime.UtcNow:yyyy/MM/dd} — صفحة [Page] / [TotalPages]";
            pageFooterText.Font = new System.Drawing.Font("Segoe UI", 7);
            pageFooterText.HorzAlign = HorzAlign.Center;
            pageFooterText.TextColor = System.Drawing.Color.Gray;
            pageFooterBand.Objects.Add(pageFooterText);

            return report;
        }

        /// <summary>
        /// Falls back to ClosedXML for Excel (FastReport OpenSource doesn't include Excel export).
        /// </summary>
        private Task<ServiceResult<string>> ExportToExcelViaClosedXmlAsync(ReportExportRequest request, string outputPath)
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.AddWorksheet("Report");

            // Title row
            ws.Cell(1, 1).Value = request.Title ?? "تقرير";
            ws.Range(1, 1, 1, request.Columns.Count).Merge().Style
                .Font.SetBold(true).Font.SetFontSize(14)
                .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

            int startRow = 2;
            if (!string.IsNullOrWhiteSpace(request.Subtitle))
            {
                ws.Cell(2, 1).Value = request.Subtitle;
                ws.Range(2, 1, 2, request.Columns.Count).Merge().Style
                    .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);
                startRow = 3;
            }

            // Header row
            for (int i = 0; i < request.Columns.Count; i++)
            {
                var cell = ws.Cell(startRow, i + 1);
                cell.Value = request.Columns[i].Header;
                cell.Style.Font.SetBold(true).Font.SetFontColor(ClosedXML.Excel.XLColor.White);
                cell.Style.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(38, 50, 56));
                cell.Style.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);
                cell.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
            }

            // Data rows
            for (int r = 0; r < request.Rows.Count; r++)
            {
                var row = request.Rows[r];
                for (int c = 0; c < Math.Min(row.Count, request.Columns.Count); c++)
                {
                    var cell = ws.Cell(startRow + 1 + r, c + 1);
                    var val = row[c] ?? "";
                    if (request.Columns[c].IsNumeric && decimal.TryParse(val, out var num))
                        cell.Value = num;
                    else
                        cell.Value = val;
                    cell.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                }
            }

            // Footer
            if (!string.IsNullOrWhiteSpace(request.FooterSummary))
            {
                int footerRow = startRow + 1 + request.Rows.Count;
                ws.Cell(footerRow, 1).Value = request.FooterSummary;
                ws.Range(footerRow, 1, footerRow, request.Columns.Count).Merge().Style
                    .Font.SetBold(true)
                    .Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(245, 245, 245));
            }

            ws.Columns().AdjustToContents();
            ws.RightToLeft = true;
            workbook.SaveAs(outputPath);

            return Task.FromResult(ServiceResult<string>.Success(outputPath));
        }
    }
}
