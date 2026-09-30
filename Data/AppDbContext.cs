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
            });

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
