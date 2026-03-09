using FluentValidation;
using MarcoERP.Application.DTOs.Settings;

namespace MarcoERP.Application.Validators.Settings
{
    public sealed class ToggleFeatureDtoValidator : AbstractValidator<ToggleFeatureDto>
    {
        public ToggleFeatureDtoValidator()
        {
            RuleFor(x => x.FeatureKey)
                .NotEmpty().WithMessage("مفتاح الميزة مطلوب.");
        }
    }
}
