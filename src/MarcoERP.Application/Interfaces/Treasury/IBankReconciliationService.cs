using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Treasury;

namespace MarcoERP.Application.Interfaces.Treasury
{
    /// <summary>
    /// Application service for Bank Reconciliation management.
    /// </summary>
    public interface IBankReconciliationService
    {
        /// <summary>استرجاع جميع التسويات — Gets all reconciliations.</summary>
        Task<ServiceResult<IReadOnlyList<BankReconciliationDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>استرجاع تسويات حساب بنكي — Gets reconciliations for a bank account.</summary>
        Task<ServiceResult<IReadOnlyList<BankReconciliationDto>>> GetByBankAccountAsync(int bankAccountId, CancellationToken ct = default);

        /// <summary>استرجاع تسوية بالمعرّف — Gets a reconciliation by ID with items.</summary>
        Task<ServiceResult<BankReconciliationDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>إنشاء تسوية جديدة — Creates a new reconciliation.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult<BankReconciliationDto>> CreateAsync(CreateBankReconciliationDto dto, CancellationToken ct = default);

        /// <summary>تعديل تسوية — Updates a reconciliation header.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult<BankReconciliationDto>> UpdateAsync(UpdateBankReconciliationDto dto, CancellationToken ct = default);

        /// <summary>إضافة بند تسوية — Adds an item to a reconciliation.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult<BankReconciliationDto>> AddItemAsync(CreateBankReconciliationItemDto dto, CancellationToken ct = default);

        /// <summary>حذف بند تسوية — Removes an item from a reconciliation.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult> RemoveItemAsync(int reconciliationId, int itemId, CancellationToken ct = default);

        /// <summary>اكتمال التسوية — Completes the reconciliation.</summary>
        [RequiresPermission(PermissionKeys.TreasuryPost)]
        Task<ServiceResult> CompleteAsync(int id, CancellationToken ct = default);

        /// <summary>إعادة فتح التسوية — Reopens a completed reconciliation.</summary>
        [RequiresPermission(PermissionKeys.TreasuryPost)]
        Task<ServiceResult> ReopenAsync(int id, CancellationToken ct = default);

        /// <summary>حذف تسوية — Deletes an incomplete reconciliation.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
    }
}
