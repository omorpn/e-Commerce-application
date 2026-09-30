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
    // Seller Central: anyone can sell products, ebooks, digital downloads and services.
    [Authorize]
    public class SellController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ListingService _listings;
        private readonly OrderService _orders;
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly PublishingOptions _options;

        public SellController(AppDbContext db, ListingService listings, OrderService orders, UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn, IOptions<PublishingOptions> options)
        {
            _db = db;
            _listings = listings;
            _orders = orders;
            _users = users;
            _signIn = signIn;
            _options = options.Value;
        }

        public async Task<IActionResult> Index()
        {
            if (!User.IsInRole(Roles.Seller))
            {
                return RedirectToAction(nameof(Profile));
            }

            var user = (await _users.GetUserAsync(User))!;
            var listings = await _db.Products.AsNoTracking().Where(p => p.SellerId == user.Id)
                .OrderByDescending(p => p.UpdatedAt).ToSummaries().ToListAsync();
            var sales = await (from item in _db.OrderItems
                               join order in _db.Orders on item.OrderNo equals order.OrderNo
                               where item.SellerId == user.Id && order.Status != OrderStatus.Cancelled
                               select new { item.ProductCode, item.ProductType, item.Quantity, item.Price, item.Fulfilled }).ToListAsync();

            var model = new SellerDashboardViewModel
            {
                SellerName = user.SellerName ?? user.UserName ?? "Seller",
                SellerId = user.Id,
                RoyaltyRate = _options.RoyaltyRate,
                SellerRate = _options.SellerRate,
                OpenOrders = sales.Count(s => !s.Fulfilled),
                Listings = listings.Select(l =>
                {
                    var lineSales = sales.Where(s => s.ProductCode == l.Product.ProductCode).ToList();
                    var revenue = lineSales.Sum(s => s.Price * s.Quantity);
                    return new ListingSalesRow
                    {
                        Product = l.Product,
                        Rating = l.Rating,
                        ReviewCount = l.ReviewCount,
                        UnitsSold = lineSales.Sum(s => s.Quantity),
                        Revenue = revenue,
                        Earnings = Math.Round(revenue * _options.RateFor(l.Product.Type), 2)
                    };
                }).ToList()
            };
            return View(model);
        }

        public async Task<IActionResult> Profile()
        {
            var user = (await _users.GetUserAsync(User))!;
            return View(new SellerProfileViewModel
            {
                SellerName = user.SellerName ?? user.DisplayName ?? string.Empty,
                SellerBio = user.SellerBio,
                AcceptTerms = User.IsInRole(Roles.Seller)
            });
        }

        [HttpPost]
        public async Task<IActionResult> Profile(SellerProfileViewModel model)
        {
            if (!model.AcceptTerms)
            {
                ModelState.AddModelError(nameof(model.AcceptTerms), "You must accept the seller terms.");
            }
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = (await _users.GetUserAsync(User))!;
            user.SellerName = model.SellerName.Trim();
            user.SellerBio = model.SellerBio?.Trim();
            await _users.UpdateAsync(user);

            if (!await _users.IsInRoleAsync(user, Roles.Seller))
            {
                await _users.AddToRoleAsync(user, Roles.Seller);
                // Re-issue the cookie so the new role takes effect immediately.
                await _signIn.RefreshSignInAsync(user);
                this.Success("Welcome to Seller Central! Create your first listing below.");
                return RedirectToAction(nameof(Create));
            }

            this.Success("Seller profile saved.");
            return RedirectToAction(nameof(Index));
        }

        // Without a type, asks what the seller wants to list.
        [Authorize(Roles = Roles.Seller)]
        public async Task<IActionResult> Create(ProductType? type)
        {
            if (type == null || !Enum.IsDefined(type.Value))
            {
                return View("ChooseType");
            }

            var user = (await _users.GetUserAsync(User))!;
            return View("Edit", new ListingFormViewModel
            {
                Type = type.Value,
                AuthorName = type == ProductType.Ebook ? user.SellerName : null,
                Language = "English",
                Price = type == ProductType.Ebook ? 4.99m : 0,
                Stock = type == ProductType.Physical ? 1 : 0,
                DurationMinutes = type == ProductType.Service ? 60 : null
            });
        }

        [HttpPost]
        [Authorize(Roles = Roles.Seller)]
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
                return View("Edit", model);
            }

            var product = new Product { SellerId = _users.GetUserId(User), Status = ListingStatus.Draft };
            await _listings.ApplyAsync(model, product, fileKind, imageKind);
            if (publishNow)
            {
                product.Status = ListingStatus.Published;
                product.PublishedAt = DateTime.UtcNow;
            }

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            this.Success(publishNow
                ? $"\"{product.Name}\" is now live in the store."
                : $"\"{product.Name}\" was saved as a draft.");
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = Roles.Seller)]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await FindOwnAsync(id);
            return product == null ? NotFound() : View(ListingService.ToForm(product));
        }

        [HttpPost]
        [Authorize(Roles = Roles.Seller)]
        [RequestSizeLimit(FileSignatures.MaxUploadRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = FileSignatures.MaxUploadRequestBytes)]
        public async Task<IActionResult> Edit(int id, ListingFormViewModel model, bool publishNow = false)
        {
            var product = await FindOwnAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // The listing type can't change after creation.
            model.Type = product.Type;
            var (fileKind, imageKind) = await _listings.ValidateAsync(model, ModelState, isNew: false);
            if (!ModelState.IsValid)
            {
                ListingService.Refill(model, product);
                return View(model);
            }

            var replaced = await _listings.ApplyAsync(model, product, fileKind, imageKind);
            if (publishNow && product.Status == ListingStatus.Draft)
            {
                product.Status = ListingStatus.Published;
                product.PublishedAt ??= DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
            _listings.DeleteFiles(replaced);

            this.Success($"\"{product.Name}\" was updated.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = Roles.Seller)]
        public async Task<IActionResult> SetStatus(int id, bool publish)
        {
            var product = await FindOwnAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            if (product.Status == ListingStatus.Blocked)
            {
                this.Error("This listing was taken down by the store team and can't be changed. Contact support for details.");
            }
            else
            {
                product.Status = publish ? ListingStatus.Published : ListingStatus.Draft;
                if (publish)
                {
                    product.PublishedAt ??= DateTime.UtcNow;
                }
                product.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                this.Success(publish
                    ? $"\"{product.Name}\" is now live in the store."
                    : $"\"{product.Name}\" was unpublished. Existing customers keep access.");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = Roles.Seller)]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await FindOwnAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            if (await _db.LibraryEntries.AnyAsync(l => l.ProductCode == id))
            {
                this.Error("Customers already own this item, so it can't be deleted. Unpublish it instead.");
                return RedirectToAction(nameof(Index));
            }

            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
            _listings.DeleteFiles(new[] { product.FilePath, product.ImagePath });
            this.Success($"\"{product.Name}\" was deleted.");
            return RedirectToAction(nameof(Index));
        }

        // Order lines the seller has to ship or perform.
        [Authorize(Roles = Roles.Seller)]
        public async Task<IActionResult> Orders(bool all = false)
        {
            var userId = _users.GetUserId(User);
            var query = from item in _db.OrderItems
                        join order in _db.Orders on item.OrderNo equals order.OrderNo
                        where item.SellerId == userId && order.Status != OrderStatus.Cancelled
                            && (item.ProductType == ProductType.Physical || item.ProductType == ProductType.Service)
                        select new SellerOrderRow { Item = item, Order = order };
            if (!all)
            {
                query = query.Where(r => !r.Item.Fulfilled);
            }
            ViewData["All"] = all;
            return View(await query.OrderBy(r => r.Item.Fulfilled).ThenBy(r => r.Item.ServiceDate ?? r.Order.OrderDate).Take(200).ToListAsync());
        }

        [HttpPost]
        [Authorize(Roles = Roles.Seller)]
        public async Task<IActionResult> Fulfil(int id)
        {
            var result = await _orders.MarkFulfilledAsync(id, _users.GetUserId(User)!);
            if (result.Succeeded)
            {
                this.Success($"Order #{result.Value!.OrderNo} updated. The customer can see the new status.");
            }
            else
            {
                this.Error(string.Join(" ", result.Errors));
            }
            return RedirectToAction(nameof(Orders));
        }

        private async Task<Product?> FindOwnAsync(int id)
        {
            var userId = _users.GetUserId(User);
            return await _db.Products.FirstOrDefaultAsync(p => p.ProductCode == id && p.SellerId == userId);
        }
    }
}
