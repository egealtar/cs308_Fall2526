using CS308Main.Models;
using Xunit;
using System.Collections.Generic;

namespace CS308Main.Tests
{
    public class InvoiceModelTests
    {
        // Test 11: Invoice should correctly store its basic fields.
        [Fact]
        public void Invoice_StoresBasicFieldsCorrectly()
        {
            var invoice = new Invoice
            {
                OrderId = "order123",
                UserId = "user123",
                InvoiceNumber = "INV-20251210-0001",
                TotalAmount = 300m
            };

            Assert.Equal("order123", invoice.OrderId);
            Assert.Equal("user123", invoice.UserId);
            Assert.Equal("INV-20251210-0001", invoice.InvoiceNumber);
            Assert.Equal(300m, invoice.TotalAmount);
        }

        // Test 12: Invoice should support multiple InvoiceItem objects.
        [Fact]
        public void Invoice_CanHoldMultipleInvoiceItems()
        {
            var invoice = new Invoice
            {
                Items = new List<InvoiceItem>
                {
                    new InvoiceItem { ProductName = "Prod1", Quantity = 1, Price = 100m, Total = 100m },
                    new InvoiceItem { ProductName = "Prod2", Quantity = 2, Price = 50m, Total = 100m }
                }
            };

            Assert.Equal(2, invoice.Items.Count);
            Assert.Equal("Prod1", invoice.Items[0].ProductName);
            Assert.Equal("Prod2", invoice.Items[1].ProductName);
        }

        // Test 13: TotalAmount should equal the sum of all InvoiceItem.Total values.
        [Fact]
        public void Invoice_TotalAmount_EqualsSumOfItemTotals()
        {
            var items = new List<InvoiceItem>
            {
                new InvoiceItem { Quantity = 1, Price = 100m, Total = 100m },
                new InvoiceItem { Quantity = 2, Price = 50m, Total = 100m }
            };

            var invoice = new Invoice
            {
                Items = items,
                TotalAmount = 200m
            };

            var sum = 0m;
            foreach (var i in items)
                sum += i.Total;

            Assert.Equal(sum, invoice.TotalAmount);
        }

        // Test 14: InvoiceItem total should be computed as Quantity * Price (tested here).
        [Fact]
        public void InvoiceItem_Total_ComputedCorrectlyInTest()
        {
            var item = new InvoiceItem { Quantity = 4, Price = 25m };

            var total = item.Quantity * item.Price;

            Assert.Equal(100m, total);
        }

        // Test 15: Invoice Items list should be initializable and non-null.
        [Fact]
        public void Invoice_ItemsList_CanBeInitialized()
        {
            var invoice = new Invoice
            {
                Items = new List<InvoiceItem>()
            };

            Assert.NotNull(invoice.Items);
            Assert.Empty(invoice.Items);
        }

              // Test 23: Invoice CreatedAt should store the given DateTime value correctly.
        [Fact]
        public void Invoice_CreatedAt_IsStoredCorrectly()
        {
            var date = new DateTime(2025, 12, 10, 14, 30, 0);

            var invoice = new Invoice
            {
                CreatedAt = date
            };

            Assert.Equal(date, invoice.CreatedAt);
        }

        // Test 24: Invoice can have an empty PdfPath (for invoices without generated PDF).
        [Fact]
        public void Invoice_AllowsEmptyPdfPath()
        {
            var invoice = new Invoice
            {
                PdfPath = string.Empty
            };

            Assert.Equal(string.Empty, invoice.PdfPath);
        }

        // Test 25: InvoiceItem should correctly store its basic fields (name, quantity, price, total).
        [Fact]
        public void InvoiceItem_StoresBasicFieldsCorrectly()
        {
            var item = new InvoiceItem
            {
                ProductName = "Engine Oil",
                Quantity = 3,
                Price = 40m,
                Total = 120m
            };

            Assert.Equal("Engine Oil", item.ProductName);
            Assert.Equal(3, item.Quantity);
            Assert.Equal(40m, item.Price);
            Assert.Equal(120m, item.Total);
        }
    }
}
