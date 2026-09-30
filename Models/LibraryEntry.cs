namespace e_Commerce_application.Models
{
    // Grants a user access to download an ebook.
    public class LibraryEntry
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public int ProductCode { get; set; }
        public Product? Product { get; set; }

        // Null for free ebooks claimed without an order.
        public int? OrderNo { get; set; }

        public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;
    }
}
