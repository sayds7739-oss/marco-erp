using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Treasury;

namespace MarcoERP.Application.Interfaces.Treasury
{
    /// <summary>
    /// Application service for CashReceipt management.
    /// Handles creation, posting (with auto-journal), cancellation, and deletion of cash receipts.
    /// </summary>
    public interface ICashReceiptService
    {
        /// <summary>استرجاع جميع سندات القبض — Gets all cash receipts (list view).</summary>
        Task<ServiceResult<IReadOnlyList<CashReceiptListDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>استرجاع سند قبض بالمعرّف — Gets a cash receipt by ID.</summary>
        Task<ServiceResult<CashReceiptDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>استرجاع الرقم التالي لسند القبض — Gets the next auto-generated receipt number.</summary>
        Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default);

        /// <summary>إنشاء سند قبض جديد — Creates a new draft cash receipt.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult<CashReceiptDto>> CreateAsync(CreateCashReceiptDto dto, CancellationToken ct = default);

        /// <summary>تعديل سند قبض — Updates a draft cash receipt.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult<CashReceiptDto>> UpdateAsync(UpdateCashReceiptDto dto, CancellationToken ct = default);

        /// <summary>ترحيل سند قبض — Posts a cash receipt (generates auto-journal).</summary>
        [RequiresPermission(PermissionKeys.TreasuryPost)]
        Task<ServiceResult<CashReceiptDto>> PostAsync(int id, CancellationToken ct = default);

        /// <summary>إلغاء سند قبض مرحّل — Cancels a posted cash receipt.</summary>
        [RequiresPermission(PermissionKeys.TreasuryPost)]
        Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default);

        /// <summary>حذف مسودة سند قبض — Deletes a draft cash receipt.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default);
    }
}
