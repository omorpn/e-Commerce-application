namespace e_Commerce_application.Models
{
    public enum ProductType
    {
        Physical = 0,
        Ebook = 1,
        Digital = 2,
        Service = 3
    }

    public enum ListingStatus
    {
        Draft = 0,
        Published = 1,
        Blocked = 2
    }

    public enum OrderStatus
    {
        Pending = 0,
        Processing = 1,
        Shipped = 2,
        Delivered = 3,
        Completed = 4,
        Cancelled = 5
    }

    public enum ServiceLocation
    {
        Online = 0,
        CustomerAddress = 1,
        ProviderLocation = 2
    }
}
