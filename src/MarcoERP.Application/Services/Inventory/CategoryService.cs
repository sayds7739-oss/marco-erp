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
    public sealed class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IValidator<CreateCategoryDto> _createValidator;
        private readonly IValidator<UpdateCategoryDto> _updateValidator;
        private readonly ILogger<CategoryService> _logger;
        private readonly IFeatureService _featureService;

        private const string CategoryNotFoundMessage = "التصنيف غير موجود.";

        public CategoryService(
            ICategoryRepository categoryRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IValidator<CreateCategoryDto> createValidator,
            IValidator<UpdateCategoryDto> updateValidator,
            ILogger<CategoryService> logger = null,
            IFeatureService featureService = null)
        {
            _categoryRepo = categoryRepo ?? throw new ArgumentNullException(nameof(categoryRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CategoryService>.Instance;
            _featureService = featureService;
        }

        public async Task<ServiceResult<IReadOnlyList<CategoryDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _categoryRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<CategoryDto>>.Success(
                entities.Select(CategoryMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<CategoryDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _categoryRepo.GetByIdAsync(id, ct);
            if (entity == null)
                return ServiceResult<CategoryDto>.Failure(CategoryNotFoundMessage);
            return ServiceResult<CategoryDto>.Success(CategoryMapper.ToDto(entity));
        }

        public async Task<ServiceResult<IReadOnlyList<CategoryDto>>> GetRootCategoriesAsync(CancellationToken ct = default)
        {
            var roots = await _categoryRepo.GetRootCategoriesAsync(ct);
            return ServiceResult<IReadOnlyList<CategoryDto>>.Success(
                roots.Select(CategoryMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<IReadOnlyList<CategoryDto>>> GetChildrenAsync(int parentId, CancellationToken ct = default)
        {
            var children = await _categoryRepo.GetChildrenAsync(parentId, ct);
            return ServiceResult<IReadOnlyList<CategoryDto>>.Success(
                children.Select(CategoryMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<CategoryDto>> CreateAsync(CreateCategoryDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "Category", 0);
            // Feature Guard — block operation if Inventory module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<CategoryDto>(_featureService, FeatureKeys.Inventory, ct);
                if (guard != null) return guard;
            }

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<CategoryDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            // Check duplicate name
            if (await _categoryRepo.NameExistsAsync(dto.NameAr, dto.ParentCategoryId, ct: ct))
                return ServiceResult<CategoryDto>.Failure("يوجد تصنيف بنفس الاسم في نفس المستوى.");

            try
            {
                var entity = new Category(dto.NameAr, dto.NameEn, dto.ParentCategoryId, dto.Level, dto.Description);
                await _categoryRepo.AddAsync(entity, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult<CategoryDto>.Success(CategoryMapper.ToDto(entity));
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult<CategoryDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<CategoryDto>> UpdateAsync(UpdateCategoryDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "Category", dto.Id);
            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<CategoryDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var entity = await _categoryRepo.GetByIdAsync(dto.Id, ct);
            if (entity == null)
                return ServiceResult<CategoryDto>.Failure(CategoryNotFoundMessage);

            if (await _categoryRepo.NameExistsAsync(dto.NameAr, entity.ParentCategoryId, dto.Id, ct))
                return ServiceResult<CategoryDto>.Failure("يوجد تصنيف بنفس الاسم في نفس المستوى.");

            try
            {
                entity.Update(dto.NameAr, dto.NameEn, dto.Description);
                _categoryRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult<CategoryDto>.Success(CategoryMapper.ToDto(entity));
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult<CategoryDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> ActivateAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ActivateAsync", "Category", id);
            var entity = await _categoryRepo.GetByIdAsync(id, ct);
            if (entity == null)
                return ServiceResult.Failure(CategoryNotFoundMessage);

            entity.Activate();
            _categoryRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeactivateAsync", "Category", id);
            var entity = await _categoryRepo.GetByIdAsync(id, ct);
            if (entity == null)
                return ServiceResult.Failure(CategoryNotFoundMessage);

            entity.Deactivate();
            _categoryRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }
    }
}
