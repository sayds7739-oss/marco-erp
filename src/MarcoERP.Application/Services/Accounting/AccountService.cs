using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Accounting;
using MarcoERP.Application.Mappers.Accounting;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Accounting;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Application.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Accounting
{
    /// <summary>
    /// Application service for Chart of Accounts management.
    /// Orchestrates CRUD operations, hierarchy validation, and tree queries.
    /// </summary>
    [Module(SystemModule.Accounting)]
    public sealed class AccountService : IAccountService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IAuditLogger _auditLogger;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IValidator<CreateAccountDto> _createValidator;
        private readonly IValidator<UpdateAccountDto> _updateValidator;
        private readonly ILogger<AccountService> _logger;
        private readonly IFeatureService _featureService;

        public AccountService(
            IAccountRepository accountRepository,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IAuditLogger auditLogger,
            IDateTimeProvider dateTimeProvider,
            IValidator<CreateAccountDto> createValidator,
            IValidator<UpdateAccountDto> updateValidator,
            ILogger<AccountService> logger = null,
            IFeatureService featureService = null)
        {
            _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountService>.Instance;
            _featureService = featureService;
        }

        // ── Queries ─────────────────────────────────────────────

        public async Task<ServiceResult<AccountDto>> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            var account = await _accountRepository.GetByIdAsync(id, cancellationToken);
            if (account == null)
                return ServiceResult<AccountDto>.Failure("الحساب غير موجود.");

            return ServiceResult<AccountDto>.Success(AccountMapper.ToDto(account));
        }

        public async Task<ServiceResult<AccountDto>> GetByCodeAsync(string accountCode, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(accountCode))
                return ServiceResult<AccountDto>.Failure("كود الحساب مطلوب.");

            var account = await _accountRepository.GetByCodeAsync(accountCode.Trim(), cancellationToken);
            if (account == null)
                return ServiceResult<AccountDto>.Failure($"لا يوجد حساب بالكود '{accountCode}'.");

            return ServiceResult<AccountDto>.Success(AccountMapper.ToDto(account));
        }

        public async Task<ServiceResult<IReadOnlyList<AccountDto>>> GetAllAsync(CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetAllAsync(cancellationToken);
            var dtos = accounts.Select(AccountMapper.ToDto).ToList();
            return ServiceResult<IReadOnlyList<AccountDto>>.Success(dtos);
        }

        public async Task<ServiceResult<IReadOnlyList<AccountDto>>> GetByTypeAsync(
            Domain.Enums.AccountType accountType, CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetByTypeAsync(accountType, cancellationToken);
            var dtos = accounts.Select(AccountMapper.ToDto).ToList();
            return ServiceResult<IReadOnlyList<AccountDto>>.Success(dtos);
        }

        public async Task<ServiceResult<IReadOnlyList<AccountDto>>> GetPostableAccountsAsync(CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetPostableAccountsAsync(cancellationToken);
            var dtos = accounts.Select(AccountMapper.ToDto).ToList();
            return ServiceResult<IReadOnlyList<AccountDto>>.Success(dtos);
        }

        public async Task<ServiceResult<IReadOnlyList<AccountDto>>> GetChildrenAsync(int parentAccountId, CancellationToken cancellationToken)
        {
            var children = await _accountRepository.GetChildrenAsync(parentAccountId, cancellationToken);
            var dtos = children.Select(AccountMapper.ToDto).ToList();
            return ServiceResult<IReadOnlyList<AccountDto>>.Success(dtos);
        }

        /// <summary>
        /// Builds the full Chart of Accounts as a hierarchical tree.
        /// Level 1 accounts are roots; each node includes its children recursively.
        /// </summary>
        public async Task<ServiceResult<IReadOnlyList<AccountTreeNodeDto>>> GetAccountTreeAsync(CancellationToken cancellationToken)
        {
            var allAccounts = await _accountRepository.GetAllAsync(cancellationToken);
            var nodes = allAccounts.Select(AccountMapper.ToTreeNode).ToList();

            // Build lookup: parentId → children
            var lookup = new Dictionary<int, List<AccountTreeNodeDto>>();
            foreach (var node in nodes)
            {
                lookup[node.Id] = new List<AccountTreeNodeDto>();
            }

            var roots = new List<AccountTreeNodeDto>();

            foreach (var acct in allAccounts)
            {
                var node = nodes.First(n => n.Id == acct.Id);
                if (acct.ParentAccountId.HasValue && lookup.ContainsKey(acct.ParentAccountId.Value))
                {
                    lookup[acct.ParentAccountId.Value].Add(node);
                }
                else
                {
                    roots.Add(node);
                }
            }

            // Assign children to each node
            foreach (var node in nodes)
            {
                if (lookup.ContainsKey(node.Id))
                {
                    node.Children = lookup[node.Id]
                        .OrderBy(c => c.AccountCode)
                        .ToList();
                }
            }

            var sortedRoots = roots.OrderBy(r => r.AccountCode).ToList();
            return ServiceResult<IReadOnlyList<AccountTreeNodeDto>>.Success(sortedRoots);
        }

        // ── Commands ────────────────────────────────────────────

        /// <summary>
        /// Creates a new account with full hierarchy validation.
        /// Steps:
        /// 1. Validate DTO format (FluentValidation)
        /// 2. Check code uniqueness
        /// 3. If child: load parent, validate parent exists, validate Level=parent.Level+1
        /// 4. Validate child code range (HIER-03)
        /// 5. Create domain entity (triggers domain invariants)
        /// 6. If parent was a leaf, mark it as parent (HIER-05)
        /// 7. Persist and audit
        /// </summary>
        public async Task<ServiceResult<AccountDto>> CreateAsync(CreateAccountDto dto, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "Account", 0);
            // Feature Guard — block operation if Accounting module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<AccountDto>(_featureService, FeatureKeys.Accounting, cancellationToken);
                if (guard != null) return guard;
            }

            // Defense-in-depth: auth guard (primary check is in AuthorizationProxy)
            if (!_currentUser.IsAuthenticated)
                return ServiceResult<AccountDto>.Failure("يجب تسجيل الدخول أولاً.");
            if (!_currentUser.HasPermission(PermissionKeys.AccountsCreate))
                return ServiceResult<AccountDto>.Failure("لا تملك الصلاحية لتنفيذ هذه العملية.");

            // Step 1: DTO format validation
            var validationResult = await _createValidator.ValidateAsync(dto, cancellationToken);
            if (!validationResult.IsValid)
                return ServiceResult<AccountDto>.Failure(validationResult.Errors.Select(e => e.ErrorMessage));

            // Step 2: Check code uniqueness
            var codeExists = await _accountRepository.CodeExistsAsync(dto.AccountCode, cancellationToken);
            if (codeExists)
                return ServiceResult<AccountDto>.Failure($"كود الحساب '{dto.AccountCode}' مستخدم بالفعل.");

            // Step 3+4: Parent validation for non-root accounts
            Account parentAccount = null;
            if (dto.ParentAccountId.HasValue)
            {
                parentAccount = await _accountRepository.GetByIdAsync(dto.ParentAccountId.Value, cancellationToken);
                if (parentAccount == null)
                    return ServiceResult<AccountDto>.Failure("الحساب الأب غير موجود.");

                // Level must be parent.Level + 1
                if (dto.Level != parentAccount.Level + 1)
                    return ServiceResult<AccountDto>.Failure(
                        $"مستوى الحساب الفرعي يجب أن يكون {parentAccount.Level + 1} (مستوى الأب + 1).");

                // HIER-03: Child code must fall within parent's range
                try
                {
                    Account.ValidateChildCode(parentAccount.AccountCode, dto.AccountCode, parentAccount.Level);
                }
                catch (AccountDomainException ex)
                {
                    return ServiceResult<AccountDto>.Failure(ex.Message);
                }
            }

            // Step 5: Create domain entity (domain invariants enforced in constructor)
            Account account;
            try
            {
                account = new Account(
                    dto.AccountCode,
                    dto.AccountNameAr,
                    dto.AccountNameEn,
                    dto.AccountType,
                    dto.ParentAccountId,
                    dto.Level,
                    dto.IsSystemAccount,
                    dto.CurrencyCode,
                    dto.Description);
            }
            catch (AccountDomainException ex)
            {
                return ServiceResult<AccountDto>.Failure(ex.Message);
            }

            // Step 6: If parent was a leaf, mark it as parent (HIER-05)
            var parentMarkedAsParent = false;
            if (parentAccount != null && parentAccount.IsLeaf)
            {
                parentAccount.MarkAsParent();
                parentMarkedAsParent = true;
            }

            // Step 7: Persist + audit in one transaction
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                if (parentMarkedAsParent)
                {
                    _accountRepository.Update(parentAccount);
                }

                await _accountRepository.AddAsync(account, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _auditLogger.LogAsync(
                    "Account", account.Id, "Created",
                    _currentUser.Username,
                    $"Account '{account.AccountCode} - {account.AccountNameAr}' created.",
                    cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, IsolationLevel.ReadCommitted, cancellationToken);

            return ServiceResult<AccountDto>.Success(AccountMapper.ToDto(account));
        }

        /// <summary>
        /// Updates mutable fields of an account.
        /// Only AccountNameAr, AccountNameEn, Description, and IsActive can be changed.
        /// </summary>
        public async Task<ServiceResult<AccountDto>> UpdateAsync(UpdateAccountDto dto, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "Account", dto.Id);
            // Defense-in-depth: auth guard
            if (!_currentUser.IsAuthenticated)
                return ServiceResult<AccountDto>.Failure("يجب تسجيل الدخول أولاً.");
            if (!_currentUser.HasPermission(PermissionKeys.AccountsEdit))
                return ServiceResult<AccountDto>.Failure("لا تملك الصلاحية لتنفيذ هذه العملية.");

            // Step 1: DTO validation
            var validationResult = await _updateValidator.ValidateAsync(dto, cancellationToken);
            if (!validationResult.IsValid)
                return ServiceResult<AccountDto>.Failure(validationResult.Errors.Select(e => e.ErrorMessage));

            // Step 2: Load account
            var account = await _accountRepository.GetByIdAsync(dto.Id, cancellationToken);
            if (account == null)
                return ServiceResult<AccountDto>.Failure("الحساب غير موجود.");

            // Step 3: Apply changes via domain methods (enforce invariants)
            try
            {
                account.ChangeNameAr(dto.AccountNameAr);
                account.ChangeNameEn(dto.AccountNameEn);
                account.ChangeDescription(dto.Description);

                if (dto.IsActive && !account.IsActive)
                    account.Activate();
                else if (!dto.IsActive && account.IsActive)
                    account.Deactivate();
            }
            catch (AccountDomainException ex)
            {
                return ServiceResult<AccountDto>.Failure(ex.Message);
            }

            // Step 4: Set concurrency token
            account.RowVersion = dto.RowVersion;

            // Step 5: Persist + audit in one transaction
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                _accountRepository.Update(account);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _auditLogger.LogAsync(
                    "Account", account.Id, "Updated",
                    _currentUser.Username,
                    $"Account '{account.AccountCode}' updated.",
                    cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, IsolationLevel.ReadCommitted, cancellationToken);

            return ServiceResult<AccountDto>.Success(AccountMapper.ToDto(account));
        }

        public async Task<ServiceResult> DeactivateAsync(int id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeactivateAsync", "Account", id);
            var account = await _accountRepository.GetByIdAsync(id, cancellationToken);
            if (account == null)
                return ServiceResult.Failure("الحساب غير موجود.");

            try
            {
                account.Deactivate();
            }
            catch (AccountDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                _accountRepository.Update(account);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _auditLogger.LogAsync(
                    "Account", account.Id, "Deactivated",
                    _currentUser.Username, null, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, IsolationLevel.ReadCommitted, cancellationToken);

            return ServiceResult.Success();
        }

        public async Task<ServiceResult> ActivateAsync(int id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ActivateAsync", "Account", id);
            var account = await _accountRepository.GetByIdAsync(id, cancellationToken);
            if (account == null)
                return ServiceResult.Failure("الحساب غير موجود.");

            account.Activate();
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                _accountRepository.Update(account);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _auditLogger.LogAsync(
                    "Account", account.Id, "Activated",
                    _currentUser.Username, null, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, IsolationLevel.ReadCommitted, cancellationToken);

            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteAsync", "Account", id);
            // Defense-in-depth: auth guard
            if (!_currentUser.IsAuthenticated)
                return ServiceResult.Failure("يجب تسجيل الدخول أولاً.");
            if (!_currentUser.HasPermission(PermissionKeys.AccountsDelete))
                return ServiceResult.Failure("لا تملك الصلاحية لتنفيذ هذه العملية.");

            var account = await _accountRepository.GetByIdAsync(id, cancellationToken);
            if (account == null)
                return ServiceResult.Failure("الحساب غير موجود.");

            // Check for children before deleting
            var hasChildren = await _accountRepository.HasChildrenAsync(id, cancellationToken);
            if (hasChildren)
                return ServiceResult.Failure("لا يمكن حذف حساب له حسابات فرعية.");

            try
            {
                account.SoftDelete(_currentUser.Username, _dateTimeProvider.UtcNow);
            }
            catch (AccountDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                _accountRepository.Update(account);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _auditLogger.LogAsync(
                    "Account", account.Id, "Deleted",
                    _currentUser.Username, null, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, IsolationLevel.ReadCommitted, cancellationToken);

            return ServiceResult.Success();
        }
    }
}
