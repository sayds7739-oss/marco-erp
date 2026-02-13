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

namespace MarcoERP.Application.Services.Inventory
{
    [Module(SystemModule.Inventory)]
    public sealed class ProductService : IProductService
    {
        private readonly IProductRepository _productRepo;
        private readonly IWarehouseProductRepository _warehouseProductRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IValidator<CreateProductDto> _createValidator;
        private readonly IValidator<UpdateProductDto> _updateValidator;

        private const string ProductNotFoundMessage = "الصنف غير موجود.";

        public ProductService(
            IProductRepository productRepo,
            IWarehouseProductRepository warehouseProductRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTimeProvider,
            IValidator<CreateProductDto> createValidator,
            IValidator<UpdateProductDto> updateValidator)
        {
            _productRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
            _warehouseProductRepo = warehouseProductRepo ?? throw new ArgumentNullException(nameof(warehouseProductRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
        }

        // ── Queries ─────────────────────────────────────────────

        public async Task<ServiceResult<string>> GetNextCodeAsync(CancellationToken ct = default)
        {
            var nextCode = await _productRepo.GetNextCodeAsync(ct);
            return ServiceResult<string>.Success(nextCode);
        }

        public async Task<ServiceResult<IReadOnlyList<ProductDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _productRepo.GetAllWithUnitsAsync(ct);
            return ServiceResult<IReadOnlyList<ProductDto>>.Success(
                entities.Select(ProductMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<ProductDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _productRepo.GetByIdWithUnitsAsync(id, ct);
            if (entity == null)
                return ServiceResult<ProductDto>.Failure(ProductNotFoundMessage);
            return ServiceResult<ProductDto>.Success(ProductMapper.ToDto(entity));
        }

        public async Task<ServiceResult<ProductDto>> GetByCodeAsync(string code, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code))
                return ServiceResult<ProductDto>.Failure("كود الصنف مطلوب.");

            var entity = await _productRepo.GetByCodeAsync(code.Trim(), ct);
            if (entity == null)
                return ServiceResult<ProductDto>.Failure($"لا يوجد صنف بالكود '{code}'.");
            return ServiceResult<ProductDto>.Success(ProductMapper.ToDto(entity));
        }

        public async Task<ServiceResult<IReadOnlyList<ProductDto>>> GetByCategoryAsync(int categoryId, CancellationToken ct = default)
        {
            var entities = await _productRepo.GetByCategoryAsync(categoryId, ct);
            return ServiceResult<IReadOnlyList<ProductDto>>.Success(
                entities.Select(ProductMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<IReadOnlyList<ProductSearchResultDto>>> SearchAsync(string searchTerm, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return ServiceResult<IReadOnlyList<ProductSearchResultDto>>.Success(new List<ProductSearchResultDto>());

            var products = await _productRepo.SearchAsync(searchTerm.Trim(), ct);
            var results = new List<ProductSearchResultDto>();

            foreach (var p in products)
            {
                var totalStock = await _warehouseProductRepo.GetTotalStockAsync(p.Id, ct);
                results.Add(ProductMapper.ToSearchResult(p, totalStock));
            }

            return ServiceResult<IReadOnlyList<ProductSearchResultDto>>.Success(results);
        }

        // ── Commands ────────────────────────────────────────────

        public async Task<ServiceResult<ProductDto>> CreateAsync(CreateProductDto dto, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<ProductDto>(_currentUser, PermissionKeys.InventoryManage);
            if (authCheck != null) return authCheck;

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<ProductDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            if (await _productRepo.CodeExistsAsync(dto.Code, ct: ct))
                return ServiceResult<ProductDto>.Failure($"كود الصنف '{dto.Code}' مستخدم مسبقاً.");

            try
            {
                var product = new Product(
                    dto.Code, dto.NameAr, dto.NameEn, dto.CategoryId, dto.BaseUnitId,
                    dto.CostPrice, dto.DefaultSalePrice, dto.MinimumStock, dto.ReorderLevel,
                    dto.VatRate, dto.Barcode, dto.Description);

                if (dto.DefaultSupplierId.HasValue)
                    product.SetDefaultSupplier(dto.DefaultSupplierId);

                // Add the base unit as a ProductUnit with factor 1
                var baseUnit = new ProductUnit(
                    0, dto.BaseUnitId, 1m, dto.DefaultSalePrice, dto.CostPrice, dto.Barcode, true);
                product.AddUnit(baseUnit);

                // Add additional units
                foreach (var u in dto.Units ?? Enumerable.Empty<CreateProductUnitDto>())
                {
                    if (u.UnitId == dto.BaseUnitId) continue; // Skip duplicate base unit
                    var pu = new ProductUnit(0, u.UnitId, u.ConversionFactor, u.SalePrice, u.PurchasePrice, u.Barcode, u.IsDefault);
                    product.AddUnit(pu);
                }

                await _productRepo.AddAsync(product, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                // Reload with nav properties
                var saved = await _productRepo.GetByIdWithUnitsAsync(product.Id, ct);
                return ServiceResult<ProductDto>.Success(ProductMapper.ToDto(saved));
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult<ProductDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<ProductDto>> UpdateAsync(UpdateProductDto dto, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<ProductDto>(_currentUser, PermissionKeys.InventoryManage);
            if (authCheck != null) return authCheck;

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<ProductDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var product = await _productRepo.GetByIdWithUnitsAsync(dto.Id, ct);
            if (product == null)
                return ServiceResult<ProductDto>.Failure(ProductNotFoundMessage);

            try
            {
                product.Update(dto.NameAr, dto.NameEn, dto.CategoryId, dto.DefaultSalePrice,
                    dto.MinimumStock, dto.ReorderLevel, dto.VatRate, dto.Barcode, dto.Description,
                    dto.DefaultSupplierId);

                product.UpdateCostPrice(dto.CostPrice);

                SyncBaseUnitPricing(product);

                SyncUnits(product, dto);

                _productRepo.Update(product);
                await _unitOfWork.SaveChangesAsync(ct);

                var updated = await _productRepo.GetByIdWithUnitsAsync(product.Id, ct);
                return ServiceResult<ProductDto>.Success(ProductMapper.ToDto(updated));
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult<ProductDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> ActivateAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.InventoryManage);
            if (authCheck != null) return authCheck;

            var product = await _productRepo.GetByIdAsync(id, ct);
            if (product == null) return ServiceResult.Failure(ProductNotFoundMessage);

            product.Activate();
            _productRepo.Update(product);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.InventoryManage);
            if (authCheck != null) return authCheck;

            var product = await _productRepo.GetByIdAsync(id, ct);
            if (product == null) return ServiceResult.Failure(ProductNotFoundMessage);

            product.Deactivate();
            _productRepo.Update(product);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.InventoryManage);
            if (authCheck != null) return authCheck;

            var product = await _productRepo.GetByIdAsync(id, ct);
            if (product == null) return ServiceResult.Failure(ProductNotFoundMessage);

            // Check if product has stock
            var totalStock = await _warehouseProductRepo.GetTotalStockAsync(id, ct);
            if (totalStock > 0)
                return ServiceResult.Failure($"لا يمكن حذف صنف له رصيد ({totalStock:N2}).");

            product.SoftDelete(_currentUser.Username, _dateTimeProvider.UtcNow);
            _productRepo.Update(product);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        private static void SyncUnits(Product product, UpdateProductDto dto)
        {
            if (dto.Units == null)
                return;

            var existingUnits = product.ProductUnits.ToList();
            var dtoUnitIds = dto.Units.Select(u => u.UnitId).ToHashSet();

            var removableUnitIds = existingUnits
                .Where(existing => existing.UnitId != product.BaseUnitId && !dtoUnitIds.Contains(existing.UnitId))
                .Select(existing => existing.UnitId)
                .ToList();

            foreach (var unitId in removableUnitIds)
                product.RemoveUnit(unitId);

            foreach (var u in dto.Units)
            {
                var existing = existingUnits.FirstOrDefault(e => e.UnitId == u.UnitId);
                if (existing != null)
                {
                    existing.UpdatePricing(u.SalePrice, u.PurchasePrice, u.Barcode);
                    if (u.ConversionFactor != existing.ConversionFactor)
                        existing.UpdateConversionFactor(u.ConversionFactor);
                    continue;
                }

                var pu = new ProductUnit(0, u.UnitId, u.ConversionFactor,
                    u.SalePrice, u.PurchasePrice, u.Barcode, u.IsDefault);
                product.AddUnit(pu);
            }
        }

        private static void SyncBaseUnitPricing(Product product)
        {
            var baseUnit = product.ProductUnits.FirstOrDefault(u => u.UnitId == product.BaseUnitId);
            if (baseUnit != null)
                baseUnit.UpdatePricing(product.DefaultSalePrice, product.CostPrice, product.Barcode);
        }
    }
}
