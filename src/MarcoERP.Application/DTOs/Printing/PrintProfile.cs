using System.Collections.Generic;

namespace MarcoERP.Application.DTOs.Printing
{
    /// <summary>
    /// Central print profile containing company info and visual style settings.
    /// Stored as SystemSetting key-value pairs.
    /// </summary>
    public sealed class PrintProfile
    {
        // ── Company Info ──
        public string CompanyName { get; set; } = "";
        public string CompanyNameEn { get; set; } = "";
        public string CompanyAddress { get; set; } = "";
        public string CompanyPhone { get; set; } = "";
        public string CompanyEmail { get; set; } = "";
        public string CompanyTaxNumber { get; set; } = "";
        /// <summary>Base64-encoded logo image (PNG/JPG), or empty.</summary>
        public string CompanyLogoBase64 { get; set; } = "";

        // ── Style: Colors ──
        /// <summary>Primary color for headers/borders (hex, e.g. #1565C0).</summary>
        public string PrimaryColor { get; set; } = "#1565C0";
        /// <summary>Header background color.</summary>
        public string HeaderBgColor { get; set; } = "#ECEFF1";
        /// <summary>Table border color.</summary>
        public string BorderColor { get; set; } = "#CFD8DC";
        /// <summary>Body text color.</summary>
        public string TextColor { get; set; } = "#263238";
        /// <summary>Subtitle / secondary text color.</summary>
        public string SubtitleColor { get; set; } = "#607D8B";

        // ── Style: Fonts ──
        /// <summary>Font family (CSS value). Default: Segoe UI, Tahoma, Arial</summary>
        public string FontFamily { get; set; } = "Segoe UI, Tahoma, Arial";
        /// <summary>Title font size in px.</summary>
        public int TitleFontSize { get; set; } = 20;
        /// <summary>Body font size in px.</summary>
        public int BodyFontSize { get; set; } = 12;

        // ── Style: Layout ──
        /// <summary>Show company logo in header.</summary>
        public bool ShowLogo { get; set; } = true;
        /// <summary>Show company name in header.</summary>
        public bool ShowCompanyName { get; set; } = true;
        /// <summary>Show company address in header.</summary>
        public bool ShowAddress { get; set; } = true;
        /// <summary>Show company phone/email in header.</summary>
        public bool ShowContact { get; set; } = true;
        /// <summary>Show tax number in header.</summary>
        public bool ShowTaxNumber { get; set; } = true;
        /// <summary>Show footer with page number.</summary>
        public bool ShowFooter { get; set; } = true;
        /// <summary>Footer text.</summary>
        public string FooterText { get; set; } = "MarcoERP — نظام إدارة الأعمال";

        // ── Customization: Section Order ──
        /// <summary>Ordered list of section keys controlling render order. 
        /// Valid keys: CompanyHeader, DocumentTitle, MetaFields, LinesTable, Summary, Notes, Footer.
        /// If empty, default order is used.</summary>
        public List<string> SectionOrder { get; set; } = new();

        /// <summary>List of column headers to hide in the lines table.</summary>
        public List<string> HiddenColumns { get; set; } = new();

        /// <summary>Custom HTML to inject after the company header.</summary>
        public string CustomHeaderHtml { get; set; } = "";

        /// <summary>Custom HTML to inject before the footer.</summary>
        public string CustomFooterHtml { get; set; } = "";
    }
}
