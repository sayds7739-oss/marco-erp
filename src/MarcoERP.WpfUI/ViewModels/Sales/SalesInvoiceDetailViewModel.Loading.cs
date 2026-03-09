using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.WpfUI.ViewModels.Common;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    public sealed partial class SalesInvoiceDetailViewModel
    {
        // ── Loading & Data Population ───────────────────────────

        public async Task RefreshProductsAsync()
        {
            var prodResult = await _productService.GetAllAsync();
            Products.Clear();
            if (prodResult.IsSuccess)
                foreach (var p in prodResult.Data.Where(x => x.Status == "Active"))
                    Products.Add(p);
        }

        // ── INavigationAware ────────────────────────────────────
        public async Task OnNavigatedToAsync(object parameter)
        {
            await RunDbGuardedAsync(async () =>
            {
                await LoadLookupsAsync();
                await LoadInvoiceIdsAsync();

                if (parameter is int invoiceId && invoiceId > 0)
                {
                    _currentInvoiceIndex = _invoiceIds.IndexOf(invoiceId);
                    await LoadInvoiceDetailAsync(invoiceId);
                }
                else
                {
                    await PrepareNewAsync();
                }

                UpdateNavigationState();
            });
        }

        // ── Load ────────────────────────────────────────────────
        private async Task LoadLookupsAsync()
        {
            var custResult = await _customerService.GetAllAsync();
            Customers.Clear();
            if (custResult.IsSuccess)
                foreach (var c in custResult.Data.Where(x => x.IsActive))
                    Customers.Add(c);

            var suppResult = await _supplierService.GetAllAsync();
            Suppliers.Clear();
            if (suppResult.IsSuccess)
                foreach (var s in suppResult.Data.Where(x => x.IsActive))
                    Suppliers.Add(s);

            var repResult = await _salesRepresentativeService.GetActiveAsync();
            SalesRepresentatives.Clear();
            if (repResult.IsSuccess)
                foreach (var rep in repResult.Data)
                    SalesRepresentatives.Add(rep);

            var whResult = await _warehouseService.GetAllAsync();
            Warehouses.Clear();
            if (whResult.IsSuccess)
                foreach (var w in whResult.Data.Where(x => x.IsActive))
                    Warehouses.Add(w);

            var prodResult = await _productService.GetAllAsync();
            Products.Clear();
            if (prodResult.IsSuccess)
                foreach (var p in prodResult.Data.Where(x => x.Status == "Active"))
                    Products.Add(p);

            await RefreshCustomerFinancialStatusAsync();
        }

        private async Task LoadInvoiceDetailAsync(int id)
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _invoiceService.GetByIdAsync(id);
                if (result.IsSuccess)
                {
                    CurrentInvoice = result.Data;
                    PopulateFormFromInvoice(result.Data);
                    await RefreshInvoicePaymentsAsync();
                    StatusMessage = $"فاتورة بيع «{result.Data.InvoiceNumber}»";
                    UpdateTabTitle(result.Data.InvoiceNumber, GetStatusText(result.Data.Status));
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تحميل الفاتورة", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Prepare New ─────────────────────────────────────────
        private async Task PrepareNewAsync()
        {
            IsEditing = true;
            IsNew = true;
            CurrentInvoice = null;
            ClearError();

            try
            {
                var numResult = await _invoiceService.GetNextNumberAsync();
                FormNumber = numResult.IsSuccess ? numResult.Data : "";
            }
            catch { FormNumber = ""; }

            FormDate = DateTime.Today;
            FormCustomerId = null;
            FormSalesRepresentativeId = null;
            FormWarehouseId = null;
            FormNotes = "";
            FormHeaderDiscountPercent = 0;
            FormHeaderDiscountAmount = 0;
            FormDeliveryFee = 0;
            FormInvoiceType = InvoiceType.Cash;
            FormPaymentMethod = PaymentMethod.Cash;
            FormDueDate = null;
            UnhookAllLines();
            FormLines.Clear();
            RefreshTotals();
            ResetDirtyTracking();
            StatusMessage = "إنشاء فاتورة بيع جديدة...";
        }

        private void PopulateFormFromInvoice(SalesInvoiceDto invoice)
        {
            FormNumber = invoice.InvoiceNumber;
            FormDate = invoice.InvoiceDate;
            FormCounterpartyType = invoice.CounterpartyType;
            FormCustomerId = invoice.CustomerId;
            FormSupplierId = invoice.SupplierId;
            FormSalesRepresentativeId = invoice.SalesRepresentativeId;
            FormWarehouseId = invoice.WarehouseId;
            FormNotes = invoice.Notes;
            FormHeaderDiscountPercent = invoice.HeaderDiscountPercent;
            FormHeaderDiscountAmount = invoice.HeaderDiscountAmount;
            FormDeliveryFee = invoice.DeliveryFee;
            FormInvoiceType = invoice.InvoiceType;
            FormPaymentMethod = invoice.PaymentMethod;
            FormDueDate = invoice.DueDate;

            UnhookAllLines();
            FormLines.Clear();
            foreach (var line in invoice.Lines ?? new List<SalesInvoiceLineDto>())
            {
                var formLine = new SalesInvoiceLineFormItem(this)
                {
                    Id = line.Id,
                    ProductId = line.ProductId,
                    UnitId = line.UnitId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountPercent = line.DiscountPercent
                };
                HookLine(formLine);
                FormLines.Add(formLine);
            }

            IsEditing = false;
            IsNew = false;
            RefreshTotals();
            ResetDirtyTracking();

            EnqueueDbWork(async () =>
            {
                await RefreshCustomerFinancialStatusAsync();
                await RefreshSmartEntryForAllLinesAsync();
                await LoadAttachmentsAsync();
            });
        }

        // ── Invoice Navigation ───────────────────────────────
        private async Task LoadInvoiceIdsAsync()
        {
            try
            {
                var result = await _invoiceService.GetAllAsync();
                if (result.IsSuccess)
                {
                    var list = result.Data.ToList();
                    _invoiceIds = list.Select(i => i.Id).ToList();
                    _invoiceNumberToId = list
                        .Where(i => !string.IsNullOrWhiteSpace(i.InvoiceNumber))
                        .GroupBy(i => i.InvoiceNumber)
                        .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch { /* non-critical */ }
        }

        private async Task RefreshCustomerFinancialStatusAsync()
        {
            if (!FormCustomerId.HasValue || FormCustomerId <= 0)
            {
                CustomerPreviousBalance = 0m;
                CustomerOutstandingAmount = 0m;
                CustomerHasOverdue = false;
                return;
            }

            var customer = Customers.FirstOrDefault(c => c.Id == FormCustomerId.Value);
            CustomerPreviousBalance = customer?.PreviousBalance ?? 0m;

            try
            {
                CustomerOutstandingAmount = await _smartEntryQueryService.GetCustomerOutstandingSalesBalanceAsync(FormCustomerId.Value);
            }
            catch
            {
                CustomerOutstandingAmount = 0m;
            }

            CustomerHasOverdue = false;
            if (customer?.DaysAllowed is int daysAllowed && daysAllowed > 0)
            {
                var cutoff = DateTime.Today.AddDays(-daysAllowed);
                try
                {
                    CustomerHasOverdue = await _smartEntryQueryService.HasOverduePostedSalesInvoicesAsync(FormCustomerId.Value, cutoff);
                }
                catch
                {
                    CustomerHasOverdue = false;
                }
            }
        }
    }
}
