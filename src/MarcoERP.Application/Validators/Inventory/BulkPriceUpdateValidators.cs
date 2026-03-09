using FluentValidation;
using MarcoERP.Application.DTOs.Inventory;

namespace MarcoERP.Application.Validators.Inventory
{
    // ════════════════════════════════════════════════════════════
    //  Bulk Price Update Validators
    // ════════════════════════════════════════════════════════════

    public sealed class BulkPriceUpdateRequestDtoValidator : AbstractValidator<BulkPriceUpdateRequestDto>
    {
        public BulkPriceUpdateRequestDtoValidator()
        {
            RuleFor(x => x.ProductIds)
                .NotEmpty().WithMessage("يجب اختيار صنف واحد على الأقل.");

            RuleFor(x => x.Mode)
                .NotEmpty().WithMessage("نوع التحديث مطلوب.")
                .Must(m => m == "Percentage" || m == "Direct")
                .WithMessage("نوع التحديث يجب أن يكون Percentage أو Direct.");

            RuleFor(x => x.PriceTarget)
                .NotEmpty().WithMessage("السعر المستهدف مطلوب.")
                .Must(t => t == "SalePrice" || t == "CostPrice")
                .WithMessage("السعر المستهدف يجب أن يكون SalePrice أو CostPrice.");

            RuleFor(x => x.UnitLevel)
                .NotEmpty().WithMessage("مستوى الوحدة مطلوب.")
                .Must(l => l == "MajorUnit" || l == "MinorUnit")
                .WithMessage("مستوى الوحدة يجب أن يكون MajorUnit أو MinorUnit.");

            RuleFor(x => x.PercentageChange)
                .InclusiveBetween(-100m, 1000m)
                .WithMessage("نسبة التغيير يجب أن تكون بين -100% و 1000%.")
                .When(x => x.Mode == "Percentage");

            RuleFor(x => x.DirectPrice)
                .GreaterThanOrEqualTo(0).WithMessage("السعر المباشر لا يمكن أن يكون سالباً.")
                .LessThanOrEqualTo(99_999_999_999m).WithMessage("السعر يتجاوز الحد الأقصى المسموح.")
                .When(x => x.Mode == "Direct");
        }
    }
}
