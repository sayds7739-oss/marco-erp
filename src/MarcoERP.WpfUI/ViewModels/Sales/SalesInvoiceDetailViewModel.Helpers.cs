using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MarcoERP.Application.DTOs.Common;
using MarcoERP.WpfUI.Common;
using MarcoERP.WpfUI.Navigation;
using MarcoERP.WpfUI.Views.Common;

namespace MarcoERP.WpfUI.ViewModels.Sales
{
    public sealed partial class SalesInvoiceDetailViewModel
    {
        // ── Calculation Helpers ──────────────────────────────────

        public void RefreshTotals()
        {
            var totals = CalculateTotals(FormLines.Select(l => l.GetCalculationRequest()));
            TotalSubtotal = totals.Subtotal;

            // Apply header-level discount via Application layer service
            var header = _lineCalculationService.ApplyHeaderDiscount(
                totals, FormHeaderDiscountPercent, FormHeaderDiscountAmount, FormDeliveryFee);
            TotalDiscount = header.TotalDiscount;
            TotalVat = header.VatTotal;
            TotalNet = header.NetTotal;

            OnPropertyChanged(nameof(BalanceAmount));
            OnPropertyChanged(nameof(RemainingAmount));
            OnPropertyChanged(nameof(CanSave));
        }

        public LineCalculationResult CalculateLine(LineCalculationRequest request)
        {
            return _lineCalculationService.CalculateLine(request);
        }

        public InvoiceTotalsResult CalculateTotals(IEnumerable<LineCalculationRequest> lines)
        {
            return _lineCalculationService.CalculateTotals(lines);
        }

        public decimal ConvertQuantity(decimal quantity, decimal factor)
        {
            return _lineCalculationService.ConvertQuantity(quantity, factor);
        }

        public decimal ConvertPrice(decimal price, decimal factor)
        {
            return _lineCalculationService.ConvertPrice(price, factor);
        }

        // ── Invoice Navigation ──────────────────────────────────

        private async Task GoToNextAsync()
        {
            if (!await DirtyStateGuard.ConfirmContinueAsync(this))
                return;
            if (!CanGoNext) return;
            _currentInvoiceIndex++;
            await LoadInvoiceDetailAsync(_invoiceIds[_currentInvoiceIndex]);
            UpdateNavigationState();
        }

        private async Task GoToPreviousAsync()
        {
            if (!await DirtyStateGuard.ConfirmContinueAsync(this))
                return;
            if (!CanGoPrevious) return;
            _currentInvoiceIndex--;
            await LoadInvoiceDetailAsync(_invoiceIds[_currentInvoiceIndex]);
            UpdateNavigationState();
        }

        private async Task JumpToInvoiceAsync()
        {
            if (!await DirtyStateGuard.ConfirmContinueAsync(this))
                return;

            if (string.IsNullOrWhiteSpace(JumpInvoiceNumber))
                return;

            if (!_invoiceNumberToId.TryGetValue(JumpInvoiceNumber.Trim(), out var id))
            {
                _dialog.ShowInfo("رقم الفاتورة غير موجود.", "تنقل الفواتير");
                return;
            }

            _currentInvoiceIndex = _invoiceIds.IndexOf(id);
            await LoadInvoiceDetailAsync(id);
            UpdateNavigationState();
        }

        private void UpdateNavigationState()
        {
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(NavigationPositionText));
            RelayCommand.RaiseCanExecuteChanged();
        }

        // ── Print / Export ──────────────────────────────────────

        private async Task ViewPdfAsync()
        {
            if (CurrentInvoice == null) return;
            await _invoicePdfPreviewService.ShowSalesInvoiceAsync(CurrentInvoice);
        }

        private async Task ExportToExcelAsync()
        {
            if (CurrentInvoice == null) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"فاتورة_{CurrentInvoice.InvoiceNumber}_{CurrentInvoice.InvoiceDate:yyyy-MM-dd}.xlsx",
                Title = "تصدير الفاتورة إلى Excel"
            };

            if (dlg.ShowDialog() != true) return;

            await Task.Run(() =>
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var ws = workbook.Worksheets.Add("فاتورة مبيعات");
                ws.RightToLeft = true;

                // ── Header Section ──
                var headerFont = ws.Style.Font;

                ws.Cell(1, 1).Value = "فاتورة مبيعات";
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(1, 1).Style.Font.FontSize = 16;
                ws.Range(1, 1, 1, 8).Merge();
                ws.Cell(1, 1).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;

                ws.Cell(3, 1).Value = "رقم الفاتورة:";
                ws.Cell(3, 1).Style.Font.Bold = true;
                ws.Cell(3, 2).Value = CurrentInvoice.InvoiceNumber;

                ws.Cell(3, 4).Value = "التاريخ:";
                ws.Cell(3, 4).Style.Font.Bold = true;
                ws.Cell(3, 5).Value = CurrentInvoice.InvoiceDate.ToString("yyyy-MM-dd");

                ws.Cell(4, 1).Value = "العميل:";
                ws.Cell(4, 1).Style.Font.Bold = true;
                ws.Cell(4, 2).Value = CurrentInvoice.CustomerNameAr ?? CurrentInvoice.SupplierNameAr ?? "";

                ws.Cell(4, 4).Value = "المستودع:";
                ws.Cell(4, 4).Style.Font.Bold = true;
                ws.Cell(4, 5).Value = CurrentInvoice.WarehouseNameAr ?? "";

                ws.Cell(5, 1).Value = "الحالة:";
                ws.Cell(5, 1).Style.Font.Bold = true;
                ws.Cell(5, 2).Value = CurrentInvoice.Status;

                // ── Lines Table Header ──
                int headerRow = 7;
                var headers = new[] { "#", "كود الصنف", "اسم الصنف", "الوحدة", "الكمية", "سعر الوحدة", "خصم %", "مبلغ الخصم", "الصافي", "نسبة الضريبة %", "مبلغ الضريبة", "الإجمالي" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(headerRow, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(0x1565C0);
                    cell.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                    cell.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                    cell.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                }

                // ── Lines Data ──
                int dataRow = headerRow + 1;
                int lineNum = 1;
                foreach (var line in CurrentInvoice.Lines)
                {
                    ws.Cell(dataRow, 1).Value = lineNum;
                    ws.Cell(dataRow, 2).Value = line.ProductCode ?? "";
                    ws.Cell(dataRow, 3).Value = line.ProductNameAr ?? "";
                    ws.Cell(dataRow, 4).Value = line.UnitNameAr ?? "";
                    ws.Cell(dataRow, 5).Value = line.Quantity;
                    ws.Cell(dataRow, 6).Value = line.UnitPrice;
                    ws.Cell(dataRow, 7).Value = line.DiscountPercent;
                    ws.Cell(dataRow, 8).Value = line.DiscountAmount;
                    ws.Cell(dataRow, 9).Value = line.NetTotal;
                    ws.Cell(dataRow, 10).Value = line.VatRate;
                    ws.Cell(dataRow, 11).Value = line.VatAmount;
                    ws.Cell(dataRow, 12).Value = line.TotalWithVat;

                    // Alternate row color
                    if (lineNum % 2 == 0)
                    {
                        ws.Range(dataRow, 1, dataRow, 12).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(0xF5F5F5);
                    }

                    // Borders
                    ws.Range(dataRow, 1, dataRow, 12).Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                    ws.Range(dataRow, 1, dataRow, 12).Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;

                    // Number formatting
                    ws.Cell(dataRow, 5).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(dataRow, 6).Style.NumberFormat.Format = "#,##0.0000";
                    ws.Cell(dataRow, 7).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(dataRow, 8).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(dataRow, 9).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(dataRow, 10).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(dataRow, 11).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(dataRow, 12).Style.NumberFormat.Format = "#,##0.00";

                    dataRow++;
                    lineNum++;
                }

                // ── Totals Row ──
                int totalsRow = dataRow + 1;
                ws.Cell(totalsRow, 1).Value = "الإجماليات";
                ws.Cell(totalsRow, 1).Style.Font.Bold = true;
                ws.Range(totalsRow, 1, totalsRow, 4).Merge();

                void SetTotalCell(int col, string label, decimal value)
                {
                    ws.Cell(totalsRow, col).Value = label;
                    ws.Cell(totalsRow, col).Style.Font.Bold = true;
                    ws.Cell(totalsRow, col + 1).Value = value;
                    ws.Cell(totalsRow, col + 1).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(totalsRow, col + 1).Style.Font.Bold = true;
                }

                SetTotalCell(5, "الإجمالي:", CurrentInvoice.Subtotal);
                SetTotalCell(7, "الخصم:", CurrentInvoice.DiscountTotal);
                SetTotalCell(9, "الضريبة:", CurrentInvoice.VatTotal);
                ws.Cell(totalsRow, 11).Value = "الصافي:";
                ws.Cell(totalsRow, 11).Style.Font.Bold = true;
                ws.Cell(totalsRow, 12).Value = CurrentInvoice.NetTotal;
                ws.Cell(totalsRow, 12).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(totalsRow, 12).Style.Font.Bold = true;
                ws.Cell(totalsRow, 12).Style.Font.FontSize = 13;
                ws.Cell(totalsRow, 12).Style.Font.FontColor = ClosedXML.Excel.XLColor.FromArgb(0x1565C0);

                // Totals border
                ws.Range(totalsRow, 1, totalsRow, 12).Style.Border.TopBorder = ClosedXML.Excel.XLBorderStyleValues.Double;
                ws.Range(totalsRow, 1, totalsRow, 12).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(0xE3F2FD);

                // ── Notes ──
                if (!string.IsNullOrWhiteSpace(CurrentInvoice.Notes))
                {
                    int notesRow = totalsRow + 2;
                    ws.Cell(notesRow, 1).Value = "ملاحظات:";
                    ws.Cell(notesRow, 1).Style.Font.Bold = true;
                    ws.Cell(notesRow, 2).Value = CurrentInvoice.Notes;
                    ws.Range(notesRow, 2, notesRow, 6).Merge();
                }

                // ── Auto-fit columns ──
                ws.Columns(1, 12).AdjustToContents();

                workbook.SaveAs(dlg.FileName);
            });

            StatusMessage = "تم تصدير الفاتورة إلى Excel بنجاح";
        }

        private async Task SendByEmailAsync()
        {
            if (CurrentInvoice == null || _emailService == null) return;

            var customerEmail = "";
            if (CurrentInvoice.CustomerId.HasValue)
            {
                var customer = Customers.FirstOrDefault(c => c.Id == CurrentInvoice.CustomerId.Value);
                customerEmail = customer?.Email ?? "";
            }

            var dialog = new SendEmailDialog(customerEmail);
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            if (dialog.ShowDialog() != true || !dialog.WasSent) return;

            IsBusy = true;
            ClearError();
            try
            {
                var pdfBytes = await _invoicePdfPreviewService.GenerateSalesInvoicePdfBytesAsync(CurrentInvoice);

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    ErrorMessage = "تعذر إنشاء ملف PDF للفاتورة.";
                    return;
                }

                var result = await _emailService.SendInvoiceByEmailAsync(
                    dialog.Email, CurrentInvoice.InvoiceNumber, pdfBytes);

                if (result.IsSuccess)
                    StatusMessage = $"تم إرسال الفاتورة «{CurrentInvoice.InvoiceNumber}» بالبريد الإلكتروني إلى {dialog.Email}";
                else
                    ErrorMessage = result.ErrorMessage;
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("إرسال البريد الإلكتروني", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Tab Title Update ────────────────────────────────────
        /// <summary>Updates the tab title to show invoice number and status.</summary>
        private void UpdateTabTitle(string invoiceNumber, string statusText)
        {
            try
            {
                // Find current tab through navigation service
                var mainViewModel = System.Windows.Application.Current.MainWindow?.DataContext as Shell.MainWindowViewModel;
                if (mainViewModel == null) return;

                var currentTab = mainViewModel.ActiveTab;
                if (currentTab != null)
                {
                    currentTab.Title = $"فاتورة بيع - {invoiceNumber}";
                    currentTab.StatusText = statusText;
                }
            }
            catch
            {
                // Tab update is non-critical; ignore failures
            }
        }

        /// <summary>Returns Arabic status text for display.</summary>
        private static string GetStatusText(string status)
        {
            return status switch
            {
                "Draft" => "مسودة",
                "Posted" => "مرحّلة",
                "Cancelled" => "ملغاة",
                _ => status
            };
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
    }
}
