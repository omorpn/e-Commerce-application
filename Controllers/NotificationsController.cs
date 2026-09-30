using e_Commerce_application.Data;
using e_Commerce_application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public NotificationsController(AppDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _users.GetUserId(User);
            var items = await _db.Notifications.AsNoTracking().Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();
            return View(items);
        }

        // Marks one notification read and follows its link.
        public async Task<IActionResult> Open(int id)
        {
            var userId = _users.GetUserId(User);
            var item = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (item == null)
            {
                return NotFound();
            }
            item.IsRead = true;
            await _db.SaveChangesAsync();
            return this.RedirectToLocal(item.Url, RedirectToAction(nameof(Index)));
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = _users.GetUserId(User);
            await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
            return RedirectToAction(nameof(Index));
        }
    }
}
