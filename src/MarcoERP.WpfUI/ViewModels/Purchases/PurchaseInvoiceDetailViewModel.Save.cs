using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Navigation;

namespace MarcoERP.WpfUI.ViewModels.Purchases
{
    public sealed partial class PurchaseInvoiceDetailViewModel
    {
        private void AddLine(object _)
        {
            if (!IsEditing && !IsNew) return;
            OpenAddLinePopup();
        }

        private void RemoveLine(object parameter)
        {
            if (!IsEditing && !IsNew) return;
            if (parameter is PurchaseInvoiceLineFormItem line)
            {
                UnhookLine(line);
                FormLines.Remove(line);
                RefreshTotals();
                MarkDirty();
            }
        }

        private void HookLine(PurchaseInvoiceLineFormItem line)
        {
            if (line == null) return;
            if (_smartRefreshVersions.ContainsKey(line))
                return;

            _smartRefreshVersions[line] = 0;
            line.PropertyChanged += LineOnPropertyChanged;
        }

        private void UnhookLine(PurchaseInvoiceLineFormItem line)
        {
            if (line == null) return;
            if (_smartRefreshVersions.ContainsKey(line))
                _smartRefreshVersions.Remove(line);
            line.PropertyChanged -= LineOnPropertyChanged;
        }

        private void UnhookAllLines()
        {
            foreach (var line in FormLines)
                UnhookLine(line);
        }

        private void LineOnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is not PurchaseInvoiceLineFormItem line)
                return;

            if (IsUserEditableLineProperty(e.PropertyName))
                MarkDirty();

            if (e.PropertyName == nameof(PurchaseInvoiceLineFormItem.ProductId) || e.PropertyName == nameof(PurchaseInvoiceLineFormItem.UnitId))
                EnqueueDbWork(() => RefreshSmartEntryForLineAsync(line));
        }

        private static bool IsUserEditableLineProperty(string propertyName)
        {
            return propertyName == nameof(PurchaseInvoiceLineFormItem.ProductId)
                   || propertyName == nameof(PurchaseInvoiceLineFormItem.UnitId)
                   || propertyName == nameof(PurchaseInvoiceLineFormItem.Quantity)
                   || propertyName == nameof(PurchaseInvoiceLineFormItem.UnitPrice)
                   || propertyName == nameof(PurchaseInvoiceLineFormItem.DiscountPercent);
        }

        private async Task RefreshSmartEntryForAllLinesAsync()
        {
            foreach (var line in FormLines.ToList())
                await RefreshSmartEntryForLineAsync(line);
        }

        private async Task RefreshSmartEntryForLineAsync(PurchaseInvoiceLineFormItem line)
        {
            if (line == null) return;
            if (line.ProductId <= 0 || line.UnitId <= 0) return;
            if (!FormWarehouseId.HasValue || FormWarehouseId <= 0) return;

            if (!_smartRefreshVersions.TryGetValue(line, out var version))
                _smartRefreshVersions[line] = 0;
            version = ++_smartRefreshVersions[line];

            var warehouseId = FormWarehouseId.Value;
            var supplierId = FormSupplierId ?? 0;

            try
            {
                var stockBase = await _smartEntryQueryService.GetStockBaseQtyAsync(warehouseId, line.ProductId);
                var lastPurchase = supplierId > 0
                    ? await _smartEntryQueryService.GetLastPurchaseUnitPriceForSupplierAsync(supplierId, line.ProductId, line.UnitId)
                    : await _smartEntryQueryService.GetLastPurchaseUnitPriceAsync(line.ProductId, line.UnitId);

                if (!_smartRefreshVersions.TryGetValue(line, out var current) || current != version)
                    return;

                line.SetSmartEntry(stockBase, lastPurchase);

                if (lastPurchase.HasValue && line.IsUnitPriceAtMasterDefault())
                    line.UnitPrice = lastPurchase.Value;
            }
            catch
            {
                // Non-critical.
            }
        }

        private async Task SaveAsync()
        {
            await RunDbGuardedAsync(async () =>
            {
                IsBusy = true; ClearError();

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

                try
                {
                    var lines = FormLines.Select(l => new CreatePurchaseInvoiceLineDto
                    { Id = l.Id, ProductId = l.ProductId, UnitId = l.UnitId, Quantity = l.Quantity, UnitPrice = l.UnitPrice, DiscountPercent = l.DiscountPercent }).ToList();

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
                            StatusMessage = $"تم إنشاء فاتورة الشراء «{result.Data.InvoiceNumber}» بنجاح";
                            CurrentInvoice = result.Data;
                            PopulateForm(result.Data);

                            await RefreshInvoicePaymentsAsync();
                        }
                        else ErrorMessage = result.ErrorMessage;
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
                            HeaderDiscountPercent = FormHeaderDiscountPercent,
                            HeaderDiscountAmount = FormHeaderDiscountAmount,
                            DeliveryFee = FormDeliveryFee,
                            InvoiceType = FormInvoiceType,
                            PaymentMethod = FormPaymentMethod,
                            DueDate = FormDueDate,
                            Lines = lines
                        };
                        var result = await _invoiceService.UpdateAsync(dto);
                        if (result.IsSuccess) { StatusMessage = $"تم تحديث فاتورة الشراء «{result.Data.InvoiceNumber}» بنجاح"; CurrentInvoice = result.Data; PopulateForm(result.Data); }
                        else ErrorMessage = result.ErrorMessage;

                        await RefreshInvoicePaymentsAsync();
                    }
                }
                catch (ConcurrencyConflictException) { ErrorMessage = "حدث تعارض في البيانات. يرجى إعادة تحميل الفاتورة."; }
                catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("حفظ الفاتورة", ex); }
                finally { IsBusy = false; }
            });
        }

        private async Task RefreshInvoicePaymentsAsync()
        {
            if (CurrentInvoice?.Id > 0)
            {
                PaidAmount = await _invoiceTreasuryIntegrationService.GetPostedPaidForPurchaseInvoiceAsync(CurrentInvoice.Id);
            }
            else
            {
                PaidAmount = 0m;
            }

            OnPropertyChanged(nameof(BalanceAmount));
            OnPropertyChanged(nameof(RemainingAmount));
        }

        private async Task PromptCreatePaymentFromInvoiceAsync(PurchaseInvoiceDto invoice)
        {
            if (invoice == null) return;

            if (!invoice.SupplierId.HasValue || invoice.SupplierId <= 0)
            {
                _dialog.ShowWarning("لا يمكن إنشاء سند صرف تلقائياً لأن المورد غير محدد للفاتورة.", "تنبيه");
                return;
            }

            var supplier = Suppliers.FirstOrDefault(s => s.Id == invoice.SupplierId.Value);
            if (supplier?.AccountId is not int supplierAccountId || supplierAccountId <= 0)
            {
                _dialog.ShowWarning("لا يمكن إنشاء سند صرف تلقائياً لأن حساب المورد غير محدد.\nيمكنك إنشاء السند يدوياً من شاشة سندات الصرف.", "تنبيه");

                _navigationService.NavigateTo(
                    "CashPayments",
                    new CashPaymentNavigationParams
                    {
                        PurchaseInvoiceId = invoice.Id,
                        SupplierId = invoice.SupplierId,
                        Date = invoice.InvoiceDate,
                        Amount = invoice.NetTotal,
                        Description = $"سداد فاتورة شراء {invoice.InvoiceNumber}",
                        Notes = invoice.Notes
                    });
                return;
            }

            var createResult = await _invoiceTreasuryIntegrationService.PromptAndCreatePurchasePaymentAsync(invoice, supplierAccountId);
            if (createResult.Created)
            {
                StatusMessage = $"تم إنشاء سند صرف مرتبط بالفاتورة «{invoice.InvoiceNumber}» بنجاح";
                await RefreshInvoicePaymentsAsync();
            }
            else if (!string.IsNullOrWhiteSpace(createResult.ErrorMessage))
            {
                ErrorMessage = createResult.ErrorMessage;
            }
        }

        private async Task PostAsync()
        {
            if (CurrentInvoice == null) return;
            if (!_dialog.Confirm($"هل تريد ترحيل فاتورة الشراء «{CurrentInvoice.InvoiceNumber}»؟\nبعد الترحيل لا يمكن التعديل.", "تأكيد الترحيل")) return;
            IsBusy = true; ClearError();
            try { var r = await _invoiceService.PostAsync(CurrentInvoice.Id); if (r.IsSuccess) { StatusMessage = $"تم ترحيل فاتورة الشراء «{r.Data.InvoiceNumber}»"; CurrentInvoice = r.Data; PopulateForm(r.Data); await PromptCreatePaymentFromInvoiceAsync(r.Data); } else ErrorMessage = r.ErrorMessage; }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("ترحيل الفاتورة", ex); }
            finally { IsBusy = false; }
        }

        /// <summary>Serializes DB access within this ViewModel.</summary>
        private async Task RunDbGuardedAsync(Func<Task> work)
        {
            await DbGuard.WaitAsync().ConfigureAwait(false);
            try
            {
                await work().ConfigureAwait(false);
            }
            finally
            {
                DbGuard.Release();
            }
        }

        private async Task CancelInvoiceAsync()
        {
            if (CurrentInvoice == null) return;
            if (!_dialog.Confirm($"هل تريد إلغاء فاتورة الشراء «{CurrentInvoice.InvoiceNumber}»؟", "تأكيد الإلغاء")) return;
            IsBusy = true; ClearError();
            try { var r = await _invoiceService.CancelAsync(CurrentInvoice.Id); if (r.IsSuccess) { StatusMessage = "تم إلغاء الفاتورة"; await LoadDetailAsync(CurrentInvoice.Id); } else ErrorMessage = r.ErrorMessage; }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("إلغاء الفاتورة", ex); }
            finally { IsBusy = false; }
        }

        private async Task DeleteDraftAsync()
        {
            if (CurrentInvoice == null) return;
            if (!_dialog.Confirm($"هل تريد حذف مسودة الفاتورة «{CurrentInvoice.InvoiceNumber}»؟", "تأكيد الحذف")) return;
            IsBusy = true; ClearError();
            try { var r = await _invoiceService.DeleteDraftAsync(CurrentInvoice.Id); if (r.IsSuccess) { StatusMessage = "تم حذف المسودة"; NavigateBack(); } else ErrorMessage = r.ErrorMessage; }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("حذف المسودة", ex); }
            finally { IsBusy = false; }
        }

        private void StartEditing()
        {
            if (CurrentInvoice != null && IsDraft)
            {
                IsEditing = true;
                IsNew = false;
            }
        }

        private void CancelEditing(object _)
        {
            IsEditing = false;
            IsNew = false;
            ClearError();

            if (CurrentInvoice != null)
                PopulateForm(CurrentInvoice);
            else
                NavigateBack();

            ResetDirtyTracking();
        }
        private void NavigateBack() => _navigationService.NavigateTo("PurchaseInvoices");

        private void PopulateForm(PurchaseInvoiceDto inv)
        {
            FormNumber = inv.InvoiceNumber;
            FormDate = inv.InvoiceDate;
            FormCounterpartyType = inv.CounterpartyType;
            FormSupplierId = inv.SupplierId;
            FormCounterpartyCustomerId = inv.CounterpartyCustomerId;
            FormSalesRepresentativeId = inv.SalesRepresentativeId;
            FormWarehouseId = inv.WarehouseId;
            FormNotes = inv.Notes;
            FormHeaderDiscountPercent = inv.HeaderDiscountPercent;
            FormHeaderDiscountAmount = inv.HeaderDiscountAmount;
            FormDeliveryFee = inv.DeliveryFee;
            FormInvoiceType = inv.InvoiceType;
            FormPaymentMethod = inv.PaymentMethod;
            FormDueDate = inv.DueDate;
            UnhookAllLines();
            FormLines.Clear();
            foreach (var line in inv.Lines ?? new List<PurchaseInvoiceLineDto>())
            {
                var formLine = new PurchaseInvoiceLineFormItem(this) { Id = line.Id, ProductId = line.ProductId, UnitId = line.UnitId, Quantity = line.Quantity, UnitPrice = line.UnitPrice, DiscountPercent = line.DiscountPercent };
                HookLine(formLine);
                FormLines.Add(formLine);
            }
            IsEditing = false; IsNew = false; RefreshTotals();
            ResetDirtyTracking();

            EnqueueDbWork(async () =>
            {
                await RefreshSmartEntryForAllLinesAsync();
                await LoadAttachmentsAsync();
            });
        }
    }
}
