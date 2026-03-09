using System;
using System.Linq;
using System.Threading.Tasks;
using MarcoERP.Domain.Enums;

namespace MarcoERP.WpfUI.ViewModels.Purchases
{
    public sealed partial class PurchaseInvoiceDetailViewModel
    {
        public async Task OnNavigatedToAsync(object parameter)
        {
            await RunDbGuardedAsync(async () =>
            {
                await LoadLookupsAsync();
                await LoadInvoiceIdsAsync();
                if (parameter is int id && id > 0)
                {
                    _currentInvoiceIndex = _invoiceIds.IndexOf(id);
                    await LoadDetailAsync(id);
                }
                else
                {
                    await PrepareNewAsync();
                }

                UpdateNavigationState();
            });
        }

        private async Task LoadLookupsAsync()
        {
            var suppResult = await _supplierService.GetAllAsync();
            Suppliers.Clear();
            if (suppResult.IsSuccess)
                foreach (var s in suppResult.Data.Where(x => x.IsActive))
                    Suppliers.Add(s);

            var custResult = await _customerService.GetAllAsync();
            Customers.Clear();
            if (custResult.IsSuccess)
                foreach (var c in custResult.Data.Where(x => x.IsActive))
                    Customers.Add(c);

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
        }

        private async Task LoadDetailAsync(int id)
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _invoiceService.GetByIdAsync(id);
                if (result.IsSuccess) { CurrentInvoice = result.Data; PopulateForm(result.Data); StatusMessage = $"فاتورة شراء «{result.Data.InvoiceNumber}»"; }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("تحميل الفاتورة", ex); }
            finally { IsBusy = false; }
        }

        private async Task PrepareNewAsync()
        {
            IsEditing = true; IsNew = true; CurrentInvoice = null; ClearError();
            try { var r = await _invoiceService.GetNextNumberAsync(); FormNumber = r.IsSuccess ? r.Data : ""; }
            catch { FormNumber = ""; }
            FormDate = DateTime.Today;
            FormCounterpartyType = CounterpartyType.Supplier;
            FormSupplierId = null;
            FormCounterpartyCustomerId = null;
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
            FormLines.Clear(); RefreshTotals();
            StatusMessage = "إنشاء فاتورة شراء جديدة...";
            ResetDirtyTracking();
        }
    }
}
