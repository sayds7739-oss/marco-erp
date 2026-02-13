using FluentValidation;
using MarcoERP.Application.DTOs.Inventory;

namespace MarcoERP.Application.Validators.Inventory
{
    public sealed class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
    {
        public CreateProductDtoValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("كود الصنف مطلوب.")
                .MaximumLength(20).WithMessage("كود الصنف لا يتجاوز 20 حرف.");

            RuleFor(x => x.NameAr)
                .NotEmpty().WithMessage("اسم الصنف بالعربي مطلوب.")
                .MaximumLength(200).WithMessage("اسم الصنف لا يتجاوز 200 حرف.");

            RuleFor(x => x.NameEn)
                .MaximumLength(200);

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("تصنيف الصنف مطلوب.");

            RuleFor(x => x.BaseUnitId)
                .GreaterThan(0).WithMessage("الوحدة الأساسية مطلوبة.");

            RuleFor(x => x.CostPrice)
                .GreaterThanOrEqualTo(0).WithMessage("سعر التكلفة لا يمكن أن يكون سالباً.");

            RuleFor(x => x.DefaultSalePrice)
                .GreaterThanOrEqualTo(0).WithMessage("سعر البيع لا يمكن أن يكون سالباً.");

            RuleFor(x => x.CostPrice)
                .GreaterThanOrEqualTo(0).WithMessage("سعر التكلفة لا يمكن أن يكون سالباً.");

            RuleFor(x => x.MinimumStock)
                .GreaterThanOrEqualTo(0).WithMessage("الحد الأدنى لا يمكن أن يكون سالباً.");

            RuleFor(x => x.VatRate)
                .InclusiveBetween(0, 100).WithMessage("نسبة الضريبة يجب أن تكون بين 0 و 100.");

            RuleFor(x => x.Barcode)
                .MaximumLength(50);

            RuleFor(x => x.Description)
                .MaximumLength(500);

            RuleForEach(x => x.Units).SetValidator(new CreateProductUnitDtoValidator());
        }
    }

    public sealed class CreateProductUnitDtoValidator : AbstractValidator<CreateProductUnitDto>
    {
        public CreateProductUnitDtoValidator()
        {
            RuleFor(x => x.UnitId)
                .GreaterThan(0).WithMessage("الوحدة مطلوبة.");

            RuleFor(x => x.ConversionFactor)
                .GreaterThan(0).WithMessage("معامل التحويل يجب أن يكون أكبر من صفر.");

            RuleFor(x => x.SalePrice)
                .GreaterThanOrEqualTo(0).WithMessage("سعر البيع لا يمكن أن يكون سالباً.");

            RuleFor(x => x.PurchasePrice)
                .GreaterThanOrEqualTo(0).WithMessage("سعر الشراء لا يمكن أن يكون سالباً.");

            RuleFor(x => x.Barcode).MaximumLength(50);
        }
    }

    public sealed class UpdateProductDtoValidator : AbstractValidator<UpdateProductDto>
    {
        public UpdateProductDtoValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0).WithMessage("معرف الصنف مطلوب.");

            RuleFor(x => x.NameAr)
                .NotEmpty().WithMessage("اسم الصنف بالعربي مطلوب.")
                .MaximumLength(200);

            RuleFor(x => x.NameEn).MaximumLength(200);

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("تصنيف الصنف مطلوب.");

            RuleFor(x => x.DefaultSalePrice)
                .GreaterThanOrEqualTo(0).WithMessage("سعر البيع لا يمكن أن يكون سالباً.");

            RuleFor(x => x.MinimumStock)
                .GreaterThanOrEqualTo(0);

            RuleFor(x => x.VatRate)
                .InclusiveBetween(0, 100);

            RuleFor(x => x.Barcode).MaximumLength(50);
            RuleFor(x => x.Description).MaximumLength(500);

            RuleForEach(x => x.Units).SetValidator(new CreateProductUnitDtoValidator());
        }
    }
}
