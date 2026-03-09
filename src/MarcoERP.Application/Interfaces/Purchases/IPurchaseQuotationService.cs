using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Purchases;

namespace MarcoERP.Application.Interfaces.Purchases
{
    /// <summary>
    /// Application service contract for Purchase Quotation operations.
    /// Handles CRUD, status transitions, and conversion to purchase invoice.
    /// </summary>
    public interface IPurchaseQuotationService
    {
        Task<ServiceResult<IReadOnlyList<PurchaseQuotationListDto>>> GetAllAsync(CancellationToken ct = default);
        Task<ServiceResult<PurchaseQuotationDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.PurchaseQuotationCreate)]
        Task<ServiceResult<PurchaseQuotationDto>> CreateAsync(CreatePurchaseQuotationDto dto, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.PurchaseQuotationCreate)]
        Task<ServiceResult<PurchaseQuotationDto>> UpdateAsync(UpdatePurchaseQuotationDto dto, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.PurchaseQuotationCreate)]
        Task<ServiceResult> SendAsync(int id, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.PurchaseQuotationCreate)]
        Task<ServiceResult> AcceptAsync(int id, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.PurchaseQuotationCreate)]
        Task<ServiceResult> RejectAsync(int id, string reason = null, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.PurchaseQuotationCreate)]
        Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default);
        [RequiresPermission(PermissionKeys.PurchaseQuotationCreate)]
        Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Converts an accepted quotation to a draft purchase invoice.
        /// Returns the new invoice ID.
        /// </summary>
        [RequiresPermission(PermissionKeys.PurchasesCreate)]
        Task<ServiceResult<int>> ConvertToInvoiceAsync(int quotationId, CancellationToken ct = default);
    }
}
