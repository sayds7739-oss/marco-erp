using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Common;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    public sealed partial class SalesInvoiceViewModel
    {
        private void AddLine(object _)
        {
            if (!IsEditing && !IsNew) return;
            FormLines.Add(new SalesInvoiceLineFormItem(this));
            RefreshTotals();
        }

        private void RemoveLine(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is SalesInvoiceLineFormItem line && FormLines.Count > 1)
            {
                FormLines.Remove(line);
                RefreshTotals();
            }
        }

        // ── Save ────────────────────────────────────────────────
        private async Task SaveAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var lines = FormLines.Select(l => new CreateSalesInvoiceLineDto
                {
                    Id = l.Id,
                    ProductId = l.ProductId,
                    UnitId = l.UnitId,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    DiscountPercent = l.DiscountPercent
                }).ToList();

                if (IsNew)
                {
                    var dto = new CreateSalesInvoiceDto
                    {
                        InvoiceDate = FormDate,
                        CustomerId = FormCustomerId ?? 0,
                        WarehouseId = FormWarehouseId ?? 0,
                        Notes = FormNotes?.Trim(),
                        SalesRepresentativeId = FormSalesRepresentativeId,
                        CounterpartyType = FormCounterpartyType,
                        SupplierId = FormSupplierId,
                        InvoiceType = FormInvoiceType,
                        PaymentMethod = FormPaymentMethod,
                        DueDate = FormDueDate,
                        Lines = lines
                    };

                    var result = await _invoiceService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء فاتورة البيع «{result.Data.InvoiceNumber}» بنجاح";
                        IsEditing = false;
                        IsNew = false;
                        await LoadInvoicesAsync();
                    }
                    else ErrorMessage = result.ErrorMessage;
                }
                else
                {
                    var dto = new UpdateSalesInvoiceDto
                    {
                        Id = CurrentInvoice.Id,
                        InvoiceDate = FormDate,
                        CustomerId = FormCustomerId ?? 0,
                        WarehouseId = FormWarehouseId ?? 0,
                        Notes = FormNotes?.Trim(),
                        SalesRepresentativeId = FormSalesRepresentativeId,
                        CounterpartyType = FormCounterpartyType,
                        SupplierId = FormSupplierId,
                        InvoiceType = FormInvoiceType,
                        PaymentMethod = FormPaymentMethod,
                        DueDate = FormDueDate,
                        Lines = lines
                    };

                    var result = await _invoiceService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث فاتورة البيع «{result.Data.InvoiceNumber}» بنجاح";
                        IsEditing = false;
                        IsNew = false;
                        await LoadInvoicesAsync();
                    }
                    else ErrorMessage = result.ErrorMessage;
                }
            }
            catch (ConcurrencyConflictException ex)
            {
                await ConcurrencyHelper.ShowConflictAndRefreshAsync(ex, LoadInvoicesAsync);
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الحفظ", ex); }
            finally { IsBusy = false; }
        }

        private async Task PostAsync()
        {
            if (CurrentInvoice == null) return;
            if (!_dialog.Confirm(
                $"هل تريد ترحيل فاتورة البيع «{CurrentInvoice.InvoiceNumber}»؟\nبعد الترحيل لا يمكن التعديل وسيتم إنشاء قيود محاسبية تلقائية.",
                "تأكيد الترحيل")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _invoiceService.PostAsync(CurrentInvoice.Id);
                if (result.IsSuccess)
                {
                    var baseMessage = $"تم ترحيل فاتورة البيع «{result.Data.InvoiceNumber}» بنجاح";
                    StatusMessage = string.IsNullOrWhiteSpace(result.Data.WarningMessage)
                        ? baseMessage
                        : $"{baseMessage}  |  ⚠ {result.Data.WarningMessage}";
                    await LoadInvoicesAsync();
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الترحيل", ex); }
            finally { IsBusy = false; }
        }

        private async Task CancelInvoiceAsync()
        {
            if (CurrentInvoice == null) return;
            if (!_dialog.Confirm(
                $"هل تريد إلغاء فاتورة البيع «{CurrentInvoice.InvoiceNumber}»؟\nسيتم إنشاء قيد عكسي وإعادة الكميات.",
                "تأكيد الإلغاء")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _invoiceService.CancelAsync(CurrentInvoice.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم إلغاء الفاتورة بنجاح";
                    await LoadInvoicesAsync();
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الإلغاء", ex); }
            finally { IsBusy = false; }
        }

        private async Task DeleteDraftAsync()
        {
            if (CurrentInvoice == null) return;
            if (!_dialog.Confirm(
                $"هل تريد حذف مسودة الفاتورة «{CurrentInvoice.InvoiceNumber}»؟",
                "تأكيد الحذف")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _invoiceService.DeleteDraftAsync(CurrentInvoice.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم حذف المسودة بنجاح";
                    CurrentInvoice = null;
                    ClearForm();
                    await LoadInvoicesAsync();
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الحذف", ex); }
            finally { IsBusy = false; }
        }

        public void EditSelected()
        {
            if (CurrentInvoice == null || !IsDraft) return;
            PopulateFormFromInvoice(CurrentInvoice);
            IsEditing = true;
            IsNew = false;
        }

        private void CancelEditing(object _)
        {
            IsEditing = false;
            IsNew = false;
            ClearError();
            if (CurrentInvoice != null)
                PopulateFormFromInvoice(CurrentInvoice);
            else
                ClearForm();
            StatusMessage = "تم الإلغاء";
        }

        private void PopulateFormFromInvoice(SalesInvoiceDto invoice)
        {
            FormNumber = invoice.InvoiceNumber;
            FormDate = invoice.InvoiceDate;
            FormCounterpartyType = invoice.CounterpartyType;
            FormCustomerId = invoice.CounterpartyType == CounterpartyType.Customer ? invoice.CustomerId : null;
            FormSupplierId = invoice.CounterpartyType == CounterpartyType.Supplier ? invoice.SupplierId : null;
            FormSalesRepresentativeId = invoice.SalesRepresentativeId;
            FormWarehouseId = invoice.WarehouseId;
            FormNotes = invoice.Notes;
            FormInvoiceType = invoice.InvoiceType;
            FormPaymentMethod = invoice.PaymentMethod;
            FormDueDate = invoice.DueDate;

            FormLines.Clear();
            foreach (var line in invoice.Lines ?? new List<SalesInvoiceLineDto>())
            {
                FormLines.Add(new SalesInvoiceLineFormItem(this)
                {
                    Id = line.Id,
                    ProductId = line.ProductId,
                    UnitId = line.UnitId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountPercent = line.DiscountPercent
                });
            }

            IsEditing = false;
            IsNew = false;
            RefreshTotals();
        }

        private void ClearForm()
        {
            FormNumber = "";
            FormDate = DateTime.Today;
            FormCounterpartyType = CounterpartyType.Customer;
            FormCustomerId = null;
            FormSupplierId = null;
            FormSalesRepresentativeId = null;
            FormWarehouseId = null;
            FormNotes = "";
            FormInvoiceType = InvoiceType.Cash;
            FormPaymentMethod = PaymentMethod.Cash;
            FormDueDate = null;
            FormLines.Clear();
            RefreshTotals();
        }
    }
}
