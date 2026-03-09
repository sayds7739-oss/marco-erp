using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Application.Mappers.Inventory;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Inventory;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Application.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Inventory
{
    [Module(SystemModule.Inventory)]
    public sealed class UnitService : IUnitService
    {
        private readonly IUnitRepository _unitRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IValidator<CreateUnitDto> _createValidator;
        private readonly IValidator<UpdateUnitDto> _updateValidator;
        private readonly ILogger<UnitService> _logger;
        private readonly IFeatureService _featureService;

        public UnitService(
            IUnitRepository unitRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IValidator<CreateUnitDto> createValidator,
            IValidator<UpdateUnitDto> updateValidator,
            ILogger<UnitService> logger = null,
            IFeatureService featureService = null)
        {
            _unitRepo = unitRepo ?? throw new ArgumentNullException(nameof(unitRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<UnitService>.Instance;
            _featureService = featureService;
        }

        public async Task<ServiceResult<IReadOnlyList<UnitDto>>> GetAllAsync(CancellationToken ct)
        {
            var entities = await _unitRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<UnitDto>>.Success(
                entities.Select(UnitMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<IReadOnlyList<UnitDto>>> GetActiveAsync(CancellationToken ct)
        {
            var entities = await _unitRepo.GetActiveUnitsAsync(ct);
            return ServiceResult<IReadOnlyList<UnitDto>>.Success(
                entities.Select(UnitMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<UnitDto>> GetByIdAsync(int id, CancellationToken ct)
        {
            var entity = await _unitRepo.GetByIdAsync(id, ct);
            if (entity == null)
                return ServiceResult<UnitDto>.Failure("الوحدة غير موجودة.");
            return ServiceResult<UnitDto>.Success(UnitMapper.ToDto(entity));
        }

        public async Task<ServiceResult<UnitDto>> CreateAsync(CreateUnitDto dto, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "Unit", 0);
            // Feature Guard — block operation if Inventory module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<UnitDto>(_featureService, FeatureKeys.Inventory, ct);
                if (guard != null) return guard;
            }

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<UnitDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            if (await _unitRepo.NameExistsAsync(dto.NameAr, ct: ct))
                return ServiceResult<UnitDto>.Failure("يوجد وحدة بنفس الاسم.");

            try
            {
                var entity = new Unit(dto.NameAr, dto.NameEn, dto.AbbreviationAr, dto.AbbreviationEn);
                await _unitRepo.AddAsync(entity, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult<UnitDto>.Success(UnitMapper.ToDto(entity));
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult<UnitDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<UnitDto>> UpdateAsync(UpdateUnitDto dto, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "Unit", dto.Id);
            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<UnitDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var entity = await _unitRepo.GetByIdAsync(dto.Id, ct);
            if (entity == null)
                return ServiceResult<UnitDto>.Failure("الوحدة غير موجودة.");

            if (await _unitRepo.NameExistsAsync(dto.NameAr, dto.Id, ct))
                return ServiceResult<UnitDto>.Failure("يوجد وحدة بنفس الاسم.");

            try
            {
                entity.Update(dto.NameAr, dto.NameEn, dto.AbbreviationAr, dto.AbbreviationEn);
                _unitRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult<UnitDto>.Success(UnitMapper.ToDto(entity));
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult<UnitDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> ActivateAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ActivateAsync", "Unit", id);
            var entity = await _unitRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("الوحدة غير موجودة.");

            entity.Activate();
            _unitRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeactivateAsync", "Unit", id);
            var entity = await _unitRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("الوحدة غير موجودة.");

            entity.Deactivate();
            _unitRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }
    }
}
