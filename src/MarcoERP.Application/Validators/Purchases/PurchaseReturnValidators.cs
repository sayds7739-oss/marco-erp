using FluentValidation;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.Interfaces;

namespace MarcoERP.Application.Validators.Purchases
{
    // ═══════════════════════════════════════════════════════════
    //  Purchase Return Validators
    // ═══════════════════════════════════════════════════════════

    /// <summary>Validates CreatePurchaseReturnDto.</summary>
    public sealed class CreatePurchaseReturnDtoValidator : AbstractValidator<CreatePurchaseReturnDto>
    {
        public CreatePurchaseReturnDtoValidator(IDateTimeProvider dateTimeProvider)
        {
            RuleFor(x => x.ReturnDate)
                .NotEmpty().WithMessage("تاريخ المرتجع مطلوب.")
                .Must(d => d.Year >= 2020 && d <= dateTimeProvider.UtcNow.AddDays(30))
                .WithMessage("تاريخ المرتجع خارج النطاق المسموح.");

            RuleFor(x => x.SupplierId)
                .Must(id => id.HasValue && id.Value > 0).WithMessage("المورد مطلوب.")
                .When(x => x.CounterpartyType == Domain.Enums.CounterpartyType.Supplier);

            RuleFor(x => x.CounterpartyCustomerId)
                .Must(id => id.HasValue && id.Value > 0).WithMessage("العميل مطلوب.")
                .When(x => x.CounterpartyType == Domain.Enums.CounterpartyType.Customer);

            RuleFor(x => x.WarehouseId)
                .GreaterThan(0).WithMessage("المستودع مطلوب.");

            RuleFor(x => x.Lines)
                .NotEmpty().WithMessage("يجب إضافة بند واحد على الأقل.")
                .ForEach(line =>
                {
                    line.SetValidator(new CreatePurchaseReturnLineDtoValidator());
                });

            RuleFor(x => x.Notes)
                .MaximumLength(1000).WithMessage("الملاحظات لا تتجاوز 1000 حرف.");
        }
    }

    /// <summary>Validates UpdatePurchaseReturnDto.</summary>
    public sealed class UpdatePurchaseReturnDtoValidator : AbstractValidator<UpdatePurchaseReturnDto>
    {
        public UpdatePurchaseReturnDtoValidator(IDateTimeProvider dateTimeProvider)
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("معرف المرتجع غير صالح.");

            RuleFor(x => x.ReturnDate)
                .NotEmpty().WithMessage("تاريخ المرتجع مطلوب.")
                .Must(d => d.Year >= 2020 && d <= dateTimeProvider.UtcNow.AddDays(30))
                .WithMessage("تاريخ المرتجع خارج النطاق المسموح.");

            RuleFor(x => x.SupplierId)
                .Must(id => id.HasValue && id.Value > 0).WithMessage("المورد مطلوب.")
                .When(x => x.CounterpartyType == Domain.Enums.CounterpartyType.Supplier);

            RuleFor(x => x.CounterpartyCustomerId)
                .Must(id => id.HasValue && id.Value > 0).WithMessage("العميل مطلوب.")
                .When(x => x.CounterpartyType == Domain.Enums.CounterpartyType.Customer);

            RuleFor(x => x.WarehouseId)
                .GreaterThan(0).WithMessage("المستودع مطلوب.");

            RuleFor(x => x.Lines)
                .NotEmpty().WithMessage("يجب إضافة بند واحد على الأقل.")
                .ForEach(line =>
                {
                    line.SetValidator(new CreatePurchaseReturnLineDtoValidator());
                });

            RuleFor(x => x.Notes)
                .MaximumLength(1000).WithMessage("الملاحظات لا تتجاوز 1000 حرف.");
        }
    }

    /// <summary>Validates a single purchase return line.</summary>
    public sealed class CreatePurchaseReturnLineDtoValidator : AbstractValidator<CreatePurchaseReturnLineDto>
    {
        public CreatePurchaseReturnLineDtoValidator()
        {
            RuleFor(x => x.ProductId)
                .GreaterThan(0).WithMessage("الصنف مطلوب.");

            RuleFor(x => x.UnitId)
                .GreaterThan(0).WithMessage("الوحدة مطلوبة.");

            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("الكمية يجب أن تكون أكبر من صفر.");

            RuleFor(x => x.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("سعر الوحدة لا يمكن أن يكون سالباً.");

            RuleFor(x => x.DiscountPercent)
                .InclusiveBetween(0, 100).WithMessage("نسبة الخصم يجب أن تكون بين 0 و 100.");
        }
    }
}
