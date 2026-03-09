using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Treasury;

namespace MarcoERP.Application.Interfaces.Treasury
{
    /// <summary>
    /// Application service for CashPayment management.
    /// Handles creation, posting (with auto-journal), cancellation, and deletion of cash payments.
    /// </summary>
    public interface ICashPaymentService
    {
        /// <summary>استرجاع جميع سندات الصرف — Gets all cash payments (list view).</summary>
        Task<ServiceResult<IReadOnlyList<CashPaymentListDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>استرجاع سند صرف بالمعرّف — Gets a cash payment by ID.</summary>
        Task<ServiceResult<CashPaymentDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>استرجاع الرقم التالي لسند الصرف — Gets the next auto-generated payment number.</summary>
        Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default);

        /// <summary>إنشاء سند صرف جديد — Creates a new draft cash payment.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult<CashPaymentDto>> CreateAsync(CreateCashPaymentDto dto, CancellationToken ct = default);

        /// <summary>تعديل سند صرف — Updates a draft cash payment.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult<CashPaymentDto>> UpdateAsync(UpdateCashPaymentDto dto, CancellationToken ct = default);

        /// <summary>ترحيل سند صرف — Posts a cash payment (generates auto-journal).</summary>
        [RequiresPermission(PermissionKeys.TreasuryPost)]
        Task<ServiceResult<CashPaymentDto>> PostAsync(int id, CancellationToken ct = default);

        /// <summary>إلغاء سند صرف مرحّل — Cancels a posted cash payment.</summary>
        [RequiresPermission(PermissionKeys.TreasuryPost)]
        Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default);

        /// <summary>حذف مسودة سند صرف — Deletes a draft cash payment.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default);
    }
}
