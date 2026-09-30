using e_Commerce_application.Data;
using e_Commerce_application.Models;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Services
{
    public record OrderLineRequest(int ProductCode, int Quantity, decimal? ExpectedPrice = null);

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

        // Ebooks need a signed-in user to deliver to; the anonymous API can't buy them.
        public bool AllowDigital { get; set; } = true;

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

        private readonly AppDbContext _db;

        public OrderService(AppDbContext db) => _db = db;

        public async Task<ServiceResult<Order>> PlaceOrderAsync(PlaceOrderRequest request)
        {
            var lines = request.Lines
                .GroupBy(l => l.ProductCode)
                .Select(g => new OrderLineRequest(g.Key, g.Sum(l => l.Quantity), g.First().ExpectedPrice))
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

                if (product.IsEbook)
                {
                    if (!request.AllowDigital || request.UserId == null)
                    {
                        errors.Add($"Product {product.ProductCode} is an ebook and can only be purchased in the store while signed in.");
                        continue;
                    }
                    if (owned.Contains(product.ProductCode))
                    {
                        errors.Add($"You already own '{product.Name}'.");
                        continue;
                    }
                    quantity = 1;
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
                    Price = product.Price,
                    Quantity = quantity
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
                // Digital-only orders are fulfilled immediately.
                Status = items.Any(i => i.ProductType == ProductType.Physical) ? OrderStatus.Pending : OrderStatus.Completed,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            foreach (var item in items.Where(i => i.ProductType == ProductType.Ebook))
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

        public async Task<ServiceResult<LibraryEntry>> ClaimFreeEbookAsync(string userId, int productCode)
        {
            var product = await _db.Products.FindAsync(productCode);
            if (product == null || !product.IsEbook || !product.IsListed)
            {
                return ServiceResult<LibraryEntry>.Fail("This ebook is not available.");
            }
            if (product.Price != 0)
            {
                return ServiceResult<LibraryEntry>.Fail("This ebook isn't free.");
            }
            if (await _db.LibraryEntries.AnyAsync(l => l.UserId == userId && l.ProductCode == productCode))
            {
                return ServiceResult<LibraryEntry>.Fail("This ebook is already in your library.");
            }

            var entry = new LibraryEntry { UserId = userId, ProductCode = productCode };
            _db.LibraryEntries.Add(entry);
            await _db.SaveChangesAsync();
            return ServiceResult<LibraryEntry>.Ok(entry);
        }

        public static bool CustomerCanCancel(Order order) =>
            order.Status is OrderStatus.Pending or OrderStatus.Processing && order.HasPhysicalItems;

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
                // Return physical stock and revoke ebook access granted by this order.
                foreach (var item in order.Products.Where(i => i.ProductType == ProductType.Physical))
                {
                    await _db.Products.Where(p => p.ProductCode == item.ProductCode)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + item.Quantity));
                }
                await _db.LibraryEntries.Where(l => l.OrderNo == order.OrderNo).ExecuteDeleteAsync();
            }

            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return ServiceResult<Order>.Ok(order);
        }
    }
}
