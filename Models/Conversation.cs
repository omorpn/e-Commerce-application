using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models
{
    // A chat between a customer and a seller (or the store team when SellerId is null).
    public class Conversation
    {
        public int Id { get; set; }

        public string BuyerId { get; set; } = string.Empty;
        public ApplicationUser? Buyer { get; set; }

        public string? SellerId { get; set; }
        public ApplicationUser? Seller { get; set; }

        public int? ProductCode { get; set; }
        public Product? Product { get; set; }

        public bool BuyerUnread { get; set; }
        public bool SellerUnread { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<ChatMessage> Messages { get; set; } = new();
    }

    public class ChatMessage
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }

        public string SenderId { get; set; } = string.Empty;
        public ApplicationUser? Sender { get; set; }

        [Required, StringLength(2000)]
        public string Body { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
