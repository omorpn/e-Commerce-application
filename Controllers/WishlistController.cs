using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Controllers
{
    [Authorize]
    public class WishlistController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public WishlistController(AppDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _users.GetUserId(User);
            var codes = await _db.WishlistItems.Where(w => w.UserId == userId)
                .OrderByDescending(w => w.AddedAt).Select(w => w.ProductCode).ToListAsync();
            var items = await _db.Products.AsNoTracking().Where(p => codes.Contains(p.ProductCode)).ToSummaries().ToListAsync();
            return View(items.OrderBy(i => codes.IndexOf(i.Product.ProductCode)).ToList());
        }

        [HttpPost]
        public async Task<IActionResult> Toggle(int productCode, string? returnUrl = null)
        {
            var userId = _users.GetUserId(User)!;
            var existing = await _db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == userId && w.ProductCode == productCode);
            if (existing != null)
            {
                _db.WishlistItems.Remove(existing);
                this.Success("Removed from your wish list.");
            }
            else if (await _db.Products.AnyAsync(p => p.ProductCode == productCode))
            {
                _db.WishlistItems.Add(new WishlistItem { UserId = userId, ProductCode = productCode });
                this.Success("Added to your wish list.");
            }
            await _db.SaveChangesAsync();
            return this.RedirectToLocal(returnUrl, RedirectToAction(nameof(Index)));
        }
    }
}
