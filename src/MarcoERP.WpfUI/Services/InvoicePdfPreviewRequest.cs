namespace MarcoERP.WpfUI.Services
{
    /// <summary>Paper size presets for PDF generation.</summary>
    public enum PdfPaperSize
    {
        /// <summary>A4 — 210 × 297 mm (default).</summary>
        A4 = 0,
        /// <summary>A5 — 148 × 210 mm.</summary>
        A5 = 1,
        /// <summary>Receipt — 80 mm thermal roll (80 × 297 mm).</summary>
        Receipt = 2
    }

    public sealed class InvoicePdfPreviewRequest
    {
        public string Title { get; set; }
        public string FilePrefix { get; set; }
        public string HtmlContent { get; set; }
        public string PdfPath { get; set; }

        /// <summary>Paper size — defaults to A4.</summary>
        public PdfPaperSize PaperSize { get; set; } = PdfPaperSize.A4;

        /// <summary>When true, the dialog opens in HTML mode first. User can toggle to PDF.</summary>
        public bool StartInHtmlMode { get; set; }
    }
}
