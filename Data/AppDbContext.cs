using e_Commerce_application.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Data
{
    // Shared model. SqliteAppDbContext and PostgresAppDbContext add provider-specific
    // settings and each has its own migrations.
    public abstract class AppDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
    {
        protected AppDbContext(DbContextOptions options) : base(options) { }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<LibraryEntry> LibraryEntries => Set<LibraryEntry>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
        public DbSet<FileBlob> FileBlobs => Set<FileBlob>();
        public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();
        public DbSet<Coupon> Coupons => Set<Coupon>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();

        // Sign-in encryption keys, so logins survive restarts on hosts without a disk.
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Product>(e =>
            {
                e.HasIndex(p => new { p.Type, p.Status });
                e.HasIndex(p => p.Category);
                e.HasIndex(p => p.SellerId);
                e.HasOne(p => p.Seller).WithMany().HasForeignKey(p => p.SellerId).OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Order>(e =>
            {
                e.HasMany(o => o.Products).WithOne().HasForeignKey(i => i.OrderNo).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(o => o.User).WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(o => o.UserId);
                e.HasIndex(o => o.PaymentReference);
                e.HasMany(o => o.Events).WithOne().HasForeignKey(ev => ev.OrderNo).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Coupon>(e => e.HasIndex(c => c.Code).IsUnique());

            builder.Entity<Notification>(e =>
            {
                e.HasIndex(n => new { n.UserId, n.IsRead });
                e.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Conversation>(e =>
            {
                e.HasIndex(c => new { c.BuyerId, c.SellerId, c.ProductCode });
                e.HasIndex(c => c.SellerId);
                e.HasOne(c => c.Buyer).WithMany().HasForeignKey(c => c.BuyerId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.Seller).WithMany().HasForeignKey(c => c.SellerId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.Product).WithMany().HasForeignKey(c => c.ProductCode).OnDelete(DeleteBehavior.SetNull);
                e.HasMany(c => c.Messages).WithOne().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ChatMessage>(e =>
                e.HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Cascade));

            builder.Entity<OrderItem>(e =>
            {
                // No FK to Product: order lines keep a snapshot even if the listing is deleted.
                e.HasIndex(i => i.ProductCode);
                e.HasIndex(i => i.SellerId);
            });

            builder.Entity<FileBlob>(e => e.HasIndex(f => f.Key).IsUnique());

            builder.Entity<WishlistItem>(e =>
            {
                e.HasIndex(w => new { w.UserId, w.ProductCode }).IsUnique();
                e.HasOne(w => w.User).WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(w => w.Product).WithMany().HasForeignKey(w => w.ProductCode).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<LibraryEntry>(e =>
            {
                e.HasIndex(l => new { l.UserId, l.ProductCode }).IsUnique();
                e.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductCode).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Review>(e =>
            {
                e.HasIndex(r => new { r.ProductCode, r.UserId }).IsUnique();
                e.HasOne(r => r.Product).WithMany(p => p.Reviews).HasForeignKey(r => r.ProductCode).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
