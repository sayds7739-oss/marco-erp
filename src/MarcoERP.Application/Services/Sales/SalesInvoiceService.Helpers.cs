using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Sales;

namespace MarcoERP.Application.Services.Sales
{
    public sealed partial class SalesInvoiceService
    {
        // ══════════════════════════════════════════════════════════
        //  HELPERS — Private utilities shared across operations
        // ══════════════════════════════════════════════════════════

        private async Task<string> GetCreditControlErrorAsync(int? customerId, decimal invoiceNetTotal, CancellationToken ct)
        {
            if (!customerId.HasValue) return null; // No customer — supplier counterparty

            var customer = await _customerRepo.GetByIdAsync(customerId.Value, ct);
            if (customer == null) return null;

            if (customer.BlockedOnOverdue && customer.DaysAllowed is int daysAllowed && daysAllowed > 0)
            {
                var cutoff = _dateTime.UtcNow.Date.AddDays(-daysAllowed);
                var hasOverdue = await _smartEntryQueryService.HasOverduePostedSalesInvoicesAsync(customerId.Value, cutoff, ct);
                if (hasOverdue)
                    return $"العميل ({customer.NameAr}) محظور بسبب وجود فواتير متأخرة السداد.";
            }

            if (customer.CreditLimit > 0)
            {
                // Outstanding = PreviousBalance + (Posted Invoices - Posted Receipts)
                var outstanding = customer.PreviousBalance +
                                  await _smartEntryQueryService.GetCustomerOutstandingSalesBalanceAsync(customerId.Value, ct);

                var newExposure = outstanding + invoiceNetTotal;
                if (newExposure > customer.CreditLimit)
                {
                    return $"تجاوز الحد الائتماني للعميل ({customer.NameAr}). " +
                           $"الرصيد المستحق: {outstanding:N2}, الفاتورة: {invoiceNetTotal:N2}, الحد: {customer.CreditLimit:N2}.";
                }
            }

            return null;
        }

        private async Task ValidateStockAsync(
            SalesInvoice invoice,
            bool allowNegativeStock,
            List<string> warnings,
            CancellationToken ct)
        {
            foreach (var line in invoice.Lines)
            {
                var whProduct = await _whProductRepo.GetAsync(
                    invoice.WarehouseId, line.ProductId, ct);

                if (whProduct == null || whProduct.Quantity < line.BaseQuantity)
                {
                    var available = whProduct?.Quantity ?? 0;
                    var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                    var productName = product?.NameAr ?? $"#{line.ProductId}";
                    if (!allowNegativeStock)
                    {
                        throw new SalesInvoiceDomainException(
                            $"الكمية المتاحة للصنف ({productName}) = {available:N2} أقل من الكمية المطلوبة ({line.BaseQuantity:N2}).");
                    }

                    var warning = $"Negative stock allowed for product {productName}";
                    warnings?.Add(warning);

                    if (_auditLogger != null)
                    {
                        var performedBy = _currentUser.Username ?? "System";
                        await _auditLogger.LogAsync(
                            "SalesInvoice",
                            invoice.Id,
                            "RiskOperation",
                            performedBy,
                            warning,
                            ct);
                    }
                }
            }
        }

        private async Task<(Account ar, Account sales, Account vatOutput, Account cogs, Account inventory)>
            ResolvePostingAccountsAsync(CancellationToken ct)
        {
            var arAccount = await _accountRepo.GetByCodeAsync(ArAccountCode, ct);
            var salesAccount = await _accountRepo.GetByCodeAsync(SalesAccountCode, ct);
            var vatOutputAccount = await _accountRepo.GetByCodeAsync(VatOutputAccountCode, ct);
            var cogsAccount = await _accountRepo.GetByCodeAsync(CogsAccountCode, ct);
            var inventoryAccount = await _accountRepo.GetByCodeAsync(InventoryAccountCode, ct);

            if (arAccount == null || salesAccount == null || vatOutputAccount == null
                || cogsAccount == null || inventoryAccount == null)
            {
                throw new SalesInvoiceDomainException(
                    "حسابات النظام المطلوبة (مدينون / مبيعات / ضريبة مخرجات / تكلفة بضاعة / مخزون) غير موجودة. تأكد من تشغيل Seed.");
            }

            return (arAccount, salesAccount, vatOutputAccount, cogsAccount, inventoryAccount);
        }

        private async Task DeductStockAsync(
            SalesInvoice invoice,
            IReadOnlyDictionary<int, decimal> lineCosts,
            bool allowNegativeStock,
            CancellationToken ct)
        {
            foreach (var line in invoice.Lines)
            {
                var costPerBaseUnit = lineCosts.TryGetValue(line.Id, out var unitCost) ? unitCost : 0;

                await _stockManager.DecreaseAsync(new StockOperation
                {
                    ProductId = line.ProductId,
                    WarehouseId = invoice.WarehouseId,
                    UnitId = line.UnitId,
                    MovementType = MovementType.SalesOut,
                    Quantity = line.Quantity,
                    BaseQuantity = line.BaseQuantity,
                    CostPerBaseUnit = costPerBaseUnit,
                    DocumentDate = invoice.InvoiceDate,
                    DocumentNumber = invoice.InvoiceNumber,
                    SourceType = SourceType.SalesInvoice,
                    SourceId = invoice.Id,
                    Notes = $"فاتورة بيع رقم {invoice.InvoiceNumber}",
                    AllowCreate = allowNegativeStock,
                    AllowNegativeStock = allowNegativeStock,
                }, ct);
            }
        }

        private async Task ReverseStockAsync(SalesInvoice invoice, DateTime today, CancellationToken ct)
        {
            foreach (var line in invoice.Lines)
            {
                var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                var costPerBaseUnit = product.WeightedAverageCost;

                await _stockManager.IncreaseAsync(new StockOperation
                {
                    ProductId = line.ProductId,
                    WarehouseId = invoice.WarehouseId,
                    UnitId = line.UnitId,
                    MovementType = MovementType.SalesReturn,
                    Quantity = line.Quantity,
                    BaseQuantity = line.BaseQuantity,
                    CostPerBaseUnit = costPerBaseUnit,
                    DocumentDate = today,
                    DocumentNumber = invoice.InvoiceNumber,
                    SourceType = SourceType.SalesInvoice,
                    SourceId = invoice.Id,
                    Notes = $"إلغاء فاتورة بيع رقم {invoice.InvoiceNumber}",
                }, ct);
            }
        }

        private async Task<bool> IsNegativeStockAllowedAsync(CancellationToken ct)
        {
            if (_featureService == null)
                return false;

            var result = await _featureService.IsEnabledAsync(FeatureKeys.AllowNegativeStock, ct);
            return result.IsSuccess && result.Data;
        }
    }
}
