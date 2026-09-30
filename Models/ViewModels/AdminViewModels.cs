using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models.ViewModels
{
    public class ProductFormViewModel
    {
        public int? ProductCode { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        [Required]
        public string Category { get; set; } = string.Empty;

        [Range(0.01, 1_000_000, ErrorMessage = "Price must be between {1} and {2}")]
        public decimal Price { get; set; }

        [Range(0, 100_000)]
        public int Stock { get; set; }

        [Display(Name = "Visible in store")]
        public bool Published { get; set; } = true;

        [Display(Name = "Product image (JPG, PNG or WEBP, max 5 MB)")]
        public IFormFile? Image { get; set; }

        [Display(Name = "Remove current image")]
        public bool RemoveImage { get; set; }

        [ValidateNever]
        public bool HasImage { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public int OrderCount { get; set; }
        public int PendingOrders { get; set; }
        public decimal Revenue { get; set; }
        public int ProductCount { get; set; }
        public int LowStockCount { get; set; }
        public int EbookCount { get; set; }
        public int UserCount { get; set; }
        public int AuthorCount { get; set; }
        public List<Order> RecentOrders { get; set; } = new();
        public List<Product> LowStock { get; set; } = new();
    }

    public class AdminUserRow
    {
        public ApplicationUser User { get; set; } = null!;
        public bool IsAdmin { get; set; }
        public bool IsAuthor { get; set; }
        public int OrderCount { get; set; }
        public int TitleCount { get; set; }
    }
}
