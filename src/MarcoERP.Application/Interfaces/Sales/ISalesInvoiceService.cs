using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;

namespace MarcoERP.Application.Interfaces.Sales
{
    /// <summary>
    /// Application service contract for Sales Invoice operations.
    /// Handles CRUD, posting (auto revenue journal + COGS journal + stock deduction), and cancellation.
    /// </summary>
    public interface ISalesInvoiceService
    {
        /// <summary>Gets all sales invoices (list view).</summary>
        Task<ServiceResult<IReadOnlyList<SalesInvoiceListDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Gets a sales invoice with all lines.</summary>
        Task<ServiceResult<SalesInvoiceDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>Gets the next auto-generated invoice number.</summary>
        Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default);

        /// <summary>Creates a new draft sales invoice.</summary>
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult<SalesInvoiceDto>> CreateAsync(CreateSalesInvoiceDto dto, CancellationToken ct = default);

        /// <summary>Updates a draft sales invoice.</summary>
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult<SalesInvoiceDto>> UpdateAsync(UpdateSalesInvoiceDto dto, CancellationToken ct = default);

        /// <summary>
        /// Posts a draft invoice: generates revenue journal + COGS journal, deducts stock, creates inventory movements.
        /// </summary>
        [RequiresPermission(PermissionKeys.SalesPost)]
        Task<ServiceResult<SalesInvoiceDto>> PostAsync(int id, CancellationToken ct = default);

        /// <summary>Cancels a posted invoice (reversal journals + stock restoration).</summary>
        [RequiresPermission(PermissionKeys.SalesPost)]
        Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default);

        /// <summary>Deletes a draft invoice (hard delete — not yet posted).</summary>
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default);
    }
}
