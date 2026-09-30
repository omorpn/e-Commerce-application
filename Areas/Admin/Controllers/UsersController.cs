using e_Commerce_application.Controllers;
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
    public class UsersController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public UsersController(AppDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        public async Task<IActionResult> Index()
        {
            var admins = (await _users.GetUsersInRoleAsync(Roles.Admin)).Select(u => u.Id).ToHashSet();
            var sellers = (await _users.GetUsersInRoleAsync(Roles.Seller)).Select(u => u.Id).ToHashSet();
            var orderCounts = await _db.Orders.Where(o => o.UserId != null).GroupBy(o => o.UserId!)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var listingCounts = await _db.Products.Where(p => p.SellerId != null).GroupBy(p => p.SellerId!)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

            var rows = (await _db.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync())
                .Select(u => new AdminUserRow
                {
                    User = u,
                    IsAdmin = admins.Contains(u.Id),
                    IsSeller = sellers.Contains(u.Id),
                    OrderCount = orderCounts.GetValueOrDefault(u.Id),
                    ListingCount = listingCounts.GetValueOrDefault(u.Id)
                }).ToList();
            return View(rows);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAdmin(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            if (user.Id == _users.GetUserId(User))
            {
                this.Error("You can't change your own admin access.");
                return RedirectToAction(nameof(Index));
            }

            if (await _users.IsInRoleAsync(user, Roles.Admin))
            {
                await _users.RemoveFromRoleAsync(user, Roles.Admin);
                this.Success($"{user.Email} is no longer an admin.");
            }
            else
            {
                await _users.AddToRoleAsync(user, Roles.Admin);
                this.Success($"{user.Email} is now an admin.");
            }
            // Role changes apply at the user's next sign-in or security stamp refresh.
            await _users.UpdateSecurityStampAsync(user);
            return RedirectToAction(nameof(Index));
        }
    }
}
