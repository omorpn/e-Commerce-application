using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models
{
    public class Order
    {
        [BindNever]
        public int OrderNo { get; set; }

        [Required(ErrorMessage = "{0} can't be blank")]
        [CustomValidation(typeof(Order), nameof(ValidateOrderDate))]
        public DateTime OrderDate { get; set; }

        [Required(ErrorMessage = "{0} must be a number")]
        [Range(0.01, double.MaxValue, ErrorMessage = "{0} must be greater than zero")]
        public double InvoicePrice { get; set; }

        [Required(ErrorMessage = "At least one product is required")]
        [MinLength(1, ErrorMessage = "At least one product is required")]
        public List<Product> Products { get; set; } = new List<Product>(); // Initialize to avoid null

        public Order() { }

        public Order(int orderNo, DateTime orderDate, double invoicePrice, List<Product> products)
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
