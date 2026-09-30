using e_Commerce_application.Data;
using e_Commerce_application.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;

namespace e_Commerce_application.Services
{
    // In-app notifications (the bell in the header), copied by email when email is configured.
    public class NotificationService
    {
        private readonly AppDbContext _db;
        private readonly IEmailSender _email;
        private readonly IHttpContextAccessor _http;
        private readonly ShopSettings _store;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(AppDbContext db, IEmailSender email, IHttpContextAccessor http,
            IOptions<ShopSettings> store, ILogger<NotificationService> logger)
        {
            _db = db;
            _email = email;
            _http = http;
            _store = store.Value;
            _logger = logger;
        }

        public async Task NotifyAsync(string? userId, string title, string message, string? url = null, bool sendEmail = true)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            _db.Notifications.Add(new Notification { UserId = userId, Title = title, Message = message, Url = url });
            await _db.SaveChangesAsync();

            if (!sendEmail)
            {
                return;
            }

            var address = await _db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync();
            if (string.IsNullOrEmpty(address))
            {
                return;
            }

            try
            {
                await _email.SendEmailAsync(address, $"{title} | {_store.Name}", EmailBody(title, message, url));
            }
            catch (Exception ex)
            {
                // Email problems must never break an order or payment.
                _logger.LogWarning(ex, "Could not email notification \"{Title}\" to user {UserId}", title, userId);
            }
        }

        public async Task NotifyManyAsync(IEnumerable<string?> userIds, string title, string message, string? url = null)
        {
            foreach (var userId in userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct())
            {
                await NotifyAsync(userId, title, message, url);
            }
        }

        public async Task NotifyAdminsAsync(string title, string message, string? url = null)
        {
            var adminIds = await (from userRole in _db.UserRoles
                                  join role in _db.Roles on userRole.RoleId equals role.Id
                                  where role.Name == Roles.Admin
                                  select userRole.UserId).ToListAsync();
            await NotifyManyAsync(adminIds, title, message, url);
        }

        private string EmailBody(string title, string message, string? url)
        {
            var request = _http.HttpContext?.Request;
            var link = url != null && request != null ? $"{request.Scheme}://{request.Host}{url}" : null;
            var button = link == null ? "" :
                $"<p><a href=\"{WebUtility.HtmlEncode(link)}\" style=\"background:#ffa41c;color:#111;padding:10px 18px;border-radius:20px;text-decoration:none\">View details</a></p>";
            return $"<div style=\"font-family:Arial,sans-serif;max-width:560px\"><h2 style=\"color:#131921\">{WebUtility.HtmlEncode(title)}</h2>" +
                   $"<p>{WebUtility.HtmlEncode(message)}</p>{button}<p style=\"color:#777;font-size:12px\">{WebUtility.HtmlEncode(_store.Name)}</p></div>";
        }
    }
}
