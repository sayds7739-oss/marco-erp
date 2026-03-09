using System;
using FluentValidation;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.Validators.Sales
{
    // ═══════════════════════════════════════════════════════════
    //  Sales Invoice Validators
    // ═══════════════════════════════════════════════════════════

    /// <summary>Validates CreateSalesInvoiceDto.</summary>
    public sealed class CreateSalesInvoiceDtoValidator : AbstractValidator<CreateSalesInvoiceDto>
    {
        private readonly IDateTimeProvider _dateTimeProvider;

        public CreateSalesInvoiceDtoValidator(IDateTimeProvider dateTimeProvider)
        {
            _dateTimeProvider = dateTimeProvider;

            RuleFor(x => x.InvoiceDate)
                .NotEmpty().WithMessage("تاريخ الفاتورة مطلوب.")
                .Must(d => d.Year >= 2020 && d <= _dateTimeProvider.UtcNow.AddDays(30))
                .WithMessage("تاريخ الفاتورة خارج النطاق المسموح.");

            // CustomerId required only when selling to a Customer
            RuleFor(x => x.CustomerId)
                .GreaterThan(0).WithMessage("العميل مطلوب.")
                .When(x => x.CounterpartyType == CounterpartyType.Customer);

            // SupplierId required when selling to a Supplier
            RuleFor(x => x.SupplierId)
                .NotNull().WithMessage("المورد مطلوب.")
                .Must(id => id.HasValue && id.Value > 0).WithMessage("المورد مطلوب.")
                .When(x => x.CounterpartyType == CounterpartyType.Supplier);

            RuleFor(x => x.WarehouseId)
                .GreaterThan(0).WithMessage("المستودع مطلوب.");

            RuleFor(x => x.Lines)
                .NotEmpty().WithMessage("يجب إضافة بند واحد على الأقل.")
                .ForEach(line =>
                {
                    line.SetValidator(new CreateSalesInvoiceLineDtoValidator());
                });

            RuleFor(x => x.Notes)
                .MaximumLength(1000).WithMessage("الملاحظات لا تتجاوز 1000 حرف.");

            RuleFor(x => x.HeaderDiscountPercent)
                .InclusiveBetween(0, 100).WithMessage("نسبة خصم الفاتورة يجب أن تكون بين 0 و 100.");

            RuleFor(x => x.HeaderDiscountAmount)
                .GreaterThanOrEqualTo(0).WithMessage("مبلغ خصم الفاتورة لا يمكن أن يكون سالباً.")
                .PrecisionScale(18, 2, false).WithMessage("مبلغ الخصم يجب أن يكون برقمين عشريين كحد أقصى.");

            RuleFor(x => x.DeliveryFee)
                .GreaterThanOrEqualTo(0).WithMessage("رسوم التوصيل لا يمكن أن تكون سالبة.")
                .PrecisionScale(18, 2, false).WithMessage("رسوم التوصيل يجب أن تكون برقمين عشريين كحد أقصى.");
        }
    }

    /// <summary>Validates UpdateSalesInvoiceDto.</summary>
    public sealed class UpdateSalesInvoiceDtoValidator : AbstractValidator<UpdateSalesInvoiceDto>
    {
        private readonly IDateTimeProvider _dateTimeProvider;

        public UpdateSalesInvoiceDtoValidator(IDateTimeProvider dateTimeProvider)
        {
            _dateTimeProvider = dateTimeProvider;

            RuleFor(x => x.Id)
                .GreaterThan(0).WithMessage("معرف الفاتورة غير صالح.");

            RuleFor(x => x.InvoiceDate)
                .NotEmpty().WithMessage("تاريخ الفاتورة مطلوب.")
                .Must(d => d.Year >= 2020 && d <= _dateTimeProvider.UtcNow.AddDays(30))
                .WithMessage("تاريخ الفاتورة خارج النطاق المسموح.");

            // CustomerId required only when selling to a Customer
            RuleFor(x => x.CustomerId)
                .GreaterThan(0).WithMessage("العميل مطلوب.")
                .When(x => x.CounterpartyType == CounterpartyType.Customer);

            // SupplierId required when selling to a Supplier
            RuleFor(x => x.SupplierId)
                .NotNull().WithMessage("المورد مطلوب.")
                .Must(id => id.HasValue && id.Value > 0).WithMessage("المورد مطلوب.")
                .When(x => x.CounterpartyType == CounterpartyType.Supplier);

            RuleFor(x => x.WarehouseId)
                .GreaterThan(0).WithMessage("المستودع مطلوب.");

            RuleFor(x => x.Lines)
                .NotEmpty().WithMessage("يجب إضافة بند واحد على الأقل.")
                .ForEach(line =>
                {
                    line.SetValidator(new CreateSalesInvoiceLineDtoValidator());
                });

            RuleFor(x => x.Notes)
                .MaximumLength(1000).WithMessage("الملاحظات لا تتجاوز 1000 حرف.");

            RuleFor(x => x.HeaderDiscountPercent)
                .InclusiveBetween(0, 100).WithMessage("نسبة خصم الفاتورة يجب أن تكون بين 0 و 100.");

            RuleFor(x => x.HeaderDiscountAmount)
                .GreaterThanOrEqualTo(0).WithMessage("مبلغ خصم الفاتورة لا يمكن أن يكون سالباً.")
                .PrecisionScale(18, 2, false).WithMessage("مبلغ الخصم يجب أن يكون برقمين عشريين كحد أقصى.");

            RuleFor(x => x.DeliveryFee)
                .GreaterThanOrEqualTo(0).WithMessage("رسوم التوصيل لا يمكن أن تكون سالبة.")
                .PrecisionScale(18, 2, false).WithMessage("رسوم التوصيل يجب أن تكون برقمين عشريين كحد أقصى.");
        }
    }

    /// <summary>Validates a single sales invoice line.</summary>
    public sealed class CreateSalesInvoiceLineDtoValidator : AbstractValidator<CreateSalesInvoiceLineDto>
    {
        public CreateSalesInvoiceLineDtoValidator()
        {
            RuleFor(x => x.ProductId)
                .GreaterThan(0).WithMessage("الصنف مطلوب.");

            RuleFor(x => x.UnitId)
                .GreaterThan(0).WithMessage("الوحدة مطلوبة.");

            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("الكمية يجب أن تكون أكبر من صفر.");

            RuleFor(x => x.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("سعر الوحدة لا يمكن أن يكون سالباً.")
                .PrecisionScale(18, 2, false).WithMessage("سعر الوحدة يجب أن يكون برقمين عشريين كحد أقصى.");

            RuleFor(x => x.DiscountPercent)
                .InclusiveBetween(0, 100).WithMessage("نسبة الخصم يجب أن تكون بين 0 و 100.");
        }
    }
}
