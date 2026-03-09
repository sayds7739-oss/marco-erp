using FluentValidation;
using MarcoERP.Application.DTOs.Accounting;

namespace MarcoERP.Application.Validators.Accounting
{
    /// <summary>
    /// FluentValidation rules for <see cref="CreateJournalEntryDto"/>.
    /// Validates structural format. Cross-aggregate validations (period open, year active,
    /// account postable) are performed in JournalEntryService.
    /// </summary>
    public sealed class CreateJournalEntryDtoValidator : AbstractValidator<CreateJournalEntryDto>
    {
        public CreateJournalEntryDtoValidator()
        {
            // JE-INV-13: Description mandatory
            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("وصف القيد مطلوب.")
                .MaximumLength(500).WithMessage("وصف القيد لا يتجاوز 500 حرف.");

            RuleFor(x => x.JournalDate)
                .NotEmpty().WithMessage("تاريخ القيد مطلوب.");

            RuleFor(x => x.SourceType)
                .IsInEnum().WithMessage("نوع المصدر غير صالح.");

            RuleFor(x => x.ReferenceNumber)
                .MaximumLength(100).WithMessage("رقم المرجع لا يتجاوز 100 حرف.")
                .When(x => !string.IsNullOrEmpty(x.ReferenceNumber));

            // JE-INV-01: at least 2 lines
            RuleFor(x => x.Lines)
                .NotNull().WithMessage("سطور القيد مطلوبة.")
                .Must(lines => lines != null && lines.Count >= 2)
                .WithMessage("القيد يجب أن يحتوي على سطرين على الأقل.");

            RuleForEach(x => x.Lines)
                .SetValidator(new CreateJournalEntryLineDtoValidator());
        }
    }

    /// <summary>
    /// Validator for individual journal entry line DTOs.
    /// </summary>
    public sealed class CreateJournalEntryLineDtoValidator : AbstractValidator<CreateJournalEntryLineDto>
    {
        public CreateJournalEntryLineDtoValidator()
        {
            RuleFor(x => x.AccountId)
                .GreaterThan(0).WithMessage("معرّف الحساب مطلوب لسطر القيد.");

            // JE-INV-05: Non-negative + precision
            RuleFor(x => x.DebitAmount)
                .GreaterThanOrEqualTo(0).WithMessage("المبلغ المدين لا يمكن أن يكون سالباً.")
                .PrecisionScale(18, 2, false).WithMessage("المبلغ المدين يجب أن يكون برقمين عشريين كحد أقصى.");

            RuleFor(x => x.CreditAmount)
                .GreaterThanOrEqualTo(0).WithMessage("المبلغ الدائن لا يمكن أن يكون سالباً.")
                .PrecisionScale(18, 2, false).WithMessage("المبلغ الدائن يجب أن يكون برقمين عشريين كحد أقصى.");

            // JE-INV-03: Not both sides populated
            RuleFor(x => x)
                .Must(x => !(x.DebitAmount > 0 && x.CreditAmount > 0))
                .WithMessage("السطر يجب أن يكون إما مدين أو دائن — لا يمكن الاثنين معاً.");

            // JE-INV-04: Not both zero
            RuleFor(x => x)
                .Must(x => !(x.DebitAmount == 0 && x.CreditAmount == 0))
                .WithMessage("لا يمكن أن يكون المدين والدائن صفر في نفس السطر.");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("وصف السطر لا يتجاوز 500 حرف.")
                .When(x => !string.IsNullOrEmpty(x.Description));
        }
    }
}
