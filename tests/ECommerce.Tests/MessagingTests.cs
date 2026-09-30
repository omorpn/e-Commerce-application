using e_Commerce_application.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace ECommerce.Tests
{
    public class MessagingTests : IClassFixture<TestAppFactory>
    {
        private readonly TestAppFactory _app;

        public MessagingTests(TestAppFactory app) => _app = app;

        [Fact]
        public async Task BuyerAndSeller_CanChat_AndOthersCantRead()
        {
            var seller = await PublishingTests.BecomeSellerAsync(_app);
            var name = "Handmade Beads " + Guid.NewGuid().ToString("N")[..6];
            await seller.PostFormAsync("/Sell/Create?type=Physical", "/Sell/Create", new()
            {
                ["Type"] = "Physical",
                ["Name"] = name,
                ["Description"] = "Colourful beads.",
                ["Category"] = "Jewelry",
                ["Stock"] = "5",
                ["Price"] = "8000",
                ["publishNow"] = "true"
            });
            var product = await _app.FindProductAsync(name);

            var buyer = await _app.SignInAsync(await _app.CreateUserAsync());
            var start = await buyer.PostFormAsync($"/Products/Details/{product.ProductCode}", "/Messages/Start",
                new() { ["productCode"] = product.ProductCode.ToString() });
            var threadUrl = start.Headers.Location!.ToString();
            Assert.Contains("/Messages/Thread/", threadUrl);

            await buyer.PostFormAsync(threadUrl, threadUrl.Replace("Thread", "Send"), new() { ["body"] = "Do you have red ones?" });

            // The seller sees an unread badge, the message and can reply.
            Assert.Contains("message-badge", await seller.GetStringAsync("/"));
            Assert.Contains("Do you have red ones?", await seller.GetStringAsync(threadUrl));
            await seller.PostFormAsync(threadUrl, threadUrl.Replace("Thread", "Send"), new() { ["body"] = "Yes, in stock!" });
            Assert.Contains("Yes, in stock!", await buyer.GetStringAsync(threadUrl));
            Assert.Contains("New message", await buyer.GetStringAsync("/Notifications"));

            var stranger = await _app.SignInAsync(await _app.CreateUserAsync());
            Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(threadUrl)).StatusCode);
        }

        [Fact]
        public async Task QuestionsAboutStoreProducts_ReachAdmins()
        {
            var watch = await _app.FindProductAsync("Smartwatch Series 5");
            var buyer = await _app.SignInAsync(await _app.CreateUserAsync());
            var start = await buyer.PostFormAsync($"/Products/Details/{watch.ProductCode}", "/Messages/Start",
                new() { ["productCode"] = watch.ProductCode.ToString() });
            var threadUrl = start.Headers.Location!.ToString();
            await buyer.PostFormAsync(threadUrl, threadUrl.Replace("Thread", "Send"), new() { ["body"] = "Is it waterproof?" });

            var admin = await _app.SignInAsync(TestAppFactory.AdminEmail, TestAppFactory.AdminPassword);
            Assert.Contains("Is it waterproof?", await admin.GetStringAsync(threadUrl));
            Assert.True(await _app.WithDbAsync(db => db.Notifications.AnyAsync(n => n.Title == "New customer message")));
        }
    }
}
