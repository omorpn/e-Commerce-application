using e_Commerce_application.Models;
using Microsoft.AspNetCore.Mvc;

namespace e_Commerce_application.Controllers
{
    [Controller]
    [Route("/order")]
    public class OrderController : Controller
    {
        [HttpPost("orders")]
        public ActionResult Order([FromBody] Order order)
        {
            var totalPrice = order.Products?.Sum(e => e.Quantity * e.Price) ?? 0;

            if (Math.Abs(order.InvoicePrice - totalPrice) > 0.005)
            {
                ModelState.AddModelError("TotalPrice", "InvoicePrice doesn't match with the total cost of the specified products in the order.");
            }

            if (!ModelState.IsValid)
            {
                var errors = string.Join("\n", ModelState.Values.SelectMany(e => e.Errors).Select(error => error.ErrorMessage));
                return BadRequest(errors);
            }

            var newOrder = new Order
            {
                OrderNo = Random.Shared.Next(1000, 9999),
                OrderDate = order.OrderDate,
                InvoicePrice = order.InvoicePrice,
                Products = order.Products ?? new List<Product>()
            };

            return Json(newOrder);
        }
    }
}
