using e_Commerce_application.Models;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Mvc;

namespace e_Commerce_application.Controllers
{
    // JSON API for placing orders for physical products.
    [Controller]
    [Route("/order")]
    [IgnoreAntiforgeryToken]
    public class OrderController : Controller
    {
        private readonly OrderService _orders;

        public OrderController(OrderService orders) => _orders = orders;

        [HttpPost("orders")]
        public async Task<ActionResult> Order([FromBody] Order order)
        {
            var products = order.Products ?? new List<OrderItem>();
            var totalPrice = products.Sum(e => e.Quantity * e.Price);

            if (Math.Abs(order.InvoicePrice - totalPrice) > 0.005m)
            {
                ModelState.AddModelError("TotalPrice", OrderService.InvoiceMismatchError);
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ErrorText(ModelState.Values.SelectMany(e => e.Errors).Select(error => error.ErrorMessage)));
            }

            var result = await _orders.PlaceOrderAsync(new PlaceOrderRequest
            {
                OrderDate = order.OrderDate,
                ExpectedTotal = order.InvoicePrice,
                AllowAccountItems = false,
                CustomerName = order.CustomerName,
                Email = order.Email,
                Phone = order.Phone,
                AddressLine = order.AddressLine,
                City = order.City,
                State = order.State,
                PostalCode = order.PostalCode,
                Country = order.Country,
                PaymentMethod = "API",
                Lines = products.Select(p => new OrderLineRequest(p.ProductCode, p.Quantity, p.Price)).ToList()
            });

            if (!result.Succeeded)
            {
                return BadRequest(ErrorText(result.Errors));
            }

            return Json(result.Value);
        }

        private static string ErrorText(IEnumerable<string> errors) => string.Join("\n", errors.Distinct());
    }
}
