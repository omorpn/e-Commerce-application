using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models
{
    // One step in an order's tracking timeline.
    public class OrderEvent
    {
        public int Id { get; set; }

        public int OrderNo { get; set; }

        [StringLength(60)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
