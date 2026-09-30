using e_Commerce_application.Models;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace e_Commerce_application.Controllers
{
    // Paystack redirects the customer back here and also calls the webhook server-to-server.
    [AllowAnonymous]
    public class PaymentsController : Controller
    {
        private readonly IPaymentGateway _gateway;
        private readonly OrderService _orders;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(IPaymentGateway gateway, OrderService orders, ILogger<PaymentsController> logger)
        {
            _gateway = gateway;
            _orders = orders;
            _logger = logger;
        }

        public static async Task<IActionResult> StartPaymentAsync(Controller controller, IPaymentGateway gateway, Order order, string currency)
        {
            var callback = controller.Url.Action("Callback", "Payments", null, controller.Request.Scheme)!;
            var start = await gateway.StartAsync(order.PaymentReference!, order.Email ?? "", order.InvoicePrice, currency, callback);
            if (start.Ok && start.AuthorizationUrl != null)
            {
                return controller.Redirect(start.AuthorizationUrl);
            }
            controller.Error($"We couldn't start the payment: {start.Error} You can try again from this page.");
            return controller.RedirectToAction("Details", "Orders", new { id = order.OrderNo });
        }

        // The customer's browser returns here after paying (or giving up) on Paystack.
        public async Task<IActionResult> Callback(string? reference)
        {
            var orderNo = OrderService.OrderNoFromReference(reference);
            if (orderNo == null)
            {
                return NotFound();
            }

            var check = await _gateway.VerifyAsync(reference!);
            if (check.Paid)
            {
                var result = await _orders.MarkPaidAsync(reference!, check.AmountMinor, check.Currency);
                if (result.Succeeded)
                {
                    return RedirectToAction("Details", "Orders", new { id = orderNo, placed = true });
                }
                this.Error(string.Join(" ", result.Errors));
            }
            else
            {
                await _orders.MarkPaymentFailedAsync(reference!, check.Error);
                this.Error($"Your payment was not completed{(string.IsNullOrEmpty(check.Error) ? "." : ": " + check.Error)} You can try again.");
            }
            return RedirectToAction("Details", "Orders", new { id = orderNo });
        }

        // Paystack's server-to-server notification; confirms payments even if the customer closes the tab.
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Webhook()
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            if (!_gateway.IsValidWebhook(body, Request.Headers["x-paystack-signature"]))
            {
                return Unauthorized("Invalid signature");
            }

            try
            {
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;
                if (root.GetProperty("event").GetString() == "charge.success")
                {
                    var data = root.GetProperty("data");
                    var reference = data.GetProperty("reference").GetString()!;
                    var result = await _orders.MarkPaidAsync(reference, data.GetProperty("amount").GetInt64(), data.GetProperty("currency").GetString());
                    if (!result.Succeeded)
                    {
                        _logger.LogWarning("Webhook payment {Reference} not applied: {Errors}", reference, string.Join("; ", result.Errors));
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Unreadable Paystack webhook");
                return BadRequest("Unreadable payload");
            }
            return Ok();
        }
    }
}
