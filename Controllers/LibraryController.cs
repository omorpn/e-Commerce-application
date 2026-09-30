using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace e_Commerce_application.Controllers
{
    // The signed-in user's ebook library and secure downloads.
    [Authorize]
    public class LibraryController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IFileStorage _storage;
        private readonly OrderService _orders;
        private readonly UserManager<ApplicationUser> _users;

        public LibraryController(AppDbContext db, IFileStorage storage, OrderService orders, UserManager<ApplicationUser> users)
        {
            _db = db;
            _storage = storage;
            _orders = orders;
            _users = users;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _users.GetUserId(User);
            var entries = await _db.LibraryEntries.AsNoTracking().Include(l => l.Product)
                .Where(l => l.UserId == userId).OrderByDescending(l => l.AcquiredAt).ToListAsync();
            return View(entries);
        }

        [HttpPost]
        public async Task<IActionResult> Claim(int id)
        {
            var result = await _orders.ClaimFreeAsync(_users.GetUserId(User)!, id);
            if (result.Succeeded)
            {
                this.Success("Added to your library. Happy reading!");
                return RedirectToAction(nameof(Index));
            }
            this.Error(string.Join(" ", result.Errors));
            return RedirectToAction("Details", "Products", new { id });
        }

        // Owners, the seller and admins may download. "read" opens PDFs in the browser.
        public async Task<IActionResult> Download(int id, bool read = false)
        {
            var product = await _db.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductCode == id && (p.Type == ProductType.Ebook || p.Type == ProductType.Digital));
            if (product?.FilePath == null)
            {
                return NotFound();
            }

            var userId = _users.GetUserId(User);
            var allowed = product.SellerId == userId
                || User.IsInRole(Roles.Admin)
                || await _db.LibraryEntries.AnyAsync(l => l.UserId == userId && l.ProductCode == id);
            if (!allowed)
            {
                return Forbid();
            }

            var stream = _storage.OpenRead(product.FilePath);
            if (stream == null)
            {
                return NotFound();
            }

            Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
            var contentType = product.FileContentType ?? "application/octet-stream";
            var fileName = SafeFileName(product.FileName ?? product.Name, contentType);

            if (read && contentType == FileSignatures.Pdf.ContentType)
            {
                Response.Headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue("inline") { FileNameStar = fileName }.ToString();
                return File(stream, contentType);
            }
            return File(stream, contentType, fileName);
        }

        private static string SafeFileName(string name, string contentType)
        {
            var extension = FileSignatures.FromContentType(contentType)?.Extension ?? ".bin";
            var baseName = Path.GetFileNameWithoutExtension(name);
            var cleaned = new string(baseName.Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' ? c : '_').ToArray()).Trim();
            return (cleaned.Length == 0 ? "download" : cleaned) + extension;
        }
    }
}
