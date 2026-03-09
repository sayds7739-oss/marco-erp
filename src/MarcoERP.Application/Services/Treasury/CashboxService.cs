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
    /// Application service for Cashbox CRUD operations.
    /// Follows the same pattern as WarehouseService.
    /// </summary>
    [Module(SystemModule.Treasury)]
    public sealed class CashboxService : ICashboxService
    {
        private readonly ICashboxRepository _cashboxRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IValidator<CreateCashboxDto> _createValidator;
        private readonly IValidator<UpdateCashboxDto> _updateValidator;
        private readonly ILogger<CashboxService> _logger;
        private readonly IFeatureService _featureService;

        public CashboxService(
            ICashboxRepository cashboxRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IValidator<CreateCashboxDto> createValidator,
            IValidator<UpdateCashboxDto> updateValidator,
            ILogger<CashboxService> logger = null,
            IFeatureService featureService = null)
        {
            _cashboxRepo = cashboxRepo ?? throw new ArgumentNullException(nameof(cashboxRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CashboxService>.Instance;
            _featureService = featureService;
        }

        public async Task<ServiceResult<IReadOnlyList<CashboxDto>>> GetAllAsync(CancellationToken ct)
        {
            var entities = await _cashboxRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<CashboxDto>>.Success(
                entities.Select(CashboxMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<IReadOnlyList<CashboxDto>>> GetActiveAsync(CancellationToken ct)
        {
            var entities = await _cashboxRepo.GetActiveAsync(ct);
            return ServiceResult<IReadOnlyList<CashboxDto>>.Success(
                entities.Select(CashboxMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<CashboxDto>> GetByIdAsync(int id, CancellationToken ct)
        {
            var entity = await _cashboxRepo.GetByIdAsync(id, ct);
            if (entity == null)
                return ServiceResult<CashboxDto>.Failure("الخزنة غير موجودة.");
            return ServiceResult<CashboxDto>.Success(CashboxMapper.ToDto(entity));
        }

        public async Task<ServiceResult<string>> GetNextCodePreviewAsync(CancellationToken ct)
        {
            var nextCode = await _cashboxRepo.GetNextCodeAsync(ct);
            return ServiceResult<string>.Success(nextCode);
        }

        public async Task<ServiceResult<CashboxDto>> CreateAsync(CreateCashboxDto dto, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "Cashbox", 0);

            // Feature Guard — block operation if Treasury module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<CashboxDto>(_featureService, FeatureKeys.Treasury, ct);
                if (guard != null) return guard;
            }

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<CashboxDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            try
            {
                var code = await _cashboxRepo.GetNextCodeAsync(ct);

                var entity = new Cashbox(code, dto.NameAr, dto.NameEn, dto.AccountId);
                await _cashboxRepo.AddAsync(entity, ct);

                // If first cashbox, make it default
                var existing = await _cashboxRepo.GetAllAsync(ct);
                if (existing.Count == 1)
                    entity.SetAsDefault();

                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult<CashboxDto>.Success(CashboxMapper.ToDto(entity));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<CashboxDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<CashboxDto>> UpdateAsync(UpdateCashboxDto dto, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "Cashbox", dto.Id);
            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<CashboxDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var entity = await _cashboxRepo.GetByIdAsync(dto.Id, ct);
            if (entity == null)
                return ServiceResult<CashboxDto>.Failure("الخزنة غير موجودة.");

            try
            {
                entity.Update(dto.NameAr, dto.NameEn, dto.AccountId);
                _cashboxRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult<CashboxDto>.Success(CashboxMapper.ToDto(entity));
            }
            catch (TreasuryDomainException ex)
            {
                return ServiceResult<CashboxDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> SetDefaultAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "SetDefaultAsync", "Cashbox", id);
            var entity = await _cashboxRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("الخزنة غير موجودة.");

            var currentDefault = await _cashboxRepo.GetDefaultAsync(ct);
            if (currentDefault != null && currentDefault.Id != id)
            {
                currentDefault.ClearDefault();
                _cashboxRepo.Update(currentDefault);
            }

            entity.SetAsDefault();
            _cashboxRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> ActivateAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ActivateAsync", "Cashbox", id);
            var entity = await _cashboxRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("الخزنة غير موجودة.");

            entity.Activate();
            _cashboxRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeactivateAsync", "Cashbox", id);
            var entity = await _cashboxRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("الخزنة غير موجودة.");

            try
            {
                entity.Deactivate();
                _cashboxRepo.Update(entity);
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
