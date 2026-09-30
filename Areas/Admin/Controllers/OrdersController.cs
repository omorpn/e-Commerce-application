using e_Commerce_application.Controllers;
using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class OrdersController : Controller
    {
        private readonly AppDbContext _db;
        private readonly OrderService _orders;

        public OrdersController(AppDbContext db, OrderService orders)
        {
            _db = db;
            _orders = orders;
        }

        public async Task<IActionResult> Index(string? q, OrderStatus? status)
        {
            var query = _db.Orders.AsNoTracking().Include(o => o.Products).AsQueryable();
            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }
            if (!string.IsNullOrWhiteSpace(q))
            {
                var text = q.Trim();
                query = int.TryParse(text.TrimStart('#'), out var orderNo)
                    ? query.Where(o => o.OrderNo == orderNo)
                    : query.Where(o => (o.Email != null && o.Email.Contains(text)) || (o.CustomerName != null && o.CustomerName.Contains(text)));
            }
            ViewData["q"] = q;
            ViewData["status"] = status;
            return View(await query.OrderByDescending(o => o.OrderDate).Take(200).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _db.Orders.AsNoTracking().Include(o => o.Products).Include(o => o.User).Include(o => o.Events)
                .FirstOrDefaultAsync(o => o.OrderNo == id);
            if (order == null)
            {
                return NotFound();
            }
            var sellerIds = order.Products.Where(i => i.SellerId != null).Select(i => i.SellerId).Distinct().ToList();
            ViewData["Sellers"] = await _db.Users.Where(u => sellerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.SellerName ?? u.Email ?? u.Id);
            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, OrderStatus status, string? note)
        {
            var result = await _orders.UpdateStatusAsync(id, status, note);
            if (result.Succeeded)
            {
                this.Success($"Order #{id} updated. The customer has been notified.");
            }
            else
            {
                this.Error(string.Join(" ", result.Errors));
            }
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
