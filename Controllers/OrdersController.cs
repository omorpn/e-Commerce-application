using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Controllers
{
    // "Your Orders" for the signed-in customer.
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly AppDbContext _db;
        private readonly OrderService _orders;
        private readonly UserManager<ApplicationUser> _users;
        private readonly IPaymentGateway _gateway;
        private readonly ShopSettings _store;

        public OrdersController(AppDbContext db, OrderService orders, UserManager<ApplicationUser> users,
            IPaymentGateway gateway, Microsoft.Extensions.Options.IOptions<ShopSettings> store)
        {
            _db = db;
            _orders = orders;
            _users = users;
            _gateway = gateway;
            _store = store.Value;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _users.GetUserId(User);
            var orders = await _db.Orders.AsNoTracking().Include(o => o.Products)
                .Where(o => o.UserId == userId).OrderByDescending(o => o.OrderDate).ToListAsync();
            return View(orders);
        }

        public async Task<IActionResult> Details(int id, bool placed = false)
        {
            var order = await FindOwnOrderAsync(id);
            if (order == null)
            {
                return NotFound();
            }
            ViewData["Placed"] = placed;
            ViewData["CanPayOnline"] = _gateway.IsConfigured;
            return View(order);
        }

        // Retry an online payment for an order that is still waiting for one.
        [HttpPost]
        public async Task<IActionResult> Pay(int id)
        {
            if (!_gateway.IsConfigured)
            {
                this.Error("Online payments aren't available right now.");
                return RedirectToAction(nameof(Details), new { id });
            }
            var result = await _orders.NewPaymentAttemptAsync(id, _users.GetUserId(User)!);
            if (!result.Succeeded)
            {
                this.Error(string.Join(" ", result.Errors));
                return RedirectToAction(nameof(Details), new { id });
            }
            return await PaymentsController.StartPaymentAsync(this, _gateway, result.Value!, _store.CurrencyCode);
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            var order = await FindOwnOrderAsync(id);
            if (order == null)
            {
                return NotFound();
            }
            if (!OrderService.CustomerCanCancel(order))
            {
                this.Error("This order can no longer be cancelled.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var result = await _orders.UpdateStatusAsync(id, OrderStatus.Cancelled);
            if (result.Succeeded)
            {
                this.Success($"Order #{id} has been cancelled.");
            }
            else
            {
                this.Error(string.Join(" ", result.Errors));
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<Order?> FindOwnOrderAsync(int id)
        {
            var userId = _users.GetUserId(User);
            return await _db.Orders.AsNoTracking().Include(o => o.Products).Include(o => o.Events)
                .FirstOrDefaultAsync(o => o.OrderNo == id && o.UserId == userId);
        }
    }
}
