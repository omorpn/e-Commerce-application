using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models.ViewModels
{
    public class SellerProfileViewModel
    {
        [Required, StringLength(100), Display(Name = "Shop / pen name")]
        public string SellerName { get; set; } = string.Empty;

        [StringLength(2000), Display(Name = "About you")]
        public string? SellerBio { get; set; }

        [Display(Name = "I own the rights to everything I sell and will fulfil my orders")]
        public bool AcceptTerms { get; set; }
    }

    // Create/edit form for every listing type, used by sellers and store admins.
    public class ListingFormViewModel
    {
        public int? ProductCode { get; set; }

        public ProductType Type { get; set; }

        [Required, StringLength(150), Display(Name = "Title")]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Subtitle { get; set; }

        [StringLength(120), Display(Name = "Author name")]
        public string? AuthorName { get; set; }

        [Required, StringLength(4000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string Category { get; set; } = string.Empty;

        public string? Language { get; set; }

        [Range(1, 20000), Display(Name = "Print length (pages)")]
        public int? PageCount { get; set; }

        [Range(0, 100_000_000, ErrorMessage = "Price must be between {1} and {2}")]
        [Display(Name = "Price")]
        public decimal Price { get; set; }

        [Range(0, 100_000_000), Display(Name = "List price before discount (optional)")]
        public decimal? ListPrice { get; set; }

        [DataType(DataType.DateTime), Display(Name = "Flash sale ends (optional)")]
        public DateTime? DealEndsAt { get; set; }

        [Url, StringLength(500), Display(Name = "Or paste an image link (https://...)")]
        public string? ImageUrl { get; set; }

        [Range(0, 100_000), Display(Name = "Units in stock")]
        public int Stock { get; set; }

        [Range(5, 10_080), Display(Name = "Duration (minutes)")]
        public int? DurationMinutes { get; set; }

        [Display(Name = "Where is the service provided?")]
        public ServiceLocation? ServiceLocation { get; set; }

        [StringLength(200), Display(Name = "Area served (optional)")]
        public string? ServiceArea { get; set; }

        [Display(Name = "File customers download")]
        public IFormFile? File { get; set; }

        [Display(Name = "Image / cover (JPG, PNG or WEBP, max 5 MB)")]
        public IFormFile? Image { get; set; }

        [Display(Name = "Remove current image")]
        public bool RemoveImage { get; set; }

        [ValidateNever]
        public string? ExistingFileName { get; set; }

        [ValidateNever]
        public bool HasImage { get; set; }

        [ValidateNever]
        public ListingStatus Status { get; set; }

        [ValidateNever]
        public string? BlockedReason { get; set; }
    }

    public class ListingSalesRow
    {
        public Product Product { get; set; } = null!;
        public int UnitsSold { get; set; }
        public decimal Revenue { get; set; }
        public decimal Earnings { get; set; }
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
    }

    public class SellerDashboardViewModel
    {
        public string SellerName { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public decimal RoyaltyRate { get; set; }
        public decimal SellerRate { get; set; }
        public int OpenOrders { get; set; }
        public List<ListingSalesRow> Listings { get; set; } = new();
        public int TotalUnits => Listings.Sum(t => t.UnitsSold);
        public decimal TotalRevenue => Listings.Sum(t => t.Revenue);
        public decimal TotalEarnings => Listings.Sum(t => t.Earnings);
    }

    public class SellerOrderRow
    {
        public OrderItem Item { get; set; } = null!;
        public Order Order { get; set; } = null!;
    }
}
