using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Treasury;
using MarcoERP.Application.Mappers.Treasury;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Treasury;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Treasury;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Application.Interfaces.SmartEntry;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Treasury
{
    /// <summary>
    /// Application service for Bank Reconciliation CRUD operations.
    /// </summary>
    [Module(SystemModule.Treasury)]
    public sealed class BankReconciliationService : IBankReconciliationService
    {
        private readonly IBankReconciliationRepository _reconciliationRepo;
        private readonly IBankAccountRepository _bankAccountRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IValidator<CreateBankReconciliationDto> _createValidator;
        private readonly IValidator<CreateBankReconciliationItemDto> _itemValidator;
        private readonly IValidator<UpdateBankReconciliationDto> _updateValidator;
        private readonly ILogger<BankReconciliationService> _logger;
        private readonly IFeatureService _featureService;
        private readonly ISmartEntryQueryService _smartEntryQuery;

        public BankReconciliationService(
            IBankReconciliationRepository reconciliationRepo,
            IBankAccountRepository bankAccountRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IValidator<CreateBankReconciliationDto> createValidator,
            IValidator<CreateBankReconciliationItemDto> itemValidator,
            IValidator<UpdateBankReconciliationDto> updateValidator,
            ILogger<BankReconciliationService> logger = null,
            IFeatureService featureService = null,
            ISmartEntryQueryService smartEntryQuery = null)
        {
            _reconciliationRepo = reconciliationRepo ?? throw new ArgumentNullException(nameof(reconciliationRepo));
            _bankAccountRepo = bankAccountRepo ?? throw new ArgumentNullException(nameof(bankAccountRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _itemValidator = itemValidator ?? throw new ArgumentNullException(nameof(itemValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationService>.Instance;
            _featureService = featureService;
            _smartEntryQuery = smartEntryQuery;
        }

        public async Task<ServiceResult<IReadOnlyList<BankReconciliationDto>>> GetAllAsync(CancellationToken ct)
        {
            var entities = await _reconciliationRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<BankReconciliationDto>>.Success(
                entities.Select(BankReconciliationMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<IReadOnlyList<BankReconciliationDto>>> GetByBankAccountAsync(int bankAccountId, CancellationToken ct)
        {
            var entities = await _reconciliationRepo.GetByBankAccountAsync(bankAccountId, ct);
            return ServiceResult<IReadOnlyList<BankReconciliationDto>>.Success(
                entities.Select(BankReconciliationMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<BankReconciliationDto>> GetByIdAsync(int id, CancellationToken ct)
        {
            var entity = await _reconciliationRepo.GetByIdWithItemsAsync(id, ct);
            if (entity == null)
                return ServiceResult<BankReconciliationDto>.Failure("التسوية غير موجودة.");
            return ServiceResult<BankReconciliationDto>.Success(BankReconciliationMapper.ToDto(entity));
        }

        public async Task<ServiceResult<BankReconciliationDto>> CreateAsync(CreateBankReconciliationDto dto, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "BankReconciliation", 0);

            // Feature Guard — block operation if Treasury module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<BankReconciliationDto>(_featureService, FeatureKeys.Treasury, ct);
                if (guard != null) return guard;
            }

            // Defense-in-depth: auth guard
            if (!_currentUser.IsAuthenticated)
                return ServiceResult<BankReconciliationDto>.Failure("يجب تسجيل الدخول أولاً.");
            if (!_currentUser.HasPermission(PermissionKeys.TreasuryCreate))
                return ServiceResult<BankReconciliationDto>.Failure("لا تملك الصلاحية لتنفيذ هذه العملية.");

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<BankReconciliationDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var bankAccount = await _bankAccountRepo.GetByIdAsync(dto.BankAccountId, ct);
            if (bankAccount == null)
                return ServiceResult<BankReconciliationDto>.Failure("الحساب البنكي غير موجود.");

            try
            {
                var entity = new BankReconciliation(
                    dto.BankAccountId,
                    dto.ReconciliationDate,
                    dto.StatementBalance,
                    dto.Notes);

                // Compute system balance from GL for the bank account's linked account
                if (_smartEntryQuery != null && bankAccount.AccountId.HasValue)
                {
                    var glBalance = await _smartEntryQuery.GetPostedAccountBalanceAsync(bankAccount.AccountId.Value, ct);
                    entity.SetSystemBalance(glBalance);
                }

                await _reconciliationRepo.AddAsync(entity, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                // Reload with navigation properties
                entity = await _reconciliationRepo.GetByIdWithItemsAsync(entity.Id, ct);
                return ServiceResult<BankReconciliationDto>.Success(BankReconciliationMapper.ToDto(entity));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<BankReconciliationDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<BankReconciliationDto>> UpdateAsync(UpdateBankReconciliationDto dto, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "BankReconciliation", dto.Id);
            // V-06 fix: Validate DTO before update
            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<BankReconciliationDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var entity = await _reconciliationRepo.GetByIdWithItemsAsync(dto.Id, ct);
            if (entity == null)
                return ServiceResult<BankReconciliationDto>.Failure("التسوية غير موجودة.");

            try
            {
                entity.Update(dto.ReconciliationDate, dto.StatementBalance, dto.Notes);

                // Recompute system balance from GL (bank account or date may have changed)
                if (_smartEntryQuery != null)
                {
                    var bankAccount = await _bankAccountRepo.GetByIdAsync(entity.BankAccountId, ct);
                    if (bankAccount?.AccountId != null)
                    {
                        var glBalance = await _smartEntryQuery.GetPostedAccountBalanceAsync(bankAccount.AccountId.Value, ct);
                        entity.SetSystemBalance(glBalance);
                    }
                }

                _reconciliationRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult<BankReconciliationDto>.Success(BankReconciliationMapper.ToDto(entity));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<BankReconciliationDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<BankReconciliationDto>> AddItemAsync(CreateBankReconciliationItemDto dto, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "AddItemAsync", "BankReconciliation", dto.BankReconciliationId);
            var vr = await _itemValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<BankReconciliationDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var entity = await _reconciliationRepo.GetByIdWithItemsAsync(dto.BankReconciliationId, ct);
            if (entity == null)
                return ServiceResult<BankReconciliationDto>.Failure("التسوية غير موجودة.");

            try
            {
                var item = new BankReconciliationItem(
                    dto.BankReconciliationId,
                    dto.TransactionDate,
                    dto.Description,
                    dto.Amount,
                    dto.Reference);

                entity.AddItem(item);
                _reconciliationRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult<BankReconciliationDto>.Success(BankReconciliationMapper.ToDto(entity));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<BankReconciliationDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> RemoveItemAsync(int reconciliationId, int itemId, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "RemoveItemAsync", "BankReconciliation", reconciliationId);
            var entity = await _reconciliationRepo.GetByIdWithItemsAsync(reconciliationId, ct);
            if (entity == null) return ServiceResult.Failure("التسوية غير موجودة.");

            var item = entity.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return ServiceResult.Failure("البند غير موجود.");

            try
            {
                entity.RemoveItem(item);
                _reconciliationRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Success();
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> CompleteAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CompleteAsync", "BankReconciliation", id);
            var entity = await _reconciliationRepo.GetByIdWithItemsAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("التسوية غير موجودة.");

            try
            {
                entity.Complete();
                _reconciliationRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Success();
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> ReopenAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ReopenAsync", "BankReconciliation", id);
            var entity = await _reconciliationRepo.GetByIdWithItemsAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("التسوية غير موجودة.");

            try
            {
                entity.Reopen();
                _reconciliationRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Success();
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteAsync", "BankReconciliation", id);
            var entity = await _reconciliationRepo.GetByIdWithItemsAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("التسوية غير موجودة.");

            if (entity.IsCompleted)
                return ServiceResult.Failure("لا يمكن حذف تسوية مكتملة.");

            _reconciliationRepo.Remove(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }
    }
}
