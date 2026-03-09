using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Purchases;

namespace MarcoERP.Application.Interfaces.Purchases
{
    /// <summary>
    /// Service for importing suppliers from Excel files.
    /// </summary>
    public interface ISupplierImportService
    {
        /// <summary>
        /// Reads an Excel file and parses supplier rows without saving.
        /// Returns parsed rows with validation results for preview.
        /// </summary>
        [RequiresPermission(PermissionKeys.PurchasesCreate)]
        Task<ServiceResult<IReadOnlyList<SupplierImportRowDto>>> ParseExcelAsync(
            string filePath, CancellationToken ct = default);

        /// <summary>
        /// Imports validated supplier rows into the database.
        /// Skips rows that fail validation.
        /// </summary>
        [RequiresPermission(PermissionKeys.PurchasesCreate)]
        Task<ServiceResult<SupplierImportResultDto>> ImportAsync(
            IReadOnlyList<SupplierImportRowDto> rows, CancellationToken ct = default);

        /// <summary>
        /// Generates a template Excel file with correct column headers and sample data.
        /// </summary>
        Task<ServiceResult<string>> GenerateTemplateAsync(
            string outputPath, CancellationToken ct = default);
    }
}
