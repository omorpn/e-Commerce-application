using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Models.ViewModels;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace e_Commerce_application.Controllers
{
    // Self-publishing dashboard for authors.
    [Authorize]
    public class PublishController : Controller
    {
        private const long MaxUploadRequestBytes = FileSignatures.MaxManuscriptBytes + FileSignatures.MaxImageBytes + 1024 * 1024;

        private readonly AppDbContext _db;
        private readonly IFileStorage _storage;
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly PublishingOptions _options;

        public PublishController(AppDbContext db, IFileStorage storage, UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn, IOptions<PublishingOptions> options)
        {
            _db = db;
            _storage = storage;
            _users = users;
            _signIn = signIn;
            _options = options.Value;
        }

        public async Task<IActionResult> Index()
        {
            var user = (await _users.GetUserAsync(User))!;
            if (!User.IsInRole(Roles.Author))
            {
                return RedirectToAction(nameof(Profile));
            }

            var titles = await _db.Products.AsNoTracking().Where(p => p.SellerId == user.Id)
                .OrderByDescending(p => p.UpdatedAt).ToSummaries().ToListAsync();
            var codes = titles.Select(t => t.Product.ProductCode).ToList();
            var sales = await (from item in _db.OrderItems
                               join order in _db.Orders on item.OrderNo equals order.OrderNo
                               where codes.Contains(item.ProductCode) && order.Status != OrderStatus.Cancelled
                               select new { item.ProductCode, item.Quantity, item.Price }).ToListAsync();

            var model = new PublishDashboardViewModel
            {
                PenName = user.PenName ?? user.UserName ?? "Author",
                RoyaltyRate = _options.RoyaltyRate,
                Titles = titles.Select(t =>
                {
                    var titleSales = sales.Where(s => s.ProductCode == t.Product.ProductCode).ToList();
                    var revenue = titleSales.Sum(s => s.Price * s.Quantity);
                    return new TitleSalesRow
                    {
                        Product = t.Product,
                        Rating = t.Rating,
                        ReviewCount = t.ReviewCount,
                        UnitsSold = titleSales.Sum(s => s.Quantity),
                        Revenue = revenue,
                        Royalty = Math.Round(revenue * _options.RoyaltyRate, 2)
                    };
                }).ToList()
            };
            return View(model);
        }

        public async Task<IActionResult> Profile()
        {
            var user = (await _users.GetUserAsync(User))!;
            return View(new AuthorProfileViewModel
            {
                PenName = user.PenName ?? user.DisplayName ?? string.Empty,
                AuthorBio = user.AuthorBio,
                AcceptTerms = User.IsInRole(Roles.Author)
            });
        }

        [HttpPost]
        public async Task<IActionResult> Profile(AuthorProfileViewModel model)
        {
            if (!model.AcceptTerms)
            {
                ModelState.AddModelError(nameof(model.AcceptTerms), "You must accept the publishing terms.");
            }
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = (await _users.GetUserAsync(User))!;
            user.PenName = model.PenName.Trim();
            user.AuthorBio = model.AuthorBio?.Trim();
            await _users.UpdateAsync(user);

            if (!await _users.IsInRoleAsync(user, Roles.Author))
            {
                await _users.AddToRoleAsync(user, Roles.Author);
                // Re-issue the cookie so the new role takes effect immediately.
                await _signIn.RefreshSignInAsync(user);
                this.Success("Welcome to self-publishing! Create your first ebook below.");
                return RedirectToAction(nameof(Create));
            }

            this.Success("Author profile saved.");
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = Roles.Author)]
        public async Task<IActionResult> Create()
        {
            var user = (await _users.GetUserAsync(User))!;
            return View("Edit", new EbookFormViewModel { AuthorName = user.PenName ?? string.Empty });
        }

        [HttpPost]
        [Authorize(Roles = Roles.Author)]
        [RequestSizeLimit(MaxUploadRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadRequestBytes)]
        public async Task<IActionResult> Create(EbookFormViewModel model, bool publishNow = false)
        {
            ValidateChoices(model);
            if (model.Manuscript == null || model.Manuscript.Length == 0)
            {
                ModelState.AddModelError(nameof(model.Manuscript), "Please upload your manuscript.");
            }
            var manuscriptKind = await Uploads.CheckManuscriptAsync(model.Manuscript, nameof(model.Manuscript), ModelState);
            var coverKind = await Uploads.CheckImageAsync(model.Cover, nameof(model.Cover), ModelState);

            if (!ModelState.IsValid)
            {
                return View("Edit", model);
            }

            var manuscript = await _storage.SaveAsync(model.Manuscript!, manuscriptKind!, "ebooks");
            var product = new Product
            {
                Type = ProductType.Ebook,
                SellerId = _users.GetUserId(User),
                Status = ListingStatus.Draft,
                FilePath = manuscript.Key,
                FileName = Path.GetFileName(model.Manuscript!.FileName),
                FileContentType = manuscript.Kind.ContentType,
                FileSize = manuscript.Size
            };
            if (coverKind != null)
            {
                var cover = await _storage.SaveAsync(model.Cover!, coverKind, "covers");
                product.ImagePath = cover.Key;
                product.ImageContentType = cover.Kind.ContentType;
            }
            Apply(model, product);
            if (publishNow)
            {
                product.Status = ListingStatus.Published;
                product.PublishedAt = DateTime.UtcNow;
            }

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            this.Success(publishNow
                ? $"\"{product.Name}\" is now live in the eBook Store."
                : $"\"{product.Name}\" was saved as a draft.");
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = Roles.Author)]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await FindOwnAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            return View(new EbookFormViewModel
            {
                ProductCode = product.ProductCode,
                Title = product.Name,
                Subtitle = product.Subtitle,
                AuthorName = product.AuthorName ?? string.Empty,
                Description = product.Description ?? string.Empty,
                Category = product.Category,
                Language = product.Language ?? "English",
                PageCount = product.PageCount,
                Price = product.Price,
                ExistingFileName = product.FileName,
                HasCover = product.ImagePath != null,
                Status = product.Status,
                BlockedReason = product.BlockedReason
            });
        }

        [HttpPost]
        [Authorize(Roles = Roles.Author)]
        [RequestSizeLimit(MaxUploadRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadRequestBytes)]
        public async Task<IActionResult> Edit(int id, EbookFormViewModel model, bool publishNow = false)
        {
            var product = await FindOwnAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            ValidateChoices(model);
            var manuscriptKind = await Uploads.CheckManuscriptAsync(model.Manuscript, nameof(model.Manuscript), ModelState);
            var coverKind = await Uploads.CheckImageAsync(model.Cover, nameof(model.Cover), ModelState);
            if (!ModelState.IsValid)
            {
                model.ProductCode = id;
                model.ExistingFileName = product.FileName;
                model.HasCover = product.ImagePath != null;
                model.Status = product.Status;
                model.BlockedReason = product.BlockedReason;
                return View(model);
            }

            var oldFiles = new List<string?>();
            if (manuscriptKind != null)
            {
                var manuscript = await _storage.SaveAsync(model.Manuscript!, manuscriptKind, "ebooks");
                oldFiles.Add(product.FilePath);
                product.FilePath = manuscript.Key;
                product.FileName = Path.GetFileName(model.Manuscript!.FileName);
                product.FileContentType = manuscript.Kind.ContentType;
                product.FileSize = manuscript.Size;
            }
            if (coverKind != null)
            {
                var cover = await _storage.SaveAsync(model.Cover!, coverKind, "covers");
                oldFiles.Add(product.ImagePath);
                product.ImagePath = cover.Key;
                product.ImageContentType = cover.Kind.ContentType;
            }
            Apply(model, product);
            if (publishNow && product.Status == ListingStatus.Draft)
            {
                product.Status = ListingStatus.Published;
                product.PublishedAt ??= DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            oldFiles.ForEach(_storage.Delete);

            this.Success($"\"{product.Name}\" was updated.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = Roles.Author)]
        public async Task<IActionResult> SetStatus(int id, bool publish)
        {
            var product = await FindOwnAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            if (product.Status == ListingStatus.Blocked)
            {
                this.Error("This title was taken down by the store team and can't be changed. Contact support for details.");
            }
            else if (publish)
            {
                product.Status = ListingStatus.Published;
                product.PublishedAt ??= DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                this.Success($"\"{product.Name}\" is now live in the eBook Store.");
            }
            else
            {
                product.Status = ListingStatus.Draft;
                product.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                this.Success($"\"{product.Name}\" was unpublished. Existing readers keep access.");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = Roles.Author)]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await FindOwnAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            if (await _db.LibraryEntries.AnyAsync(l => l.ProductCode == id))
            {
                this.Error("Readers already own this title, so it can't be deleted. Unpublish it instead.");
                return RedirectToAction(nameof(Index));
            }

            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
            _storage.Delete(product.FilePath);
            _storage.Delete(product.ImagePath);
            this.Success($"\"{product.Name}\" was deleted.");
            return RedirectToAction(nameof(Index));
        }

        private async Task<Product?> FindOwnAsync(int id)
        {
            var userId = _users.GetUserId(User);
            return await _db.Products.FirstOrDefaultAsync(p => p.ProductCode == id && p.Type == ProductType.Ebook && p.SellerId == userId);
        }

        private void ValidateChoices(EbookFormViewModel model)
        {
            if (!Catalog.EbookCategories.Contains(model.Category))
            {
                ModelState.AddModelError(nameof(model.Category), "Please choose a category.");
            }
            if (!Catalog.Languages.Contains(model.Language))
            {
                ModelState.AddModelError(nameof(model.Language), "Please choose a language.");
            }
        }

        private static void Apply(EbookFormViewModel model, Product product)
        {
            product.Name = model.Title.Trim();
            product.Subtitle = model.Subtitle?.Trim();
            product.AuthorName = model.AuthorName.Trim();
            product.Description = model.Description.Trim();
            product.Category = model.Category;
            product.Language = model.Language;
            product.PageCount = model.PageCount;
            product.Price = Math.Round(model.Price, 2);
            product.UpdatedAt = DateTime.UtcNow;
        }
    }
}
