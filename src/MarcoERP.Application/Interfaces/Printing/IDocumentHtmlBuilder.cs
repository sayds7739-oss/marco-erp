using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.DTOs.Printing;

namespace MarcoERP.Application.Interfaces.Printing
{
    /// <summary>
    /// Builds styled HTML for any printable document using the central <see cref="PrintProfile"/>.
    /// Consumers call the appropriate overload and receive ready-to-render HTML.
    /// </summary>
    public interface IDocumentHtmlBuilder
    {
        /// <summary>
        /// Builds HTML for a document given its structured data.
        /// </summary>
        Task<string> BuildAsync(DocumentData data, CancellationToken ct = default);
    }

    /// <summary>
    /// Structured data describing a printable document (document-type-agnostic).
    /// Each ViewModel constructs one of these and passes it to <see cref="IDocumentHtmlBuilder"/>.
    /// </summary>
    public sealed class DocumentData
    {
        /// <summary>Document title (e.g. "فاتورة بيع رقم INV-001").</summary>
        public string Title { get; set; }
        /// <summary>Document type for template selection.</summary>
        public PrintableDocumentType DocumentType { get; set; }

        /// <summary>Key-value metadata rows displayed below title (date, customer, warehouse, status, etc.).</summary>
        public List<DocumentField> MetaFields { get; set; } = new();

        /// <summary>Column definitions for the lines table. Null if no table (e.g. CashReceipt).</summary>
        public List<TableColumn> Columns { get; set; }

        /// <summary>Table data rows matching <see cref="Columns"/>.</summary>
        public List<List<string>> Rows { get; set; } = new();

        /// <summary>Summary fields shown after the table (subtotal, discount, VAT, net, etc.).</summary>
        public List<DocumentField> SummaryFields { get; set; } = new();

        /// <summary>Optional notes / comments section.</summary>
        public string Notes { get; set; }
    }

    /// <summary>A label-value pair for metadata display.</summary>
    public sealed class DocumentField
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public bool IsBold { get; set; }

        public DocumentField() { }
        public DocumentField(string label, string value, bool bold = false)
        {
            Label = label;
            Value = value;
            IsBold = bold;
        }
    }

    /// <summary>A column definition for the lines table.</summary>
    public sealed class TableColumn
    {
        public string Header { get; set; }
        /// <summary>If true, column is right-aligned (numbers).</summary>
        public bool IsNumeric { get; set; }

        public TableColumn() { }
        public TableColumn(string header, bool isNumeric = false)
        {
            Header = header;
            IsNumeric = isNumeric;
        }
    }
}
