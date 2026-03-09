using System;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces.Treasury;
using MarcoERP.Domain.Enums;

namespace MarcoERP.WpfUI.Services
{
    public sealed class InvoiceTreasuryIntegrationService : IInvoiceTreasuryIntegrationService
    {
        private readonly ICashReceiptService _cashReceiptService;
        private readonly ICashPaymentService _cashPaymentService;
        private readonly IQuickTreasuryDialogService _quickTreasuryDialogService;
        private readonly ITreasuryInvoicePaymentQueryService _invoicePaymentQueryService;

        public InvoiceTreasuryIntegrationService(
            ICashReceiptService cashReceiptService,
            ICashPaymentService cashPaymentService,
            IQuickTreasuryDialogService quickTreasuryDialogService,
            ITreasuryInvoicePaymentQueryService invoicePaymentQueryService)
        {
            _cashReceiptService = cashReceiptService ?? throw new ArgumentNullException(nameof(cashReceiptService));
            _cashPaymentService = cashPaymentService ?? throw new ArgumentNullException(nameof(cashPaymentService));
            _quickTreasuryDialogService = quickTreasuryDialogService ?? throw new ArgumentNullException(nameof(quickTreasuryDialogService));
            _invoicePaymentQueryService = invoicePaymentQueryService ?? throw new ArgumentNullException(nameof(invoicePaymentQueryService));
        }

        public async Task<InvoiceTreasuryCreateResult> PromptAndCreateSalesReceiptAsync(SalesInvoiceDto invoice, int customerAccountId, CancellationToken ct = default)
        {
            var dialogResult = await _quickTreasuryDialogService.ShowAsync(
                new QuickTreasuryDialogRequest(
                    Title: "تحصيل اختياري",
                    VoucherDate: invoice.InvoiceDate,
                    DefaultAmount: invoice.NetTotal,
                    Description: $"تحصيل فاتورة بيع {invoice.InvoiceNumber}",
                    Notes: invoice.Notes,
                    Kind: QuickTreasuryDialogKind.Receipt),
                ct);

            if (dialogResult == null)
                return new InvoiceTreasuryCreateResult(false, null);

            var createDto = new CreateCashReceiptDto
            {
                ReceiptDate = invoice.InvoiceDate,
                CashboxId = dialogResult.CashboxId,
                AccountId = customerAccountId,
                CustomerId = invoice.CustomerId,
                SalesInvoiceId = invoice.Id,
                Amount = dialogResult.Amount,
                Description = $"تحصيل فاتورة بيع {invoice.InvoiceNumber} ({GetPaymentMethodLabel(dialogResult.PaymentMethod)})",
                Notes = invoice.Notes
            };

            var createResult = await _cashReceiptService.CreateAsync(createDto, ct);
            if (createResult.IsFailure)
                return new InvoiceTreasuryCreateResult(false, createResult.ErrorMessage);

            var postResult = await _cashReceiptService.PostAsync(createResult.Data.Id, ct);
            if (postResult.IsFailure)
            {
                return new InvoiceTreasuryCreateResult(
                    false,
                    $"تم إنشاء سند قبض كمسودة (#{createResult.Data.ReceiptNumber}) لكن فشل الترحيل: {postResult.ErrorMessage}");
            }

            return new InvoiceTreasuryCreateResult(true, null);
        }

        public async Task<InvoiceTreasuryCreateResult> PromptAndCreatePurchasePaymentAsync(PurchaseInvoiceDto invoice, int supplierAccountId, CancellationToken ct = default)
        {
            var dialogResult = await _quickTreasuryDialogService.ShowAsync(
                new QuickTreasuryDialogRequest(
                    Title: "دفع فوري",
                    VoucherDate: invoice.InvoiceDate,
                    DefaultAmount: invoice.NetTotal,
                    Description: $"سداد فاتورة شراء {invoice.InvoiceNumber}",
                    Notes: invoice.Notes,
                    Kind: QuickTreasuryDialogKind.Payment),
                ct);

            if (dialogResult == null)
                return new InvoiceTreasuryCreateResult(false, null);

            var createDto = new CreateCashPaymentDto
            {
                PaymentDate = invoice.InvoiceDate,
                CashboxId = dialogResult.CashboxId,
                AccountId = supplierAccountId,
                SupplierId = invoice.SupplierId,
                PurchaseInvoiceId = invoice.Id,
                Amount = dialogResult.Amount,
                Description = $"سداد فاتورة شراء {invoice.InvoiceNumber} ({GetPaymentMethodLabel(dialogResult.PaymentMethod)})",
                Notes = invoice.Notes
            };

            var createResult = await _cashPaymentService.CreateAsync(createDto, ct);
            if (createResult.IsFailure)
                return new InvoiceTreasuryCreateResult(false, createResult.ErrorMessage);

            var postResult = await _cashPaymentService.PostAsync(createResult.Data.Id, ct);
            if (postResult.IsFailure)
            {
                return new InvoiceTreasuryCreateResult(
                    false,
                    $"تم إنشاء سند صرف كمسودة (#{createResult.Data.PaymentNumber}) لكن فشل الترحيل: {postResult.ErrorMessage}");
            }

            return new InvoiceTreasuryCreateResult(true, null);
        }

        public async Task<decimal> GetPostedPaidForSalesInvoiceAsync(int salesInvoiceId, CancellationToken ct = default)
        {
            var result = await _invoicePaymentQueryService.GetPostedReceiptsTotalForSalesInvoiceAsync(salesInvoiceId, ct);
            return result.IsSuccess ? result.Data : 0m;
        }

        public async Task<decimal> GetPostedPaidForPurchaseInvoiceAsync(int purchaseInvoiceId, CancellationToken ct = default)
        {
            var result = await _invoicePaymentQueryService.GetPostedPaymentsTotalForPurchaseInvoiceAsync(purchaseInvoiceId, ct);
            return result.IsSuccess ? result.Data : 0m;
        }

        public string GetPaymentMethodLabel(PaymentMethod method)
        {
            return method switch
            {
                PaymentMethod.Cash => "نقدي",
                PaymentMethod.Card => "بطاقة",
                PaymentMethod.OnAccount => "على الحساب",
                _ => method.ToString()
            };
        }
    }
}
