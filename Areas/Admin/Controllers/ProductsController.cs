using e_Commerce_application.Controllers;
using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Models.ViewModels;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Areas.Admin.Controllers
{
    // Store-owned physical products.
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class ProductsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IFileStorage _storage;

        public ProductsController(AppDbContext db, IFileStorage storage)
        {
            _db = db;
            _storage = storage;
        }

        public async Task<IActionResult> Index(string? q, string? category)
        {
            var query = _db.Products.AsNoTracking().Where(p => p.Type == ProductType.Physical).Search(q);
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(p => p.Category == category);
            }
            ViewData["q"] = q;
            ViewData["category"] = category;
            return View(await query.OrderBy(p => p.Name).ToListAsync());
        }

        public IActionResult Create() => View("Edit", new ProductFormViewModel());

        [HttpPost]
        [RequestSizeLimit(FileSignatures.MaxImageBytes + 1024 * 1024)]
        public async Task<IActionResult> Create(ProductFormViewModel model)
        {
            ValidateCategory(model);
            var imageKind = await Uploads.CheckImageAsync(model.Image, nameof(model.Image), ModelState);
            if (!ModelState.IsValid)
            {
                return View("Edit", model);
            }

            var product = new Product { Type = ProductType.Physical, CreatedAt = DateTime.UtcNow };
            Apply(model, product);
            if (imageKind != null)
            {
                var image = await _storage.SaveAsync(model.Image!, imageKind, "products");
                product.ImagePath = image.Key;
                product.ImageContentType = image.Kind.ContentType;
            }

            _db.Products.Add(product);
            await _db.SaveChangesAsync();
            this.Success($"Created \"{product.Name}\".");
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var product = await FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            return View(new ProductFormViewModel
            {
                ProductCode = product.ProductCode,
                Name = product.Name,
                Description = product.Description,
                Category = product.Category,
                Price = product.Price,
                Stock = product.Stock,
                Published = product.Status == ListingStatus.Published,
                HasImage = product.ImagePath != null
            });
        }

        [HttpPost]
        [RequestSizeLimit(FileSignatures.MaxImageBytes + 1024 * 1024)]
        public async Task<IActionResult> Edit(int id, ProductFormViewModel model)
        {
            var product = await FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            ValidateCategory(model);
            var imageKind = await Uploads.CheckImageAsync(model.Image, nameof(model.Image), ModelState);
            if (!ModelState.IsValid)
            {
                model.ProductCode = id;
                model.HasImage = product.ImagePath != null;
                return View(model);
            }

            var oldImage = product.ImagePath;
            Apply(model, product);
            if (imageKind != null)
            {
                var image = await _storage.SaveAsync(model.Image!, imageKind, "products");
                product.ImagePath = image.Key;
                product.ImageContentType = image.Kind.ContentType;
            }
            else if (model.RemoveImage)
            {
                product.ImagePath = null;
                product.ImageContentType = null;
            }

            await _db.SaveChangesAsync();
            if (oldImage != product.ImagePath)
            {
                _storage.Delete(oldImage);
            }
            this.Success($"Saved \"{product.Name}\".");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
            _storage.Delete(product.ImagePath);
            this.Success($"Deleted \"{product.Name}\". Past orders keep their line items.");
            return RedirectToAction(nameof(Index));
        }

        private Task<Product?> FindAsync(int id) =>
            _db.Products.FirstOrDefaultAsync(p => p.ProductCode == id && p.Type == ProductType.Physical);

        private void ValidateCategory(ProductFormViewModel model)
        {
            if (!Catalog.ProductCategories.Contains(model.Category))
            {
                ModelState.AddModelError(nameof(model.Category), "Please choose a category.");
            }
        }

        private static void Apply(ProductFormViewModel model, Product product)
        {
            product.Name = model.Name.Trim();
            product.Description = model.Description?.Trim();
            product.Category = model.Category;
            product.Price = Math.Round(model.Price, 2);
            product.Stock = model.Stock;
            product.Status = model.Published ? ListingStatus.Published : ListingStatus.Draft;
            if (model.Published)
            {
                product.PublishedAt ??= DateTime.UtcNow;
            }
            product.UpdatedAt = DateTime.UtcNow;
        }
    }
}
