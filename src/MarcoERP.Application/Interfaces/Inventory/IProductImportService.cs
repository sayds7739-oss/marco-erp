using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;

namespace MarcoERP.Application.Interfaces.Inventory
{
    /// <summary>
    /// Service for importing products from Excel files.
    /// </summary>
    public interface IProductImportService
    {
        /// <summary>
        /// Reads an Excel file and parses product rows without saving.
        /// Returns parsed rows with validation results for preview.
        /// </summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult<IReadOnlyList<ProductImportRowDto>>> ParseExcelAsync(
            string filePath, CancellationToken ct = default);

        /// <summary>
        /// Imports validated product rows into the database.
        /// Skips rows that fail validation.
        /// </summary>
        [RequiresPermission(PermissionKeys.InventoryManage)]
        Task<ServiceResult<ProductImportResultDto>> ImportAsync(
            IReadOnlyList<ProductImportRowDto> rows, CancellationToken ct = default);

        /// <summary>
        /// Generates a template Excel file with correct column headers and sample data.
        /// </summary>
        Task<ServiceResult<string>> GenerateTemplateAsync(
            string outputPath, CancellationToken ct = default);
    }
}
