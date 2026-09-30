namespace e_Commerce_application.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int OrderCount { get; set; }
        public int PendingOrders { get; set; }
        public decimal Revenue { get; set; }
        public int LowStockCount { get; set; }
        public Dictionary<ProductType, int> ListingCounts { get; set; } = new();
        public int SellerListings { get; set; }
        public int UserCount { get; set; }
        public int SellerCount { get; set; }
        public List<Order> RecentOrders { get; set; } = new();
        public List<Product> LowStock { get; set; } = new();
    }

    public class AdminUserRow
    {
        public ApplicationUser User { get; set; } = null!;
        public bool IsAdmin { get; set; }
        public bool IsSeller { get; set; }
        public int OrderCount { get; set; }
        public int ListingCount { get; set; }
    }
}
