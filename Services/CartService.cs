using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace e_Commerce_application.Services
{
    // Shopping cart kept in the session as product code -> quantity.
    public class CartService
    {
        private const string SessionKey = "cart";
        public const int MaxQuantityPerLine = 99;

        private readonly IHttpContextAccessor _accessor;
        private readonly AppDbContext _db;

        public CartService(IHttpContextAccessor accessor, AppDbContext db)
        {
            _accessor = accessor;
            _db = db;
        }

        private ISession Session => _accessor.HttpContext!.Session;

        public Dictionary<int, int> GetItems()
        {
            var json = Session.GetString(SessionKey);
            return string.IsNullOrEmpty(json)
                ? new Dictionary<int, int>()
                : JsonSerializer.Deserialize<Dictionary<int, int>>(json) ?? new Dictionary<int, int>();
        }

        public int Count => GetItems().Values.Sum();

        public void Add(int productCode, int quantity)
        {
            var items = GetItems();
            items.TryGetValue(productCode, out var current);
            Save(items, productCode, current + quantity);
        }

        public void SetQuantity(int productCode, int quantity) => Save(GetItems(), productCode, quantity);

        public void Remove(int productCode) => Save(GetItems(), productCode, 0);

        public void Clear() => Session.Remove(SessionKey);

        public async Task<CartViewModel> BuildAsync(string? userId)
        {
            var items = GetItems();
            var codes = items.Keys.ToList();
            var products = await _db.Products.AsNoTracking()
                .Where(p => codes.Contains(p.ProductCode))
                .ToDictionaryAsync(p => p.ProductCode);

            var owned = userId == null
                ? new HashSet<int>()
                : (await _db.LibraryEntries.Where(l => l.UserId == userId && codes.Contains(l.ProductCode))
                    .Select(l => l.ProductCode).ToListAsync()).ToHashSet();

            var cart = new CartViewModel();
            foreach (var (code, quantity) in items)
            {
                if (!products.TryGetValue(code, out var product))
                {
                    Remove(code);
                    continue;
                }

                var line = new CartLine { Product = product, Quantity = quantity };
                if (!product.IsListed)
                {
                    line.Problem = "This item is no longer available.";
                }
                else if (product.IsEbook && owned.Contains(code))
                {
                    line.Problem = "You already own this ebook.";
                }
                else if (!product.IsEbook && product.Stock < quantity)
                {
                    line.Problem = product.Stock == 0
                        ? "Out of stock."
                        : $"Only {product.Stock} left in stock.";
                }
                cart.Lines.Add(line);
            }

            return cart;
        }

        private void Save(Dictionary<int, int> items, int productCode, int quantity)
        {
            if (quantity <= 0)
            {
                items.Remove(productCode);
            }
            else
            {
                items[productCode] = Math.Min(quantity, MaxQuantityPerLine);
            }
            Session.SetString(SessionKey, JsonSerializer.Serialize(items));
        }
    }
}
