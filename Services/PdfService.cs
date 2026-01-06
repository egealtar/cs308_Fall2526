using CS308Main.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Font;
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

        public Task<string> GenerateInvoicePdfAsync(Invoice invoice, User user)
        {
            // PDF kayıt klasörü
            var invoiceFolder = Path.Combine(_environment.WebRootPath, "Invoices");
            if (!Directory.Exists(invoiceFolder))
            {
                Directory.CreateDirectory(invoiceFolder);
            }

            var fileName = $"Invoice_{invoice.InvoiceNumber}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            var filePath = Path.Combine(invoiceFolder, fileName);

            // PDF oluştur - basit mod, encryption olmadan
            using (var writer = new PdfWriter(filePath))
            {
                using (var pdf = new PdfDocument(writer))
                {
                    var document = new Document(pdf);

                    // Create fonts - use standard fonts with string names
                    var boldFont = PdfFontFactory.CreateFont("Helvetica-Bold");
                    var normalFont = PdfFontFactory.CreateFont("Helvetica");





                    // Header
                    document.Add(new Paragraph("MOTORMATCH")
                        .SetFont(boldFont)
                        .SetFontSize(24)
                        .SetTextAlignment(TextAlignment.CENTER));

                    document.Add(new Paragraph("INVOICE")
                        .SetFont(boldFont)
                        .SetFontSize(18)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetMarginBottom(20));

                    // Invoice info
                    document.Add(new Paragraph($"Invoice Number: {invoice.InvoiceNumber}")
                        .SetFont(boldFont));
                    document.Add(new Paragraph($"Date: {invoice.CreatedAt:yyyy-MM-dd HH:mm}")
                        .SetFont(normalFont));
                    document.Add(new Paragraph($"Customer: {user.Name}")
                        .SetFont(normalFont));
                    document.Add(new Paragraph($"Email: {user.Email}")
                        .SetFont(normalFont));
                    document.Add(new Paragraph($"Address: {user.HomeAddress}")
                        .SetFont(normalFont)
                        .SetMarginBottom(20));

                    // Items table
                    var table = new Table(4);
                    table.SetWidth(UnitValue.CreatePercentValue(100));

                    // Table headers
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Product").SetFont(boldFont)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Quantity").SetFont(boldFont)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Price").SetFont(boldFont)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Total").SetFont(boldFont)));

                    // Table rows
                    foreach (var item in invoice.Items)
                    {
                        table.AddCell(new Cell().Add(new Paragraph(item.ProductName).SetFont(normalFont)));
                        table.AddCell(new Cell().Add(new Paragraph(item.Quantity.ToString()).SetFont(normalFont)));
                        table.AddCell(new Cell().Add(new Paragraph($"${item.Price:F2}").SetFont(normalFont)));
                        table.AddCell(new Cell().Add(new Paragraph($"${item.Total:F2}").SetFont(normalFont)));
                    }

                    document.Add(table);

                    // Total
                    document.Add(new Paragraph($"\nTotal Amount: ${invoice.TotalAmount:F2}")
                        .SetFont(boldFont)
                        .SetFontSize(16)
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetMarginTop(20));

                    // Footer
                    document.Add(new Paragraph("\nThank you for your business!")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetMarginTop(30));

                    // Close document to ensure PDF is properly finalized
                    document.Close();
                }
            }

            // Return relative path for web access
            string webPath;
            try
            {
                var relativePath = Path.GetRelativePath(_environment.WebRootPath, filePath);
                // Ensure path uses forward slashes and starts with /
                webPath = "/" + relativePath.Replace("\\", "/");
            }
            catch (ArgumentException)
            {
                // If paths are not related (e.g., different drives), use the file name only
                var fallbackFileName = Path.GetFileName(filePath);
                webPath = "/Invoices/" + fallbackFileName;
            }
            
            return Task.FromResult(webPath);
        }
    }
}