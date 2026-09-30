using e_Commerce_application.Models;
using System.Globalization;

namespace e_Commerce_application.Services
{
    public static class ViewHelpers
    {
        private static readonly CultureInfo Numbers = CultureInfo.GetCultureInfo("en-US");

        // Set from the "Store" settings at startup.
        public static string CurrencySymbol { get; set; } = "₦";

        public static string Price(decimal price) => price == 0 ? "Free" : Currency(price);

        // ₦185,000 for whole amounts, ₦1,234.50 otherwise.
        public static string Currency(decimal amount)
        {
            var value = Math.Abs(amount).ToString(amount % 1 == 0 ? "N0" : "N2", Numbers);
            return (amount < 0 ? "-" : "") + CurrencySymbol + value;
        }

        public static string CategoryIcon(string? category) => category switch
        {
            "Electronics" => "bi-headphones",
            "Computers" => "bi-laptop",
            "Phones & Tablets" => "bi-phone",
            "Phone Accessories" => "bi-earbuds",
            "Watches" => "bi-smartwatch",
            "Fragrances" => "bi-stars",
            "Souvenirs" => "bi-gift",
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

        // Soft colour pair per category for placeholder tiles and category circles.
        public static string PlaceholderStyle(string? key)
        {
            var hash = 0;
            foreach (var c in key ?? "")
            {
                hash = unchecked(hash * 31 + c);
            }
            var hue = (hash & 0x7fffffff) % 360;
            return $"--ph-a: hsl({hue} 85% 95%); --ph-b: hsl({(hue + 25) % 360} 70% 86%); --ph-ink: hsl({hue} 45% 32%);";
        }

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
            OrderStatus.AwaitingPayment => "text-bg-warning",
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

        // Uploaded image first, then an external link; null means "show a placeholder".
        public static string? ImageUrl(Product p) =>
            p.ImagePath != null ? $"/Media/Image/{p.ProductCode}?v={p.UpdatedAt.Ticks}" : p.ImageUrl;

        public static string StatusLabel(OrderStatus status) => status switch
        {
            OrderStatus.AwaitingPayment => "Awaiting payment",
            _ => status.ToString()
        };

        public static string PaymentLabel(PaymentStatus status) => status switch
        {
            PaymentStatus.Paid => "Paid",
            PaymentStatus.Pending => "Awaiting payment",
            PaymentStatus.Failed => "Payment failed",
            _ => "Not paid yet"
        };

        public static string TimeAgo(DateTime utc)
        {
            var span = DateTime.UtcNow - utc;
            return span.TotalMinutes < 1 ? "just now"
                : span.TotalHours < 1 ? $"{(int)span.TotalMinutes} min ago"
                : span.TotalDays < 1 ? $"{(int)span.TotalHours} h ago"
                : span.TotalDays < 7 ? $"{(int)span.TotalDays} d ago"
                : utc.ToString("MMM d, yyyy");
        }
    }
}
