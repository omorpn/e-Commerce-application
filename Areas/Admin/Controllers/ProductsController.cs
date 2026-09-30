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
    // Listings owned and fulfilled by the store itself (any type).
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class ProductsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ListingService _listings;

        public ProductsController(AppDbContext db, ListingService listings)
        {
            _db = db;
            _listings = listings;
        }

        public async Task<IActionResult> Index(string? q, ProductType? type)
        {
            var query = _db.Products.AsNoTracking().Where(p => p.SellerId == null).Search(q);
            if (type.HasValue)
            {
                query = query.Where(p => p.Type == type.Value);
            }
            ViewData["q"] = q;
            ViewData["type"] = type;
            return View(await query.OrderBy(p => p.Type).ThenBy(p => p.Name).ToListAsync());
        }

        public IActionResult Create(ProductType type = ProductType.Physical) =>
            View("~/Views/Sell/Edit.cshtml", new ListingFormViewModel
            {
                Type = type,
                Language = "English",
                Stock = type == ProductType.Physical ? 1 : 0,
                DurationMinutes = type == ProductType.Service ? 60 : null
            });

        [HttpPost]
        [RequestSizeLimit(FileSignatures.MaxUploadRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = FileSignatures.MaxUploadRequestBytes)]
        public async Task<IActionResult> Create(ListingFormViewModel model, bool publishNow = false)
        {
            if (!Enum.IsDefined(model.Type))
            {
                return BadRequest();
            }

            var (fileKind, imageKind) = await _listings.ValidateAsync(model, ModelState, isNew: true);
            if (!ModelState.IsValid)
            {
                return View("~/Views/Sell/Edit.cshtml", model);
            }

            var product = new Product { Status = publishNow ? ListingStatus.Published : ListingStatus.Draft };
            await _listings.ApplyAsync(model, product, fileKind, imageKind);
            if (publishNow)
            {
                product.PublishedAt = DateTime.UtcNow;
            }
            _db.Products.Add(product);
            await _db.SaveChangesAsync();
            this.Success($"Created \"{product.Name}\".");
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var product = await FindAsync(id);
            return product == null ? NotFound() : View("~/Views/Sell/Edit.cshtml", ListingService.ToForm(product));
        }

        [HttpPost]
        [RequestSizeLimit(FileSignatures.MaxUploadRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = FileSignatures.MaxUploadRequestBytes)]
        public async Task<IActionResult> Edit(int id, ListingFormViewModel model, bool publishNow = false)
        {
            var product = await FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            model.Type = product.Type;
            var (fileKind, imageKind) = await _listings.ValidateAsync(model, ModelState, isNew: false);
            if (!ModelState.IsValid)
            {
                ListingService.Refill(model, product);
                return View("~/Views/Sell/Edit.cshtml", model);
            }

            var replaced = await _listings.ApplyAsync(model, product, fileKind, imageKind);
            if (publishNow && product.Status == ListingStatus.Draft)
            {
                product.Status = ListingStatus.Published;
                product.PublishedAt ??= DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
            _listings.DeleteFiles(replaced);
            this.Success($"Saved \"{product.Name}\".");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> SetStatus(int id, bool publish)
        {
            var product = await FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            product.Status = publish ? ListingStatus.Published : ListingStatus.Draft;
            if (publish)
            {
                product.PublishedAt ??= DateTime.UtcNow;
            }
            product.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            this.Success(publish ? $"\"{product.Name}\" is visible in the store." : $"\"{product.Name}\" is hidden from the store.");
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
            if (await _db.LibraryEntries.AnyAsync(l => l.ProductCode == id))
            {
                this.Error("Customers already own this item, so it can't be deleted. Hide it instead.");
                return RedirectToAction(nameof(Index));
            }

            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
            _listings.DeleteFiles(new[] { product.FilePath, product.ImagePath });
            this.Success($"Deleted \"{product.Name}\". Past orders keep their line items.");
            return RedirectToAction(nameof(Index));
        }

        private Task<Product?> FindAsync(int id) =>
            _db.Products.FirstOrDefaultAsync(p => p.ProductCode == id && p.SellerId == null);
    }
}
