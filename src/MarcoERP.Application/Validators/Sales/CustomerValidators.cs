using FluentValidation;
using MarcoERP.Application.DTOs.Sales;

namespace MarcoERP.Application.Validators.Sales
{
    // ═══════════════════════════════════════════════════════════
    //  Customer Validators
    // ═══════════════════════════════════════════════════════════

    /// <summary>Validates CreateCustomerDto.</summary>
    public sealed class CreateCustomerDtoValidator : AbstractValidator<CreateCustomerDto>
    {
        public CreateCustomerDtoValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("كود العميل مطلوب.")
                .MaximumLength(20).WithMessage("كود العميل لا يتجاوز 20 حرف.");

            RuleFor(x => x.NameAr)
                .NotEmpty().WithMessage("اسم العميل بالعربي مطلوب.")
                .MaximumLength(200).WithMessage("اسم العميل لا يتجاوز 200 حرف.");

            RuleFor(x => x.NameEn)
                .MaximumLength(200).WithMessage("اسم العميل بالإنجليزي لا يتجاوز 200 حرف.");

            RuleFor(x => x.Phone)
                .MaximumLength(30).WithMessage("رقم الهاتف لا يتجاوز 30 حرف.");

            RuleFor(x => x.Mobile)
                .MaximumLength(30).WithMessage("رقم الموبايل لا يتجاوز 30 حرف.");

            RuleFor(x => x.Address)
                .MaximumLength(500).WithMessage("العنوان لا يتجاوز 500 حرف.");

            RuleFor(x => x.City)
                .MaximumLength(100).WithMessage("المدينة لا تتجاوز 100 حرف.");

            RuleFor(x => x.TaxNumber)
                .MaximumLength(50).WithMessage("الرقم الضريبي لا يتجاوز 50 حرف.");

            RuleFor(x => x.Email)
                .MaximumLength(200).WithMessage("البريد الإلكتروني لا يتجاوز 200 حرف.");

            RuleFor(x => x.CommercialRegister)
                .MaximumLength(50).WithMessage("السجل التجاري لا يتجاوز 50 حرف.");

            RuleFor(x => x.Country)
                .MaximumLength(100).WithMessage("الدولة لا تتجاوز 100 حرف.");

            RuleFor(x => x.PostalCode)
                .MaximumLength(20).WithMessage("الرمز البريدي لا يتجاوز 20 حرف.");

            RuleFor(x => x.ContactPerson)
                .MaximumLength(200).WithMessage("جهة الاتصال لا تتجاوز 200 حرف.");

            RuleFor(x => x.Website)
                .MaximumLength(200).WithMessage("الموقع الإلكتروني لا يتجاوز 200 حرف.");

            RuleFor(x => x.DefaultDiscountPercent)
                .InclusiveBetween(0, 100).WithMessage("نسبة الخصم الافتراضية يجب أن تكون بين 0 و 100.");

            RuleFor(x => x.CreditLimit)
                .GreaterThanOrEqualTo(0).WithMessage("حد الائتمان لا يمكن أن يكون بالسالب.");

            RuleFor(x => x.Notes)
                .MaximumLength(1000).WithMessage("الملاحظات لا تتجاوز 1000 حرف.");
        }
    }

    /// <summary>Validates UpdateCustomerDto.</summary>
    public sealed class UpdateCustomerDtoValidator : AbstractValidator<UpdateCustomerDto>
    {
        public UpdateCustomerDtoValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("معرف العميل غير صالح.");

            RuleFor(x => x.NameAr)
                .NotEmpty().WithMessage("اسم العميل بالعربي مطلوب.")
                .MaximumLength(200).WithMessage("اسم العميل لا يتجاوز 200 حرف.");

            RuleFor(x => x.NameEn)
                .MaximumLength(200).WithMessage("اسم العميل بالإنجليزي لا يتجاوز 200 حرف.");

            RuleFor(x => x.Phone)
                .MaximumLength(30).WithMessage("رقم الهاتف لا يتجاوز 30 حرف.");

            RuleFor(x => x.Mobile)
                .MaximumLength(30).WithMessage("رقم الموبايل لا يتجاوز 30 حرف.");

            RuleFor(x => x.Address)
                .MaximumLength(500).WithMessage("العنوان لا يتجاوز 500 حرف.");

            RuleFor(x => x.City)
                .MaximumLength(100).WithMessage("المدينة لا تتجاوز 100 حرف.");

            RuleFor(x => x.TaxNumber)
                .MaximumLength(50).WithMessage("الرقم الضريبي لا يتجاوز 50 حرف.");

            RuleFor(x => x.Email)
                .MaximumLength(200).WithMessage("البريد الإلكتروني لا يتجاوز 200 حرف.");

            RuleFor(x => x.CommercialRegister)
                .MaximumLength(50).WithMessage("السجل التجاري لا يتجاوز 50 حرف.");

            RuleFor(x => x.Country)
                .MaximumLength(100).WithMessage("الدولة لا تتجاوز 100 حرف.");

            RuleFor(x => x.PostalCode)
                .MaximumLength(20).WithMessage("الرمز البريدي لا يتجاوز 20 حرف.");

            RuleFor(x => x.ContactPerson)
                .MaximumLength(200).WithMessage("جهة الاتصال لا تتجاوز 200 حرف.");

            RuleFor(x => x.Website)
                .MaximumLength(200).WithMessage("الموقع الإلكتروني لا يتجاوز 200 حرف.");

            RuleFor(x => x.DefaultDiscountPercent)
                .InclusiveBetween(0, 100).WithMessage("نسبة الخصم الافتراضية يجب أن تكون بين 0 و 100.");

            RuleFor(x => x.CreditLimit)
                .GreaterThanOrEqualTo(0).WithMessage("حد الائتمان لا يمكن أن يكون بالسالب.");

            RuleFor(x => x.Notes)
                .MaximumLength(1000).WithMessage("الملاحظات لا تتجاوز 1000 حرف.");
        }
    }
}
