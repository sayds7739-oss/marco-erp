using FluentValidation;
using MarcoERP.Application.DTOs.Purchases;

namespace MarcoERP.Application.Validators.Purchases
{
    // ═══════════════════════════════════════════════════════════
    //  Supplier Validators
    // ═══════════════════════════════════════════════════════════

    /// <summary>Validates CreateSupplierDto.</summary>
    public sealed class CreateSupplierDtoValidator : AbstractValidator<CreateSupplierDto>
    {
        public CreateSupplierDtoValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("كود المورد مطلوب.")
                .MaximumLength(20).WithMessage("كود المورد لا يتجاوز 20 حرف.");

            RuleFor(x => x.NameAr)
                .NotEmpty().WithMessage("اسم المورد بالعربي مطلوب.")
                .MaximumLength(200).WithMessage("اسم المورد لا يتجاوز 200 حرف.");

            RuleFor(x => x.NameEn)
                .MaximumLength(200).WithMessage("اسم المورد بالإنجليزي لا يتجاوز 200 حرف.");

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

            RuleFor(x => x.BankName)
                .MaximumLength(200).WithMessage("اسم البنك لا يتجاوز 200 حرف.");

            RuleFor(x => x.BankAccountName)
                .MaximumLength(200).WithMessage("اسم صاحب الحساب لا يتجاوز 200 حرف.");

            RuleFor(x => x.BankAccountNumber)
                .MaximumLength(50).WithMessage("رقم الحساب البنكي لا يتجاوز 50 حرف.");

            RuleFor(x => x.IBAN)
                .MaximumLength(34).WithMessage("رقم الآيبان لا يتجاوز 34 حرف.");

            RuleFor(x => x.CreditLimit)
                .GreaterThanOrEqualTo(0).WithMessage("حد الائتمان لا يمكن أن يكون بالسالب.");

            RuleFor(x => x.Notes)
                .MaximumLength(1000).WithMessage("الملاحظات لا تتجاوز 1000 حرف.");
        }
    }

    /// <summary>Validates UpdateSupplierDto.</summary>
    public sealed class UpdateSupplierDtoValidator : AbstractValidator<UpdateSupplierDto>
    {
        public UpdateSupplierDtoValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("معرف المورد غير صالح.");

            RuleFor(x => x.NameAr)
                .NotEmpty().WithMessage("اسم المورد بالعربي مطلوب.")
                .MaximumLength(200).WithMessage("اسم المورد لا يتجاوز 200 حرف.");

            RuleFor(x => x.NameEn)
                .MaximumLength(200).WithMessage("اسم المورد بالإنجليزي لا يتجاوز 200 حرف.");

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

            RuleFor(x => x.BankName)
                .MaximumLength(200).WithMessage("اسم البنك لا يتجاوز 200 حرف.");

            RuleFor(x => x.BankAccountName)
                .MaximumLength(200).WithMessage("اسم صاحب الحساب لا يتجاوز 200 حرف.");

            RuleFor(x => x.BankAccountNumber)
                .MaximumLength(50).WithMessage("رقم الحساب البنكي لا يتجاوز 50 حرف.");

            RuleFor(x => x.IBAN)
                .MaximumLength(34).WithMessage("رقم الآيبان لا يتجاوز 34 حرف.");

            RuleFor(x => x.CreditLimit)
                .GreaterThanOrEqualTo(0).WithMessage("حد الائتمان لا يمكن أن يكون بالسالب.");

            RuleFor(x => x.Notes)
                .MaximumLength(1000).WithMessage("الملاحظات لا تتجاوز 1000 حرف.");
        }
    }
}
