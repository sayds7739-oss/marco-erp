using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Extensions.DependencyInjection;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.WpfUI.Views.Common;

namespace MarcoERP.WpfUI.Services
{
    public sealed class InvoicePdfPreviewService : IInvoicePdfPreviewService
    {
        private readonly IServiceProvider _serviceProvider;

        public InvoicePdfPreviewService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public Task ShowSalesInvoiceAsync(SalesInvoiceDto invoice)
        {
            if (invoice == null)
                return Task.CompletedTask;

            var request = new InvoicePdfPreviewRequest
            {
                Title = $"فاتورة بيع {invoice.InvoiceNumber}",
                FilePrefix = $"sales_{invoice.InvoiceNumber}",
                HtmlContent = BuildSalesInvoiceHtml(invoice)
            };

            ShowDialog(request);
            return Task.CompletedTask;
        }

        public Task ShowPurchaseInvoiceAsync(PurchaseInvoiceDto invoice)
        {
            if (invoice == null)
                return Task.CompletedTask;

            var request = new InvoicePdfPreviewRequest
            {
                Title = $"فاتورة شراء {invoice.InvoiceNumber}",
                FilePrefix = $"purchase_{invoice.InvoiceNumber}",
                HtmlContent = BuildPurchaseInvoiceHtml(invoice)
            };

            ShowDialog(request);
            return Task.CompletedTask;
        }

        private void ShowDialog(InvoicePdfPreviewRequest request)
        {
            var dialog = _serviceProvider.GetRequiredService<InvoicePdfPreviewDialog>();
            dialog.Owner = System.Windows.Application.Current?.MainWindow;
            dialog.Initialize(request);
            dialog.ShowDialog();
        }

        public Task ShowHtmlPreviewAsync(InvoicePdfPreviewRequest request)
        {
            if (request == null)
                return Task.CompletedTask;
            ShowDialog(request);
            return Task.CompletedTask;
        }

        public Task ShowPdfFileAsync(InvoicePdfPreviewRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PdfPath))
                return Task.CompletedTask;

            ShowDialog(request);
            return Task.CompletedTask;
        }

        public async Task<byte[]> GenerateSalesInvoicePdfBytesAsync(SalesInvoiceDto invoice)
        {
            if (invoice == null) return null;

            var html = BuildSalesInvoiceHtml(invoice);
            var folder = Path.Combine(Path.GetTempPath(), "MarcoERP", "email-pdf");
            Directory.CreateDirectory(folder);
            var tempFile = Path.Combine(folder, $"invoice_{Guid.NewGuid():N}.pdf");

            byte[] pdfBytes = null;

            // Use a hidden off-screen window with WebView2 to render HTML to PDF
            var webView = new Microsoft.Web.WebView2.Wpf.WebView2();
            var hiddenWindow = new Window
            {
                Width = 800,
                Height = 600,
                Left = -10000,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                Content = webView
            };

            try
            {
                hiddenWindow.Show();

                await webView.EnsureCoreWebView2Async();

                var tcs = new TaskCompletionSource<bool>();
                webView.NavigationCompleted += (_, e) => tcs.TrySetResult(e.IsSuccess);
                webView.CoreWebView2.NavigateToString(html);
                await tcs.Task;

                var settings = webView.CoreWebView2.Environment.CreatePrintSettings();
                settings.PageWidth = 8.27;   // A4 width in inches
                settings.PageHeight = 11.69;  // A4 height in inches
                settings.MarginTop = 0.4;
                settings.MarginBottom = 0.4;
                settings.MarginLeft = 0.4;
                settings.MarginRight = 0.4;

                var success = await webView.CoreWebView2.PrintToPdfAsync(tempFile, settings);
                if (success && File.Exists(tempFile))
                {
                    pdfBytes = await File.ReadAllBytesAsync(tempFile);
                }
            }
            finally
            {
                webView.Dispose();
                hiddenWindow.Close();
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }

            return pdfBytes;
        }

        private static string BuildSalesInvoiceHtml(SalesInvoiceDto invoice)
        {
            var culture = CultureInfo.GetCultureInfo("ar-EG");
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"ar\" dir=\"rtl\">");
            sb.AppendLine("<head><meta charset=\"utf-8\" />");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:Segoe UI, Tahoma, Arial; margin:24px; color:#263238;}");
            sb.AppendLine("h1{font-size:20px; margin:0 0 8px;}");
            sb.AppendLine(".meta{font-size:12px; color:#607D8B; margin-bottom:16px;}");
            sb.AppendLine("table{width:100%; border-collapse:collapse; font-size:12px;}");
            sb.AppendLine("th,td{border:1px solid #CFD8DC; padding:6px 8px;}");
            sb.AppendLine("th{background:#ECEFF1;}");
            sb.AppendLine(".totals{margin-top:16px; display:flex; gap:16px; font-size:12px;}");
            sb.AppendLine(".totals div{background:#F5F5F5; padding:8px 10px; border-radius:6px;}");
            sb.AppendLine("</style></head><body>");

            var salesCounterpartyLabel = invoice.CounterpartyType == CounterpartyType.Supplier ? "المورد" : "العميل";
            var salesCounterpartyName = invoice.CounterpartyType == CounterpartyType.Supplier ? invoice.SupplierNameAr : invoice.CustomerNameAr;

            sb.AppendLine($"<h1>فاتورة بيع رقم {WebUtility.HtmlEncode(invoice.InvoiceNumber)}</h1>");
            sb.AppendLine($"<div class=\"meta\">التاريخ: {invoice.InvoiceDate.ToString("yyyy-MM-dd", culture)} | {salesCounterpartyLabel}: {WebUtility.HtmlEncode(salesCounterpartyName)}</div>");

            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr>");
            sb.AppendLine("<th>#</th><th>الصنف</th><th>الوحدة</th><th>الكمية</th><th>السعر</th><th>خصم %</th><th>الإجمالي</th>");
            sb.AppendLine("</tr></thead><tbody>");

            var index = 1;
            foreach (var line in invoice.Lines)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{index++}</td>");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(line.ProductNameAr)}</td>");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(line.UnitNameAr)}</td>");
                sb.AppendLine($"<td>{line.Quantity.ToString("N2", culture)}</td>");
                sb.AppendLine($"<td>{line.UnitPrice.ToString("N2", culture)}</td>");
                sb.AppendLine($"<td>{line.DiscountPercent.ToString("N2", culture)}</td>");
                sb.AppendLine($"<td>{line.TotalWithVat.ToString("N2", culture)}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");

            sb.AppendLine("<div class=\"totals\">");
            sb.AppendLine($"<div>الإجمالي: {invoice.Subtotal.ToString("N2", culture)}</div>");
            sb.AppendLine($"<div>الخصم: {invoice.DiscountTotal.ToString("N2", culture)}</div>");
            sb.AppendLine($"<div>الضريبة: {invoice.VatTotal.ToString("N2", culture)}</div>");
            sb.AppendLine($"<div>الصافي: {invoice.NetTotal.ToString("N2", culture)}</div>");
            sb.AppendLine("</div>");

            if (!string.IsNullOrWhiteSpace(invoice.Notes))
                sb.AppendLine($"<div class=\"meta\">ملاحظات: {WebUtility.HtmlEncode(invoice.Notes)}</div>");

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static string BuildPurchaseInvoiceHtml(PurchaseInvoiceDto invoice)
        {
            var culture = CultureInfo.GetCultureInfo("ar-EG");
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"ar\" dir=\"rtl\">");
            sb.AppendLine("<head><meta charset=\"utf-8\" />");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:Segoe UI, Tahoma, Arial; margin:24px; color:#263238;}");
            sb.AppendLine("h1{font-size:20px; margin:0 0 8px;}");
            sb.AppendLine(".meta{font-size:12px; color:#607D8B; margin-bottom:16px;}");
            sb.AppendLine("table{width:100%; border-collapse:collapse; font-size:12px;}");
            sb.AppendLine("th,td{border:1px solid #CFD8DC; padding:6px 8px;}");
            sb.AppendLine("th{background:#ECEFF1;}");
            sb.AppendLine(".totals{margin-top:16px; display:flex; gap:16px; font-size:12px;}");
            sb.AppendLine(".totals div{background:#F5F5F5; padding:8px 10px; border-radius:6px;}");
            sb.AppendLine("</style></head><body>");

            var purchaseCounterpartyLabel = invoice.CounterpartyType == CounterpartyType.Customer ? "العميل" : "المورد";
            var purchaseCounterpartyName = invoice.CounterpartyType == CounterpartyType.Customer ? invoice.CounterpartyCustomerNameAr : invoice.SupplierNameAr;

            sb.AppendLine($"<h1>فاتورة شراء رقم {WebUtility.HtmlEncode(invoice.InvoiceNumber)}</h1>");
            sb.AppendLine($"<div class=\"meta\">التاريخ: {invoice.InvoiceDate.ToString("yyyy-MM-dd", culture)} | {purchaseCounterpartyLabel}: {WebUtility.HtmlEncode(purchaseCounterpartyName)}</div>");

            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr>");
            sb.AppendLine("<th>#</th><th>الصنف</th><th>الوحدة</th><th>الكمية</th><th>السعر</th><th>خصم %</th><th>الإجمالي</th>");
            sb.AppendLine("</tr></thead><tbody>");

            var index = 1;
            foreach (var line in invoice.Lines)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{index++}</td>");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(line.ProductNameAr)}</td>");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(line.UnitNameAr)}</td>");
                sb.AppendLine($"<td>{line.Quantity.ToString("N2", culture)}</td>");
                sb.AppendLine($"<td>{line.UnitPrice.ToString("N2", culture)}</td>");
                sb.AppendLine($"<td>{line.DiscountPercent.ToString("N2", culture)}</td>");
                sb.AppendLine($"<td>{line.TotalWithVat.ToString("N2", culture)}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");

            sb.AppendLine("<div class=\"totals\">");
            sb.AppendLine($"<div>الإجمالي: {invoice.Subtotal.ToString("N2", culture)}</div>");
            sb.AppendLine($"<div>الخصم: {invoice.DiscountTotal.ToString("N2", culture)}</div>");
            sb.AppendLine($"<div>الضريبة: {invoice.VatTotal.ToString("N2", culture)}</div>");
            sb.AppendLine($"<div>الصافي: {invoice.NetTotal.ToString("N2", culture)}</div>");
            sb.AppendLine("</div>");

            if (!string.IsNullOrWhiteSpace(invoice.Notes))
                sb.AppendLine($"<div class=\"meta\">ملاحظات: {WebUtility.HtmlEncode(invoice.Notes)}</div>");

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }
    }
}
