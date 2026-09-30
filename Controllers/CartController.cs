using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Controllers
{
    public class CartController : Controller
    {
        private readonly CartService _cart;
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public CartController(CartService cart, AppDbContext db, UserManager<ApplicationUser> users)
        {
            _cart = cart;
            _db = db;
            _users = users;
        }

        public async Task<IActionResult> Index() => View(await _cart.BuildAsync(_users.GetUserId(User)));

        [HttpPost]
        public async Task<IActionResult> Add(int productCode, int quantity = 1, bool buyNow = false, string? returnUrl = null)
        {
            var product = await _db.Products.AsNoTracking().Listed().FirstOrDefaultAsync(p => p.ProductCode == productCode);
            if (product == null)
            {
                this.Error("That item is not available.");
                return this.RedirectToLocal(returnUrl, RedirectToAction(nameof(Index)));
            }

            var userId = _users.GetUserId(User);
            var inCart = _cart.GetItems().GetValueOrDefault(productCode);

            if (product.IsEbook)
            {
                if (userId != null && await _db.LibraryEntries.AnyAsync(l => l.UserId == userId && l.ProductCode == productCode))
                {
                    this.Error($"You already own \"{product.Name}\". Find it in your library.");
                    return this.RedirectToLocal(returnUrl, RedirectToAction(nameof(Index)));
                }
                if (inCart == 0)
                {
                    _cart.Add(productCode, 1);
                }
            }
            else
            {
                if (product.Stock <= inCart)
                {
                    this.Error(product.Stock == 0
                        ? $"\"{product.Name}\" is out of stock."
                        : $"You already have all {product.Stock} available units of \"{product.Name}\" in your cart.");
                    return this.RedirectToLocal(returnUrl, RedirectToAction(nameof(Index)));
                }
                _cart.Add(productCode, Math.Clamp(quantity, 1, product.Stock - inCart));
            }

            if (buyNow)
            {
                return RedirectToAction("Index", "Checkout");
            }

            this.Success($"Added \"{product.Name}\" to your cart.");
            return this.RedirectToLocal(returnUrl, RedirectToAction(nameof(Index)));
        }

        [HttpPost]
        public async Task<IActionResult> Update(int productCode, int quantity)
        {
            var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductCode == productCode);
            if (product == null || quantity <= 0)
            {
                _cart.Remove(productCode);
            }
            else
            {
                _cart.SetQuantity(productCode, product.IsEbook ? 1 : Math.Min(quantity, Math.Max(product.Stock, 1)));
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult Remove(int productCode)
        {
            _cart.Remove(productCode);
            this.Success("Item removed from your cart.");
            return RedirectToAction(nameof(Index));
        }
    }
}
