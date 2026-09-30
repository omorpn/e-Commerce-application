using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(100)]
        public string? DisplayName { get; set; }

        // Set once the user signs up for self-publishing.
        [StringLength(100)]
        public string? PenName { get; set; }

        [StringLength(2000)]
        public string? AuthorBio { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
