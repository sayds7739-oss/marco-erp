using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.Interfaces;
using MarcoERP.Domain.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Infrastructure.Services
{
    /// <summary>
    /// SMTP-based email service. Reads settings from SystemSettings table (keys prefixed with SMTP_).
    /// </summary>
    public sealed class EmailService : IEmailService
    {
        private readonly ISystemSettingRepository _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(ISystemSettingRepository settings, ILogger<EmailService> logger = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger;
        }

        public async Task<ServiceResult> SendAsync(
            string to, string subject, string body,
            IReadOnlyList<EmailAttachment> attachments = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(to))
                return ServiceResult.Failure("عنوان البريد الإلكتروني مطلوب.");

            try
            {
                var host = await GetSettingAsync("SMTP_Host", ct);
                var portStr = await GetSettingAsync("SMTP_Port", ct);
                var username = await GetSettingAsync("SMTP_Username", ct);
                var password = await GetSettingAsync("SMTP_Password", ct);
                var fromAddress = await GetSettingAsync("SMTP_FromAddress", ct);
                var useSslStr = await GetSettingAsync("SMTP_UseSsl", ct);

                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromAddress))
                    return ServiceResult.Failure("إعدادات SMTP غير مكتملة. يرجى تكوين إعدادات البريد الإلكتروني من إعدادات النظام.");

                int port = int.TryParse(portStr, out var p) ? p : 587;
                bool useSsl = !bool.TryParse(useSslStr, out var ssl) || ssl;

                using var message = new MailMessage();
                message.From = new MailAddress(fromAddress);
                message.To.Add(to);
                message.Subject = subject ?? "";
                message.Body = body ?? "";
                message.IsBodyHtml = false;
                message.SubjectEncoding = System.Text.Encoding.UTF8;
                message.BodyEncoding = System.Text.Encoding.UTF8;

                if (attachments != null)
                {
                    foreach (var att in attachments)
                    {
                        if (att?.Content != null)
                        {
                            var stream = new MemoryStream(att.Content);
                            message.Attachments.Add(new System.Net.Mail.Attachment(stream, att.FileName, att.ContentType));
                        }
                    }
                }

                using var client = new SmtpClient(host, port);
                client.EnableSsl = useSsl;
                if (!string.IsNullOrWhiteSpace(username))
                    client.Credentials = new NetworkCredential(username, password);

                await client.SendMailAsync(message, ct);

                _logger?.LogInformation("Email sent to {To} with subject '{Subject}'", to, subject);
                return ServiceResult.Success();
            }
            catch (SmtpException ex)
            {
                _logger?.LogError(ex, "SMTP error sending email to {To}", to);
                return ServiceResult.Failure($"فشل إرسال البريد: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send email to {To}", to);
                return ServiceResult.Failure("حدث خطأ أثناء إرسال البريد الإلكتروني.");
            }
        }

        public async Task<ServiceResult> SendInvoiceByEmailAsync(
            string recipientEmail,
            string invoiceNumber,
            byte[] pdfBytes,
            CancellationToken ct = default)
        {
            var subject = $"فاتورة رقم {invoiceNumber} — MarcoERP";
            var body = $"مرفق فاتورة رقم {invoiceNumber}.\n\nشكراً لتعاملكم معنا.\nMarcoERP";
            var attachments = new List<EmailAttachment>
            {
                new EmailAttachment
                {
                    FileName = $"Invoice_{invoiceNumber}.pdf",
                    Content = pdfBytes,
                    ContentType = "application/pdf"
                }
            };

            return await SendAsync(recipientEmail, subject, body, attachments, ct);
        }

        private async Task<string> GetSettingAsync(string key, CancellationToken ct)
        {
            var setting = await _settings.GetByKeyAsync(key, ct);
            return setting?.SettingValue ?? "";
        }
    }
}
