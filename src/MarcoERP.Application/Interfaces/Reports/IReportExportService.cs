using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Reports;

namespace MarcoERP.Application.Interfaces.Reports
{
    /// <summary>
    /// Service for exporting reports to PDF and Excel formats.
    /// </summary>
    public interface IReportExportService
    {
        /// <summary>Exports report data to a PDF file. Returns the file path.</summary>
        [RequiresPermission(PermissionKeys.ReportsView)]
        Task<ServiceResult<string>> ExportToPdfAsync(ReportExportRequest request, string outputPath, CancellationToken ct = default);

        /// <summary>Exports report data to an Excel file. Returns the file path.</summary>
        [RequiresPermission(PermissionKeys.ReportsView)]
        Task<ServiceResult<string>> ExportToExcelAsync(ReportExportRequest request, string outputPath, CancellationToken ct = default);
    }
}
