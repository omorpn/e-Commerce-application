namespace e_Commerce_application.Services
{
    // Branding, currency and contact details shown across the store ("Store" settings section).
    public class ShopSettings
    {
        public string Name { get; set; } = "ShopNest";
        public string Tagline { get; set; } = "Shop smart. Genuine products, fast delivery.";
        public string CurrencyCode { get; set; } = "NGN";
        public string CurrencySymbol { get; set; } = "₦";

        // International format without "+", e.g. 2348012345678. Shows a WhatsApp chat button when set.
        public string? WhatsApp { get; set; }
        public string? SupportEmail { get; set; }
        public string? SupportPhone { get; set; }
        public string? Address { get; set; }
    }

    // Delivery fees for physical items by Nigerian state.
    public class ShippingOptions
    {
        public decimal DefaultFee { get; set; } = 4500;

        // Orders whose items total at least this much ship free (0 disables free shipping).
        public decimal FreeShippingOver { get; set; } = 150000;

        public Dictionary<string, decimal> StateFees { get; set; } = new()
        {
            ["Lagos"] = 2500,
            ["Ogun"] = 3000,
            ["Edo"] = 2500,
            ["FCT - Abuja"] = 3500
        };

        public Dictionary<string, string> StateDeliveryTimes { get; set; } = new()
        {
            ["Lagos"] = "1-2 business days",
            ["Edo"] = "1-2 business days"
        };

        public string DefaultDeliveryTime { get; set; } = "2-5 business days";

        public static readonly string[] NigerianStates =
        {
            "Abia", "Adamawa", "Akwa Ibom", "Anambra", "Bauchi", "Bayelsa", "Benue", "Borno", "Cross River", "Delta",
            "Ebonyi", "Edo", "Ekiti", "Enugu", "FCT - Abuja", "Gombe", "Imo", "Jigawa", "Kaduna", "Kano", "Katsina",
            "Kebbi", "Kogi", "Kwara", "Lagos", "Nasarawa", "Niger", "Ogun", "Ondo", "Osun", "Oyo", "Plateau", "Rivers",
            "Sokoto", "Taraba", "Yobe", "Zamfara"
        };

        public decimal FeeFor(string? state, decimal subtotal)
        {
            if (FreeShippingOver > 0 && subtotal >= FreeShippingOver)
            {
                return 0;
            }
            return state != null && StateFees.TryGetValue(state, out var fee) ? fee : DefaultFee;
        }

        public string DeliveryTimeFor(string? state) =>
            state != null && StateDeliveryTimes.TryGetValue(state, out var time) ? time : DefaultDeliveryTime;
    }
}
