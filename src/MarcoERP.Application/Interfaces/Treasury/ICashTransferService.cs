using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Treasury;

namespace MarcoERP.Application.Interfaces.Treasury
{
    /// <summary>
    /// Application service for CashTransfer management.
    /// Handles creation, posting (with auto-journal), cancellation, and deletion of cash transfers.
    /// </summary>
    public interface ICashTransferService
    {
        /// <summary>استرجاع جميع التحويلات — Gets all cash transfers (list view).</summary>
        Task<ServiceResult<IReadOnlyList<CashTransferListDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>استرجاع تحويل بالمعرّف — Gets a cash transfer by ID.</summary>
        Task<ServiceResult<CashTransferDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>استرجاع الرقم التالي للتحويل — Gets the next auto-generated transfer number.</summary>
        Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default);

        /// <summary>إنشاء تحويل جديد — Creates a new draft cash transfer.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult<CashTransferDto>> CreateAsync(CreateCashTransferDto dto, CancellationToken ct = default);

        /// <summary>تعديل تحويل — Updates a draft cash transfer.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult<CashTransferDto>> UpdateAsync(UpdateCashTransferDto dto, CancellationToken ct = default);

        /// <summary>ترحيل تحويل — Posts a cash transfer (generates auto-journal).</summary>
        [RequiresPermission(PermissionKeys.TreasuryPost)]
        Task<ServiceResult<CashTransferDto>> PostAsync(int id, CancellationToken ct = default);

        /// <summary>إلغاء تحويل مرحّل — Cancels a posted cash transfer.</summary>
        [RequiresPermission(PermissionKeys.TreasuryPost)]
        Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default);

        /// <summary>حذف مسودة تحويل — Deletes a draft cash transfer.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default);
    }
}
