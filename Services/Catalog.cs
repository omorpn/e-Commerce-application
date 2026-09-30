namespace e_Commerce_application.Services
{
    public static class Catalog
    {
        public static readonly string[] ProductCategories =
        {
            "Electronics", "Home & Kitchen", "Fashion", "Sports & Outdoors", "Beauty", "Toys & Games", "Office"
        };

        public static readonly string[] EbookCategories =
        {
            "Fiction", "Science Fiction", "Mystery & Thriller", "Romance", "Business & Money",
            "Computers & Technology", "Self-Help", "Cookbooks", "Biographies", "Children's", "Education", "Other"
        };

        public static readonly string[] Languages =
        {
            "English", "French", "Spanish", "German", "Portuguese", "Yoruba", "Igbo", "Hausa", "Swahili", "Arabic", "Other"
        };

        public const int PageSize = 12;
    }

    public class PublishingOptions
    {
        // Share of each ebook sale paid to the author.
        public decimal RoyaltyRate { get; set; } = 0.70m;
    }
}
