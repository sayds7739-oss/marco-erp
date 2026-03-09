using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;

namespace MarcoERP.Application.Interfaces
{
    /// <summary>
    /// Sends emails via SMTP. Configuration is read from SystemSettings (SMTP_* keys).
    /// </summary>
    public interface IEmailService
    {
        /// <summary>Sends an email with optional file attachments.</summary>
        Task<ServiceResult> SendAsync(
            string to, string subject, string body,
            IReadOnlyList<EmailAttachment> attachments = null,
            CancellationToken ct = default);

        /// <summary>Sends a sales invoice PDF by email.</summary>
        Task<ServiceResult> SendInvoiceByEmailAsync(
            string recipientEmail,
            string invoiceNumber,
            byte[] pdfBytes,
            CancellationToken ct = default);
    }

    public sealed class EmailAttachment
    {
        public string FileName { get; set; }
        public byte[] Content { get; set; }
        public string ContentType { get; set; } = "application/octet-stream";
    }
}
