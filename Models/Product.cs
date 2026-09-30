using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace e_Commerce_application.Models
{
    // A catalog listing: a physical product, an ebook, another digital download or a
    // bookable service. Listings are owned by the store (SellerId null) or a seller.
    public class Product
    {
        [Key]
        public int ProductCode { get; set; }

        public ProductType Type { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Subtitle { get; set; }

        [StringLength(4000)]
        public string? Description { get; set; }

        [Required, StringLength(60)]
        public string Category { get; set; } = string.Empty;

        public decimal Price { get; set; }

        // Optional "was" price; when higher than Price the listing shows as a deal.
        public decimal? ListPrice { get; set; }

        // Physical products only.
        public int Stock { get; set; }

        public ListingStatus Status { get; set; } = ListingStatus.Published;

        [StringLength(500)]
        public string? BlockedReason { get; set; }

        // Storage key of the product image / cover.
        public string? ImagePath { get; set; }
        public string? ImageContentType { get; set; }

        // Optional https link to an image hosted elsewhere, used when nothing was uploaded.
        [StringLength(500)]
        public string? ImageUrl { get; set; }

        // End of a flash sale; the listing shows a countdown until then.
        public DateTime? DealEndsAt { get; set; }

        // Ebook metadata
        [StringLength(120)]
        public string? AuthorName { get; set; }

        [StringLength(40)]
        public string? Language { get; set; }

        public int? PageCount { get; set; }

        // Storage key of the downloadable file (ebooks and digital products).
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
        public string? FileContentType { get; set; }
        public long? FileSize { get; set; }

        // Service details
        public int? DurationMinutes { get; set; }
        public ServiceLocation? ServiceLocation { get; set; }

        [StringLength(200)]
        public string? ServiceArea { get; set; }

        public string? SellerId { get; set; }
        public ApplicationUser? Seller { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }

        public List<Review> Reviews { get; set; } = new();

        [NotMapped]
        public bool IsEbook => Type == ProductType.Ebook;

        [NotMapped]
        public bool IsPhysical => Type == ProductType.Physical;

        [NotMapped]
        public bool IsService => Type == ProductType.Service;

        // Ebooks and digital products are delivered as a download.
        [NotMapped]
        public bool IsDownloadable => Type is ProductType.Ebook or ProductType.Digital;

        [NotMapped]
        public bool IsListed => Status == ListingStatus.Published;

        [NotMapped]
        public bool InStock => Type switch
        {
            ProductType.Physical => Stock > 0,
            ProductType.Service => true,
            _ => FilePath != null
        };

        [NotMapped]
        public bool IsDeal => ListPrice.HasValue && ListPrice.Value > Price;

        [NotMapped]
        public int DiscountPercent => IsDeal ? (int)Math.Round(100 * (1 - Price / ListPrice!.Value)) : 0;

        [NotMapped]
        public bool NeedsCustomerAddress =>
            IsPhysical || (IsService && ServiceLocation == Models.ServiceLocation.CustomerAddress);
    }
}
