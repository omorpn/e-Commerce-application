using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Controllers
{
    // Chat between customers and sellers. Conversations about store-owned listings
    // (no seller) are answered by the store's admins.
    [Authorize]
    public class MessagesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;
        private readonly NotificationService _notifications;

        public MessagesController(AppDbContext db, UserManager<ApplicationUser> users, NotificationService notifications)
        {
            _db = db;
            _users = users;
            _notifications = notifications;
        }

        private bool IsAdmin => User.IsInRole(Roles.Admin);

        public async Task<IActionResult> Index()
        {
            var userId = _users.GetUserId(User);
            var isAdmin = IsAdmin;
            var conversations = await _db.Conversations.AsNoTracking()
                .Include(c => c.Buyer).Include(c => c.Seller).Include(c => c.Product)
                .Where(c => c.BuyerId == userId || c.SellerId == userId || (isAdmin && c.SellerId == null))
                .OrderByDescending(c => c.UpdatedAt).Take(100).ToListAsync();
            ViewData["UserId"] = userId;
            return View(conversations);
        }

        // Opens (or creates) the conversation with whoever sells a listing.
        [HttpPost]
        public async Task<IActionResult> Start(int productCode)
        {
            var userId = _users.GetUserId(User)!;
            var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductCode == productCode);
            if (product == null)
            {
                return NotFound();
            }
            if (product.SellerId == userId)
            {
                this.Error("This is your own listing.");
                return RedirectToAction("Details", "Products", new { id = productCode });
            }

            var conversation = await _db.Conversations.FirstOrDefaultAsync(c =>
                c.BuyerId == userId && c.SellerId == product.SellerId && c.ProductCode == productCode);
            if (conversation == null)
            {
                conversation = new Conversation { BuyerId = userId, SellerId = product.SellerId, ProductCode = productCode };
                _db.Conversations.Add(conversation);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Thread), new { id = conversation.Id });
        }

        public async Task<IActionResult> Thread(int id)
        {
            var conversation = await LoadAsync(id);
            if (conversation == null)
            {
                return NotFound();
            }

            var userId = _users.GetUserId(User);
            if (conversation.BuyerId == userId && conversation.BuyerUnread)
            {
                conversation.BuyerUnread = false;
                await _db.SaveChangesAsync();
            }
            else if (conversation.BuyerId != userId && conversation.SellerUnread)
            {
                conversation.SellerUnread = false;
                await _db.SaveChangesAsync();
            }

            ViewData["UserId"] = userId;
            if (Request.Query.ContainsKey("partial"))
            {
                return PartialView("_Messages", conversation);
            }
            return View(conversation);
        }

        [HttpPost]
        public async Task<IActionResult> Send(int id, string? body)
        {
            var conversation = await LoadAsync(id);
            if (conversation == null)
            {
                return NotFound();
            }
            if (string.IsNullOrWhiteSpace(body))
            {
                return RedirectToAction(nameof(Thread), new { id });
            }

            var userId = _users.GetUserId(User)!;
            var fromBuyer = conversation.BuyerId == userId;
            conversation.Messages.Add(new ChatMessage { SenderId = userId, Body = body.Trim()[..Math.Min(body.Trim().Length, 2000)] });
            conversation.UpdatedAt = DateTime.UtcNow;
            conversation.SellerUnread = fromBuyer;
            conversation.BuyerUnread = !fromBuyer;
            await _db.SaveChangesAsync();

            var about = conversation.Product?.Name ?? "your conversation";
            var url = Url.Action(nameof(Thread), new { id })!;
            if (!fromBuyer)
            {
                await _notifications.NotifyAsync(conversation.BuyerId, "New message", $"You have a reply about {about}.", url);
            }
            else if (conversation.SellerId != null)
            {
                await _notifications.NotifyAsync(conversation.SellerId, "New message from a customer", $"A customer asked about {about}.", url);
            }
            else
            {
                await _notifications.NotifyAdminsAsync("New customer message", $"A customer asked about {about}.", url);
            }
            return RedirectToAction(nameof(Thread), new { id });
        }

        private async Task<Conversation?> LoadAsync(int id)
        {
            var userId = _users.GetUserId(User);
            var isAdmin = IsAdmin;
            return await _db.Conversations
                .Include(c => c.Buyer).Include(c => c.Seller).Include(c => c.Product)
                .Include(c => c.Messages.OrderBy(m => m.CreatedAt)).ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(c => c.Id == id && (c.BuyerId == userId || c.SellerId == userId || (isAdmin && c.SellerId == null)));
        }
    }
}
