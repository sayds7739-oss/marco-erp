using System;
using FluentValidation;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces;

namespace MarcoERP.Application.Validators.Treasury
{
    public sealed class CreateBankReconciliationDtoValidator : AbstractValidator<CreateBankReconciliationDto>
    {
        private readonly IDateTimeProvider _dateTimeProvider;

        public CreateBankReconciliationDtoValidator(IDateTimeProvider dateTimeProvider)
        {
            _dateTimeProvider = dateTimeProvider;

            RuleFor(x => x.BankAccountId)
                .GreaterThan(0).WithMessage("الحساب البنكي مطلوب.");

            RuleFor(x => x.ReconciliationDate)
                .GreaterThanOrEqualTo(new DateTime(2020, 1, 1))
                .WithMessage("تاريخ التسوية يجب أن يكون بعد 2020/01/01.")
                .LessThanOrEqualTo(_dateTimeProvider.UtcNow.AddDays(30))
                .WithMessage("تاريخ التسوية لا يمكن أن يكون في المستقبل البعيد.");

            RuleFor(x => x.StatementBalance)
                .PrecisionScale(18, 4, false).WithMessage("رصيد كشف الحساب يتجاوز الدقة المسموحة.");

            RuleFor(x => x.Notes)
                .MaximumLength(500).WithMessage("الملاحظات لا تتجاوز 500 حرف.");
        }
    }

    public sealed class CreateBankReconciliationItemDtoValidator : AbstractValidator<CreateBankReconciliationItemDto>
    {
        public CreateBankReconciliationItemDtoValidator()
        {
            RuleFor(x => x.BankReconciliationId)
                .GreaterThan(0).WithMessage("معرف التسوية مطلوب.");

            RuleFor(x => x.TransactionDate)
                .GreaterThanOrEqualTo(new DateTime(2020, 1, 1))
                .WithMessage("تاريخ العملية يجب أن يكون بعد 2020/01/01.");

            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("وصف البند مطلوب.")
                .MaximumLength(300).WithMessage("الوصف لا يتجاوز 300 حرف.");

            RuleFor(x => x.Amount)
                .NotEqual(0).WithMessage("مبلغ البند مطلوب.")
                .LessThanOrEqualTo(99_999_999_999m).WithMessage("المبلغ يتجاوز الحد المسموح.")
                .GreaterThanOrEqualTo(-99_999_999_999m).WithMessage("المبلغ يتجاوز الحد المسموح.");

            RuleFor(x => x.Reference)
                .MaximumLength(100).WithMessage("المرجع لا يتجاوز 100 حرف.");
        }
    }

    /// <summary>V-01 fix: Validates UpdateBankReconciliationDto before update.</summary>
    public sealed class UpdateBankReconciliationDtoValidator : AbstractValidator<UpdateBankReconciliationDto>
    {
        private readonly IDateTimeProvider _dateTimeProvider;

        public UpdateBankReconciliationDtoValidator(IDateTimeProvider dateTimeProvider)
        {
            _dateTimeProvider = dateTimeProvider;

            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("معرف التسوية مطلوب.");

            RuleFor(x => x.ReconciliationDate)
                .GreaterThanOrEqualTo(new DateTime(2020, 1, 1))
                .WithMessage("تاريخ التسوية يجب أن يكون بعد 2020/01/01.")
                .LessThanOrEqualTo(_dateTimeProvider.UtcNow.AddDays(30))
                .WithMessage("تاريخ التسوية لا يمكن أن يكون في المستقبل البعيد.");

            RuleFor(x => x.StatementBalance)
                .PrecisionScale(18, 4, false).WithMessage("رصيد كشف الحساب يتجاوز الدقة المسموحة.");

            RuleFor(x => x.Notes)
                .MaximumLength(500).WithMessage("الملاحظات لا تتجاوز 500 حرف.");
        }
    }
}
