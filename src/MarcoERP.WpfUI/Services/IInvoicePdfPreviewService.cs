using System.Threading.Tasks;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.DTOs.Sales;

namespace MarcoERP.WpfUI.Services
{
    public interface IInvoicePdfPreviewService
    {
        Task ShowSalesInvoiceAsync(SalesInvoiceDto invoice);
        Task ShowPurchaseInvoiceAsync(PurchaseInvoiceDto invoice);
        Task ShowPdfFileAsync(InvoicePdfPreviewRequest request);

        /// <summary>Shows a generic HTML preview with PDF generation.</summary>
        Task ShowHtmlPreviewAsync(InvoicePdfPreviewRequest request);

        /// <summary>Generates PDF bytes for a sales invoice without showing a dialog.</summary>
        Task<byte[]> GenerateSalesInvoicePdfBytesAsync(SalesInvoiceDto invoice);
    }
}
