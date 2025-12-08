using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CS308Main.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly IWebHostEnvironment _environment;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _logger = logger;
            _environment = environment;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body, string? attachmentPath = null)
        {
            var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp-mail.outlook.com";
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUsername = _configuration["Email:SmtpUsername"] ?? "";
            var smtpPassword = _configuration["Email:SmtpPassword"] ?? "";
            var fromEmail = _configuration["Email:FromEmail"] ?? "";

            if (string.IsNullOrEmpty(smtpUsername) ||
                string.IsNullOrEmpty(smtpPassword) ||
                string.IsNullOrEmpty(fromEmail))
            {
                _logger.LogWarning($"Email is not configured. Would send email to {toEmail}.");
                if (_environment.IsDevelopment())
                {
                    await SaveEmailToFileAsync(toEmail, subject, body, attachmentPath);
                    return;
                }
                throw new InvalidOperationException("Email service is not configured.");
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("MotorMatch", fromEmail));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder { HtmlBody = body };

                if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
                    bodyBuilder.Attachments.Add(attachmentPath);

                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    client.CheckCertificateRevocation = false;

                    // 🔥 En kritik satırlar — XOAUTH2 devre dışı
                    client.AuthenticationMechanisms.Remove("XOAUTH2");

                    await client.ConnectAsync(
                        smtpHost,
                        smtpPort,
                        SecureSocketOptions.StartTls
                    );

                    await client.AuthenticateAsync(smtpUsername, smtpPassword);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }

                _logger.LogInformation($"Email sent successfully to {toEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {toEmail}. Error: {ex.Message}");

                if (_environment.IsDevelopment())
                {
                    _logger.LogInformation("Saving email to file in development mode");
                    await SaveEmailToFileAsync(toEmail, subject, body, attachmentPath);
                    return;
                }

                throw;
            }
        }

        private async Task SaveEmailToFileAsync(string toEmail, string subject, string body, string? attachmentPath)
        {
            try
            {
                var emailsFolder = Path.Combine(_environment.WebRootPath, "Emails");
                if (!Directory.Exists(emailsFolder))
                    Directory.CreateDirectory(emailsFolder);

                var fileName = $"Email_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}.txt";
                var filePath = Path.Combine(emailsFolder, fileName);

                var emailContent = $@"Email saved (service not configured or failed)
========================================
To: {toEmail}
Subject: {subject}
Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
Attachment: {(string.IsNullOrEmpty(attachmentPath) ? "None" : attachmentPath)}

Body:
{body}
";

                await File.WriteAllTextAsync(filePath, emailContent);
                _logger.LogInformation($"Email saved to file: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save email to file");
            }
        }

        public async Task SendInvoiceEmailAsync(string toEmail, string invoiceNumber, string pdfPath)
        {
            var subject = $"Your Invoice - {invoiceNumber}";
            var body = $@"
<html>
<body style='font-family: Arial, sans-serif;'>
    <h2>Thank you for your purchase!</h2>
    <p>Your order has been successfully processed.</p>
    <p><strong>Invoice Number:</strong> {invoiceNumber}</p>
    <p>Please find your invoice attached.</p>
    <br>
    <p>Best regards,</p>
    <p><strong>MotorMatch Team</strong></p>
</body>
</html>";

            await SendEmailAsync(toEmail, subject, body, pdfPath);
        }
    }
}
