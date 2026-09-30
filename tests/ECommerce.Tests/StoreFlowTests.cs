using e_Commerce_application.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.RegularExpressions;

namespace ECommerce.Tests
{
    public class StoreFlowTests : IClassFixture<TestAppFactory>
    {
        private readonly TestAppFactory _app;

        public StoreFlowTests(TestAppFactory app) => _app = app;

        [Theory]
        [InlineData("/")]
        [InlineData("/Products")]
        [InlineData("/Products?dept=ebooks&sort=price-desc")]
        [InlineData("/Products?q=bottle")]
        [InlineData("/Products?dept=services")]
        [InlineData("/Products?dept=digital")]
        [InlineData("/Products?dept=deals&sort=discount")]
        [InlineData("/healthz")]
        [InlineData("/Cart")]
        [InlineData("/Home/Privacy")]
        [InlineData("/Identity/Account/Login")]
        [InlineData("/Identity/Account/Register")]
        public async Task PublicPages_Render(string url)
        {
            var response = await _app.CreateClient().GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData("/Checkout")]
        [InlineData("/Orders")]
        [InlineData("/Library")]
        [InlineData("/Sell")]
        [InlineData("/Wishlist")]
        [InlineData("/Admin")]
        public async Task AccountPages_RequireSignIn(string url)
        {
            var response = await _app.CreateBrowser().GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Identity/Account/Login", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task Customer_CanCheckOut_PhysicalAndDigitalItems()
        {
            var headphones = await _app.FindProductAsync("Wireless Noise-Cancelling Headphones");
            var ebook = await _app.FindProductAsync("Learn C# in 30 Days");
            var email = await _app.CreateUserAsync();
            var client = await _app.SignInAsync(email);

            foreach (var (code, qty) in new[] { (headphones.ProductCode, 2), (ebook.ProductCode, 1) })
            {
                var add = await client.PostFormAsync($"/Products/Details/{code}", "/Cart/Add",
                    new() { ["productCode"] = code.ToString(), ["quantity"] = qty.ToString() });
                Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
            }

            var checkout = await client.PostFormAsync("/Checkout", "/Checkout", new()
            {
                ["FullName"] = "Test Customer",
                ["Email"] = email,
                ["Phone"] = "08031234567",
                ["AddressLine"] = "1 Test Road",
                ["City"] = "Ikeja",
                ["State"] = "Lagos",
                ["Country"] = "Nigeria",
                ["PaymentMethod"] = "Card (demo)"
            });
            Assert.Equal(HttpStatusCode.Redirect, checkout.StatusCode);
            var location = checkout.Headers.Location!.ToString();
            var orderNo = int.Parse(Regex.Match(location, @"/Orders/Details/(\d+)").Groups[1].Value);

            var order = await _app.WithDbAsync(db => db.Orders.Include(o => o.Products).FirstAsync(o => o.OrderNo == orderNo));
            Assert.Equal(headphones.Price * 2 + ebook.Price, order.InvoicePrice);
            Assert.Equal(OrderStatus.Pending, order.Status);
            Assert.Equal((await _app.FindProductAsync(headphones.Name)).Stock, headphones.Stock - 2);

            // The ebook is now in the library and downloadable.
            var library = await client.GetStringAsync("/Library");
            Assert.Contains(ebook.Name, library);
            var download = await client.GetAsync($"/Library/Download/{ebook.ProductCode}");
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.StartsWith("%PDF-", await download.Content.ReadAsStringAsync());

            // The customer can cancel before shipping; stock comes back and ebook access is revoked.
            var cancel = await client.PostFormAsync($"/Orders/Details/{orderNo}", $"/Orders/Cancel/{orderNo}", new());
            Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
            Assert.Equal(headphones.Stock, (await _app.FindProductAsync(headphones.Name)).Stock);
            var revoked = await client.GetAsync($"/Library/Download/{ebook.ProductCode}");
            Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode);
            Assert.Contains("AccessDenied", revoked.Headers.Location!.ToString());
        }

        [Fact]
        public async Task Orders_AndDownloads_ArePrivateToTheirOwner()
        {
            var ebook = await _app.FindProductAsync("The Mindful Morning");
            var owner = await _app.SignInAsync(await _app.CreateUserAsync());
            await owner.PostFormAsync($"/Products/Details/{ebook.ProductCode}", "/Cart/Add", new() { ["productCode"] = ebook.ProductCode.ToString() });
            var checkout = await owner.PostFormAsync("/Checkout", "/Checkout", new()
            {
                ["FullName"] = "Owner",
                ["Email"] = "owner@test.local",
                ["Phone"] = "08031234567",
                ["PaymentMethod"] = "Card (demo)"
            });
            var orderUrl = checkout.Headers.Location!.ToString();

            var stranger = await _app.SignInAsync(await _app.CreateUserAsync());
            Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(orderUrl)).StatusCode);
            var download = await stranger.GetAsync($"/Library/Download/{ebook.ProductCode}");
            Assert.Contains("AccessDenied", download.Headers.Location!.ToString());
        }

        [Fact]
        public async Task FreeEbook_CanBeClaimed_AndReviewed()
        {
            var free = await _app.FindProductAsync("Letters from Lagos");
            var client = await _app.SignInAsync(await _app.CreateUserAsync());
            var details = $"/Products/Details/{free.ProductCode}";

            var claim = await client.PostFormAsync(details, $"/Library/Claim/{free.ProductCode}", new());
            Assert.Equal("/Library", claim.Headers.Location!.ToString());

            await client.PostFormAsync(details, "/Reviews/Save", new()
            {
                ["ProductCode"] = free.ProductCode.ToString(),
                ["Rating"] = "4",
                ["Title"] = "Lovely stories"
            });
            var review = await _app.WithDbAsync(db => db.Reviews.FirstAsync(r => r.ProductCode == free.ProductCode));
            Assert.Equal(4, review.Rating);
            Assert.True(review.VerifiedPurchase);
        }

        [Fact]
        public async Task FormPosts_WithoutAntiforgeryToken_AreRejected()
        {
            var response = await _app.CreateClient().PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string> { ["productCode"] = "1" }));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Admin_CanUpdateOrderStatus_AndCustomersCannotReachAdmin()
        {
            var customer = await _app.SignInAsync(await _app.CreateUserAsync());
            var denied = await customer.GetAsync("/Admin/Orders");
            Assert.Contains("AccessDenied", denied.Headers.Location!.ToString());

            var speaker = await _app.FindProductAsync("Portable Bluetooth Speaker");
            await customer.PostFormAsync($"/Products/Details/{speaker.ProductCode}", "/Cart/Add", new() { ["productCode"] = speaker.ProductCode.ToString() });
            var checkout = await customer.PostFormAsync("/Checkout", "/Checkout", new()
            {
                ["FullName"] = "Customer",
                ["Email"] = "c@test.local",
                ["Phone"] = "08031234567",
                ["AddressLine"] = "2 Road",
                ["City"] = "Garki",
                ["State"] = "FCT - Abuja",
                ["Country"] = "Nigeria",
                ["PaymentMethod"] = "Pay on delivery"
            });
            var orderNo = Regex.Match(checkout.Headers.Location!.ToString(), @"(\d+)").Value;

            var admin = await _app.SignInAsync(TestAppFactory.AdminEmail, TestAppFactory.AdminPassword);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Admin")).StatusCode);
            var update = await admin.PostFormAsync($"/Admin/Orders/Details/{orderNo}", $"/Admin/Orders/UpdateStatus/{orderNo}", new() { ["status"] = "Shipped" });
            Assert.Equal(HttpStatusCode.Redirect, update.StatusCode);

            var order = await _app.WithDbAsync(db => db.Orders.FirstAsync(o => o.OrderNo == int.Parse(orderNo)));
            Assert.Equal(OrderStatus.Shipped, order.Status);

            // Once shipped, the customer can no longer cancel.
            await customer.PostFormAsync($"/Orders/Details/{orderNo}", $"/Orders/Cancel/{orderNo}", new());
            order = await _app.WithDbAsync(db => db.Orders.FirstAsync(o => o.OrderNo == int.Parse(orderNo)));
            Assert.Equal(OrderStatus.Shipped, order.Status);
        }
    }
}
