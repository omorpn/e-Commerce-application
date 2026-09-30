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
            IQueryable<Product> OfType(ProductType type) => listed.Where(p => p.Type == type);

            var model = new HomeViewModel
            {
                Deals = await listed.Deals().Where(p => p.Type != ProductType.Physical || p.Stock > 0)
                    .OrderByDescending(p => (p.ListPrice! - p.Price) / p.ListPrice!).ToSummaries().Take(6).ToListAsync(),
                Featured = await OfType(ProductType.Physical).Where(p => p.Stock > 0)
                    .OrderByDescending(p => p.Reviews.Count).ThenByDescending(p => p.PublishedAt).ToSummaries().Take(6).ToListAsync(),
                Services = await OfType(ProductType.Service)
                    .OrderByDescending(p => p.Reviews.Count).ThenByDescending(p => p.PublishedAt).ToSummaries().Take(6).ToListAsync(),
                Digital = await OfType(ProductType.Digital).Where(p => p.Price > 0)
                    .OrderByDescending(p => p.PublishedAt).ToSummaries().Take(6).ToListAsync(),
                NewEbooks = await OfType(ProductType.Ebook).Where(p => p.Price > 0)
                    .OrderByDescending(p => p.PublishedAt).ToSummaries().Take(6).ToListAsync(),
                FreeDownloads = await listed.Where(p => (p.Type == ProductType.Ebook || p.Type == ProductType.Digital) && p.Price == 0)
                    .OrderByDescending(p => p.PublishedAt).ToSummaries().Take(6).ToListAsync(),
                Counts = await listed.GroupBy(p => p.Type).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count)
            };

            var recent = RecentlyViewed.Get(HttpContext.Session);
            if (recent.Count > 0)
            {
                var viewed = await listed.Where(p => recent.Contains(p.ProductCode)).ToSummaries().ToListAsync();
                model.RecentlyViewed = viewed.OrderBy(s => recent.IndexOf(s.Product.ProductCode)).Take(6).ToList();
            }

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
