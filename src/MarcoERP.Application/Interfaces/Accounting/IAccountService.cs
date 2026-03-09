using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.Interfaces.Accounting
{
    /// <summary>
    /// Application service for Chart of Accounts management.
    /// Handles CRUD, hierarchy validation, and tree queries.
    /// </summary>
    public interface IAccountService
    {
        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets an account by ID.</summary>
        Task<ServiceResult<AccountDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Gets an account by its 4-digit code.</summary>
        Task<ServiceResult<AccountDto>> GetByCodeAsync(string accountCode, CancellationToken cancellationToken = default);

        /// <summary>Gets all accounts (flat list).</summary>
        Task<ServiceResult<IReadOnlyList<AccountDto>>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Gets all accounts by type.</summary>
        Task<ServiceResult<IReadOnlyList<AccountDto>>> GetByTypeAsync(AccountType accountType, CancellationToken cancellationToken = default);

        /// <summary>Gets all postable (leaf, active, AllowPosting) accounts.</summary>
        Task<ServiceResult<IReadOnlyList<AccountDto>>> GetPostableAccountsAsync(CancellationToken cancellationToken = default);

        /// <summary>Gets the Chart of Accounts as a hierarchical tree.</summary>
        Task<ServiceResult<IReadOnlyList<AccountTreeNodeDto>>> GetAccountTreeAsync(CancellationToken cancellationToken = default);

        /// <summary>Gets all children of a parent account.</summary>
        Task<ServiceResult<IReadOnlyList<AccountDto>>> GetChildrenAsync(int parentAccountId, CancellationToken cancellationToken = default);

        // ── Commands ────────────────────────────────────────────

        /// <summary>Creates a new account with hierarchy validation.</summary>
        [RequiresPermission(PermissionKeys.AccountsCreate)]
        Task<ServiceResult<AccountDto>> CreateAsync(CreateAccountDto dto, CancellationToken cancellationToken = default);

        /// <summary>Updates an existing account's mutable fields.</summary>
        [RequiresPermission(PermissionKeys.AccountsEdit)]
        Task<ServiceResult<AccountDto>> UpdateAsync(UpdateAccountDto dto, CancellationToken cancellationToken = default);

        /// <summary>Deactivates an account (ACC-INV-10, ACC-INV-11).</summary>
        [RequiresPermission(PermissionKeys.AccountsEdit)]
        Task<ServiceResult> DeactivateAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Reactivates a previously deactivated account.</summary>
        [RequiresPermission(PermissionKeys.AccountsEdit)]
        Task<ServiceResult> ActivateAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Soft-deletes an account (ACC-INV-08, ACC-INV-10).</summary>
        [RequiresPermission(PermissionKeys.AccountsDelete)]
        Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
