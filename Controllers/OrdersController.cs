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

        public OrdersController(AppDbContext db, OrderService orders, UserManager<ApplicationUser> users)
        {
            _db = db;
            _orders = orders;
            _users = users;
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
            return View(order);
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
            return await _db.Orders.AsNoTracking().Include(o => o.Products)
                .FirstOrDefaultAsync(o => o.OrderNo == id && o.UserId == userId);
        }
    }
}
