using e_Commerce_application.Controllers;
using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Areas.Admin.Controllers
{
    // Moderation of listings created by sellers.
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class ListingsController : Controller
    {
        private readonly AppDbContext _db;

        public ListingsController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index(string? q, ListingStatus? status, ProductType? type)
        {
            var query = _db.Products.AsNoTracking().Include(p => p.Seller).Where(p => p.SellerId != null).Search(q);
            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }
            if (type.HasValue)
            {
                query = query.Where(p => p.Type == type.Value);
            }
            ViewData["q"] = q;
            ViewData["status"] = status;
            ViewData["type"] = type;
            return View(await query.OrderByDescending(p => p.UpdatedAt).Take(300).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Block(int id, string? reason)
        {
            var product = await FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            product.Status = ListingStatus.Blocked;
            product.BlockedReason = string.IsNullOrWhiteSpace(reason) ? "Removed for violating the marketplace guidelines." : reason.Trim();
            product.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            this.Success($"\"{product.Name}\" was taken down.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Unblock(int id)
        {
            var product = await FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // Returned to draft so the seller decides when to republish.
            product.Status = ListingStatus.Draft;
            product.BlockedReason = null;
            product.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            this.Success($"\"{product.Name}\" was reinstated as a draft.");
            return RedirectToAction(nameof(Index));
        }

        private Task<Product?> FindAsync(int id) =>
            _db.Products.FirstOrDefaultAsync(p => p.ProductCode == id && p.SellerId != null);
    }
}
