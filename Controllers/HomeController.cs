using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Models.ViewModels;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;

        public HomeController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var listed = _db.Products.AsNoTracking().Listed();
            var model = new HomeViewModel
            {
                Featured = await listed.Where(p => p.Type == ProductType.Physical && p.Stock > 0)
                    .OrderByDescending(p => p.Reviews.Count).ThenByDescending(p => p.PublishedAt)
                    .ToSummaries().Take(8).ToListAsync(),
                NewEbooks = await listed.Where(p => p.Type == ProductType.Ebook && p.Price > 0)
                    .OrderByDescending(p => p.PublishedAt).ToSummaries().Take(6).ToListAsync(),
                FreeEbooks = await listed.Where(p => p.Type == ProductType.Ebook && p.Price == 0)
                    .OrderByDescending(p => p.PublishedAt).ToSummaries().Take(6).ToListAsync(),
                ProductCategories = await listed.Where(p => p.Type == ProductType.Physical).Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync(),
                EbookCategories = await listed.Where(p => p.Type == ProductType.Ebook).Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync()
            };
            return View(model);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            ViewData["StatusCode"] = 500;
            return View();
        }

        [Route("Home/Status/{code:int}")]
        public IActionResult Status(int code)
        {
            ViewData["StatusCode"] = code;
            ViewData["OriginalPath"] = HttpContext.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath;
            return View("Error");
        }
    }
}
