using System.CodeDom.Compiler;
using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models
{
    public class Product
    {
        [Required]
        public int ProductCode { get; set; }
        [Required]
        public  double Price { get; set; }
        [Required]
        public int Quantity { get; set; } = 0;
        public Product() { }
        public Product(int productCode, double price, int quantity) => 
            (ProductCode, Price, Quantity) 
            = (productCode, price, quantity);
    }
}
