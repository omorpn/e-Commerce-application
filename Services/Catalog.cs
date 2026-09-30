using e_Commerce_application.Models;

namespace e_Commerce_application.Services
{
    public static class Catalog
    {
        public static readonly string[] ProductCategories =
        {
            "Electronics", "Computers", "Phones & Tablets", "Home & Kitchen", "Furniture", "Fashion", "Jewelry",
            "Beauty", "Health", "Grocery", "Baby", "Toys & Games", "Sports & Outdoors", "Garden", "Pet Supplies",
            "Automotive", "Books", "Office"
        };

        public static readonly string[] EbookCategories =
        {
            "Fiction", "Science Fiction", "Mystery & Thriller", "Romance", "Business & Money",
            "Computers & Technology", "Self-Help", "Cookbooks", "Biographies", "Children's", "Education", "Other"
        };

        public static readonly string[] DigitalCategories =
        {
            "Software", "Music", "Audiobooks", "Online Courses", "Templates", "Graphics & Art", "Photography",
            "Video", "Games", "Fonts", "Other digital"
        };

        public static readonly string[] ServiceCategories =
        {
            "Home Services", "Repairs", "Tutoring & Lessons", "Design & Creative", "Writing & Translation",
            "Programming & Tech", "Marketing", "Business Consulting", "Beauty & Wellness", "Fitness & Coaching",
            "Events & Photography", "Other services"
        };

        public static readonly string[] Languages =
        {
            "English", "French", "Spanish", "German", "Portuguese", "Yoruba", "Igbo", "Hausa", "Swahili", "Arabic", "Other"
        };

        public const int PageSize = 12;

        public static string[] CategoriesFor(ProductType type) => type switch
        {
            ProductType.Ebook => EbookCategories,
            ProductType.Digital => DigitalCategories,
            ProductType.Service => ServiceCategories,
            _ => ProductCategories
        };

        public static string Label(ProductType type) => type switch
        {
            ProductType.Ebook => "eBook",
            ProductType.Digital => "Digital download",
            ProductType.Service => "Service",
            _ => "Product"
        };

        // URL "dept" value for each listing type.
        public static string Slug(ProductType type) => type switch
        {
            ProductType.Ebook => "ebooks",
            ProductType.Digital => "digital",
            ProductType.Service => "services",
            _ => "products"
        };

        public static ProductType? FromSlug(string? slug) => slug switch
        {
            "products" => ProductType.Physical,
            "ebooks" => ProductType.Ebook,
            "digital" => ProductType.Digital,
            "services" => ProductType.Service,
            _ => null
        };

        public static string DepartmentName(ProductType type) => type switch
        {
            ProductType.Ebook => "eBook Store",
            ProductType.Digital => "Digital Downloads",
            ProductType.Service => "Services",
            _ => "Products"
        };

        public static string LocationLabel(ServiceLocation? location) => location switch
        {
            ServiceLocation.CustomerAddress => "At your address",
            ServiceLocation.ProviderLocation => "At the provider's location",
            _ => "Online / remote"
        };
    }

    public class PublishingOptions
    {
        // Share of each ebook sale paid to the author.
        public decimal RoyaltyRate { get; set; } = 0.70m;

        // Share of each non-ebook sale paid to the seller.
        public decimal SellerRate { get; set; } = 0.85m;

        public decimal RateFor(ProductType type) => type == ProductType.Ebook ? RoyaltyRate : SellerRate;
    }
}
