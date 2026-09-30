using e_Commerce_application.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace ECommerce.Tests
{
    // Services, digital downloads, seller fulfilment, wish lists and deals.
    public class MarketplaceTests : IClassFixture<TestAppFactory>
    {
        private readonly TestAppFactory _app;

        public MarketplaceTests(TestAppFactory app) => _app = app;

        private static string Date(int daysFromToday) => DateTime.UtcNow.Date.AddDays(daysFromToday).ToString("yyyy-MM-dd");

        private static async Task<HttpResponseMessage> CheckoutAsync(HttpClient client, Dictionary<string, string> extra)
        {
            var fields = new Dictionary<string, string>
            {
                ["FullName"] = "Test Customer",
                ["Email"] = "customer@test.local",
                ["AddressLine"] = "5 Test Close",
                ["City"] = "Lagos",
                ["Country"] = "Nigeria",
                ["PaymentMethod"] = "Card (demo)"
            };
            foreach (var (key, value) in extra)
            {
                fields[key] = value;
            }
            return await client.PostFormAsync("/Checkout", "/Checkout", fields);
        }

        private static async Task AddToCartAsync(HttpClient client, int code, int quantity = 1) =>
            await client.PostFormAsync($"/Products/Details/{code}", "/Cart/Add",
                new() { ["productCode"] = code.ToString(), ["quantity"] = quantity.ToString() });

        [Fact]
        public async Task Service_IsBooked_ThenFulfilledBySeller()
        {
            var seller = await PublishingTests.BecomeSellerAsync(_app);
            var name = "Garden Makeover " + Guid.NewGuid().ToString("N")[..6];
            var created = await seller.PostFormAsync("/Sell/Create?type=Service", "/Sell/Create", new()
            {
                ["Type"] = "Service",
                ["Name"] = name,
                ["Description"] = "We tidy and plant your garden.",
                ["Category"] = "Home Services",
                ["DurationMinutes"] = "240",
                ["ServiceLocation"] = "CustomerAddress",
                ["Price"] = "80",
                ["publishNow"] = "true"
            });
            Assert.Equal("/Sell", created.Headers.Location!.ToString());
            var service = await _app.FindProductAsync(name);
            Assert.Equal(ProductType.Service, service.Type);

            var buyer = await _app.SignInAsync(await _app.CreateUserAsync());
            await AddToCartAsync(buyer, service.ProductCode, 2);

            // A booking needs a future date.
            var noDate = await CheckoutAsync(buyer, new() { ["Bookings[0].ProductCode"] = service.ProductCode.ToString() });
            Assert.Equal(HttpStatusCode.OK, noDate.StatusCode);
            Assert.Contains("Please choose a date.", await noDate.Content.ReadAsStringAsync());
            var pastDate = await CheckoutAsync(buyer, new()
            {
                ["Bookings[0].ProductCode"] = service.ProductCode.ToString(),
                ["Bookings[0].Date"] = Date(-1)
            });
            Assert.Contains("Choose tomorrow or a later date.", await pastDate.Content.ReadAsStringAsync());

            var booked = await CheckoutAsync(buyer, new()
            {
                ["Bookings[0].ProductCode"] = service.ProductCode.ToString(),
                ["Bookings[0].Date"] = Date(5),
                ["Bookings[0].Notes"] = "Bring compost"
            });
            var orderNo = int.Parse(Regex.Match(booked.Headers.Location!.ToString(), @"/Orders/Details/(\d+)").Groups[1].Value);
            var item = await _app.WithDbAsync(db => db.OrderItems.FirstAsync(i => i.OrderNo == orderNo));
            Assert.Equal(160m, item.LineTotal);
            Assert.Equal(DateTime.UtcNow.Date.AddDays(5), item.ServiceDate);
            Assert.Equal("Bring compost", item.ServiceNotes);
            Assert.False(item.Fulfilled);

            // The seller sees the booking and marks it done; the order completes.
            var queue = await seller.GetStringAsync("/Sell/Orders");
            Assert.Contains("Bring compost", queue);
            var fulfil = await seller.PostFormAsync("/Sell/Orders", $"/Sell/Fulfil/{item.Id}", new());
            Assert.Equal(HttpStatusCode.Redirect, fulfil.StatusCode);
            var order = await _app.WithDbAsync(db => db.Orders.FirstAsync(o => o.OrderNo == orderNo));
            Assert.Equal(OrderStatus.Completed, order.Status);

            Assert.Contains("$136.00", await seller.GetStringAsync("/Sell")); // seller earns 85% of $160
        }

        [Fact]
        public async Task DigitalDownload_IsDeliveredImmediately()
        {
            var templates = await _app.FindProductAsync("Small Business Budget Templates");
            var buyer = await _app.SignInAsync(await _app.CreateUserAsync());
            await AddToCartAsync(buyer, templates.ProductCode);

            var response = await CheckoutAsync(buyer, new());
            var orderNo = int.Parse(Regex.Match(response.Headers.Location!.ToString(), @"(\d+)").Value);
            var order = await _app.WithDbAsync(db => db.Orders.Include(o => o.Products).FirstAsync(o => o.OrderNo == orderNo));
            Assert.Equal(OrderStatus.Completed, order.Status);
            Assert.Null(order.AddressLine); // no address stored for download-only orders
            Assert.True(order.Products.Single().Fulfilled);

            var download = await buyer.GetAsync($"/Library/Download/{templates.ProductCode}");
            Assert.Equal("application/zip", download.Content.Headers.ContentType!.MediaType);
            Assert.Equal((byte)'P', (await download.Content.ReadAsByteArrayAsync())[0]);
        }

        [Fact]
        public async Task Sellers_CannotBuyTheirOwnListings()
        {
            var seller = await PublishingTests.BecomeSellerAsync(_app);
            var name = "Own Item " + Guid.NewGuid().ToString("N")[..6];
            await seller.PostFormAsync("/Sell/Create?type=Physical", "/Sell/Create", new()
            {
                ["Type"] = "Physical",
                ["Name"] = name,
                ["Description"] = "Handmade.",
                ["Category"] = "Jewelry",
                ["Stock"] = "3",
                ["Price"] = "12",
                ["publishNow"] = "true"
            });
            var product = await _app.FindProductAsync(name);
            Assert.Equal(3, product.Stock);

            await AddToCartAsync(seller, product.ProductCode);
            var cart = await seller.GetStringAsync("/Cart");
            Assert.DoesNotContain(name, cart);
        }

        [Fact]
        public async Task Wishlist_AddsAndRemoves()
        {
            var tent = await _app.FindProductAsync("2-Person Camping Tent");
            var client = await _app.SignInAsync(await _app.CreateUserAsync());

            await client.PostFormAsync($"/Products/Details/{tent.ProductCode}", "/Wishlist/Toggle", new() { ["productCode"] = tent.ProductCode.ToString() });
            Assert.Contains(tent.Name, await client.GetStringAsync("/Wishlist"));

            await client.PostFormAsync("/Wishlist", "/Wishlist/Toggle", new() { ["productCode"] = tent.ProductCode.ToString() });
            Assert.DoesNotContain(tent.Name, await client.GetStringAsync("/Wishlist"));
        }

        [Fact]
        public async Task Deals_ShowOnlyDiscountedListings()
        {
            var html = await _app.CreateClient().GetStringAsync("/Products?dept=deals");
            Assert.Contains("Wireless Noise-Cancelling Headphones", html);
            Assert.DoesNotContain("Smartwatch Series 5", html);
        }

        [Fact]
        public async Task Api_RejectsServices()
        {
            var cleaning = await _app.FindProductAsync("Home Deep Cleaning (3 hours)");
            var response = await _app.CreateClient().PostAsJsonAsync("/order/orders", new
            {
                orderDate = DateTime.UtcNow,
                invoicePrice = cleaning.Price,
                products = new[] { new { productCode = cleaning.ProductCode, price = cleaning.Price, quantity = 1 } }
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("is a service", await response.Content.ReadAsStringAsync());
        }
    }
}
