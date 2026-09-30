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
            "Computers" => "bi-laptop",
            "Phones & Tablets" => "bi-phone",
            "Home & Kitchen" => "bi-cup-hot",
            "Furniture" => "bi-lamp",
            "Fashion" => "bi-bag",
            "Jewelry" => "bi-gem",
            "Beauty" => "bi-droplet",
            "Health" => "bi-heart-pulse",
            "Grocery" => "bi-basket",
            "Baby" => "bi-balloon-heart",
            "Toys & Games" => "bi-controller",
            "Sports & Outdoors" => "bi-bicycle",
            "Garden" => "bi-flower1",
            "Pet Supplies" => "bi-heart",
            "Automotive" => "bi-car-front",
            "Books" => "bi-book",
            "Office" => "bi-briefcase",
            "Software" => "bi-window-stack",
            "Music" => "bi-music-note-beamed",
            "Audiobooks" => "bi-headset",
            "Online Courses" => "bi-mortarboard",
            "Templates" => "bi-file-earmark-spreadsheet",
            "Graphics & Art" => "bi-palette",
            "Photography" => "bi-camera",
            "Video" => "bi-camera-reels",
            "Games" => "bi-joystick",
            "Fonts" => "bi-fonts",
            "Home Services" => "bi-house-check",
            "Repairs" => "bi-tools",
            "Tutoring & Lessons" => "bi-easel",
            "Design & Creative" => "bi-vector-pen",
            "Writing & Translation" => "bi-pencil",
            "Programming & Tech" => "bi-code-slash",
            "Marketing" => "bi-megaphone",
            "Business Consulting" => "bi-graph-up-arrow",
            "Beauty & Wellness" => "bi-flower2",
            "Fitness & Coaching" => "bi-activity",
            "Events & Photography" => "bi-camera2",
            _ => "bi-box-seam"
        };

        public static string TypeIcon(ProductType type) => type switch
        {
            ProductType.Ebook => "bi-book",
            ProductType.Digital => "bi-cloud-download",
            ProductType.Service => "bi-calendar-check",
            _ => "bi-box-seam"
        };

        public static string Duration(int? minutes) => minutes switch
        {
            null => "",
            < 60 => $"{minutes} min",
            < 1440 when minutes % 60 == 0 => $"{minutes / 60} hr",
            < 1440 => $"{minutes / 60.0:0.#} hr",
            _ => $"{minutes / 1440.0:0.#} days"
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
