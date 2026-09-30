using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models.ViewModels
{
    public class AuthorProfileViewModel
    {
        [Required, StringLength(100), Display(Name = "Pen name")]
        public string PenName { get; set; } = string.Empty;

        [StringLength(2000), Display(Name = "About the author")]
        public string? AuthorBio { get; set; }

        [Display(Name = "I own the rights to the content I publish")]
        public bool AcceptTerms { get; set; }
    }

    public class EbookFormViewModel
    {
        public int? ProductCode { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Subtitle { get; set; }

        [Required, StringLength(120), Display(Name = "Author name")]
        public string AuthorName { get; set; } = string.Empty;

        [Required, StringLength(4000), Display(Name = "Book description")]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string Category { get; set; } = string.Empty;

        [Required]
        public string Language { get; set; } = "English";

        [Range(1, 20000), Display(Name = "Print length (pages)")]
        public int? PageCount { get; set; }

        [Range(0, 999.99, ErrorMessage = "Price must be between {1} and {2} (0 = free)")]
        [Display(Name = "List price (USD)")]
        public decimal Price { get; set; } = 4.99m;

        [Display(Name = "Manuscript (PDF or EPUB, max 50 MB)")]
        public IFormFile? Manuscript { get; set; }

        [Display(Name = "Cover image (JPG, PNG or WEBP, max 5 MB)")]
        public IFormFile? Cover { get; set; }

        [ValidateNever]
        public string? ExistingFileName { get; set; }

        [ValidateNever]
        public bool HasCover { get; set; }

        [ValidateNever]
        public ListingStatus Status { get; set; }

        [ValidateNever]
        public string? BlockedReason { get; set; }
    }

    public class TitleSalesRow
    {
        public Product Product { get; set; } = null!;
        public int UnitsSold { get; set; }
        public decimal Revenue { get; set; }
        public decimal Royalty { get; set; }
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
    }

    public class PublishDashboardViewModel
    {
        public string PenName { get; set; } = string.Empty;
        public decimal RoyaltyRate { get; set; }
        public List<TitleSalesRow> Titles { get; set; } = new();
        public int TotalUnits => Titles.Sum(t => t.UnitsSold);
        public decimal TotalRevenue => Titles.Sum(t => t.Revenue);
        public decimal TotalRoyalty => Titles.Sum(t => t.Royalty);
    }
}
