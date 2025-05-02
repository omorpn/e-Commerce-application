using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models
{
    public class Order
    {

        [BindNever]
        public int OrderNo { get; set; }
        [Required(ErrorMessage = "{0} can;t be blank")]
        [DateType(InputDateType.Now)]
        [CustomValidation(typeof(Order), nameof(ValidateOrderDate)]
        public DateTime OrderDate { get; set; }
        [Required(ErrorMessage = "{0} Must be a number")]
        [Range(0.01, double.MaxValue, ErrorMessage = "{0} can't be negative")]
        public double InvoicePrice { get; set; }
        [Required(ErrorMessage = " At lest one product is required")]
        [MinLength(1, ErrorMessage = "Product must be present")]
        public List<Product> Products { get; set; } = new List<Product>(); // Initialize to avoid null

        public Order() { }
        private ValidationResult ValidateOrderDate(DateTime date, ValidationContext validation)
        {


            var now = DateTime.UtcNow;
            var fiveMinutes = DateTime.UtcNow.AddMinutes(-5);
           
            if (date >= fiveMinutes && date <= now)
            {
                return ValidationResult.Success;
            }


            if (date > now)
            {
                return new ValidationResult("Invalid order Date can't be in the future");

            }

            return new ValidationResult("Invalid order Date can't be in the past");

        }



        public Order(int orderNo, DateTime orderDate, double invoicePrice, List<Product> products)
            => (OrderNo, OrderDate, InvoicePrice, Products)
            = (orderNo, orderDate, invoicePrice, products);
    }

}
