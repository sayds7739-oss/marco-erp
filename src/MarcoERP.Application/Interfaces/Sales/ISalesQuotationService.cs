using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;

namespace MarcoERP.Application.Interfaces.Sales
{
    /// <summary>
    /// Application service contract for Sales Quotation operations.
    /// Handles CRUD, status transitions, and conversion to sales invoice.
    /// </summary>
    public interface ISalesQuotationService
    {
        Task<ServiceResult<IReadOnlyList<SalesQuotationListDto>>> GetAllAsync(CancellationToken ct = default);
        Task<ServiceResult<SalesQuotationDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.SalesQuotationCreate)]
        Task<ServiceResult<SalesQuotationDto>> CreateAsync(CreateSalesQuotationDto dto, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.SalesQuotationCreate)]
        Task<ServiceResult<SalesQuotationDto>> UpdateAsync(UpdateSalesQuotationDto dto, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.SalesQuotationCreate)]
        Task<ServiceResult> SendAsync(int id, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.SalesQuotationCreate)]
        Task<ServiceResult> AcceptAsync(int id, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.SalesQuotationCreate)]
        Task<ServiceResult> RejectAsync(int id, string reason = null, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.SalesQuotationCreate)]
        Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.SalesQuotationCreate)]
        Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Converts an accepted quotation to a draft sales invoice.
        /// Returns the new invoice ID.
        /// </summary>
        [RequiresPermission(PermissionKeys.SalesCreate)]
        Task<ServiceResult<int>> ConvertToInvoiceAsync(int quotationId, CancellationToken ct = default);
    }
}
