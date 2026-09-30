using e_Commerce_application.Data;
using e_Commerce_application.Models;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Services
{
    public record OrderLineRequest(int ProductCode, int Quantity, decimal? ExpectedPrice = null,
        DateTime? ServiceDate = null, string? ServiceNotes = null);

    public class PlaceOrderRequest
    {
        public string? UserId { get; set; }
        public string? CustomerName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? PaymentMethod { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        // When set, the computed total must match (used by the JSON API).
        public decimal? ExpectedTotal { get; set; }

        // Downloads and services are tied to a customer account; the anonymous API can't buy them.
        public bool AllowAccountItems { get; set; } = true;

        public List<OrderLineRequest> Lines { get; set; } = new();
    }

    public class ServiceResult<T>
    {
        public T? Value { get; init; }
        public List<string> Errors { get; init; } = new();
        public bool Succeeded => Errors.Count == 0;

        public static ServiceResult<T> Ok(T value) => new() { Value = value };
        public static ServiceResult<T> Fail(params string[] errors) => new() { Errors = errors.ToList() };
    }

    public class OrderService
    {
        public const string InvoiceMismatchError =
            "InvoicePrice doesn't match with the total cost of the specified products in the order.";

        public const int MaxServiceQuantity = 20;

        private readonly AppDbContext _db;

        public OrderService(AppDbContext db) => _db = db;

        public static DateTime EarliestServiceDate => DateTime.UtcNow.Date.AddDays(1);

        public async Task<ServiceResult<Order>> PlaceOrderAsync(PlaceOrderRequest request)
        {
            var lines = request.Lines
                .GroupBy(l => l.ProductCode)
                .Select(g => g.First() with { Quantity = g.Sum(l => l.Quantity) })
                .ToList();

            if (lines.Count == 0)
            {
                return ServiceResult<Order>.Fail("At least one product is required");
            }

            var codes = lines.Select(l => l.ProductCode).ToList();
            var products = await _db.Products.Where(p => codes.Contains(p.ProductCode)).ToDictionaryAsync(p => p.ProductCode);
            var owned = request.UserId == null
                ? new HashSet<int>()
                : (await _db.LibraryEntries.Where(l => l.UserId == request.UserId && codes.Contains(l.ProductCode))
                    .Select(l => l.ProductCode).ToListAsync()).ToHashSet();

            var errors = new List<string>();
            var items = new List<OrderItem>();
            foreach (var line in lines)
            {
                if (!products.TryGetValue(line.ProductCode, out var product) || !product.IsListed)
                {
                    errors.Add($"Product {line.ProductCode} is not available.");
                    continue;
                }

                var quantity = line.Quantity;
                if (quantity < 1)
                {
                    errors.Add($"Quantity for '{product.Name}' must be at least 1.");
                    continue;
                }
                if (product.SellerId != null && product.SellerId == request.UserId)
                {
                    errors.Add($"You can't buy your own listing '{product.Name}'.");
                    continue;
                }
                if (!product.IsPhysical && (!request.AllowAccountItems || request.UserId == null))
                {
                    errors.Add(product.IsService
                        ? $"Product {product.ProductCode} is a service and can only be booked in the store while signed in."
                        : $"Product {product.ProductCode} is {(product.IsEbook ? "an ebook" : "a digital download")} and can only be purchased in the store while signed in.");
                    continue;
                }

                DateTime? serviceDate = null;
                if (product.IsDownloadable)
                {
                    if (owned.Contains(product.ProductCode))
                    {
                        errors.Add($"You already own '{product.Name}'.");
                        continue;
                    }
                    quantity = 1;
                }
                else if (product.IsService)
                {
                    if (quantity > MaxServiceQuantity)
                    {
                        errors.Add($"You can book at most {MaxServiceQuantity} sessions of '{product.Name}' at once.");
                        continue;
                    }
                    if (line.ServiceDate == null)
                    {
                        errors.Add($"Choose a date for '{product.Name}'.");
                        continue;
                    }
                    serviceDate = DateTime.SpecifyKind(line.ServiceDate.Value.Date, DateTimeKind.Utc);
                    if (serviceDate < EarliestServiceDate)
                    {
                        errors.Add($"The date for '{product.Name}' must be tomorrow or later.");
                        continue;
                    }
                }
                else if (product.Stock < quantity)
                {
                    errors.Add(product.Stock == 0
                        ? $"'{product.Name}' is out of stock."
                        : $"Only {product.Stock} of '{product.Name}' left in stock.");
                    continue;
                }

                if (line.ExpectedPrice.HasValue && line.ExpectedPrice.Value != product.Price)
                {
                    errors.Add($"Price for product {product.ProductCode} is {product.Price:0.00}, not {line.ExpectedPrice.Value:0.00}.");
                    continue;
                }

                items.Add(new OrderItem
                {
                    ProductCode = product.ProductCode,
                    ProductName = product.Name,
                    ProductType = product.Type,
                    SellerId = product.SellerId,
                    Price = product.Price,
                    Quantity = quantity,
                    ServiceDate = serviceDate,
                    ServiceNotes = product.IsService ? line.ServiceNotes?.Trim() : null,
                    // Downloads are delivered the moment the order is placed.
                    Fulfilled = product.IsDownloadable,
                    FulfilledAt = product.IsDownloadable ? DateTime.UtcNow : null
                });
            }

            var total = items.Sum(i => i.LineTotal);
            if (request.ExpectedTotal.HasValue && Math.Abs(request.ExpectedTotal.Value - total) > 0.005m && errors.Count == 0)
            {
                errors.Add(InvoiceMismatchError);
            }

            if (errors.Count > 0)
            {
                return new ServiceResult<Order> { Errors = errors };
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();

            // Conditional decrement guards against two orders racing for the last unit.
            foreach (var item in items.Where(i => i.ProductType == ProductType.Physical))
            {
                var updated = await _db.Products
                    .Where(p => p.ProductCode == item.ProductCode && p.Stock >= item.Quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - item.Quantity));
                if (updated == 0)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<Order>.Fail($"'{item.ProductName}' just sold out. Please update your cart.");
                }
            }

            var order = new Order
            {
                OrderDate = request.OrderDate,
                InvoicePrice = total,
                Products = items,
                UserId = request.UserId,
                CustomerName = request.CustomerName,
                Email = request.Email,
                Phone = request.Phone,
                AddressLine = request.AddressLine,
                City = request.City,
                State = request.State,
                PostalCode = request.PostalCode,
                Country = request.Country,
                PaymentMethod = request.PaymentMethod,
                Status = items.All(i => i.Fulfilled) ? OrderStatus.Completed : OrderStatus.Pending,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            foreach (var item in items.Where(i => i.ProductType is ProductType.Ebook or ProductType.Digital))
            {
                _db.LibraryEntries.Add(new LibraryEntry
                {
                    UserId = request.UserId!,
                    ProductCode = item.ProductCode,
                    OrderNo = order.OrderNo
                });
            }
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return ServiceResult<Order>.Ok(order);
        }

        public async Task<ServiceResult<LibraryEntry>> ClaimFreeAsync(string userId, int productCode)
        {
            var product = await _db.Products.FindAsync(productCode);
            if (product == null || !product.IsDownloadable || !product.IsListed || product.FilePath == null)
            {
                return ServiceResult<LibraryEntry>.Fail("This item is not available.");
            }
            if (product.Price != 0)
            {
                return ServiceResult<LibraryEntry>.Fail("This item isn't free.");
            }
            if (await _db.LibraryEntries.AnyAsync(l => l.UserId == userId && l.ProductCode == productCode))
            {
                return ServiceResult<LibraryEntry>.Fail("This item is already in your library.");
            }

            var entry = new LibraryEntry { UserId = userId, ProductCode = productCode };
            _db.LibraryEntries.Add(entry);
            await _db.SaveChangesAsync();
            return ServiceResult<LibraryEntry>.Ok(entry);
        }

        // Customers may cancel until anything that needs shipping or performing has been fulfilled.
        public static bool CustomerCanCancel(Order order) =>
            order.Status is OrderStatus.Pending or OrderStatus.Processing
            && order.HasFulfilmentItems
            && !order.Products.Any(i => i.Fulfilled && i.ProductType is ProductType.Physical or ProductType.Service);

        public async Task<ServiceResult<Order>> UpdateStatusAsync(int orderNo, OrderStatus status)
        {
            var order = await _db.Orders.Include(o => o.Products).FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null)
            {
                return ServiceResult<Order>.Fail("Order not found.");
            }
            if (order.Status == status)
            {
                return ServiceResult<Order>.Ok(order);
            }
            if (order.Status == OrderStatus.Cancelled)
            {
                return ServiceResult<Order>.Fail("Cancelled orders can't be changed.");
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();

            if (status == OrderStatus.Cancelled)
            {
                // Return physical stock and revoke download access granted by this order.
                foreach (var item in order.Products.Where(i => i.ProductType == ProductType.Physical))
                {
                    await _db.Products.Where(p => p.ProductCode == item.ProductCode)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + item.Quantity));
                }
                await _db.LibraryEntries.Where(l => l.OrderNo == order.OrderNo).ExecuteDeleteAsync();
            }
            else if (status is OrderStatus.Delivered or OrderStatus.Completed)
            {
                foreach (var item in order.Products.Where(i => !i.Fulfilled))
                {
                    item.Fulfilled = true;
                    item.FulfilledAt = DateTime.UtcNow;
                }
            }

            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return ServiceResult<Order>.Ok(order);
        }

        // A seller marks one of their order lines as shipped (products) or done (services).
        public async Task<ServiceResult<Order>> MarkFulfilledAsync(int itemId, string sellerId)
        {
            var item = await _db.OrderItems.FirstOrDefaultAsync(i => i.Id == itemId && i.SellerId == sellerId);
            if (item == null)
            {
                return ServiceResult<Order>.Fail("Order item not found.");
            }

            var order = await _db.Orders.Include(o => o.Products).FirstAsync(o => o.OrderNo == item.OrderNo);
            if (order.Status == OrderStatus.Cancelled)
            {
                return ServiceResult<Order>.Fail("This order was cancelled.");
            }
            if (item.Fulfilled)
            {
                return ServiceResult<Order>.Ok(order);
            }

            item.Fulfilled = true;
            item.FulfilledAt = DateTime.UtcNow;
            order.Status = order.Products.All(i => i.Fulfilled)
                ? (order.HasPhysicalItems ? OrderStatus.Shipped : OrderStatus.Completed)
                : OrderStatus.Processing;
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return ServiceResult<Order>.Ok(order);
        }
    }
}
