using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;

namespace MarcoERP.Application.Interfaces.Sales
{
    /// <summary>
    /// Service for importing customers from Excel files.
    /// </summary>
    public interface ICustomerImportService
    {
        /// <summary>
        /// Reads an Excel file and parses customer rows without saving.
        /// Returns parsed rows with validation results for preview.
        /// </summary>
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult<IReadOnlyList<CustomerImportRowDto>>> ParseExcelAsync(
            string filePath, CancellationToken ct = default);

        /// <summary>
        /// Imports validated customer rows into the database.
        /// Skips rows that fail validation.
        /// </summary>
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult<CustomerImportResultDto>> ImportAsync(
            IReadOnlyList<CustomerImportRowDto> rows, CancellationToken ct = default);

        /// <summary>
        /// Generates a template Excel file with correct column headers and sample data.
        /// </summary>
        Task<ServiceResult<string>> GenerateTemplateAsync(
            string outputPath, CancellationToken ct = default);
    }
}
