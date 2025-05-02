using e_Commerce_application.Models;
using Microsoft.AspNetCore.Mvc;

namespace e_Commerce_application.Controllers
{
    [Controller]
    [Route("/order")]
    public class OrderController:Controller
    {
        [HttpPost("orders")]
        public ActionResult Order(Order order)
        {


            var totalPrice = order.Products.Sum(e => e.Quantity * e.Price);

            if (order.InvoicePrice != totalPrice)
            {
                ModelState.AddModelError("TotalPrice", "InvoicePrice doesn't match with the total cost of the specified products in the order.\r\n\r\n");

            }
            string errors = string.Join("\n", ModelState.Values.SelectMany(e => e.Errors).Select(error => error.ErrorMessage));

            if (ModelState.IsValid)
            {
                Order newOrder = new();

                var rand = Random.Shared.Next(1000, 9999);
                newOrder.OrderNo = rand;
                newOrder.OrderDate = order.OrderDate;
                newOrder.InvoicePrice = order.InvoicePrice;
                newOrder.Products = order.Products;

                return Json(newOrder);
            }

            return BadRequest(errors);


        }


    }
}
