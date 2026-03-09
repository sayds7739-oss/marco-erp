using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Treasury;

namespace MarcoERP.Application.Interfaces.Treasury
{
    /// <summary>
    /// Application service for Cashbox management.
    /// </summary>
    public interface ICashboxService
    {
        /// <summary>استرجاع جميع الخزن — Gets all cashboxes.</summary>
        Task<ServiceResult<IReadOnlyList<CashboxDto>>> GetAllAsync(CancellationToken ct = default);

        /// <summary>استرجاع الخزن النشطة فقط — Gets only active cashboxes.</summary>
        Task<ServiceResult<IReadOnlyList<CashboxDto>>> GetActiveAsync(CancellationToken ct = default);

        /// <summary>استرجاع خزنة بالمعرّف — Gets a cashbox by ID.</summary>
        Task<ServiceResult<CashboxDto>> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>معاينة الكود التالي قبل الإنشاء — Gets next generated cashbox code preview.</summary>
        Task<ServiceResult<string>> GetNextCodePreviewAsync(CancellationToken ct = default);

        /// <summary>إنشاء خزنة جديدة — Creates a new cashbox.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult<CashboxDto>> CreateAsync(CreateCashboxDto dto, CancellationToken ct = default);

        /// <summary>تعديل خزنة — Updates an existing cashbox.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult<CashboxDto>> UpdateAsync(UpdateCashboxDto dto, CancellationToken ct = default);

        /// <summary>تعيين خزنة كافتراضية — Sets a cashbox as the default.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult> SetDefaultAsync(int id, CancellationToken ct = default);

        /// <summary>تفعيل خزنة — Activates a cashbox.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult> ActivateAsync(int id, CancellationToken ct = default);

        /// <summary>تعطيل خزنة — Deactivates a cashbox.</summary>
        [RequiresPermission(PermissionKeys.TreasuryCreate)]
        Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct = default);
    }
}
