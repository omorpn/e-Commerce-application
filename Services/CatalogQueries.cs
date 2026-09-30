using e_Commerce_application.Models;
using e_Commerce_application.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Services
{
    public static class CatalogQueries
    {
        // Listings a shopper can see: published, and for downloads, with a file attached.
        public static IQueryable<Product> Listed(this IQueryable<Product> query) =>
            query.Where(p => p.Status == ListingStatus.Published
                && (p.Type == ProductType.Physical || p.Type == ProductType.Service || p.FilePath != null));

        public static IQueryable<Product> Deals(this IQueryable<Product> query) =>
            query.Where(p => p.ListPrice != null && p.ListPrice > p.Price);

        public static IQueryable<ProductSummary> ToSummaries(this IQueryable<Product> query) =>
            query.Select(p => new ProductSummary(p, p.Reviews.Select(r => (double?)r.Rating).Average() ?? 0, p.Reviews.Count));

        public static IQueryable<Product> Search(this IQueryable<Product> query, string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return query;
            }

            var pattern = "%" + text.Trim().ToLower().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            return query.Where(p =>
                EF.Functions.Like(p.Name.ToLower(), pattern, "\\") ||
                EF.Functions.Like((p.AuthorName ?? "").ToLower(), pattern, "\\") ||
                EF.Functions.Like(p.Category.ToLower(), pattern, "\\") ||
                EF.Functions.Like((p.Description ?? "").ToLower(), pattern, "\\"));
        }

        public static async Task<bool> HasPurchasedAsync(this Data.AppDbContext db, string userId, int productCode) =>
            await db.LibraryEntries.AnyAsync(l => l.UserId == userId && l.ProductCode == productCode) ||
            await db.Orders.AnyAsync(o => o.UserId == userId && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.AwaitingPayment
                && o.Products.Any(i => i.ProductCode == productCode));
    }
}
