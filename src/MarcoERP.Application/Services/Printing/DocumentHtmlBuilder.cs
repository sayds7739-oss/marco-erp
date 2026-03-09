using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.DTOs.Printing;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Printing;

namespace MarcoERP.Application.Services.Printing
{
    /// <summary>
    /// Generates RTL Arabic HTML documents using the <see cref="PrintProfile"/> settings.
    /// Supports all document types with a unified layout:
    ///   Company Header -> Document Title -> Meta Fields -> Lines Table -> Summary -> Notes -> Footer.
    /// </summary>
    public sealed class DocumentHtmlBuilder : IDocumentHtmlBuilder
    {
        private readonly IPrintProfileProvider _profileProvider;
        private readonly IDateTimeProvider _dateTime;

        public DocumentHtmlBuilder(IPrintProfileProvider profileProvider, IDateTimeProvider dateTime)
        {
            _profileProvider = profileProvider ?? throw new ArgumentNullException(nameof(profileProvider));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
        }

        public async Task<string> BuildAsync(DocumentData data, CancellationToken ct = default)
        {
            var p = await _profileProvider.GetProfileAsync(ct);
            var sb = new StringBuilder(4096);

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"ar\" dir=\"rtl\">");
            sb.AppendLine("<head><meta charset=\"utf-8\" />");
            AppendStyles(sb, p);
            sb.AppendLine("</head><body>");

            var defaultOrder = new[] { "CompanyHeader", "DocumentTitle", "MetaFields", "LinesTable", "Summary", "Notes", "Footer" };
            var order = p.SectionOrder != null && p.SectionOrder.Count > 0 ? p.SectionOrder : (System.Collections.Generic.IReadOnlyList<string>)defaultOrder;

            foreach (var section in order)
            {
                switch (section)
                {
                    case "CompanyHeader":
                        AppendCompanyHeader(sb, p);
                        if (!string.IsNullOrWhiteSpace(p.CustomHeaderHtml))
                            sb.AppendLine(p.CustomHeaderHtml);
                        break;
                    case "DocumentTitle":
                        AppendDocumentTitle(sb, data, p);
                        break;
                    case "MetaFields":
                        AppendMetaFields(sb, data);
                        break;
                    case "LinesTable":
                        AppendTable(sb, data, p);
                        break;
                    case "Summary":
                        AppendSummary(sb, data, p);
                        break;
                    case "Notes":
                        AppendNotes(sb, data);
                        break;
                    case "Footer":
                        if (!string.IsNullOrWhiteSpace(p.CustomFooterHtml))
                            sb.AppendLine(p.CustomFooterHtml);
                        AppendFooter(sb, p, _dateTime);
                        break;
                }
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        // ── CSS Styles ──────────────────────────────────────────
        private static void AppendStyles(StringBuilder sb, PrintProfile p)
        {
            sb.AppendLine("<style>");
            sb.AppendLine("@page { margin: 12mm; }");
            sb.AppendLine($"* {{ box-sizing: border-box; }}");
            sb.AppendLine($"body {{ font-family: {Esc(p.FontFamily)}; margin: 20px; color: {Esc(p.TextColor)}; font-size: {p.BodyFontSize}px; }}");
            sb.AppendLine($"h1 {{ font-size: {p.TitleFontSize}px; margin: 0 0 6px; color: {Esc(p.PrimaryColor)}; }}");
            sb.AppendLine($".subtitle {{ font-size: {p.BodyFontSize}px; color: {Esc(p.SubtitleColor)}; margin-bottom: 14px; }}");

            // Company header
            sb.AppendLine(".company-header { display: flex; align-items: center; gap: 16px; margin-bottom: 16px; padding-bottom: 12px; }");
            sb.AppendLine($".company-header {{ border-bottom: 2px solid {Esc(p.PrimaryColor)}; }}");
            sb.AppendLine(".company-logo { max-height: 60px; max-width: 120px; }");
            sb.AppendLine($".company-info {{ flex: 1; }}");
            sb.AppendLine($".company-name {{ font-size: {p.TitleFontSize + 2}px; font-weight: bold; color: {Esc(p.PrimaryColor)}; margin: 0; }}");
            sb.AppendLine($".company-detail {{ font-size: {p.BodyFontSize - 1}px; color: {Esc(p.SubtitleColor)}; margin: 2px 0; }}");

            // Meta fields grid
            sb.AppendLine(".meta-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 6px 24px; margin-bottom: 16px; }");
            sb.AppendLine($".meta-item {{ font-size: {p.BodyFontSize}px; }}");
            sb.AppendLine($".meta-label {{ font-weight: bold; color: {Esc(p.TextColor)}; }}");
            sb.AppendLine($".meta-value {{ color: {Esc(p.SubtitleColor)}; }}");

            // Table
            sb.AppendLine($"table {{ width: 100%; border-collapse: collapse; font-size: {p.BodyFontSize}px; margin-bottom: 16px; }}");
            sb.AppendLine($"th, td {{ border: 1px solid {Esc(p.BorderColor)}; padding: 6px 8px; }}");
            sb.AppendLine($"th {{ background: {Esc(p.HeaderBgColor)}; font-weight: bold; text-align: center; }}");
            sb.AppendLine("td.num { text-align: left; direction: ltr; }");
            sb.AppendLine("tr:nth-child(even) { background: #FAFAFA; }");

            // Summary
            sb.AppendLine(".summary { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 16px; }");
            sb.AppendLine($".summary-item {{ background: {Esc(p.HeaderBgColor)}; padding: 8px 14px; border-radius: 6px; font-size: {p.BodyFontSize}px; }}");
            sb.AppendLine(".summary-label { font-weight: bold; }");

            // Notes
            sb.AppendLine($".notes {{ background: #FFF8E1; padding: 10px 14px; border-radius: 6px; font-size: {p.BodyFontSize}px; margin-bottom: 16px; border-right: 4px solid #FFB300; }}");

            // Footer
            sb.AppendLine($".footer {{ text-align: center; font-size: {p.BodyFontSize - 2}px; color: {Esc(p.SubtitleColor)}; border-top: 1px solid {Esc(p.BorderColor)}; padding-top: 8px; margin-top: 16px; }}");

            sb.AppendLine("</style>");
        }

        // ── Company Header ──────────────────────────────────────
        private static void AppendCompanyHeader(StringBuilder sb, PrintProfile p)
        {
            bool hasAnyHeader = p.ShowCompanyName || p.ShowLogo || p.ShowAddress || p.ShowContact || p.ShowTaxNumber;
            if (!hasAnyHeader) return;

            sb.AppendLine("<div class=\"company-header\">");

            if (p.ShowLogo && !string.IsNullOrWhiteSpace(p.CompanyLogoBase64))
            {
                sb.AppendLine($"<img class=\"company-logo\" src=\"data:image/png;base64,{p.CompanyLogoBase64}\" alt=\"Logo\" />");
            }

            sb.AppendLine("<div class=\"company-info\">");
            if (p.ShowCompanyName && !string.IsNullOrWhiteSpace(p.CompanyName))
                sb.AppendLine($"<p class=\"company-name\">{Esc(p.CompanyName)}</p>");
            if (p.ShowAddress && !string.IsNullOrWhiteSpace(p.CompanyAddress))
                sb.AppendLine($"<p class=\"company-detail\">{Esc(p.CompanyAddress)}</p>");
            if (p.ShowContact)
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(p.CompanyPhone)) parts.Add($"هاتف: {Esc(p.CompanyPhone)}");
                if (!string.IsNullOrWhiteSpace(p.CompanyEmail)) parts.Add($"بريد: {Esc(p.CompanyEmail)}");
                if (parts.Count > 0)
                    sb.AppendLine($"<p class=\"company-detail\">{string.Join(" | ", parts)}</p>");
            }
            if (p.ShowTaxNumber && !string.IsNullOrWhiteSpace(p.CompanyTaxNumber))
                sb.AppendLine($"<p class=\"company-detail\">الرقم الضريبي: {Esc(p.CompanyTaxNumber)}</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</div>");
        }

        // ── Document Title ──────────────────────────────────────
        private static void AppendDocumentTitle(StringBuilder sb, DocumentData data, PrintProfile p)
        {
            sb.AppendLine($"<h1>{Esc(data.Title)}</h1>");
        }

        // ── Meta Fields ─────────────────────────────────────────
        private static void AppendMetaFields(StringBuilder sb, DocumentData data)
        {
            if (data.MetaFields == null || data.MetaFields.Count == 0) return;

            sb.AppendLine("<div class=\"meta-grid\">");
            foreach (var field in data.MetaFields)
            {
                sb.Append("<div class=\"meta-item\">");
                sb.Append($"<span class=\"meta-label\">{Esc(field.Label)}: </span>");
                sb.Append($"<span class=\"meta-value\">{Esc(field.Value)}</span>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");
        }

        // ── Lines Table ─────────────────────────────────────────
        private static void AppendTable(StringBuilder sb, DocumentData data, PrintProfile p)
        {
            if (data.Columns == null || data.Columns.Count == 0) return;

            var hiddenSet = p.HiddenColumns != null && p.HiddenColumns.Count > 0
                ? new System.Collections.Generic.HashSet<string>(p.HiddenColumns, StringComparer.OrdinalIgnoreCase)
                : null;

            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr>");
            for (int i = 0; i < data.Columns.Count; i++)
            {
                if (hiddenSet != null && hiddenSet.Contains(data.Columns[i].Header)) continue;
                sb.AppendLine($"<th>{Esc(data.Columns[i].Header)}</th>");
            }
            sb.AppendLine("</tr></thead>");

            sb.AppendLine("<tbody>");
            foreach (var row in data.Rows)
            {
                sb.Append("<tr>");
                for (int i = 0; i < row.Count && i < data.Columns.Count; i++)
                {
                    if (hiddenSet != null && hiddenSet.Contains(data.Columns[i].Header)) continue;
                    var cls = data.Columns[i].IsNumeric ? " class=\"num\"" : "";
                    sb.Append($"<td{cls}>{Esc(row[i])}</td>");
                }
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        // ── Summary ─────────────────────────────────────────────
        private static void AppendSummary(StringBuilder sb, DocumentData data, PrintProfile p)
        {
            if (data.SummaryFields == null || data.SummaryFields.Count == 0) return;

            sb.AppendLine("<div class=\"summary\">");
            foreach (var f in data.SummaryFields)
            {
                var boldStyle = f.IsBold ? "font-weight:bold;" : "";
                sb.AppendLine($"<div class=\"summary-item\" style=\"{boldStyle}\">");
                sb.AppendLine($"<span class=\"summary-label\">{Esc(f.Label)}: </span>{Esc(f.Value)}");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");
        }

        // ── Notes ───────────────────────────────────────────────
        private static void AppendNotes(StringBuilder sb, DocumentData data)
        {
            if (string.IsNullOrWhiteSpace(data.Notes)) return;
            sb.AppendLine($"<div class=\"notes\"><strong>ملاحظات:</strong> {Esc(data.Notes)}</div>");
        }

        // ── Footer ──────────────────────────────────────────────
        private static void AppendFooter(StringBuilder sb, PrintProfile p, IDateTimeProvider dateTime)
        {
            if (!p.ShowFooter) return;
            sb.AppendLine($"<div class=\"footer\">{Esc(p.FooterText)} — {dateTime.Today:yyyy/MM/dd HH:mm}</div>");
        }

        private static string Esc(string s) => WebUtility.HtmlEncode(s ?? "");
    }
}
