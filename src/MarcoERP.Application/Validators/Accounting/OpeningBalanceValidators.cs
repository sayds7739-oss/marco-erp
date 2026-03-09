using FluentValidation;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.Interfaces;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.Validators.Accounting
{
    // ════════════════════════════════════════════════════════════
    //  Opening Balance Validators — الأرصدة الافتتاحية
    // ════════════════════════════════════════════════════════════

    public sealed class CreateOpeningBalanceDtoValidator : AbstractValidator<CreateOpeningBalanceDto>
    {
        public CreateOpeningBalanceDtoValidator(IDateTimeProvider dateTimeProvider)
        {
            RuleFor(x => x.FiscalYearId)
                .GreaterThan(0).WithMessage("السنة المالية مطلوبة.");

            RuleFor(x => x.BalanceDate)
                .NotEmpty().WithMessage("تاريخ الأرصدة الافتتاحية مطلوب.")
                .Must(d => d.Year >= 2020 && d <= dateTimeProvider.UtcNow.AddYears(1))
                .WithMessage("تاريخ الأرصدة الافتتاحية خارج النطاق المسموح.");

            RuleFor(x => x.Notes)
                .MaximumLength(1000).WithMessage("الملاحظات لا تتجاوز 1000 حرف.");

            When(x => x.Lines != null && x.Lines.Count > 0, () =>
            {
                RuleForEach(x => x.Lines)
                    .SetValidator(new CreateOpeningBalanceLineDtoValidator());
            });
        }
    }

    public sealed class UpdateOpeningBalanceDtoValidator : AbstractValidator<UpdateOpeningBalanceDto>
    {
        public UpdateOpeningBalanceDtoValidator(IDateTimeProvider dateTimeProvider)
        {
            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("معرف الأرصدة الافتتاحية مطلوب.");

            RuleFor(x => x.BalanceDate)
                .NotEmpty().WithMessage("تاريخ الأرصدة الافتتاحية مطلوب.")
                .Must(d => d.Year >= 2020 && d <= dateTimeProvider.UtcNow.AddYears(1))
                .WithMessage("تاريخ الأرصدة الافتتاحية خارج النطاق المسموح.");

            RuleFor(x => x.Notes)
                .MaximumLength(1000).WithMessage("الملاحظات لا تتجاوز 1000 حرف.");

            RuleFor(x => x.Lines)
                .NotEmpty().WithMessage("يجب إضافة بند واحد على الأقل.");

            RuleForEach(x => x.Lines)
                .SetValidator(new CreateOpeningBalanceLineDtoValidator());
        }
    }

    public sealed class CreateOpeningBalanceLineDtoValidator : AbstractValidator<CreateOpeningBalanceLineDto>
    {
        public CreateOpeningBalanceLineDtoValidator()
        {
            RuleFor(x => x.LineType)
                .IsInEnum().WithMessage("نوع البند غير صالح.");

            RuleFor(x => x.Notes)
                .MaximumLength(500).WithMessage("ملاحظات البند لا تتجاوز 500 حرف.");

            // ── Account lines: require AccountId + debit/credit ──
            When(x => x.LineType == OpeningBalanceLineType.Account, () =>
            {
                RuleFor(x => x.AccountId)
                    .NotNull().WithMessage("الحساب مطلوب لبنود الحسابات العامة.")
                    .GreaterThan(0).WithMessage("الحساب مطلوب لبنود الحسابات العامة.");

                RuleFor(x => x.DebitAmount)
                    .GreaterThanOrEqualTo(0).WithMessage("المبلغ المدين لا يمكن أن يكون سالباً.")
                    .PrecisionScale(18, 2, false).WithMessage("المبلغ المدين يجب أن يكون برقمين عشريين كحد أقصى.");

                RuleFor(x => x.CreditAmount)
                    .GreaterThanOrEqualTo(0).WithMessage("المبلغ الدائن لا يمكن أن يكون سالباً.")
                    .PrecisionScale(18, 2, false).WithMessage("المبلغ الدائن يجب أن يكون برقمين عشريين كحد أقصى.");

                RuleFor(x => x)
                    .Must(x => (x.DebitAmount > 0) != (x.CreditAmount > 0))
                    .WithMessage("يجب تحديد مبلغ مدين أو دائن (ليس كلاهما).")
                    .WithName("Amount");
            });

            // ── Customer lines ──────────────────────────────────
            When(x => x.LineType == OpeningBalanceLineType.Customer, () =>
            {
                RuleFor(x => x.CustomerId)
                    .NotNull().WithMessage("العميل مطلوب لبنود العملاء.")
                    .GreaterThan(0).WithMessage("العميل مطلوب لبنود العملاء.");

                RuleFor(x => x.Amount)
                    .NotEqual(0).WithMessage("المبلغ مطلوب لبند العميل.");
            });

            // ── Supplier lines ──────────────────────────────────
            When(x => x.LineType == OpeningBalanceLineType.Supplier, () =>
            {
                RuleFor(x => x.SupplierId)
                    .NotNull().WithMessage("المورد مطلوب لبنود الموردين.")
                    .GreaterThan(0).WithMessage("المورد مطلوب لبنود الموردين.");

                RuleFor(x => x.Amount)
                    .NotEqual(0).WithMessage("المبلغ مطلوب لبند المورد.");
            });

            // ── Inventory lines ─────────────────────────────────
            When(x => x.LineType == OpeningBalanceLineType.Inventory, () =>
            {
                RuleFor(x => x.ProductId)
                    .NotNull().WithMessage("الصنف مطلوب لبنود المخزون.")
                    .GreaterThan(0).WithMessage("الصنف مطلوب لبنود المخزون.");

                RuleFor(x => x.WarehouseId)
                    .NotNull().WithMessage("المخزن مطلوب لبنود المخزون.")
                    .GreaterThan(0).WithMessage("المخزن مطلوب لبنود المخزون.");

                RuleFor(x => x.Quantity)
                    .GreaterThan(0).WithMessage("الكمية يجب أن تكون أكبر من صفر.");

                RuleFor(x => x.UnitCost)
                    .GreaterThanOrEqualTo(0).WithMessage("تكلفة الوحدة لا يمكن أن تكون سالبة.");
            });

            // ── Cashbox lines ───────────────────────────────────
            When(x => x.LineType == OpeningBalanceLineType.Cashbox, () =>
            {
                RuleFor(x => x.CashboxId)
                    .NotNull().WithMessage("الصندوق مطلوب لبنود الصناديق.")
                    .GreaterThan(0).WithMessage("الصندوق مطلوب لبنود الصناديق.");

                RuleFor(x => x.Amount)
                    .GreaterThan(0).WithMessage("مبلغ الصندوق يجب أن يكون أكبر من صفر.");
            });

            // ── BankAccount lines ───────────────────────────────
            When(x => x.LineType == OpeningBalanceLineType.BankAccount, () =>
            {
                RuleFor(x => x.BankAccountId)
                    .NotNull().WithMessage("الحساب البنكي مطلوب لبنود البنوك.")
                    .GreaterThan(0).WithMessage("الحساب البنكي مطلوب لبنود البنوك.");

                RuleFor(x => x.Amount)
                    .GreaterThan(0).WithMessage("رصيد البنك يجب أن يكون أكبر من صفر.");
            });
        }
    }
}
