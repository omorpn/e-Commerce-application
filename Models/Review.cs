using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models
{
    public class Review
    {
        public int Id { get; set; }

        public int ProductCode { get; set; }
        public Product? Product { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        [StringLength(100)]
        public string ReviewerName { get; set; } = string.Empty;

        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(120)]
        public string? Title { get; set; }

        [StringLength(4000)]
        public string? Body { get; set; }

        public bool VerifiedPurchase { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
