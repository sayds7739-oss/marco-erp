using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Common;

namespace MarcoERP.WpfUI.ViewModels.Purchases
{
    public sealed partial class PurchaseInvoiceViewModel
    {
        // ── Add / Remove Lines ──────────────────────────────────
        private void AddLine(object _)
        {
            if (!IsEditing && !IsNew) return;
            FormLines.Add(new PurchaseInvoiceLineFormItem(this));
            RefreshTotals();
        }

        private void RemoveLine(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is PurchaseInvoiceLineFormItem line && FormLines.Count > 1)
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
                var lines = FormLines.Select(l => new CreatePurchaseInvoiceLineDto
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
                    var dto = new CreatePurchaseInvoiceDto
                    {
                        InvoiceDate = FormDate,
                        SupplierId = FormSupplierId,
                        WarehouseId = FormWarehouseId ?? 0,
                        Notes = FormNotes?.Trim(),
                        SalesRepresentativeId = FormSalesRepresentativeId,
                        CounterpartyType = FormCounterpartyType,
                        CounterpartyCustomerId = FormCounterpartyCustomerId,
                        InvoiceType = FormInvoiceType,
                        PaymentMethod = FormPaymentMethod,
                        DueDate = FormDueDate,
                        Lines = lines
                    };

                    var result = await _invoiceService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء فاتورة الشراء «{result.Data.InvoiceNumber}» بنجاح";
                        IsEditing = false;
                        IsNew = false;
                        await LoadInvoicesAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else
                {
                    var dto = new UpdatePurchaseInvoiceDto
                    {
                        Id = CurrentInvoice.Id,
                        InvoiceDate = FormDate,
                        SupplierId = FormSupplierId,
                        WarehouseId = FormWarehouseId ?? 0,
                        Notes = FormNotes?.Trim(),
                        SalesRepresentativeId = FormSalesRepresentativeId,
                        CounterpartyType = FormCounterpartyType,
                        CounterpartyCustomerId = FormCounterpartyCustomerId,
                        InvoiceType = FormInvoiceType,
                        PaymentMethod = FormPaymentMethod,
                        DueDate = FormDueDate,
                        Lines = lines
                    };

                    var result = await _invoiceService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم تحديث فاتورة الشراء «{result.Data.InvoiceNumber}» بنجاح";
                        IsEditing = false;
                        IsNew = false;
                        await LoadInvoicesAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
            }
            catch (ConcurrencyConflictException ex)
            {
                await ConcurrencyHelper.ShowConflictAndRefreshAsync(ex, LoadInvoicesAsync);
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("الحفظ", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Post ────────────────────────────────────────────────
        private async Task PostAsync()
        {
            if (CurrentInvoice == null) return;

            if (!_dialog.Confirm($"هل تريد ترحيل فاتورة الشراء «{CurrentInvoice.InvoiceNumber}»؟\nبعد الترحيل لا يمكن التعديل وسيتم إنشاء قيد محاسبي تلقائي.", "تأكيد الترحيل")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _invoiceService.PostAsync(CurrentInvoice.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم ترحيل فاتورة الشراء «{result.Data.InvoiceNumber}» — قيد رقم: {result.Data.JournalEntryId}";
                    await LoadInvoicesAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("الترحيل", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Cancel Invoice ──────────────────────────────────────
        private async Task CancelInvoiceAsync()
        {
            if (CurrentInvoice == null) return;

            if (!_dialog.Confirm($"هل تريد إلغاء فاتورة الشراء «{CurrentInvoice.InvoiceNumber}»؟\nسيتم إنشاء قيد عكسي وإعادة الكميات.", "تأكيد الإلغاء")) return;

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
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("الإلغاء", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Delete Draft ────────────────────────────────────────
        private async Task DeleteDraftAsync()
        {
            if (CurrentInvoice == null) return;

            if (!_dialog.Confirm($"هل تريد حذف مسودة الفاتورة «{CurrentInvoice.InvoiceNumber}»؟", "تأكيد الحذف")) return;

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
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("الحذف", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Edit Selected ───────────────────────────────────────
        public void EditSelected()
        {
            if (CurrentInvoice == null || !IsDraft) return;
            PopulateFormFromInvoice(CurrentInvoice);
            IsEditing = true;
            IsNew = false;
        }

        // ── Cancel Editing ──────────────────────────────────────
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

        // ── Helpers ─────────────────────────────────────────────
        private void PopulateFormFromInvoice(PurchaseInvoiceDto invoice)
        {
            FormNumber = invoice.InvoiceNumber;
            FormDate = invoice.InvoiceDate;
            FormCounterpartyType = invoice.CounterpartyType;
            FormSupplierId = invoice.SupplierId;
            FormCounterpartyCustomerId = invoice.CounterpartyCustomerId;
            FormSalesRepresentativeId = invoice.SalesRepresentativeId;
            FormWarehouseId = invoice.WarehouseId;
            FormNotes = invoice.Notes;
            FormInvoiceType = invoice.InvoiceType;
            FormPaymentMethod = invoice.PaymentMethod;
            FormDueDate = invoice.DueDate;

            FormLines.Clear();
            foreach (var line in invoice.Lines ?? new List<PurchaseInvoiceLineDto>())
            {
                FormLines.Add(new PurchaseInvoiceLineFormItem(this)
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
            FormCounterpartyType = CounterpartyType.Supplier;
            FormSupplierId = null;
            FormCounterpartyCustomerId = null;
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
