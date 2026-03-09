using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Navigation;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    public sealed partial class SalesInvoiceDetailViewModel
    {
        // ── Save ────────────────────────────────────────────────
        private async Task SaveAsync()
        {
            await RunDbGuardedAsync(async () =>
            {
                IsBusy = true;
                ClearError();

                // ── Pre-save validation ──
                if (FormLines.Count == 0)
                {
                    ErrorMessage = "لا يمكن حفظ فاتورة بدون بنود. أضف صنف واحد على الأقل.";
                    IsBusy = false;
                    return;
                }

                var invalidLines = FormLines.Where(l => l.ProductId <= 0 || l.Quantity <= 0 || l.UnitPrice < 0).ToList();
                if (invalidLines.Any())
                {
                    ErrorMessage = "يوجد بنود غير مكتملة (صنف أو كمية أو سعر = صفر). يرجى مراجعة البنود.";
                    IsBusy = false;
                    return;
                }

                // Duplicate products are allowed (user confirmed during add)

                var wasNew = IsNew;
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
                            HeaderDiscountPercent = FormHeaderDiscountPercent,
                            HeaderDiscountAmount = FormHeaderDiscountAmount,
                            DeliveryFee = FormDeliveryFee,
                            InvoiceType = FormInvoiceType,
                            PaymentMethod = FormPaymentMethod,
                            DueDate = FormDueDate,
                            Lines = lines
                        };

                        var result = await _invoiceService.CreateAsync(dto);
                        if (result.IsSuccess)
                        {
                            StatusMessage = $"تم إنشاء فاتورة البيع «{result.Data.InvoiceNumber}» بنجاح";
                            CurrentInvoice = result.Data;
                            PopulateFormFromInvoice(result.Data);
                            UpdateTabTitle(result.Data.InvoiceNumber, GetStatusText(result.Data.Status));
                            ResetDirtyTracking();

                            await RefreshInvoicePaymentsAsync();
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
                            HeaderDiscountPercent = FormHeaderDiscountPercent,
                            HeaderDiscountAmount = FormHeaderDiscountAmount,
                            DeliveryFee = FormDeliveryFee,
                            InvoiceType = FormInvoiceType,
                            PaymentMethod = FormPaymentMethod,
                            DueDate = FormDueDate,
                            Lines = lines
                        };

                        var result = await _invoiceService.UpdateAsync(dto);
                        if (result.IsSuccess)
                        {
                            StatusMessage = $"تم تحديث فاتورة البيع «{result.Data.InvoiceNumber}» بنجاح";
                            CurrentInvoice = result.Data;
                            PopulateFormFromInvoice(result.Data);
                            UpdateTabTitle(result.Data.InvoiceNumber, GetStatusText(result.Data.Status));

                            await RefreshInvoicePaymentsAsync();
                        }
                        else ErrorMessage = result.ErrorMessage;
                    }
                }
                catch (ConcurrencyConflictException)
                {
                    ErrorMessage = "حدث تعارض في البيانات. يرجى إعادة تحميل الفاتورة.";
                }
                catch (Exception ex)
                {
                    ErrorMessage = FriendlyErrorMessage("حفظ الفاتورة", ex);
                }
                finally { IsBusy = false; }
            });
        }

        private async Task RefreshInvoicePaymentsAsync()
        {
            if (CurrentInvoice?.Id > 0)
            {
                PaidAmount = await _invoiceTreasuryIntegrationService.GetPostedPaidForSalesInvoiceAsync(CurrentInvoice.Id);
            }
            else
            {
                PaidAmount = 0m;
            }

            OnPropertyChanged(nameof(BalanceAmount));
            OnPropertyChanged(nameof(RemainingAmount));
        }

        private async Task PromptCreateReceiptFromInvoiceAsync(SalesInvoiceDto invoice)
        {
            if (invoice == null) return;

            var customer = Customers.FirstOrDefault(c => c.Id == invoice.CustomerId);
            if (customer?.AccountId is not int customerAccountId || customerAccountId <= 0)
            {
                _dialog.ShowWarning(
                    "لا يمكن إنشاء سند قبض تلقائياً لأن حساب العميل غير محدد.\nيمكنك إنشاء السند يدوياً من شاشة سندات القبض.",
                    "تنبيه");

                _navigationService.NavigateTo(
                    "CashReceipts",
                    new CashReceiptNavigationParams
                    {
                        SalesInvoiceId = invoice.Id,
                        CustomerId = invoice.CustomerId,
                        Date = invoice.InvoiceDate,
                        Amount = invoice.NetTotal,
                        Description = $"تحصيل فاتورة بيع {invoice.InvoiceNumber}",
                        Notes = invoice.Notes
                    });
                return;
            }

            var createResult = await _invoiceTreasuryIntegrationService.PromptAndCreateSalesReceiptAsync(invoice, customerAccountId);
            if (createResult.Created)
            {
                StatusMessage = $"تم إنشاء سند قبض مرتبط بالفاتورة «{invoice.InvoiceNumber}» بنجاح";
                await RefreshInvoicePaymentsAsync();
            }
            else if (!string.IsNullOrWhiteSpace(createResult.ErrorMessage))
            {
                ErrorMessage = createResult.ErrorMessage;
            }
            // Note: PromptAndCreateSalesReceiptAsync already handles the user prompt internally.
            // No secondary prompt needed — removed to prevent double-prompting.
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
                    CurrentInvoice = result.Data;
                    PopulateFormFromInvoice(result.Data);
                    await PromptCreateReceiptFromInvoiceAsync(result.Data);
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("ترحيل الفاتورة", ex); }
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
                    await LoadInvoiceDetailAsync(CurrentInvoice.Id);
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("إلغاء الفاتورة", ex); }
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
                    NavigateBack();
                }
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("حذف المسودة", ex); }
            finally { IsBusy = false; }
        }

        private void StartEditing()
        {
            if (CurrentInvoice == null || !IsDraft) return;
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
                NavigateBack();
            StatusMessage = "تم الإلغاء";
            ResetDirtyTracking();
        }

        private void NavigateBack()
        {
            _navigationService.NavigateTo("SalesInvoices");
        }
    }
}
