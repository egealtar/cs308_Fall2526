using CS308Main.Models;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace CS308Main.Services
{
    public class PdfService : IPdfService
    {
        private readonly IWebHostEnvironment _environment;

        public PdfService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public Task<string> GenerateInvoicePdfAsync(Invoice invoice, User user)  // async kaldır
        {
            // PDF kayıt klasörü
            var invoiceFolder = Path.Combine(_environment.WebRootPath, "invoices");
            if (!Directory.Exists(invoiceFolder))
            {
                Directory.CreateDirectory(invoiceFolder);
            }

            var fileName = $"Invoice_{invoice.InvoiceNumber}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            var filePath = Path.Combine(invoiceFolder, fileName);

            // PDF oluştur
            using (var writer = new PdfWriter(filePath))
            {
                using (var pdf = new PdfDocument(writer))
                {
                    var document = new Document(pdf);

                    // Header
                    document.Add(new Paragraph("MOTORMATCH")
                        .SetFontSize(24)
                        .SetBold()
                        .SetTextAlignment(TextAlignment.CENTER));

                    document.Add(new Paragraph("INVOICE")
                        .SetFontSize(18)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetMarginBottom(20));

                    // Invoice info
                    document.Add(new Paragraph($"Invoice Number: {invoice.InvoiceNumber}")
                        .SetBold());
                    document.Add(new Paragraph($"Date: {invoice.CreatedAt:yyyy-MM-dd HH:mm}"));
                    document.Add(new Paragraph($"Customer: {user.Name}"));
                    document.Add(new Paragraph($"Email: {user.Email}"));
                    document.Add(new Paragraph($"Address: {user.HomeAddress}")
                        .SetMarginBottom(20));

                    // Items table
                    var table = new Table(4);
                    table.SetWidth(UnitValue.CreatePercentValue(100));

                    // Table headers
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Product").SetBold()));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Quantity").SetBold()));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Price").SetBold()));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Total").SetBold()));

                    // Table rows
                    foreach (var item in invoice.Items)
                    {
                        table.AddCell(new Cell().Add(new Paragraph(item.ProductName)));
                        table.AddCell(new Cell().Add(new Paragraph(item.Quantity.ToString())));
                        table.AddCell(new Cell().Add(new Paragraph($"${item.Price:F2}")));
                        table.AddCell(new Cell().Add(new Paragraph($"${item.Total:F2}")));
                    }

                    document.Add(table);

                    // Total
                    document.Add(new Paragraph($"\nTotal Amount: ${invoice.TotalAmount:F2}")
                        .SetFontSize(16)
                        .SetBold()
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetMarginTop(20));

                    // Footer
                    document.Add(new Paragraph("\nThank you for your business!")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetMarginTop(30));
                }
            }

            return Task.FromResult(filePath);  // Bu satırı ekle
        }
    }
}