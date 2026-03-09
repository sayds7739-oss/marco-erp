using System;
using System.Linq;
using System.Threading.Tasks;
using MarcoERP.Domain.Enums;
using MarcoERP.WpfUI.Common;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    public sealed partial class SalesInvoiceViewModel
    {
        // ── Load ────────────────────────────────────────────────
        public async Task LoadInvoicesAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                await LoadLookupsAsync();

                var result = await _invoiceService.GetAllAsync();
                _allInvoices.Clear();
                Invoices.Clear();
                if (result.IsSuccess)
                {
                    _allInvoices = result.Data.ToList();
                    ApplyFilter();
                }

                StatusMessage = $"تم تحميل {Invoices.Count} فاتورة بيع";
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("التحميل", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadLookupsAsync()
        {
            var custTask = _customerService.GetAllAsync();
            var suppTask = _supplierService.GetAllAsync();
            var repTask = _salesRepresentativeService.GetActiveAsync();
            var whTask = _warehouseService.GetAllAsync();
            var prodTask = _productService.GetAllAsync();

            await Task.WhenAll(custTask, suppTask, repTask, whTask, prodTask);

            var custResult = await custTask;
            Customers.Clear();
            if (custResult.IsSuccess)
                foreach (var c in custResult.Data.Where(x => x.IsActive))
                    Customers.Add(c);

            var suppResult = await suppTask;
            Suppliers.Clear();
            if (suppResult.IsSuccess)
                foreach (var s in suppResult.Data.Where(x => x.IsActive))
                    Suppliers.Add(s);

            var repResult = await repTask;
            SalesRepresentatives.Clear();
            if (repResult.IsSuccess)
                foreach (var rep in repResult.Data)
                    SalesRepresentatives.Add(rep);

            var whResult = await whTask;
            Warehouses.Clear();
            if (whResult.IsSuccess)
                foreach (var w in whResult.Data.Where(x => x.IsActive))
                    Warehouses.Add(w);

            var prodResult = await prodTask;
            Products.Clear();
            if (prodResult.IsSuccess)
                foreach (var p in prodResult.Data.Where(x => x.Status == "Active"))
                    Products.Add(p);
        }

        private void ApplyDefaultWarehouse()
        {
            if (FormWarehouseId.HasValue && FormWarehouseId.Value > 0)
                return;

            if (Warehouses.Count == 1)
            {
                FormWarehouseId = Warehouses[0].Id;
                return;
            }

            var lastId = SessionSelections.LastWarehouseId;
            if (lastId.HasValue && Warehouses.Any(w => w.Id == lastId.Value))
                FormWarehouseId = lastId.Value;
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
            FormCounterpartyType = CounterpartyType.Customer;
            FormCustomerId = null;
            FormSupplierId = null;
            FormSalesRepresentativeId = null;
            FormWarehouseId = null;
            FormNotes = "";
            ApplyDefaultWarehouse();
            FormLines.Clear();
            AddLine(null);
            RefreshTotals();
            StatusMessage = "إنشاء فاتورة بيع جديدة...";
        }
    }
}
