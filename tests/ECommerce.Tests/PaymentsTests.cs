using e_Commerce_application.Models;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ECommerce.Tests
{
    // Stands in for Paystack: payments "succeed" for the amount that was started.
    public class FakeGateway : IPaymentGateway
    {
        public ConcurrentDictionary<string, long> Started { get; } = new();
        public string Name => "Paystack";
        public bool IsConfigured => true;

        public Task<PaymentStart> StartAsync(string reference, string email, decimal amount, string currency, string callbackUrl, CancellationToken ct = default)
        {
            Started[reference] = PaystackGateway.ToMinorUnits(amount);
            return Task.FromResult(new PaymentStart(true, $"https://pay.test/{reference}", null));
        }

        public Task<PaymentCheck> VerifyAsync(string reference, CancellationToken ct = default) =>
            Task.FromResult(Started.TryGetValue(reference, out var amount)
                ? new PaymentCheck(true, amount, "NGN", null)
                : new PaymentCheck(false, 0, null, "Declined"));

        public bool IsValidWebhook(string body, string? signature) => signature == "valid";
    }

    public class PaystackAppFactory : TestAppFactory
    {
        public FakeGateway Gateway { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentGateway>(Gateway));
        }
    }

    public class PaymentsTests : IClassFixture<PaystackAppFactory>
    {
        private readonly PaystackAppFactory _app;

        public PaymentsTests(PaystackAppFactory app) => _app = app;

        private static Dictionary<string, string> Checkout(string state, string payment = CheckoutPayment.Paystack) => new()
        {
            ["FullName"] = "Ada Buyer",
            ["Email"] = "ada@test.local",
            ["Phone"] = "08031234567",
            ["AddressLine"] = "3 Allen Avenue",
            ["City"] = "Ikeja",
            ["State"] = state,
            ["Country"] = "Nigeria",
            ["PaymentMethod"] = payment
        };

        private static async Task AddAsync(HttpClient client, int code, int quantity = 1) =>
            await client.PostFormAsync($"/Products/Details/{code}", "/Cart/Add",
                new() { ["productCode"] = code.ToString(), ["quantity"] = quantity.ToString() });

        [Fact]
        public async Task OnlinePayment_ConfirmsOrder_AndDeliversDownloads()
        {
            var speaker = await _app.FindProductAsync("Portable Bluetooth Speaker");
            var course = await _app.FindProductAsync("Freelancer Starter Kit");
            var client = await _app.SignInAsync(await _app.CreateUserAsync());
            await AddAsync(client, speaker.ProductCode);
            await AddAsync(client, course.ProductCode);

            var response = await client.PostFormAsync("/Checkout", "/Checkout", Checkout("Kano"));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var payUrl = response.Headers.Location!.ToString();
            Assert.StartsWith("https://pay.test/SN", payUrl);
            var reference = payUrl["https://pay.test/".Length..];
            var orderNo = OrderService.OrderNoFromReference(reference)!.Value;

            // Before payment: waiting, no download access, stock reserved, default delivery fee.
            var order = await _app.WithDbAsync(db => db.Orders.Include(o => o.Products).FirstAsync(o => o.OrderNo == orderNo));
            Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
            Assert.Equal(4500m, order.ShippingFee);
            Assert.Equal(speaker.Price + course.Price + 4500m, order.InvoicePrice);
            Assert.Equal(speaker.Stock - 1, (await _app.FindProductAsync(speaker.Name)).Stock);
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync($"/Library/Download/{course.ProductCode}")).StatusCode);

            // Paystack sends the customer back.
            var callback = await client.GetAsync($"/Payments/Callback?reference={reference}");
            Assert.Contains($"/Orders/Details/{orderNo}", callback.Headers.Location!.ToString());

            order = await _app.WithDbAsync(db => db.Orders.Include(o => o.Events).FirstAsync(o => o.OrderNo == orderNo));
            Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
            Assert.Equal(OrderStatus.Pending, order.Status);
            Assert.Contains(order.Events, e => e.Title == "Payment received");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Library/Download/{course.ProductCode}")).StatusCode);

            // The webhook for the same payment is idempotent.
            var webhook = new StringContent($"{{\"event\":\"charge.success\",\"data\":{{\"reference\":\"{reference}\",\"amount\":{_app.Gateway.Started[reference]},\"currency\":\"NGN\"}}}}");
            webhook.Headers.Add("x-paystack-signature", "valid");
            Assert.Equal(HttpStatusCode.OK, (await _app.CreateClient().PostAsync("/Payments/Webhook", webhook)).StatusCode);
            Assert.Equal(1, await _app.WithDbAsync(db => db.OrderEvents.CountAsync(e => e.OrderNo == orderNo && e.Title == "Payment received")));

            // The buyer got notifications.
            var notifications = await client.GetStringAsync("/Notifications");
            Assert.Contains($"Payment received for order #{orderNo}", notifications);
        }

        [Fact]
        public async Task Webhook_RejectsBadSignatures_AndUnderpayments()
        {
            var bad = new StringContent("{\"event\":\"charge.success\"}");
            bad.Headers.Add("x-paystack-signature", "forged");
            Assert.Equal(HttpStatusCode.Unauthorized, (await _app.CreateClient().PostAsync("/Payments/Webhook", bad)).StatusCode);

            var watch = await _app.FindProductAsync("Men's Chronograph Leather Watch");
            var client = await _app.SignInAsync(await _app.CreateUserAsync());
            await AddAsync(client, watch.ProductCode);
            var response = await client.PostFormAsync("/Checkout", "/Checkout", Checkout("Lagos"));
            var reference = response.Headers.Location!.ToString()["https://pay.test/".Length..];

            var underpaid = new StringContent($"{{\"event\":\"charge.success\",\"data\":{{\"reference\":\"{reference}\",\"amount\":100,\"currency\":\"NGN\"}}}}");
            underpaid.Headers.Add("x-paystack-signature", "valid");
            await _app.CreateClient().PostAsync("/Payments/Webhook", underpaid);

            var order = await _app.WithDbAsync(db => db.Orders.FirstAsync(o => o.PaymentReference == reference));
            Assert.NotEqual(PaymentStatus.Paid, order.PaymentStatus);
            Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        }

        [Fact]
        public async Task UnpaidOrders_ExpireAndReturnStock()
        {
            var perfume = await _app.FindProductAsync("Floral Bloom Eau de Parfum for Women 50 ml");
            var client = await _app.SignInAsync(await _app.CreateUserAsync());
            await AddAsync(client, perfume.ProductCode, 2);
            await client.PostFormAsync("/Checkout", "/Checkout", Checkout("Lagos"));
            Assert.Equal(perfume.Stock - 2, (await _app.FindProductAsync(perfume.Name)).Stock);

            using (var scope = _app.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<OrderService>().ExpireUnpaidAsync(TimeSpan.Zero);
            }

            Assert.Equal(perfume.Stock, (await _app.FindProductAsync(perfume.Name)).Stock);
            var html = await client.GetStringAsync("/Orders");
            Assert.Contains("Cancelled", html);
        }

        [Fact]
        public async Task Coupon_AndStateDeliveryFee_AreAppliedToTotal()
        {
            var bottle = await _app.FindProductAsync("Stainless Steel Water Bottle");
            var client = await _app.SignInAsync(await _app.CreateUserAsync());
            await AddAsync(client, bottle.ProductCode, 2); // 30,000: over the WELCOME10 minimum, under free delivery

            var tooSmall = await _app.WithDbAsync(db => db.Coupons.FirstAsync(c => c.Code == "WELCOME10"));
            Assert.Equal(20000m, tooSmall.MinSubtotal);

            await client.PostFormAsync("/Cart", "/Cart/ApplyCoupon", new() { ["code"] = "welcome10" });
            Assert.Contains("WELCOME10", await client.GetStringAsync("/Cart"));

            var response = await client.PostFormAsync("/Checkout", "/Checkout", Checkout("Lagos", CheckoutPayment.OnDelivery));
            var orderNo = int.Parse(Regex.Match(response.Headers.Location!.ToString(), @"/Orders/Details/(\d+)").Groups[1].Value);
            var order = await _app.WithDbAsync(db => db.Orders.FirstAsync(o => o.OrderNo == orderNo));

            Assert.Equal(30000m, order.Subtotal);
            Assert.Equal(3000m, order.Discount);
            Assert.Equal(2500m, order.ShippingFee); // Lagos rate
            Assert.Equal(29500m, order.InvoicePrice);
            Assert.Equal("WELCOME10", order.CouponCode);
            Assert.Equal(OrderStatus.Pending, order.Status);
            Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);
        }

        [Fact]
        public async Task InvalidCoupon_IsRejected()
        {
            var client = await _app.SignInAsync(await _app.CreateUserAsync());
            var mount = await _app.FindProductAsync("Car Phone Mount");
            await AddAsync(client, mount.ProductCode);
            await client.PostFormAsync("/Cart", "/Cart/ApplyCoupon", new() { ["code"] = "NOPE" });
            var cart = await client.GetStringAsync("/Cart");
            Assert.DoesNotContain("NOPE applied", cart);
        }

        [Fact]
        public async Task Downloads_CantBePaidOnDelivery()
        {
            var ebook = await _app.FindProductAsync("Naija Kitchen Classics");
            var client = await _app.SignInAsync(await _app.CreateUserAsync());
            await AddAsync(client, ebook.ProductCode);
            var response = await client.PostFormAsync("/Checkout", "/Checkout", Checkout("Lagos", CheckoutPayment.OnDelivery));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Please choose a payment method.", await response.Content.ReadAsStringAsync());
        }
    }

    public class PaystackGatewayTests
    {
        private sealed class StubHandler : HttpMessageHandler
        {
            public HttpRequestMessage? Last { get; private set; }
            public string? LastBody { get; private set; }
            public string Response { get; set; } = "{}";

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Last = request;
                LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Response, Encoding.UTF8, "application/json") };
            }
        }

        private static (PaystackGateway, StubHandler) Create(string secret = "sk_test_123")
        {
            var handler = new StubHandler();
            var gateway = new PaystackGateway(new HttpClient(handler), Options.Create(new PaystackOptions { SecretKey = secret }), NullLogger<PaystackGateway>.Instance);
            return (gateway, handler);
        }

        [Fact]
        public async Task Start_SendsAmountInKobo_WithBearerKey()
        {
            var (gateway, handler) = Create();
            handler.Response = "{\"status\":true,\"data\":{\"authorization_url\":\"https://checkout.paystack.com/abc\"}}";

            var start = await gateway.StartAsync("SN7-abc", "a@b.ng", 1234.5m, "NGN", "https://shop/cb");

            Assert.True(start.Ok);
            Assert.Equal("https://checkout.paystack.com/abc", start.AuthorizationUrl);
            Assert.Equal("https://api.paystack.co/transaction/initialize", handler.Last!.RequestUri!.ToString());
            Assert.Equal("Bearer sk_test_123", handler.Last.Headers.Authorization!.ToString());
            Assert.Contains("\"amount\":123450", handler.LastBody);
            Assert.Contains("\"reference\":\"SN7-abc\"", handler.LastBody);
        }

        [Fact]
        public async Task Verify_ReadsStatusAmountAndCurrency()
        {
            var (gateway, handler) = Create();
            handler.Response = "{\"status\":true,\"data\":{\"status\":\"success\",\"amount\":500000,\"currency\":\"NGN\",\"gateway_response\":\"Approved\"}}";

            var check = await gateway.VerifyAsync("SN7-abc");

            Assert.True(check.Paid);
            Assert.Equal(500000, check.AmountMinor);
            Assert.Equal("NGN", check.Currency);
            Assert.EndsWith("/transaction/verify/SN7-abc", handler.Last!.RequestUri!.ToString());
        }

        [Fact]
        public void Webhook_SignatureIsHmacSha512OfBody()
        {
            var (gateway, _) = Create("sk_live_secret");
            const string body = "{\"event\":\"charge.success\"}";
            var signature = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes("sk_live_secret"), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

            Assert.True(gateway.IsValidWebhook(body, signature));
            Assert.False(gateway.IsValidWebhook(body + " ", signature));
            Assert.False(gateway.IsValidWebhook(body, "zz"));
            Assert.False(Create("").Item1.IsValidWebhook(body, signature));
        }

        [Fact]
        public void References_CarryTheOrderNumber()
        {
            var reference = OrderService.NewPaymentReference(4821);
            Assert.StartsWith("SN4821-", reference);
            Assert.Equal(4821, OrderService.OrderNoFromReference(reference));
            Assert.Null(OrderService.OrderNoFromReference("random"));
        }
    }
}
