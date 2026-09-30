using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Models.ViewModels;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace e_Commerce_application.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly CartService _cart;
        private readonly OrderService _orders;
        private readonly IPaymentGateway _gateway;
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;
        private readonly ShippingOptions _shipping;
        private readonly ShopSettings _store;

        public CheckoutController(CartService cart, OrderService orders, IPaymentGateway gateway, AppDbContext db,
            UserManager<ApplicationUser> users, IOptions<ShippingOptions> shipping, IOptions<ShopSettings> store)
        {
            _cart = cart;
            _orders = orders;
            _gateway = gateway;
            _db = db;
            _users = users;
            _shipping = shipping.Value;
            _store = store.Value;
        }

        public async Task<IActionResult> Index()
        {
            var user = (await _users.GetUserAsync(User))!;
            var cart = await BuildCartAsync(user.Id);
            if (cart.IsEmpty)
            {
                return RedirectToAction("Index", "Cart");
            }

            // Pre-fill from the customer's most recent order.
            var last = await _db.Orders.AsNoTracking().Where(o => o.UserId == user.Id && o.AddressLine != null)
                .OrderByDescending(o => o.OrderDate).FirstOrDefaultAsync();

            var model = new CheckoutViewModel
            {
                FullName = last?.CustomerName ?? user.DisplayName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Phone = last?.Phone ?? user.PhoneNumber ?? string.Empty,
                AddressLine = last?.AddressLine,
                City = last?.City,
                State = last?.State,
                PostalCode = last?.PostalCode,
                Country = last?.Country ?? "Nigeria",
                Bookings = cart.Lines.Where(l => l.Product.IsService)
                    .Select(l => new ServiceBookingInput { ProductCode = l.Product.ProductCode }).ToList(),
                Cart = cart
            };
            Prepare(model);
            model.PaymentMethod = model.PaymentOptions[0].Value;
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(CheckoutViewModel model)
        {
            var userId = _users.GetUserId(User)!;
            model.Cart = await BuildCartAsync(userId);
            if (model.Cart.IsEmpty)
            {
                return RedirectToAction("Index", "Cart");
            }
            if (model.Cart.HasProblems)
            {
                this.Error("Some items in your cart need attention before you can check out.");
                return RedirectToAction("Index", "Cart");
            }
            if (model.Cart.CouponError != null)
            {
                this.Error(model.Cart.CouponError);
                return RedirectToAction("Index", "Cart");
            }

            // One booking (date + notes) per service in the cart.
            var bookings = model.Cart.Lines.Where(l => l.Product.IsService).Select(l =>
                model.Bookings.FirstOrDefault(b => b.ProductCode == l.Product.ProductCode) ?? new ServiceBookingInput { ProductCode = l.Product.ProductCode }).ToList();
            for (var i = 0; i < bookings.Count; i++)
            {
                if (bookings[i].Date == null)
                {
                    ModelState.AddModelError($"Bookings[{i}].Date", "Please choose a date.");
                }
                else if (bookings[i].Date!.Value.Date < OrderService.EarliestServiceDate)
                {
                    ModelState.AddModelError($"Bookings[{i}].Date", "Choose tomorrow or a later date.");
                }
            }
            model.Bookings = bookings;

            if (model.Cart.NeedsAddress)
            {
                if (string.IsNullOrWhiteSpace(model.AddressLine)) ModelState.AddModelError(nameof(model.AddressLine), "The Street address field is required.");
                if (string.IsNullOrWhiteSpace(model.City)) ModelState.AddModelError(nameof(model.City), "The City / Town field is required.");
                if (string.IsNullOrWhiteSpace(model.State)) ModelState.AddModelError(nameof(model.State), "Please choose your state.");
                if (string.IsNullOrWhiteSpace(model.Country)) ModelState.AddModelError(nameof(model.Country), "The Country field is required.");
            }

            Prepare(model);
            if (!model.PaymentOptions.Any(o => o.Value == model.PaymentMethod))
            {
                ModelState.AddModelError(nameof(model.PaymentMethod), "Please choose a payment method.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var payment = model.PaymentMethod switch
            {
                CheckoutPayment.Paystack => PaymentMode.Online,
                CheckoutPayment.OnDelivery => PaymentMode.OnDelivery,
                _ => PaymentMode.PaidNow
            };
            var result = await _orders.PlaceOrderAsync(new PlaceOrderRequest
            {
                UserId = userId,
                CustomerName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                Phone = model.Phone.Trim(),
                AddressLine = model.Cart.NeedsAddress ? model.AddressLine : null,
                City = model.Cart.NeedsAddress ? model.City : null,
                State = model.Cart.NeedsAddress ? model.State : null,
                PostalCode = model.Cart.NeedsAddress ? model.PostalCode : null,
                Country = model.Cart.NeedsAddress ? model.Country : null,
                PaymentMethod = model.PaymentMethod,
                Payment = payment,
                CouponCode = model.Cart.Coupon?.Code,
                Lines = model.Cart.Lines.Select(l =>
                {
                    var booking = bookings.FirstOrDefault(b => b.ProductCode == l.Product.ProductCode);
                    return new OrderLineRequest(l.Product.ProductCode, l.EffectiveQuantity, null, booking?.Date, booking?.Notes);
                }).ToList()
            });

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
                model.Cart = await BuildCartAsync(userId);
                Prepare(model);
                return View(model);
            }

            _cart.Clear();
            var order = result.Value!;
            if (payment == PaymentMode.Online)
            {
                return await PaymentsController.StartPaymentAsync(this, _gateway, order, _store.CurrencyCode);
            }
            return RedirectToAction("Details", "Orders", new { id = order.OrderNo, placed = true });
        }

        private async Task<CartViewModel> BuildCartAsync(string userId)
        {
            var cart = await _cart.BuildAsync(userId);
            if (_cart.CouponCode != null && !cart.IsEmpty)
            {
                var coupon = await _orders.FindCouponAsync(_cart.CouponCode, cart.Subtotal);
                cart.Coupon = coupon.Value;
                cart.CouponError = coupon.Succeeded ? null : coupon.Errors[0];
            }
            return cart;
        }

        private void Prepare(CheckoutViewModel model)
        {
            model.ShippingFee = model.Cart.HasPhysical ? _shipping.FeeFor(model.State, model.Cart.Subtotal) : 0;

            model.PaymentOptions.Clear();
            model.PaymentOptions.Add(_gateway.IsConfigured
                ? (CheckoutPayment.Paystack, "Pay online with Paystack", "Card, bank transfer or USSD. You'll be taken to Paystack's secure page.")
                : (CheckoutPayment.Demo, "Card (demo)", "Online payments aren't set up on this store yet; no money is charged."));
            if (model.Cart.NeedsAddress && !model.Cart.HasDownloads)
            {
                model.PaymentOptions.Add((CheckoutPayment.OnDelivery, "Pay on delivery", "Pay by cash or transfer when your order arrives."));
            }
        }
    }
}
