using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Treasury;

namespace MarcoERP.Application.Interfaces.Treasury
{
    /// <summary>
    /// Application service for Bank Account management.
    /// </summary>
    public interface IBankAccountService
    {
        /// <summary>استرجاع جميع الحسابات البنكية — Gets all bank accounts.</summary>
        Task<ServiceResult<IReadOnlyList<BankAccountDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>استرجاع الحسابات البنكية النشطة — Gets only active bank accounts.</summary>
        Task<ServiceResult<IReadOnlyList<BankAccountDto>>> GetActiveAsync(CancellationToken ct = default);

        /// <summary>استرجاع حساب بنكي بالمعرّف — Gets a bank account by ID.</summary>
        Task<ServiceResult<BankAccountDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>إنشاء حساب بنكي جديد — Creates a new bank account.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult<BankAccountDto>> CreateAsync(CreateBankAccountDto dto, CancellationToken ct = default);

        /// <summary>تعديل حساب بنكي — Updates an existing bank account.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult<BankAccountDto>> UpdateAsync(UpdateBankAccountDto dto, CancellationToken ct = default);

        /// <summary>تعيين حساب بنكي كافتراضي — Sets a bank account as the default.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult> SetDefaultAsync(int id, CancellationToken ct = default);

        /// <summary>تفعيل حساب بنكي — Activates a bank account.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult> ActivateAsync(int id, CancellationToken ct = default);

        /// <summary>تعطيل حساب بنكي — Deactivates a bank account.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct = default);
    }
}
