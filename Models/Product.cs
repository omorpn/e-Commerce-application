using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace e_Commerce_application.Models
{
    // A catalog listing: either a physical product sold by the store or an ebook
    // self-published by an author.
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

        // Physical products only; ebooks are never out of stock.
        public int Stock { get; set; }

        public ListingStatus Status { get; set; } = ListingStatus.Published;

        [StringLength(500)]
        public string? BlockedReason { get; set; }

        // Storage key of the product image / ebook cover.
        public string? ImagePath { get; set; }
        public string? ImageContentType { get; set; }

        // Ebook metadata
        [StringLength(120)]
        public string? AuthorName { get; set; }

        [StringLength(40)]
        public string? Language { get; set; }

        public int? PageCount { get; set; }

        // Storage key of the manuscript (PDF/EPUB).
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
        public string? FileContentType { get; set; }
        public long? FileSize { get; set; }

        // Author account that published the ebook (null for store-owned listings).
        public string? SellerId { get; set; }
        public ApplicationUser? Seller { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }

        public List<Review> Reviews { get; set; } = new();

        [NotMapped]
        public bool IsEbook => Type == ProductType.Ebook;

        [NotMapped]
        public bool IsListed => Status == ListingStatus.Published;

        [NotMapped]
        public bool InStock => IsEbook ? FilePath != null : Stock > 0;
    }
}
