using e_Commerce_application.Data;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace e_Commerce_application.Controllers
{
    // Serves product images and ebook covers from private storage.
    public class MediaController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IFileStorage _storage;

        public MediaController(AppDbContext db, IFileStorage storage)
        {
            _db = db;
            _storage = storage;
        }

        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> Image(int id)
        {
            var image = await _db.Products.AsNoTracking().Where(p => p.ProductCode == id)
                .Select(p => new { p.ImagePath, p.ImageContentType }).FirstOrDefaultAsync();
            var stream = _storage.OpenRead(image?.ImagePath);
            if (stream == null)
            {
                return NotFound();
            }

            Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
            return File(stream, image!.ImageContentType ?? "application/octet-stream");
        }
    }
}
