using e_Commerce_application.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ECommerce.Tests
{
    // The original JSON endpoint: POST /order/orders.
    public class OrderApiTests : IClassFixture<TestAppFactory>
    {
        private readonly TestAppFactory _app;

        public OrderApiTests(TestAppFactory app) => _app = app;

        private static object OrderBody(DateTime date, decimal invoice, params object[] products) =>
            new { orderDate = date, invoicePrice = invoice, products };

        [Fact]
        public async Task ValidOrder_IsSaved_AndReducesStock()
        {
            var bottle = await _app.FindProductAsync("Stainless Steel Water Bottle");
            var client = _app.CreateClient();

            var response = await client.PostAsJsonAsync("/order/orders",
                OrderBody(DateTime.UtcNow, bottle.Price * 2, new { productCode = bottle.ProductCode, price = bottle.Price, quantity = 2 }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var orderNo = json.GetProperty("orderNo").GetInt32();
            Assert.True(orderNo > 0);
            Assert.Equal("Pending", json.GetProperty("status").GetString());

            var after = await _app.FindProductAsync("Stainless Steel Water Bottle");
            Assert.Equal(bottle.Stock - 2, after.Stock);
        }

        [Fact]
        public async Task InvoiceMismatch_IsRejected()
        {
            var speaker = await _app.FindProductAsync("Portable Bluetooth Speaker");
            var response = await _app.CreateClient().PostAsJsonAsync("/order/orders",
                OrderBody(DateTime.UtcNow, 99m, new { productCode = speaker.ProductCode, price = speaker.Price, quantity = 1 }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("InvoicePrice doesn't match", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task OldOrderDate_IsRejected()
        {
            var speaker = await _app.FindProductAsync("Portable Bluetooth Speaker");
            var response = await _app.CreateClient().PostAsJsonAsync("/order/orders",
                OrderBody(DateTime.UtcNow.AddHours(-1), speaker.Price, new { productCode = speaker.ProductCode, price = speaker.Price, quantity = 1 }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("can't be in the past", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task WrongPrice_UnknownProduct_AndOutOfStock_AreRejected()
        {
            var speaker = await _app.FindProductAsync("Portable Bluetooth Speaker");
            var hub = await _app.FindProductAsync("USB-C 7-in-1 Hub");
            var client = _app.CreateClient();

            var wrongPrice = await client.PostAsJsonAsync("/order/orders",
                OrderBody(DateTime.UtcNow, 1m, new { productCode = speaker.ProductCode, price = 1m, quantity = 1 }));
            Assert.Contains($"Price for product {speaker.ProductCode}", await wrongPrice.Content.ReadAsStringAsync());

            var unknown = await client.PostAsJsonAsync("/order/orders",
                OrderBody(DateTime.UtcNow, 5m, new { productCode = 99999, price = 5m, quantity = 1 }));
            Assert.Contains("Product 99999 is not available", await unknown.Content.ReadAsStringAsync());

            var soldOut = await client.PostAsJsonAsync("/order/orders",
                OrderBody(DateTime.UtcNow, hub.Price, new { productCode = hub.ProductCode, price = hub.Price, quantity = 1 }));
            Assert.Equal(HttpStatusCode.BadRequest, soldOut.StatusCode);
            Assert.Contains("out of stock", await soldOut.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Ebooks_CannotBeBoughtThroughTheAnonymousApi()
        {
            var book = await _app.FindProductAsync("The Quiet Algorithm");
            var response = await _app.CreateClient().PostAsJsonAsync("/order/orders",
                OrderBody(DateTime.UtcNow, book.Price, new { productCode = book.ProductCode, price = book.Price, quantity = 1 }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("is an ebook", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task CatalogApi_ListsPublishedProducts()
        {
            var products = await _app.CreateClient().GetFromJsonAsync<JsonElement>("/api/products?type=Ebook");
            Assert.Contains(products.EnumerateArray(), p => p.GetProperty("name").GetString() == "The Quiet Algorithm");
            Assert.All(products.EnumerateArray(), p => Assert.Equal(nameof(ProductType.Ebook), p.GetProperty("type").GetString()));
        }
    }
}
