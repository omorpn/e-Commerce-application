using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Models.ViewModels;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly CartService _cart;
        private readonly OrderService _orders;
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public CheckoutController(CartService cart, OrderService orders, AppDbContext db, UserManager<ApplicationUser> users)
        {
            _cart = cart;
            _orders = orders;
            _db = db;
            _users = users;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _users.GetUserAsync(User);
            var cart = await _cart.BuildAsync(user!.Id);
            if (cart.IsEmpty)
            {
                return RedirectToAction("Index", "Cart");
            }

            // Pre-fill from the customer's most recent order.
            var last = await _db.Orders.AsNoTracking().Where(o => o.UserId == user.Id && o.AddressLine != null)
                .OrderByDescending(o => o.OrderDate).FirstOrDefaultAsync();

            return View(new CheckoutViewModel
            {
                FullName = last?.CustomerName ?? user.DisplayName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Phone = last?.Phone ?? user.PhoneNumber,
                AddressLine = last?.AddressLine,
                City = last?.City,
                State = last?.State,
                PostalCode = last?.PostalCode,
                Country = last?.Country,
                Cart = cart
            });
        }

        [HttpPost]
        public async Task<IActionResult> Index(CheckoutViewModel model)
        {
            var userId = _users.GetUserId(User)!;
            model.Cart = await _cart.BuildAsync(userId);
            if (model.Cart.IsEmpty)
            {
                return RedirectToAction("Index", "Cart");
            }
            if (model.Cart.HasProblems)
            {
                this.Error("Some items in your cart need attention before you can check out.");
                return RedirectToAction("Index", "Cart");
            }

            if (model.Cart.HasPhysical)
            {
                if (string.IsNullOrWhiteSpace(model.AddressLine)) ModelState.AddModelError(nameof(model.AddressLine), "The Address field is required for shipping.");
                if (string.IsNullOrWhiteSpace(model.City)) ModelState.AddModelError(nameof(model.City), "The City field is required for shipping.");
                if (string.IsNullOrWhiteSpace(model.Country)) ModelState.AddModelError(nameof(model.Country), "The Country field is required for shipping.");
            }

            var allowedPayments = model.Cart.HasPhysical
                ? new[] { CheckoutViewModel.PayByCard, CheckoutViewModel.PayOnDelivery }
                : new[] { CheckoutViewModel.PayByCard };
            if (!allowedPayments.Contains(model.PaymentMethod))
            {
                ModelState.AddModelError(nameof(model.PaymentMethod), "Please choose a valid payment method.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _orders.PlaceOrderAsync(new PlaceOrderRequest
            {
                UserId = userId,
                CustomerName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                Phone = model.Phone,
                AddressLine = model.Cart.HasPhysical ? model.AddressLine : null,
                City = model.Cart.HasPhysical ? model.City : null,
                State = model.Cart.HasPhysical ? model.State : null,
                PostalCode = model.Cart.HasPhysical ? model.PostalCode : null,
                Country = model.Cart.HasPhysical ? model.Country : null,
                PaymentMethod = model.PaymentMethod,
                Lines = model.Cart.Lines.Select(l => new OrderLineRequest(l.Product.ProductCode, l.EffectiveQuantity)).ToList()
            });

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
                model.Cart = await _cart.BuildAsync(userId);
                return View(model);
            }

            _cart.Clear();
            return RedirectToAction("Details", "Orders", new { id = result.Value!.OrderNo, placed = true });
        }
    }
}
