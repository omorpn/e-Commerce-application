using System.ComponentModel.DataAnnotations;

namespace e_Commerce_application.Models
{
    public class Coupon
    {
        public int Id { get; set; }

        [Required, StringLength(40)]
        [RegularExpression("^[A-Za-z0-9_-]+$", ErrorMessage = "Use letters, numbers, - and _ only")]
        public string Code { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        [Range(1, 100), Display(Name = "Percent off")]
        public int? PercentOff { get; set; }

        [Range(0.01, 100_000_000), Display(Name = "Amount off")]
        public decimal? AmountOff { get; set; }

        [Range(0, 100_000_000), Display(Name = "Minimum order")]
        public decimal? MinSubtotal { get; set; }

        [Display(Name = "Expires")]
        public DateTime? ExpiresAt { get; set; }

        [Range(1, 1_000_000), Display(Name = "Maximum uses")]
        public int? MaxUses { get; set; }

        public int UsedCount { get; set; }

        public bool Active { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public decimal DiscountFor(decimal subtotal)
        {
            var discount = PercentOff.HasValue ? Math.Round(subtotal * PercentOff.Value / 100m, 2) : AmountOff ?? 0;
            return Math.Min(discount, subtotal);
        }
    }
}
