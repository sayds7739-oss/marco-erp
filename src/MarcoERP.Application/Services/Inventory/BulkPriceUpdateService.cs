using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Application.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Inventory
{
    [Module(SystemModule.Inventory)]
    public sealed class BulkPriceUpdateService : IBulkPriceUpdateService
    {
        private readonly IProductRepository _productRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IAuditLogger _auditLogger;
        private readonly ILogger<BulkPriceUpdateService> _logger;
        private readonly IFeatureService _featureService;

        public BulkPriceUpdateService(
            IProductRepository productRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IAuditLogger auditLogger,
            ILogger<BulkPriceUpdateService> logger = null,
            IFeatureService featureService = null)
        {
            _productRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BulkPriceUpdateService>.Instance;
            _featureService = featureService;
        }

        public async Task<ServiceResult<IReadOnlyList<BulkPricePreviewItemDto>>> PreviewAsync(
            BulkPriceUpdateRequestDto request, CancellationToken ct = default)
        {
            var validationError = ValidateRequest(request);
            if (validationError != null)
                return ServiceResult<IReadOnlyList<BulkPricePreviewItemDto>>.Failure(validationError);

            var items = new List<BulkPricePreviewItemDto>();

            foreach (var productId in request.ProductIds)
            {
                var product = await _productRepo.GetByIdWithUnitsAsync(productId, ct);
                if (product == null) continue;

                var pricingContext = BuildPricingContext(product, request);
                if (!pricingContext.IsValid)
                    continue;

                var newTargetPrice = CalculateNewPrice(pricingContext.CurrentTargetPrice, request);
                var (newMajorPrice, newMinorPrice) = CalculateMajorMinorPrices(pricingContext, newTargetPrice);

                items.Add(new BulkPricePreviewItemDto
                {
                    ProductId = product.Id,
                    Code = product.Code,
                    NameAr = product.NameAr,
                    UnitLevel = request.UnitLevel,
                    UnitNameAr = pricingContext.UnitNameAr,
                    ConversionFactor = pricingContext.MinorConversionFactor,
                    CurrentPrice = pricingContext.CurrentTargetPrice,
                    NewPrice = newTargetPrice,
                    Difference = newTargetPrice - pricingContext.CurrentTargetPrice,
                    PercentageChange = pricingContext.CurrentTargetPrice != 0
                        ? Math.Round((newTargetPrice - pricingContext.CurrentTargetPrice) / pricingContext.CurrentTargetPrice * 100, 2)
                        : 0,
                    CurrentMajorPrice = pricingContext.CurrentMajorPrice,
                    NewMajorPrice = newMajorPrice,
                    CurrentMinorPrice = pricingContext.CurrentMinorPrice,
                    NewMinorPrice = newMinorPrice
                });
            }

            return ServiceResult<IReadOnlyList<BulkPricePreviewItemDto>>.Success(items);
        }

        public async Task<ServiceResult<BulkPriceUpdateResultDto>> ApplyAsync(
            BulkPriceUpdateRequestDto request, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ApplyAsync", "BulkPriceUpdate", 0);
            // Feature Guard — block operation if Inventory module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<BulkPriceUpdateResultDto>(_featureService, FeatureKeys.Inventory, ct);
                if (guard != null) return guard;
            }

            var validationError = ValidateRequest(request);
            if (validationError != null)
                return ServiceResult<BulkPriceUpdateResultDto>.Failure(validationError);

            var result = new BulkPriceUpdateResultDto();

            // A-04 Fix: Wrap entire bulk operation in a transaction for atomicity.
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                foreach (var productId in request.ProductIds)
                {
                    try
                    {
                        var product = await _productRepo.GetByIdWithUnitsAsync(productId, ct);
                        if (product == null)
                        {
                            result.FailedCount++;
                            result.Errors.Add($"الصنف {productId} غير موجود.");
                            continue;
                        }

                        var pricingContext = BuildPricingContext(product, request);
                        if (!pricingContext.IsValid)
                        {
                            result.FailedCount++;
                            result.Errors.Add($"الصنف {product.Code}: {pricingContext.ValidationMessage}");
                            continue;
                        }

                        var newTargetPrice = CalculateNewPrice(pricingContext.CurrentTargetPrice, request);
                        var (newMajorPrice, newMinorPrice) = CalculateMajorMinorPrices(pricingContext, newTargetPrice);

                        if (newTargetPrice < 0 || newMajorPrice < 0 || newMinorPrice < 0)
                        {
                            result.FailedCount++;
                            result.Errors.Add($"السعر الجديد سالب للصنف {product.Code}.");
                            continue;
                        }

                        ApplyMajorPrice(product, request, newMajorPrice);

                        if (request.SyncByConversion)
                            SyncUnitPricesByConversion(product, request, newMajorPrice);
                        else
                            UpdateTargetUnitOnly(pricingContext, request, newTargetPrice);

                        _productRepo.Update(product);

                        await _auditLogger.LogAsync(
                            "Product",
                            product.Id,
                            "BulkPriceUpdate",
                            _currentUser.Username ?? "System",
                            $"تحديث {request.PriceTarget} ({request.UnitLevel}) للصنف {product.Code} من {pricingContext.CurrentTargetPrice:N2} إلى {newTargetPrice:N2}",
                            ct);

                        result.UpdatedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "ApplyAsync failed for Product {ProductId}.", productId);
                        result.FailedCount++;
                        result.Errors.Add(ErrorSanitizer.SanitizeGeneric(ex, $"تحديث سعر الصنف {productId}"));
                    }
                }

                if (result.UpdatedCount > 0)
                    await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken: ct);

            return ServiceResult<BulkPriceUpdateResultDto>.Success(result);
        }

        private static decimal CalculateNewPrice(decimal currentPrice, BulkPriceUpdateRequestDto request)
        {
            if (request.Mode == "Direct")
                return Math.Round(request.DirectPrice, 4);

            // Percentage mode
            var change = currentPrice * request.PercentageChange / 100m;
            return Math.Round(currentPrice + change, 4);
        }

        private static string ValidateRequest(BulkPriceUpdateRequestDto request)
        {
            if (request == null) return "بيانات التحديث مطلوبة.";
            if (request.ProductIds == null || request.ProductIds.Count == 0) return "يجب اختيار صنف واحد على الأقل.";
            if (request.Mode != "Percentage" && request.Mode != "Direct") return "وضع التحديث غير صالح (Percentage أو Direct).";
            if (request.PriceTarget != "SalePrice" && request.PriceTarget != "CostPrice") return "هدف السعر غير صالح.";
            if (request.UnitLevel != "MajorUnit" && request.UnitLevel != "MinorUnit") return "مستوى الوحدة غير صالح.";
            if (request.Mode == "Direct" && request.DirectPrice < 0) return "السعر المباشر لا يمكن أن يكون سالباً.";
            if (request.Mode == "Percentage" && (request.PercentageChange < -100 || request.PercentageChange > 1000))
                return "نسبة التغيير يجب أن تكون بين -100% و 1000%.";
            return null;
        }

        private static PricingContext BuildPricingContext(Domain.Entities.Inventory.Product product, BulkPriceUpdateRequestDto request)
        {
            var majorPrice = request.PriceTarget == "CostPrice" ? product.CostPrice : product.DefaultSalePrice;

            var minorUnit = product.ProductUnits
                .Where(unit => unit.UnitId != product.BaseUnitId && unit.ConversionFactor > 0)
                .OrderByDescending(unit => unit.ConversionFactor)
                .FirstOrDefault();

            if (request.UnitLevel == "MinorUnit" && minorUnit == null)
            {
                return new PricingContext
                {
                    IsValid = false,
                    ValidationMessage = "لا يوجد وحدة صغرى مرتبطة بالصنف.",
                    UnitNameAr = "-"
                };
            }

            if (minorUnit == null)
            {
                return new PricingContext
                {
                    IsValid = true,
                    UnitNameAr = "الوحدة الكلية",
                    MinorConversionFactor = 1,
                    CurrentMajorPrice = majorPrice,
                    CurrentMinorPrice = majorPrice,
                    CurrentTargetPrice = majorPrice,
                    TargetUnit = null
                };
            }

            var minorPrice = request.PriceTarget == "CostPrice"
                ? minorUnit.PurchasePrice
                : minorUnit.SalePrice;

            if (minorPrice <= 0)
            {
                minorPrice = Math.Round(majorPrice / minorUnit.ConversionFactor, 4);
            }

            return new PricingContext
            {
                IsValid = true,
                UnitNameAr = minorUnit.Unit?.NameAr ?? "الوحدة الصغرى",
                MinorConversionFactor = minorUnit.ConversionFactor,
                CurrentMajorPrice = majorPrice,
                CurrentMinorPrice = minorPrice,
                CurrentTargetPrice = request.UnitLevel == "MinorUnit" ? minorPrice : majorPrice,
                TargetUnit = minorUnit
            };
        }

        private static (decimal NewMajorPrice, decimal NewMinorPrice) CalculateMajorMinorPrices(PricingContext context, decimal newTargetPrice)
        {
            if (context.TargetUnit == null)
                return (newTargetPrice, newTargetPrice);

            if (context.CurrentTargetPrice == context.CurrentMinorPrice)
            {
                var majorFromMinor = Math.Round(newTargetPrice * context.MinorConversionFactor, 4);
                return (majorFromMinor, newTargetPrice);
            }

            var minorFromMajor = Math.Round(newTargetPrice / context.MinorConversionFactor, 4);
            return (newTargetPrice, minorFromMajor);
        }

        private static void ApplyMajorPrice(Domain.Entities.Inventory.Product product, BulkPriceUpdateRequestDto request, decimal newMajorPrice)
        {
            if (request.PriceTarget == "CostPrice")
            {
                product.UpdateCostPrice(newMajorPrice);
                return;
            }

            product.Update(
                product.NameAr,
                product.NameEn,
                product.CategoryId,
                newMajorPrice,
                product.MinimumStock,
                product.ReorderLevel,
                product.VatRate,
                product.Barcode,
                product.Description,
                product.DefaultSupplierId,
                product.WholesalePrice,
                product.RetailPrice,
                product.ImagePath,
                product.MaximumStock);
        }

        private static void SyncUnitPricesByConversion(
            Domain.Entities.Inventory.Product product,
            BulkPriceUpdateRequestDto request,
            decimal newMajorPrice)
        {
            foreach (var unit in product.ProductUnits)
            {
                if (unit.ConversionFactor <= 0)
                    continue;

                var convertedPrice = Math.Round(newMajorPrice / unit.ConversionFactor, 4);
                if (request.PriceTarget == "CostPrice")
                    unit.UpdatePricing(unit.SalePrice, convertedPrice, unit.Barcode);
                else
                    unit.UpdatePricing(convertedPrice, unit.PurchasePrice, unit.Barcode);
            }
        }

        private static void UpdateTargetUnitOnly(PricingContext context, BulkPriceUpdateRequestDto request, decimal newTargetPrice)
        {
            if (context.TargetUnit == null)
                return;

            if (request.PriceTarget == "CostPrice")
                context.TargetUnit.UpdatePricing(context.TargetUnit.SalePrice, newTargetPrice, context.TargetUnit.Barcode);
            else
                context.TargetUnit.UpdatePricing(newTargetPrice, context.TargetUnit.PurchasePrice, context.TargetUnit.Barcode);
        }

        private sealed class PricingContext
        {
            public bool IsValid { get; set; }
            public string ValidationMessage { get; set; }
            public string UnitNameAr { get; set; }
            public decimal MinorConversionFactor { get; set; }
            public decimal CurrentTargetPrice { get; set; }
            public decimal CurrentMajorPrice { get; set; }
            public decimal CurrentMinorPrice { get; set; }
            public Domain.Entities.Inventory.ProductUnit TargetUnit { get; set; }
        }
    }
}
