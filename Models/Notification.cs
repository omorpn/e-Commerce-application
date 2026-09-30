using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models
{
    public class Notification
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        [StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Message { get; set; }

        [StringLength(300)]
        public string? Url { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
