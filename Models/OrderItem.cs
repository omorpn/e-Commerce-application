using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace e_Commerce_application.Models
{
    // A line on an order. Name, type and price are snapshotted at purchase time so
    // the order stays correct if the catalog listing later changes or is removed.
    public class OrderItem
    {
        [BindNever]
        [JsonIgnore]
        public int Id { get; set; }

        [BindNever]
        [JsonIgnore]
        public int OrderNo { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "{0} must be a valid product code")]
        public int ProductCode { get; set; }

        [BindNever]
        [StringLength(150)]
        public string ProductName { get; set; } = string.Empty;

        [BindNever]
        public ProductType ProductType { get; set; }

        [Range(0.01, 1_000_000_000, ErrorMessage = "{0} must be greater than zero")]
        public decimal Price { get; set; }

        [Range(1, 1000, ErrorMessage = "{0} must be between {1} and {2}")]
        public int Quantity { get; set; }

        public decimal LineTotal => Price * Quantity;

        public OrderItem() { }

        public OrderItem(int productCode, decimal price, int quantity) =>
            (ProductCode, Price, Quantity)
            = (productCode, price, quantity);
    }
}
