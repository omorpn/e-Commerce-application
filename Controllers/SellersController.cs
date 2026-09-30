using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Models.ViewModels;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Controllers
{
    // Public storefront page for each seller.
    public class SellersController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public SellersController(AppDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        public async Task<IActionResult> Details(string id)
        {
            var seller = await _users.FindByIdAsync(id);
            if (seller?.SellerName == null)
            {
                return NotFound();
            }

            var listings = await _db.Products.AsNoTracking().Listed().Where(p => p.SellerId == id)
                .OrderByDescending(p => p.PublishedAt).ToSummaries().ToListAsync();
            var ratings = await _db.Reviews.Where(r => r.Product!.SellerId == id).Select(r => r.Rating).ToListAsync();

            return View(new SellerPageViewModel
            {
                Seller = seller,
                Listings = listings,
                ReviewCount = ratings.Count,
                Rating = ratings.Count == 0 ? 0 : ratings.Average()
            });
        }
    }
}
