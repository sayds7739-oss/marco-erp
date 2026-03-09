using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Application.Interfaces.Settings;
using static MarcoERP.Domain.Entities.Sales.PriceList;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Sales
{
    /// <summary>
    /// Implements PriceList CRUD and tiered pricing resolution.
    /// </summary>
    [Module(SystemModule.Sales)]
    public sealed class PriceListService : IPriceListService
    {
        private readonly IPriceListRepository _priceListRepo;
        private readonly ICustomerRepository _customerRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly ILogger<PriceListService> _logger;
        private readonly IFeatureService _featureService;
        private readonly IValidator<CreatePriceListDto> _createValidator;
        private readonly IValidator<UpdatePriceListDto> _updateValidator;

        public PriceListService(
            IPriceListRepository priceListRepo,
            ICustomerRepository customerRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime,
            IValidator<CreatePriceListDto> createValidator,
            IValidator<UpdatePriceListDto> updateValidator,
            ILogger<PriceListService> logger = null,
            IFeatureService featureService = null)
        {
            _priceListRepo = priceListRepo ?? throw new ArgumentNullException(nameof(priceListRepo));
            _customerRepo = customerRepo ?? throw new ArgumentNullException(nameof(customerRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PriceListService>.Instance;
            _featureService = featureService;
        }

        public async Task<ServiceResult<IReadOnlyList<PriceListListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _priceListRepo.GetAllAsync(ct);
            var dtos = entities.Select(e => new PriceListListDto
            {
                Id = e.Id,
                Code = e.Code,
                NameAr = e.NameAr,
                NameEn = e.NameEn,
                ValidFrom = e.ValidFrom,
                ValidTo = e.ValidTo,
                IsActive = e.IsActive,
                TierCount = e.Tiers?.Count ?? 0
            }).ToList();

            return ServiceResult<IReadOnlyList<PriceListListDto>>.Success(dtos);
        }

        public async Task<ServiceResult<PriceListDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _priceListRepo.GetWithTiersAsync(id, ct);
            if (entity == null)
                return ServiceResult<PriceListDto>.Failure("قائمة الأسعار غير موجودة.");

            return ServiceResult<PriceListDto>.Success(MapToDto(entity));
        }

        public async Task<ServiceResult<string>> GetNextCodeAsync(CancellationToken ct = default)
        {
            var next = await _priceListRepo.GetNextCodeAsync(ct);
            return ServiceResult<string>.Success(next);
        }

        public async Task<ServiceResult<PriceListDto>> CreateAsync(CreatePriceListDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "PriceList", 0);
            // Feature Guard — block operation if Sales module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<PriceListDto>(_featureService, FeatureKeys.Sales, ct);
                if (guard != null) return guard;
            }

            if (string.IsNullOrWhiteSpace(dto.NameAr))
                return ServiceResult<PriceListDto>.Failure("اسم قائمة الأسعار بالعربي مطلوب.");

            // V-02 fix: Use FluentValidation validator
            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<PriceListDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            try
            {
                var code = await _priceListRepo.GetNextCodeAsync(ct);
                var draft = new PriceListDraft
                {
                    Code = code,
                    NameAr = dto.NameAr,
                    NameEn = dto.NameEn,
                    Description = dto.Description,
                    ValidFrom = dto.ValidFrom,
                    ValidTo = dto.ValidTo
                };

                var priceList = new PriceList(draft);

                foreach (var tierDto in dto.Tiers ?? Enumerable.Empty<CreatePriceTierDto>())
                {
                    priceList.AddTier(tierDto.ProductId, tierDto.MinimumQuantity, tierDto.Price);
                }

                await _priceListRepo.AddAsync(priceList, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _priceListRepo.GetWithTiersAsync(priceList.Id, ct);
                return ServiceResult<PriceListDto>.Success(MapToDto(saved));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateAsync failed for PriceList.");
                return ServiceResult<PriceListDto>.Failure(ErrorSanitizer.SanitizeGeneric(ex, "حفظ قائمة الأسعار"));
            }
        }

        public async Task<ServiceResult<PriceListDto>> UpdateAsync(UpdatePriceListDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "PriceList", dto.Id);
            // V-03 fix: Use FluentValidation validator
            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<PriceListDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var entity = await _priceListRepo.GetWithTiersAsync(dto.Id, ct);
            if (entity == null)
                return ServiceResult<PriceListDto>.Failure("قائمة الأسعار غير موجودة.");

            try
            {
                entity.Update(new PriceListUpdate
                {
                    NameAr = dto.NameAr,
                    NameEn = dto.NameEn,
                    Description = dto.Description,
                    ValidFrom = dto.ValidFrom,
                    ValidTo = dto.ValidTo
                });

                // Clear existing tiers and re-add
                var existingTiers = entity.Tiers.ToList();
                foreach (var tier in existingTiers)
                    entity.RemoveTier(tier);

                foreach (var tierDto in dto.Tiers ?? Enumerable.Empty<CreatePriceTierDto>())
                    entity.AddTier(tierDto.ProductId, tierDto.MinimumQuantity, tierDto.Price);

                _priceListRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _priceListRepo.GetWithTiersAsync(entity.Id, ct);
                return ServiceResult<PriceListDto>.Success(MapToDto(saved));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateAsync failed for PriceList.");
                return ServiceResult<PriceListDto>.Failure(ErrorSanitizer.SanitizeGeneric(ex, "تحديث قائمة الأسعار"));
            }
        }

        public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteAsync", "PriceList", id);
            var entity = await _priceListRepo.GetByIdAsync(id, ct);
            if (entity == null)
                return ServiceResult.Failure("قائمة الأسعار غير موجودة.");

            entity.SoftDelete(_currentUser.Username, _dateTime.UtcNow);
            _priceListRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Success();
        }

        public async Task<ServiceResult> ActivateAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ActivateAsync", "PriceList", id);
            var entity = await _priceListRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("قائمة الأسعار غير موجودة.");
            entity.Activate();
            _priceListRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeactivateAsync", "PriceList", id);
            var entity = await _priceListRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("قائمة الأسعار غير موجودة.");
            entity.Deactivate();
            _priceListRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult<decimal?>> GetBestPriceForCustomerAsync(
            int customerId, int productId, decimal quantity, CancellationToken ct = default)
        {
            var customer = await _customerRepo.GetByIdAsync(customerId, ct);
            if (customer == null)
                return ServiceResult<decimal?>.Failure("العميل غير موجود.");

            var today = _dateTime.UtcNow.Date;

            // If customer has a specific price list, check it first
            if (customer.PriceListId.HasValue)
            {
                var customerPriceList = await _priceListRepo.GetWithTiersAsync(customer.PriceListId.Value, ct);
                if (customerPriceList != null && customerPriceList.IsValidOn(today))
                {
                    var tier = customerPriceList.GetBestPrice(productId, quantity);
                    if (tier != null)
                        return ServiceResult<decimal?>.Success(tier.Price);
                }
            }

            // Fall back to best price from all active lists
            var bestPrice = await _priceListRepo.GetBestPriceAsync(productId, quantity, today, ct);
            return ServiceResult<decimal?>.Success(bestPrice);
        }

        private static PriceListDto MapToDto(PriceList entity) => new()
        {
            Id = entity.Id,
            Code = entity.Code,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            Description = entity.Description,
            ValidFrom = entity.ValidFrom,
            ValidTo = entity.ValidTo,
            IsActive = entity.IsActive,
            Tiers = entity.Tiers?.Select(t => new PriceTierDto
            {
                Id = t.Id,
                ProductId = t.ProductId,
                ProductCode = t.Product?.Code,
                ProductName = t.Product?.NameAr,
                MinimumQuantity = t.MinimumQuantity,
                Price = t.Price
            }).ToList() ?? new()
        };
    }
}
