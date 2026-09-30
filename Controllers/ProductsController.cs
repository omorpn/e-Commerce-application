using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Models.ViewModels;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Controllers
{
    public class ProductsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly CartService _cart;
        private readonly UserManager<ApplicationUser> _users;

        public ProductsController(AppDbContext db, CartService cart, UserManager<ApplicationUser> users)
        {
            _db = db;
            _cart = cart;
            _users = users;
        }

        public async Task<IActionResult> Index([FromQuery] CatalogQuery query)
        {
            var products = _db.Products.AsNoTracking().Listed().Search(query.Q);
            var heading = "All departments";

            switch (query.Dept)
            {
                case "products":
                    products = products.Where(p => p.Type == ProductType.Physical);
                    heading = "Shop products";
                    break;
                case "ebooks":
                    products = products.Where(p => p.Type == ProductType.Ebook);
                    heading = "eBook Store";
                    break;
                default:
                    query.Dept = "all";
                    break;
            }

            var categories = await products.Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync();

            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                products = products.Where(p => p.Category == query.Category);
                heading = query.Category;
            }
            if (query.MinPrice.HasValue)
            {
                products = products.Where(p => p.Price >= query.MinPrice.Value);
            }
            if (query.MaxPrice.HasValue)
            {
                products = products.Where(p => p.Price <= query.MaxPrice.Value);
            }
            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                heading = $"Results for \"{query.Q.Trim()}\"";
            }

            products = query.Sort switch
            {
                "price-asc" => products.OrderBy(p => p.Price).ThenBy(p => p.Name),
                "price-desc" => products.OrderByDescending(p => p.Price).ThenBy(p => p.Name),
                "newest" => products.OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.ProductCode),
                "rating" => products.OrderByDescending(p => p.Reviews.Average(r => (double?)r.Rating) ?? 0).ThenByDescending(p => p.Reviews.Count),
                _ => products.OrderByDescending(p => p.Reviews.Count).ThenByDescending(p => p.PublishedAt).ThenBy(p => p.ProductCode)
            };

            var total = await products.CountAsync();
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)Catalog.PageSize));
            query.Page = Math.Clamp(query.Page, 1, pages);

            var model = new CatalogViewModel
            {
                Query = query,
                TotalCount = total,
                Categories = categories,
                Heading = heading,
                Results = await products.Skip((query.Page - 1) * Catalog.PageSize).Take(Catalog.PageSize).ToSummaries().ToListAsync()
            };
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductCode == id);
            if (product == null)
            {
                return NotFound();
            }

            var userId = _users.GetUserId(User);
            var isSeller = userId != null && product.SellerId == userId;
            var isAdmin = User.IsInRole(Roles.Admin);
            var owned = userId != null && await _db.LibraryEntries.AnyAsync(l => l.UserId == userId && l.ProductCode == id);

            // Unlisted items stay visible to their seller, admins and existing owners only.
            if (!product.IsListed && !isSeller && !isAdmin && !owned)
            {
                return NotFound();
            }

            var reviews = await _db.Reviews.AsNoTracking().Where(r => r.ProductCode == id)
                .OrderByDescending(r => r.CreatedAt).ToListAsync();
            var histogram = new int[5];
            foreach (var review in reviews)
            {
                histogram[review.Rating - 1]++;
            }
            var userReview = userId == null ? null : reviews.FirstOrDefault(r => r.UserId == userId);

            var model = new ProductDetailsViewModel
            {
                Product = product,
                Reviews = reviews,
                ReviewCount = reviews.Count,
                Rating = reviews.Count == 0 ? 0 : reviews.Average(r => r.Rating),
                RatingHistogram = histogram,
                UserReview = userReview,
                Owned = owned,
                IsSeller = isSeller,
                InCart = _cart.GetItems().GetValueOrDefault(id),
                Related = await _db.Products.AsNoTracking().Listed()
                    .Where(p => p.Category == product.Category && p.Type == product.Type && p.ProductCode != id)
                    .OrderByDescending(p => p.Reviews.Count).ToSummaries().Take(4).ToListAsync(),
                ReviewForm = new ReviewInput
                {
                    ProductCode = id,
                    Rating = userReview?.Rating ?? 0,
                    Title = userReview?.Title,
                    Body = userReview?.Body
                }
            };
            return View(model);
        }
    }
}
