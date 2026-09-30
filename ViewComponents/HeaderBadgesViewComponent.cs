using e_Commerce_application.Data;
using e_Commerce_application.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.ViewComponents
{
    // Unread message and notification counts for the header.
    public class HeaderBadgesViewComponent : ViewComponent
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public HeaderBadgesViewComponent(AppDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = _users.GetUserId(UserClaimsPrincipal);
            var isAdmin = UserClaimsPrincipal.IsInRole(Roles.Admin);
            var notifications = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            var messages = await _db.Conversations.CountAsync(c =>
                (c.BuyerId == userId && c.BuyerUnread)
                || (c.BuyerId != userId && c.SellerUnread && (c.SellerId == userId || (isAdmin && c.SellerId == null))));
            return View((notifications, messages));
        }
    }
}
