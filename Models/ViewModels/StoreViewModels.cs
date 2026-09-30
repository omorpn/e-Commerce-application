using e_Commerce_application.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models.ViewModels
{
    public record ProductSummary(Product Product, double Rating, int ReviewCount);

    public class CatalogQuery
    {
        public string? Q { get; set; }

        // "all", "products", "ebooks", "digital", "services" or "deals"
        public string? Dept { get; set; }

        public string? Category { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        // "featured", "price-asc", "price-desc", "newest", "rating", "discount"
        public string? Sort { get; set; }

        public int Page { get; set; } = 1;
    }

    public class CatalogViewModel
    {
        public CatalogQuery Query { get; set; } = new();
        public List<ProductSummary> Results { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Catalog.PageSize));
        public List<string> Categories { get; set; } = new();
        public string Heading { get; set; } = "All departments";
    }

    public class HomeViewModel
    {
        public List<ProductSummary> Deals { get; set; } = new();
        public List<ProductSummary> Featured { get; set; } = new();
        public List<ProductSummary> Services { get; set; } = new();
        public List<ProductSummary> Digital { get; set; } = new();
        public List<ProductSummary> NewEbooks { get; set; } = new();
        public List<ProductSummary> FreeDownloads { get; set; } = new();
        public List<ProductSummary> RecentlyViewed { get; set; } = new();
        public Dictionary<ProductType, int> Counts { get; set; } = new();
    }

    public class ProductDetailsViewModel
    {
        public Product Product { get; set; } = null!;
        public string? SellerName { get; set; }
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
        public int[] RatingHistogram { get; set; } = new int[5];
        public List<Review> Reviews { get; set; } = new();
        public Review? UserReview { get; set; }
        public bool Owned { get; set; }
        public bool IsSeller { get; set; }
        public bool InWishlist { get; set; }
        public int InCart { get; set; }
        public List<ProductSummary> Related { get; set; } = new();
        public ReviewInput ReviewForm { get; set; } = new();
    }

    public class ReviewInput
    {
        public int ProductCode { get; set; }

        [Range(1, 5, ErrorMessage = "Please choose a rating from 1 to 5 stars")]
        public int Rating { get; set; }

        [StringLength(120)]
        public string? Title { get; set; }

        [StringLength(4000)]
        public string? Body { get; set; }
    }

    public class SellerPageViewModel
    {
        public ApplicationUser Seller { get; set; } = null!;
        public List<ProductSummary> Listings { get; set; } = new();
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
    }

    public class CartLine
    {
        public Product Product { get; set; } = null!;
        public int Quantity { get; set; }
        public string? Problem { get; set; }
        public int EffectiveQuantity => Product.IsDownloadable ? 1 : Quantity;
        public decimal LineTotal => Product.Price * EffectiveQuantity;
    }

    public class CartViewModel
    {
        public List<CartLine> Lines { get; set; } = new();
        public int ItemCount => Lines.Sum(l => l.EffectiveQuantity);
        public decimal Subtotal => Lines.Where(l => l.Problem == null).Sum(l => l.LineTotal);
        public bool HasProblems => Lines.Any(l => l.Problem != null);
        public bool HasPhysical => Lines.Any(l => l.Product.IsPhysical);
        public bool HasServices => Lines.Any(l => l.Product.IsService);
        public bool NeedsAddress => Lines.Any(l => l.Product.NeedsCustomerAddress);
        public bool IsEmpty => Lines.Count == 0;
    }

    public class ServiceBookingInput
    {
        public int ProductCode { get; set; }

        [DataType(DataType.Date)]
        public DateTime? Date { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public class CheckoutViewModel
    {
        public const string PayByCard = "Card (demo)";
        public const string PayOnDelivery = "Pay on delivery";

        [Required, StringLength(100), Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Phone, StringLength(30)]
        public string? Phone { get; set; }

        [StringLength(200), Display(Name = "Address")]
        public string? AddressLine { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100), Display(Name = "State / Region")]
        public string? State { get; set; }

        [StringLength(20), Display(Name = "Postal code")]
        public string? PostalCode { get; set; }

        [StringLength(100)]
        public string? Country { get; set; }

        [Required, Display(Name = "Payment method")]
        public string PaymentMethod { get; set; } = PayByCard;

        public List<ServiceBookingInput> Bookings { get; set; } = new();

        [ValidateNever]
        public CartViewModel Cart { get; set; } = new();
    }
}
