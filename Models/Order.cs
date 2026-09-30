using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace e_Commerce_application.Models
{
    public class Order
    {
        [Key]
        [BindNever]
        public int OrderNo { get; set; }

        [Required(ErrorMessage = "{0} can't be blank")]
        [CustomValidation(typeof(Order), nameof(ValidateOrderDate))]
        public DateTime OrderDate { get; set; }

        [Required(ErrorMessage = "{0} must be a number")]
        [Range(0.01, 1_000_000_000, ErrorMessage = "{0} must be greater than zero")]
        public decimal InvoicePrice { get; set; }

        [Required(ErrorMessage = "At least one product is required")]
        [MinLength(1, ErrorMessage = "At least one product is required")]
        public List<OrderItem> Products { get; set; } = new List<OrderItem>(); // Initialize to avoid null

        [BindNever]
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [BindNever]
        [JsonIgnore]
        public string? UserId { get; set; }

        [BindNever]
        [JsonIgnore]
        public ApplicationUser? User { get; set; }

        [StringLength(100)]
        public string? CustomerName { get; set; }

        [EmailAddress, StringLength(256)]
        public string? Email { get; set; }

        [StringLength(30)]
        public string? Phone { get; set; }

        [StringLength(200)]
        public string? AddressLine { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        public string? State { get; set; }

        [StringLength(20)]
        public string? PostalCode { get; set; }

        [StringLength(100)]
        public string? Country { get; set; }

        [BindNever]
        [StringLength(40)]
        public string? PaymentMethod { get; set; }

        [BindNever]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool HasPhysicalItems => Products.Any(p => p.ProductType == ProductType.Physical);

        public Order() { }

        public Order(int orderNo, DateTime orderDate, decimal invoicePrice, List<OrderItem> products)
            => (OrderNo, OrderDate, InvoicePrice, Products)
            = (orderNo, orderDate, invoicePrice, products);

        // Order date must fall within the last five minutes (not in the future).
        public static ValidationResult? ValidateOrderDate(DateTime date, ValidationContext context)
        {
            var now = DateTime.UtcNow;
            var utcDate = date.ToUniversalTime();

            if (utcDate > now)
            {
                return new ValidationResult("Invalid order date: can't be in the future");
            }

            if (utcDate < now.AddMinutes(-5))
            {
                return new ValidationResult("Invalid order date: can't be in the past");
            }

            return ValidationResult.Success;
        }
    }
}
