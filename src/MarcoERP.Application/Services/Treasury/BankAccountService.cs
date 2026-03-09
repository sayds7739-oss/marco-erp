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
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Treasury
{
    /// <summary>
    /// Application service for Bank Account CRUD operations.
    /// Follows the same pattern as CashboxService.
    /// </summary>
    [Module(SystemModule.Treasury)]
    public sealed class BankAccountService : IBankAccountService
    {
        private readonly IBankAccountRepository _bankAccountRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IValidator<CreateBankAccountDto> _createValidator;
        private readonly IValidator<UpdateBankAccountDto> _updateValidator;
        private readonly ILogger<BankAccountService> _logger;
        private readonly IFeatureService _featureService;

        public BankAccountService(
            IBankAccountRepository bankAccountRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IValidator<CreateBankAccountDto> createValidator,
            IValidator<UpdateBankAccountDto> updateValidator,
            ILogger<BankAccountService> logger = null,
            IFeatureService featureService = null)
        {
            _bankAccountRepo = bankAccountRepo ?? throw new ArgumentNullException(nameof(bankAccountRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BankAccountService>.Instance;
            _featureService = featureService;
        }

        public async Task<ServiceResult<IReadOnlyList<BankAccountDto>>> GetAllAsync(CancellationToken ct)
        {
            var entities = await _bankAccountRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<BankAccountDto>>.Success(
                entities.Select(BankAccountMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<IReadOnlyList<BankAccountDto>>> GetActiveAsync(CancellationToken ct)
        {
            var entities = await _bankAccountRepo.GetActiveAsync(ct);
            return ServiceResult<IReadOnlyList<BankAccountDto>>.Success(
                entities.Select(BankAccountMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<BankAccountDto>> GetByIdAsync(int id, CancellationToken ct)
        {
            var entity = await _bankAccountRepo.GetByIdAsync(id, ct);
            if (entity == null)
                return ServiceResult<BankAccountDto>.Failure("الحساب البنكي غير موجود.");
            return ServiceResult<BankAccountDto>.Success(BankAccountMapper.ToDto(entity));
        }

        public async Task<ServiceResult<BankAccountDto>> CreateAsync(CreateBankAccountDto dto, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "BankAccount", 0);

            // Feature Guard — block operation if Treasury module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<BankAccountDto>(_featureService, FeatureKeys.Treasury, ct);
                if (guard != null) return guard;
            }

            // Defense-in-depth: auth guard
            if (!_currentUser.IsAuthenticated)
                return ServiceResult<BankAccountDto>.Failure("يجب تسجيل الدخول أولاً.");
            if (!_currentUser.HasPermission(PermissionKeys.TreasuryCreate))
                return ServiceResult<BankAccountDto>.Failure("لا تملك الصلاحية لتنفيذ هذه العملية.");

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<BankAccountDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            try
            {
                var code = await _bankAccountRepo.GetNextCodeAsync(ct);

                var entity = new BankAccount(
                    code, dto.NameAr, dto.NameEn,
                    dto.BankName, dto.AccountNumber, dto.IBAN,
                    dto.AccountId);
                await _bankAccountRepo.AddAsync(entity, ct);

                // If first bank account, make it default
                var existing = await _bankAccountRepo.GetAllAsync(ct);
                if (existing.Count == 1)
                    entity.SetAsDefault();

                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult<BankAccountDto>.Success(BankAccountMapper.ToDto(entity));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<BankAccountDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<BankAccountDto>> UpdateAsync(UpdateBankAccountDto dto, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "BankAccount", dto.Id);
            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<BankAccountDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var entity = await _bankAccountRepo.GetByIdAsync(dto.Id, ct);
            if (entity == null)
                return ServiceResult<BankAccountDto>.Failure("الحساب البنكي غير موجود.");

            try
            {
                entity.Update(dto.NameAr, dto.NameEn, dto.BankName,
                    dto.AccountNumber, dto.IBAN, dto.AccountId);
                _bankAccountRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult<BankAccountDto>.Success(BankAccountMapper.ToDto(entity));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<BankAccountDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> SetDefaultAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "SetDefaultAsync", "BankAccount", id);
            var entity = await _bankAccountRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("الحساب البنكي غير موجود.");

            var currentDefault = await _bankAccountRepo.GetDefaultAsync(ct);
            if (currentDefault != null && currentDefault.Id != id)
            {
                currentDefault.ClearDefault();
                _bankAccountRepo.Update(currentDefault);
            }

            entity.SetAsDefault();
            _bankAccountRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> ActivateAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ActivateAsync", "BankAccount", id);
            var entity = await _bankAccountRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("الحساب البنكي غير موجود.");

            entity.Activate();
            _bankAccountRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeactivateAsync", "BankAccount", id);
            var entity = await _bankAccountRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("الحساب البنكي غير موجود.");

            try
            {
                entity.Deactivate();
                _bankAccountRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Success();
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }
    }
}
