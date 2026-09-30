using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(100)]
        public string? DisplayName { get; set; }

        // Public shop name, set once the user signs up to sell.
        [StringLength(100)]
        public string? SellerName { get; set; }

        [StringLength(2000)]
        public string? SellerBio { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
