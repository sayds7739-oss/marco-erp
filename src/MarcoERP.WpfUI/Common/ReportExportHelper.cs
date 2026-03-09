using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.DTOs.Reports;
using MarcoERP.Application.Interfaces.Reports;
using Microsoft.Win32;
using MarcoERP.WpfUI.Services;

namespace MarcoERP.WpfUI.Common
{
    /// <summary>
    /// Helper for exporting reports to PDF/Excel with SaveFileDialog.
    /// </summary>
    public static class ReportExportHelper
    {
        /// <summary>
        /// Generates a PDF using FastReport and opens it in the in-app preview.
        /// Falls back to the save dialog if the preview service is unavailable.
        /// </summary>
        public static async Task<string> ExportPdfAsync(IReportExportService exportService, ReportExportRequest request)
        {
            if (exportService == null)
                return "خدمة تصدير التقارير غير متاحة.";
            if (request == null)
                return "بيانات التقرير مطلوبة.";

            if (global::MarcoERP.WpfUI.App.Services?.GetService(typeof(IInvoicePdfPreviewService)) is IInvoicePdfPreviewService previewService)
            {
                var folder = Path.Combine(Path.GetTempPath(), "MarcoERP", "report-preview");
                Directory.CreateDirectory(folder);

                var safeTitle = BuildSafeFileName(request.Title);
                var outputPath = Path.Combine(folder, $"{safeTitle}_{Guid.NewGuid():N}.pdf");
                var previewResult = await exportService.ExportToPdfAsync(request, outputPath, CancellationToken.None);
                if (!previewResult.IsSuccess)
                    return previewResult.ErrorMessage;

                await previewService.ShowPdfFileAsync(new InvoicePdfPreviewRequest
                {
                    Title = request.Title ?? "تقرير",
                    FilePrefix = safeTitle,
                    PdfPath = previewResult.Data,
                    StartInHtmlMode = false,
                });

                return "تم فتح معاينة PDF.";
            }

            var dlg = new SaveFileDialog
            {
                Title = "تصدير PDF",
                Filter = "PDF Files (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                FileName = $"{BuildSafeFileName(request.Title)}_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            };

            if (dlg.ShowDialog() != true)
                return null;

            var result = await exportService.ExportToPdfAsync(request, dlg.FileName, CancellationToken.None);
            return result.IsSuccess ? result.Data : result.ErrorMessage;
        }

        /// <summary>
        /// Shows a Save dialog for Excel and exports the report.
        /// Returns null on cancel, file path on success, or error message.
        /// </summary>
        public static async Task<string> ExportExcelAsync(IReportExportService exportService, ReportExportRequest request)
        {
            var dlg = new SaveFileDialog
            {
                Title = "تصدير Excel",
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                // TODO: Replace with IDateTimeProvider when refactored — static class cannot use DI
                FileName = $"{request.Title}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (dlg.ShowDialog() != true)
                return null;

            var result = await exportService.ExportToExcelAsync(request, dlg.FileName, CancellationToken.None);
            return result.IsSuccess ? result.Data : result.ErrorMessage;
        }

        private static string BuildSafeFileName(string fileName)
        {
            var safe = string.IsNullOrWhiteSpace(fileName) ? "report" : fileName.Trim();
            foreach (var ch in Path.GetInvalidFileNameChars())
                safe = safe.Replace(ch, '_');

            return safe;
        }
    }
}
