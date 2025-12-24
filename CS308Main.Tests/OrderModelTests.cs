using CS308Main.Models;
using Xunit;
using System.Collections.Generic;

namespace CS308Main.Tests
{
    public class OrderModelTests
    {
        // Test 7: Order should correctly store its basic field values.
        [Fact]
        public void Order_StoresBasicInfoCorrectly()
        {
            var order = new Order
            {
                UserId = "user123",
                Status = "Processing",
                ShippingAddress = "Some Address",
                PaymentMethod = "CreditCard"
            };

            Assert.Equal("user123", order.UserId);
            Assert.Equal("Processing", order.Status);
            Assert.Equal("Some Address", order.ShippingAddress);
            Assert.Equal("CreditCard", order.PaymentMethod);
        }

        // Test 8: TotalPrice should match the assigned value.
        [Fact]
        public void Order_TotalPrice_MatchesAssignedValue()
        {
            var order = new Order { TotalPrice = 250.75m };

            Assert.Equal(250.75m, order.TotalPrice);
        }

        // Test 9: Order should support holding multiple OrderItem objects.
        [Fact]
        public void Order_CanHoldMultipleOrderItems()
        {
            var order = new Order
            {
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = "p1", ProductName = "Prod 1", Quantity = 1, Price = 50m },
                    new OrderItem { ProductId = "p2", ProductName = "Prod 2", Quantity = 2, Price = 75m }
                }
            };

            Assert.Equal(2, order.Items.Count);
            Assert.Equal("p1", order.Items[0].ProductId);
            Assert.Equal("p2", order.Items[1].ProductId);
        }

        // Test 10: OrderItem total cost should equal Quantity * Price (computed locally).
        [Fact]
        public void OrderItem_TotalCost_ComputedCorrectlyInTest()
        {
            var item = new OrderItem { Quantity = 3, Price = 20m };

            var total = item.Quantity * item.Price;

            Assert.Equal(60m, total);
        }

         // Test 20: Order status can be updated from one value to another.
        [Fact]
        public void Order_Status_CanBeUpdated()
        {
            var order = new Order
            {
                Status = "Pending"
            };

            order.Status = "Shipped";

            Assert.Equal("Shipped", order.Status);
        }

        // Test 21: Order can have an empty item list without throwing errors.
        [Fact]
        public void Order_CanHaveEmptyItemList()
        {
            var order = new Order
            {
                Items = new List<OrderItem>()
            };

            Assert.Empty(order.Items);
        }

        // Test 22: OrderItem basic fields should be stored correctly.
        [Fact]
        public void OrderItem_StoresBasicFieldsCorrectly()
        {
            var item = new OrderItem
            {
                ProductId = "p99",
                ProductName = "Air Filter",
                Quantity = 4,
                Price = 30m
            };

            Assert.Equal("p99", item.ProductId);
            Assert.Equal("Air Filter", item.ProductName);
            Assert.Equal(4, item.Quantity);
            Assert.Equal(30m, item.Price);
        }
    }
}
