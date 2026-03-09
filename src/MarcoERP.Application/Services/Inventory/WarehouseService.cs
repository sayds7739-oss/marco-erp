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
    public sealed class WarehouseService : IWarehouseService
    {
        private readonly IWarehouseRepository _warehouseRepo;
        private readonly IWarehouseProductRepository _warehouseProductRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IValidator<CreateWarehouseDto> _createValidator;
        private readonly IValidator<UpdateWarehouseDto> _updateValidator;
        private readonly ILogger<WarehouseService> _logger;
        private readonly IFeatureService _featureService;

        public WarehouseService(
            IWarehouseRepository warehouseRepo,
            IWarehouseProductRepository warehouseProductRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IValidator<CreateWarehouseDto> createValidator,
            IValidator<UpdateWarehouseDto> updateValidator,
            ILogger<WarehouseService> logger = null,
            IFeatureService featureService = null)
        {
            _warehouseRepo = warehouseRepo ?? throw new ArgumentNullException(nameof(warehouseRepo));
            _warehouseProductRepo = warehouseProductRepo ?? throw new ArgumentNullException(nameof(warehouseProductRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WarehouseService>.Instance;
            _featureService = featureService;
        }

        // ── Warehouse CRUD ──────────────────────────────────────

        public async Task<ServiceResult<IReadOnlyList<WarehouseDto>>> GetAllAsync(CancellationToken ct)
        {
            var entities = await _warehouseRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<WarehouseDto>>.Success(
                entities.Select(WarehouseMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<IReadOnlyList<WarehouseDto>>> GetActiveAsync(CancellationToken ct)
        {
            var entities = await _warehouseRepo.GetActiveWarehousesAsync(ct);
            return ServiceResult<IReadOnlyList<WarehouseDto>>.Success(
                entities.Select(WarehouseMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<WarehouseDto>> GetByIdAsync(int id, CancellationToken ct)
        {
            var entity = await _warehouseRepo.GetByIdAsync(id, ct);
            if (entity == null)
                return ServiceResult<WarehouseDto>.Failure("المخزن غير موجود.");
            return ServiceResult<WarehouseDto>.Success(WarehouseMapper.ToDto(entity));
        }

        public async Task<ServiceResult<WarehouseDto>> CreateAsync(CreateWarehouseDto dto, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "Warehouse", 0);
            // Feature Guard — block operation if Inventory module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<WarehouseDto>(_featureService, FeatureKeys.Inventory, ct);
                if (guard != null) return guard;
            }

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<WarehouseDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            if (await _warehouseRepo.CodeExistsAsync(dto.Code, ct: ct))
                return ServiceResult<WarehouseDto>.Failure($"كود المخزن '{dto.Code}' مستخدم مسبقاً.");

            try
            {
                var entity = new Warehouse(dto.Code, dto.NameAr, dto.NameEn, dto.Address, dto.Phone, dto.AccountId);
                await _warehouseRepo.AddAsync(entity, ct);

                // If first warehouse, make it default
                var existing = await _warehouseRepo.GetAllAsync(ct);
                if (existing.Count == 0)
                    entity.SetAsDefault();

                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult<WarehouseDto>.Success(WarehouseMapper.ToDto(entity));
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult<WarehouseDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<WarehouseDto>> UpdateAsync(UpdateWarehouseDto dto, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "Warehouse", dto.Id);
            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<WarehouseDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var entity = await _warehouseRepo.GetByIdAsync(dto.Id, ct);
            if (entity == null)
                return ServiceResult<WarehouseDto>.Failure("المخزن غير موجود.");

            try
            {
                entity.Update(dto.NameAr, dto.NameEn, dto.Address, dto.Phone, dto.AccountId);
                _warehouseRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult<WarehouseDto>.Success(WarehouseMapper.ToDto(entity));
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult<WarehouseDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> SetDefaultAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "SetDefaultAsync", "Warehouse", id);
            var entity = await _warehouseRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("المخزن غير موجود.");

            // Clear existing default
            var currentDefault = await _warehouseRepo.GetDefaultAsync(ct);
            if (currentDefault != null && currentDefault.Id != id)
            {
                currentDefault.ClearDefault();
                _warehouseRepo.Update(currentDefault);
            }

            entity.SetAsDefault();
            _warehouseRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> ActivateAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ActivateAsync", "Warehouse", id);
            var entity = await _warehouseRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("المخزن غير موجود.");

            entity.Activate();
            _warehouseRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeactivateAsync", "Warehouse", id);
            var entity = await _warehouseRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("المخزن غير موجود.");

            try
            {
                entity.Deactivate();
                _warehouseRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Success();
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }

        // ── Stock Queries ───────────────────────────────────────

        public async Task<ServiceResult<IReadOnlyList<StockBalanceDto>>> GetStockByWarehouseAsync(int warehouseId, CancellationToken ct)
        {
            var stocks = await _warehouseProductRepo.GetByWarehouseAsync(warehouseId, ct);
            return ServiceResult<IReadOnlyList<StockBalanceDto>>.Success(
                stocks.Select(WarehouseMapper.ToStockDto).ToList());
        }

        public async Task<ServiceResult<IReadOnlyList<StockBalanceDto>>> GetStockByProductAsync(int productId, CancellationToken ct)
        {
            var stocks = await _warehouseProductRepo.GetByProductAsync(productId, ct);
            return ServiceResult<IReadOnlyList<StockBalanceDto>>.Success(
                stocks.Select(WarehouseMapper.ToStockDto).ToList());
        }

        public async Task<ServiceResult<IReadOnlyList<StockBalanceDto>>> GetBelowMinimumStockAsync(CancellationToken ct)
        {
            var stocks = await _warehouseProductRepo.GetBelowMinimumStockAsync(ct);
            return ServiceResult<IReadOnlyList<StockBalanceDto>>.Success(
                stocks.Select(WarehouseMapper.ToStockDto).ToList());
        }
    }
}
