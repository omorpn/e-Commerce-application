using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class DashboardController : Controller
    {
        private const int LowStockThreshold = 5;

        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public DashboardController(AppDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        public async Task<IActionResult> Index()
        {
            var activeTotals = await _db.Orders.Where(o => o.Status != OrderStatus.Cancelled).Select(o => o.InvoicePrice).ToListAsync();
            var physical = _db.Products.Where(p => p.Type == ProductType.Physical);

            var model = new AdminDashboardViewModel
            {
                OrderCount = await _db.Orders.CountAsync(),
                PendingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Processing),
                Revenue = activeTotals.Sum(),
                ProductCount = await physical.CountAsync(),
                LowStockCount = await physical.CountAsync(p => p.Stock <= LowStockThreshold),
                EbookCount = await _db.Products.CountAsync(p => p.Type == ProductType.Ebook),
                UserCount = await _db.Users.CountAsync(),
                AuthorCount = (await _users.GetUsersInRoleAsync(Roles.Author)).Count,
                RecentOrders = await _db.Orders.AsNoTracking().Include(o => o.Products).OrderByDescending(o => o.OrderDate).Take(8).ToListAsync(),
                LowStock = await physical.AsNoTracking().Where(p => p.Stock <= LowStockThreshold).OrderBy(p => p.Stock).Take(8).ToListAsync()
            };
            return View(model);
        }
    }
}
