namespace CS308Main.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body, string? attachmentPath = null);
        Task SendInvoiceEmailAsync(string toEmail, string invoiceNumber, string pdfPath);
    }
}