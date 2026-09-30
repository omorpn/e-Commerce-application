using e_Commerce_application.Models;
using System.Globalization;

namespace e_Commerce_application.Services
{
    public static class ViewHelpers
    {
        private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("en-US");

        public static string Price(decimal price) => price == 0 ? "Free" : price.ToString("C", Money);

        public static string Currency(decimal amount) => amount.ToString("C", Money);

        public static string CategoryIcon(string? category) => category switch
        {
            "Electronics" => "bi-headphones",
            "Home & Kitchen" => "bi-cup-hot",
            "Fashion" => "bi-bag",
            "Sports & Outdoors" => "bi-bicycle",
            "Beauty" => "bi-droplet",
            "Toys & Games" => "bi-controller",
            "Office" => "bi-briefcase",
            _ => "bi-box-seam"
        };

        // Deterministic colour pair so each generated book cover looks distinct but stable.
        public static string CoverGradient(string title)
        {
            string[] palettes =
            {
                "#1e3a5f,#3d7cc9", "#5b2a86,#a45dd6", "#8a2c2c,#d9654b", "#1f5f4a,#3fb68b",
                "#7a4b10,#d99a2b", "#2d3142,#6b7aa1", "#6a1b4d,#d64f8a", "#0f4c5c,#3aa1b3"
            };
            var hash = 0;
            foreach (var c in title)
            {
                hash = unchecked(hash * 31 + c);
            }
            return palettes[(hash & 0x7fffffff) % palettes.Length];
        }

        public static string StatusBadge(OrderStatus status) => status switch
        {
            OrderStatus.Pending => "text-bg-warning",
            OrderStatus.Processing => "text-bg-info",
            OrderStatus.Shipped => "text-bg-primary",
            OrderStatus.Delivered or OrderStatus.Completed => "text-bg-success",
            OrderStatus.Cancelled => "text-bg-secondary",
            _ => "text-bg-light"
        };

        public static string ListingBadge(ListingStatus status) => status switch
        {
            ListingStatus.Published => "text-bg-success",
            ListingStatus.Draft => "text-bg-secondary",
            ListingStatus.Blocked => "text-bg-danger",
            _ => "text-bg-light"
        };

        public static string FileSize(long? bytes) => bytes switch
        {
            null => "",
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
            _ => $"{bytes / (1024.0 * 1024):0.#} MB"
        };

        public static string ImageUrl(Product p) => $"/Media/Image/{p.ProductCode}?v={p.UpdatedAt.Ticks}";
    }
}
