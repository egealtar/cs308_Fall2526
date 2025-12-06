using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace CS308Main.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body, string? attachmentPath = null)
        {
            try
            {
                // SMTP ayarları (appsettings.json'dan gelecek)
                var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var smtpUsername = _configuration["Email:SmtpUsername"] ?? "";
                var smtpPassword = _configuration["Email:SmtpPassword"] ?? "";
                var fromEmail = _configuration["Email:FromEmail"] ?? "";

                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(fromEmail, "MotorMatch"),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(toEmail);

                    if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
                    {
                        mailMessage.Attachments.Add(new Attachment(attachmentPath));
                    }

                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation($"Email sent to {toEmail}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {toEmail}");
                throw;
            }
        }

        public async Task SendInvoiceEmailAsync(string toEmail, string invoiceNumber, string pdfPath)
        {
            var subject = $"Your Invoice - {invoiceNumber}";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2>Thank you for your purchase!</h2>
                    <p>Dear Customer,</p>
                    <p>Your order has been successfully processed.</p>
                    <p><strong>Invoice Number:</strong> {invoiceNumber}</p>
                    <p>Please find your invoice attached to this email.</p>
                    <br>
                    <p>Best regards,</p>
                    <p><strong>MotorMatch Team</strong></p>
                </body>
                </html>
            ";

            await SendEmailAsync(toEmail, subject, body, pdfPath);
        }
    }
}