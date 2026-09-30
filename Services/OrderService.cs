using e_Commerce_application.Data;
using e_Commerce_application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace e_Commerce_application.Services
{
    public record OrderLineRequest(int ProductCode, int Quantity, decimal? ExpectedPrice = null,
        DateTime? ServiceDate = null, string? ServiceNotes = null);

    public enum PaymentMode
    {
        // Paid at checkout without a gateway (demo mode); downloads are delivered at once.
        PaidNow,
        // Waiting for an online payment (Paystack); nothing is delivered until it's confirmed.
        Online,
        // Paid in cash or transfer on delivery (and orders placed through the JSON API).
        OnDelivery
    }

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
        public PaymentMode Payment { get; set; } = PaymentMode.PaidNow;
        public string? CouponCode { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        // Delivery fees apply to store checkouts; the JSON API orders items only.
        public bool ChargeShipping { get; set; } = true;

        // When set, the items total must match (used by the JSON API).
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
        private readonly NotificationService _notifications;
        private readonly ShippingOptions _shipping;
        private readonly ShopSettings _store;

        public OrderService(AppDbContext db, NotificationService notifications, IOptions<ShippingOptions> shipping, IOptions<ShopSettings> store)
        {
            _db = db;
            _notifications = notifications;
            _shipping = shipping.Value;
            _store = store.Value;
        }

        public static DateTime EarliestServiceDate => DateTime.UtcNow.Date.AddDays(1);

        // Payment references look like "SN42-1a2b3c4d" so any attempt can be traced to its order.
        public static string NewPaymentReference(int orderNo) => $"SN{orderNo}-{Guid.NewGuid():N}"[..(orderNo.ToString().Length + 11)];

        public static int? OrderNoFromReference(string? reference)
        {
            if (reference == null || !reference.StartsWith("SN", StringComparison.Ordinal))
            {
                return null;
            }
            var dash = reference.IndexOf('-');
            return dash > 2 && int.TryParse(reference[2..dash], out var orderNo) ? orderNo : null;
        }

        public async Task<ServiceResult<Coupon>> FindCouponAsync(string? code, decimal subtotal)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return ServiceResult<Coupon>.Fail("Enter a coupon code.");
            }

            var normalized = code.Trim().ToUpperInvariant();
            var coupon = await _db.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Code == normalized);
            if (coupon == null || !coupon.Active || (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value < DateTime.UtcNow))
            {
                return ServiceResult<Coupon>.Fail($"Coupon {normalized} is not valid.");
            }
            if (coupon.MaxUses.HasValue && coupon.UsedCount >= coupon.MaxUses.Value)
            {
                return ServiceResult<Coupon>.Fail($"Coupon {normalized} has been used up.");
            }
            if (coupon.MinSubtotal.HasValue && subtotal < coupon.MinSubtotal.Value)
            {
                return ServiceResult<Coupon>.Fail($"Coupon {normalized} needs an order of at least {ViewHelpers.Currency(coupon.MinSubtotal.Value)}.");
            }
            return ServiceResult<Coupon>.Ok(coupon);
        }

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
                    if (request.Payment == PaymentMode.OnDelivery)
                    {
                        errors.Add($"'{product.Name}' is a download and must be paid for online.");
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
                    ServiceNotes = product.IsService ? line.ServiceNotes?.Trim() : null
                });
            }

            var subtotal = items.Sum(i => i.LineTotal);
            if (request.ExpectedTotal.HasValue && Math.Abs(request.ExpectedTotal.Value - subtotal) > 0.005m && errors.Count == 0)
            {
                errors.Add(InvoiceMismatchError);
            }

            Coupon? coupon = null;
            if (!string.IsNullOrWhiteSpace(request.CouponCode) && errors.Count == 0)
            {
                var found = await FindCouponAsync(request.CouponCode, subtotal);
                if (found.Succeeded)
                {
                    coupon = found.Value;
                }
                else
                {
                    errors.AddRange(found.Errors);
                }
            }

            if (errors.Count > 0)
            {
                return new ServiceResult<Order> { Errors = errors };
            }

            var shippingFee = request.ChargeShipping && items.Any(i => i.ProductType == ProductType.Physical)
                ? _shipping.FeeFor(request.State, subtotal)
                : 0;
            var discount = coupon?.DiscountFor(subtotal) ?? 0;

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

            if (coupon != null)
            {
                var claimed = await _db.Coupons
                    .Where(c => c.Id == coupon.Id && (c.MaxUses == null || c.UsedCount < c.MaxUses))
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, c => c.UsedCount + 1));
                if (claimed == 0)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<Order>.Fail($"Coupon {coupon.Code} has been used up.");
                }
            }

            var order = new Order
            {
                OrderDate = request.OrderDate,
                Subtotal = subtotal,
                ShippingFee = shippingFee,
                Discount = discount,
                CouponCode = coupon?.Code,
                InvoicePrice = subtotal + shippingFee - discount,
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
                Status = request.Payment == PaymentMode.Online ? OrderStatus.AwaitingPayment : OrderStatus.Pending,
                PaymentStatus = request.Payment == PaymentMode.Online ? PaymentStatus.Pending : PaymentStatus.Unpaid,
                UpdatedAt = DateTime.UtcNow
            };
            order.Events.Add(new OrderEvent
            {
                Title = "Order placed",
                Message = request.Payment switch
                {
                    PaymentMode.Online => "Waiting for your payment to be confirmed.",
                    PaymentMode.OnDelivery when order.HasFulfilmentItems => "You'll pay on delivery.",
                    _ => null
                }
            });
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            if (request.Payment == PaymentMode.Online)
            {
                order.PaymentReference = NewPaymentReference(order.OrderNo);
            }
            else if (request.Payment == PaymentMode.PaidNow)
            {
                ConfirmPayment(order, null);
            }
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            await NotifyPlacedAsync(order);
            return ServiceResult<Order>.Ok(order);
        }

        // Records a confirmed payment. Safe to call more than once (callback and webhook).
        public async Task<ServiceResult<Order>> MarkPaidAsync(string reference, long amountMinor, string? currency)
        {
            var orderNo = OrderNoFromReference(reference);
            var order = orderNo == null ? null : await _db.Orders.Include(o => o.Products).FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null)
            {
                return ServiceResult<Order>.Fail("Order not found for this payment.");
            }
            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                return ServiceResult<Order>.Ok(order);
            }

            var expected = PaystackGateway.ToMinorUnits(order.InvoicePrice);
            if (amountMinor < expected || (currency != null && !currency.Equals(_store.CurrencyCode, StringComparison.OrdinalIgnoreCase)))
            {
                order.PaymentStatus = PaymentStatus.Failed;
                AddEvent(order, "Payment problem", $"Received {amountMinor / 100m:0.00} {currency}; expected {ViewHelpers.Currency(order.InvoicePrice)}. Our team will contact you.");
                await _db.SaveChangesAsync();
                await _notifications.NotifyAdminsAsync($"Payment mismatch on order #{order.OrderNo}",
                    $"Reference {reference} paid {amountMinor / 100m:0.00} {currency}; order total is {ViewHelpers.Currency(order.InvoicePrice)}.", $"/Admin/Orders/Details/{order.OrderNo}");
                return ServiceResult<Order>.Fail("The amount paid doesn't match the order total.");
            }

            if (order.Status == OrderStatus.Cancelled)
            {
                // Paid after the order expired: keep it cancelled and flag a refund.
                order.PaymentStatus = PaymentStatus.Paid;
                order.PaidAt = DateTime.UtcNow;
                order.PaymentReference = reference;
                AddEvent(order, "Payment received after cancellation", "This order had already been cancelled. A refund will be arranged.");
                await _db.SaveChangesAsync();
                await _notifications.NotifyAdminsAsync($"Refund needed for order #{order.OrderNo}",
                    "Payment arrived after the order was cancelled.", $"/Admin/Orders/Details/{order.OrderNo}");
                return ServiceResult<Order>.Ok(order);
            }

            order.PaymentReference = reference;
            ConfirmPayment(order, reference);
            await _db.SaveChangesAsync();

            await _notifications.NotifyAsync(order.UserId, $"Payment received for order #{order.OrderNo}",
                $"We received {ViewHelpers.Currency(order.InvoicePrice)}. Thank you!", $"/Orders/Details/{order.OrderNo}");
            await NotifySellersAsync(order);
            return ServiceResult<Order>.Ok(order);
        }

        public async Task MarkPaymentFailedAsync(string reference, string? reason)
        {
            var orderNo = OrderNoFromReference(reference);
            var order = orderNo == null ? null : await _db.Orders.FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null || order.PaymentStatus == PaymentStatus.Paid)
            {
                return;
            }
            order.PaymentStatus = PaymentStatus.Failed;
            AddEvent(order, "Payment not completed", reason);
            await _db.SaveChangesAsync();
        }

        // Starts a new payment attempt for an unpaid online order.
        public async Task<ServiceResult<Order>> NewPaymentAttemptAsync(int orderNo, string userId)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderNo == orderNo && o.UserId == userId);
            if (order == null || order.Status != OrderStatus.AwaitingPayment || order.PaymentStatus == PaymentStatus.Paid)
            {
                return ServiceResult<Order>.Fail("This order doesn't need a payment.");
            }
            order.PaymentReference = NewPaymentReference(order.OrderNo);
            order.PaymentStatus = PaymentStatus.Pending;
            await _db.SaveChangesAsync();
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
            order.Status == OrderStatus.AwaitingPayment
            || (order.Status is OrderStatus.Pending or OrderStatus.Processing
                && order.HasFulfilmentItems
                && !order.Products.Any(i => i.Fulfilled && i.ProductType is ProductType.Physical or ProductType.Service));

        public async Task<ServiceResult<Order>> UpdateStatusAsync(int orderNo, OrderStatus status, string? note = null)
        {
            var order = await _db.Orders.Include(o => o.Products).FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null)
            {
                return ServiceResult<Order>.Fail("Order not found.");
            }
            if (order.Status == OrderStatus.Cancelled)
            {
                return ServiceResult<Order>.Fail("Cancelled orders can't be changed.");
            }
            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (order.Status == status && note == null)
            {
                return ServiceResult<Order>.Ok(order);
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();

            if (status == OrderStatus.Cancelled)
            {
                // Return physical stock, release the coupon and revoke download access from this order.
                foreach (var item in order.Products.Where(i => i.ProductType == ProductType.Physical))
                {
                    await _db.Products.Where(p => p.ProductCode == item.ProductCode)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + item.Quantity));
                }
                if (order.CouponCode != null && order.PaymentStatus != PaymentStatus.Paid)
                {
                    await _db.Coupons.Where(c => c.Code == order.CouponCode && c.UsedCount > 0)
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, c => c.UsedCount - 1));
                }
                await _db.LibraryEntries.Where(l => l.OrderNo == order.OrderNo).ExecuteDeleteAsync();
                if (order.PaymentStatus == PaymentStatus.Paid)
                {
                    note = (note == null ? "" : note + " ") + "A refund will be issued for the amount paid.";
                }
            }
            else if (status is OrderStatus.Delivered or OrderStatus.Completed)
            {
                foreach (var item in order.Products.Where(i => !i.Fulfilled))
                {
                    item.Fulfilled = true;
                    item.FulfilledAt = DateTime.UtcNow;
                }
                if (order.PaymentStatus != PaymentStatus.Paid && order.PaymentMethod == CheckoutPayment.OnDelivery)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.PaidAt = DateTime.UtcNow;
                }
            }

            var changed = order.Status != status;
            order.Status = status;
            if (note != null)
            {
                order.TrackingNote = note.Length > 200 ? note[..200] : note;
            }
            AddEvent(order, changed ? ViewHelpers.StatusLabel(status) : "Update", note);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            await _notifications.NotifyAsync(order.UserId,
                changed ? $"Order #{order.OrderNo} is now {ViewHelpers.StatusLabel(status).ToLowerInvariant()}" : $"Update on order #{order.OrderNo}",
                note ?? StatusMessage(status), $"/Orders/Details/{order.OrderNo}");
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
            if (order.Status is OrderStatus.Cancelled or OrderStatus.AwaitingPayment)
            {
                return ServiceResult<Order>.Fail(order.Status == OrderStatus.Cancelled ? "This order was cancelled." : "This order hasn't been paid yet.");
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
            var title = item.ProductType == ProductType.Service ? "Service completed" : "Item shipped";
            AddEvent(order, title, item.ProductName);
            await _db.SaveChangesAsync();

            await _notifications.NotifyAsync(order.UserId, $"{title}: {item.ProductName}",
                $"Update on your order #{order.OrderNo}.", $"/Orders/Details/{order.OrderNo}");
            return ServiceResult<Order>.Ok(order);
        }

        // Cancels online orders that were never paid, releasing their stock.
        public async Task<int> ExpireUnpaidAsync(TimeSpan maxAge)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            var expired = await _db.Orders
                .Where(o => o.Status == OrderStatus.AwaitingPayment && o.PaymentStatus != PaymentStatus.Paid && o.OrderDate < cutoff)
                .Select(o => o.OrderNo).ToListAsync();
            foreach (var orderNo in expired)
            {
                await UpdateStatusAsync(orderNo, OrderStatus.Cancelled, "Payment was not completed in time.");
            }
            return expired.Count;
        }

        private void ConfirmPayment(Order order, string? reference)
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.PaidAt = DateTime.UtcNow;
            foreach (var item in order.Products.Where(i => i.ProductType is ProductType.Ebook or ProductType.Digital && !i.Fulfilled))
            {
                item.Fulfilled = true;
                item.FulfilledAt = DateTime.UtcNow;
                if (order.UserId != null
                    && !_db.LibraryEntries.Local.Any(l => l.UserId == order.UserId && l.ProductCode == item.ProductCode)
                    && !_db.LibraryEntries.Any(l => l.UserId == order.UserId && l.ProductCode == item.ProductCode))
                {
                    _db.LibraryEntries.Add(new LibraryEntry { UserId = order.UserId, ProductCode = item.ProductCode, OrderNo = order.OrderNo });
                }
            }
            order.Status = order.Products.All(i => i.Fulfilled) ? OrderStatus.Completed : OrderStatus.Pending;
            order.UpdatedAt = DateTime.UtcNow;
            AddEvent(order, "Payment received", reference == null ? null : $"Reference {reference}");
            if (order.Status == OrderStatus.Completed)
            {
                AddEvent(order, "Delivered to your library", "Your downloads are ready.");
            }
        }

        private async Task NotifyPlacedAsync(Order order)
        {
            var url = $"/Orders/Details/{order.OrderNo}";
            if (order.Status == OrderStatus.AwaitingPayment)
            {
                await _notifications.NotifyAsync(order.UserId, $"Order #{order.OrderNo} is waiting for payment",
                    $"Complete your payment of {ViewHelpers.Currency(order.InvoicePrice)} to confirm the order.", url, sendEmail: false);
                return;
            }
            await _notifications.NotifyAsync(order.UserId, $"Order #{order.OrderNo} confirmed",
                $"Thanks for shopping with {_store.Name}! Total: {ViewHelpers.Currency(order.InvoicePrice)}.", url);
            await NotifySellersAsync(order);
        }

        private async Task NotifySellersAsync(Order order)
        {
            foreach (var group in order.Products.Where(i => i.SellerId != null).GroupBy(i => i.SellerId))
            {
                var needsAction = group.Any(i => i.ProductType is ProductType.Physical or ProductType.Service);
                await _notifications.NotifyAsync(group.Key, $"New order #{order.OrderNo}",
                    string.Join(", ", group.Select(i => $"{i.Quantity} × {i.ProductName}")) + (needsAction ? " - please fulfil it." : "."),
                    needsAction ? "/Sell/Orders" : "/Sell");
            }
        }

        private static void AddEvent(Order order, string title, string? message) =>
            order.Events.Add(new OrderEvent { OrderNo = order.OrderNo, Title = title, Message = message });

        private static string StatusMessage(OrderStatus status) => status switch
        {
            OrderStatus.Processing => "We're preparing your order.",
            OrderStatus.Shipped => "Your order is on its way.",
            OrderStatus.Delivered => "Your order has been delivered.",
            OrderStatus.Completed => "Your order is complete. Thank you!",
            OrderStatus.Cancelled => "Your order was cancelled.",
            _ => "Your order was updated."
        };
    }

    // Payment method labels stored on orders.
    public static class CheckoutPayment
    {
        public const string Paystack = "Paystack";
        public const string Demo = "Card (demo)";
        public const string OnDelivery = "Pay on delivery";
    }
}
