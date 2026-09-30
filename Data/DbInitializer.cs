using e_Commerce_application.Models;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var provider = scope.ServiceProvider;
            var db = provider.GetRequiredService<AppDbContext>();
            var config = provider.GetRequiredService<IConfiguration>();
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbInitializer));

            await db.Database.EnsureCreatedAsync();

            var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { Roles.Admin, Roles.Author })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            await EnsureAdminAsync(provider.GetRequiredService<UserManager<ApplicationUser>>(), config, logger);

            if (config.GetValue("SeedSampleData", true) && !await db.Products.AnyAsync())
            {
                await SeedCatalogAsync(db, provider.GetRequiredService<IFileStorage>());
                logger.LogInformation("Seeded sample catalog");
            }
        }

        private static async Task EnsureAdminAsync(UserManager<ApplicationUser> users, IConfiguration config, ILogger logger)
        {
            var email = config["Admin:Email"];
            var password = config["Admin:Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("Admin:Email / Admin:Password are not configured; no admin account was created.");
                return;
            }

            var admin = await users.FindByEmailAsync(email);
            if (admin == null)
            {
                admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Store Admin" };
                var result = await users.CreateAsync(admin, password);
                if (!result.Succeeded)
                {
                    logger.LogError("Could not create admin account: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
                    return;
                }
            }

            if (!await users.IsInRoleAsync(admin, Roles.Admin))
            {
                await users.AddToRoleAsync(admin, Roles.Admin);
            }
        }

        private static async Task SeedCatalogAsync(AppDbContext db, IFileStorage storage)
        {
            Product Physical(string name, string category, decimal price, int stock, string description) => new()
            {
                Type = ProductType.Physical,
                Name = name,
                Category = category,
                Price = price,
                Stock = stock,
                Description = description,
                Status = ListingStatus.Published,
                PublishedAt = DateTime.UtcNow
            };

            db.Products.AddRange(
                Physical("Wireless Noise-Cancelling Headphones", "Electronics", 129.99m, 25,
                    "Over-ear Bluetooth headphones with active noise cancelling, 30-hour battery life and fast USB-C charging."),
                Physical("Smartwatch Series 5", "Electronics", 199.00m, 15,
                    "Fitness and health tracking, heart-rate monitor, GPS and a bright always-on display. Water resistant to 50 m."),
                Physical("Portable Bluetooth Speaker", "Electronics", 49.99m, 40,
                    "Compact waterproof speaker with deep bass and 12 hours of playtime."),
                Physical("USB-C 7-in-1 Hub", "Electronics", 34.99m, 0,
                    "HDMI 4K, 3x USB-A, SD/microSD and 100 W power delivery pass-through."),
                Physical("Stainless Steel Water Bottle", "Home & Kitchen", 19.99m, 100,
                    "Double-wall insulated 750 ml bottle. Keeps drinks cold for 24 hours or hot for 12."),
                Physical("Ceramic Pour-Over Coffee Set", "Home & Kitchen", 39.00m, 30,
                    "Hand-glazed dripper, server and two cups for a slow, flavourful brew."),
                Physical("Non-stick Frying Pan 28 cm", "Home & Kitchen", 28.75m, 50,
                    "PFOA-free non-stick coating, induction compatible, oven safe to 220 C."),
                Physical("Classic Denim Jacket", "Fashion", 59.99m, 20,
                    "Timeless mid-wash denim jacket with button front and chest pockets."),
                Physical("Women's Running Sneakers", "Fashion", 74.99m, 35,
                    "Lightweight breathable mesh upper with responsive cushioning."),
                Physical("Leather Bifold Wallet", "Fashion", 24.50m, 60,
                    "Genuine leather wallet with RFID blocking and eight card slots."),
                Physical("Non-slip Yoga Mat", "Sports & Outdoors", 22.00m, 45,
                    "6 mm thick eco-friendly mat with carrying strap."),
                Physical("2-Person Camping Tent", "Sports & Outdoors", 119.00m, 8,
                    "Waterproof, quick-pitch dome tent that packs down to 2.3 kg."));

            var ebooks = new[]
            {
                (Title: "The Quiet Algorithm", Author: "Mara Ellison", Category: "Science Fiction", Price: 4.99m,
                    Description: "When a city's traffic AI starts making choices no one programmed, a junior engineer has seventy-two hours to find out who - or what - is really in control."),
                (Title: "Small Business, Big Plans", Author: "Daniel Okafor", Category: "Business & Money", Price: 9.99m,
                    Description: "A practical, step-by-step playbook for turning a side hustle into a profitable small business: pricing, cash flow, marketing and hiring your first employee."),
                (Title: "Learn C# in 30 Days", Author: "Priya Raman", Category: "Computers & Technology", Price: 14.99m,
                    Description: "Thirty focused lessons that take you from your first Console.WriteLine to building and testing a real ASP.NET Core web application."),
                (Title: "Letters from Lagos", Author: "Adaeze Obi", Category: "Fiction", Price: 0m,
                    Description: "A free collection of short stories about love, ambition and family across three generations in one of Africa's busiest cities."),
                (Title: "The Mindful Morning", Author: "Hannah Brooks", Category: "Self-Help", Price: 3.99m,
                    Description: "Simple ten-minute routines to start every day calmer, clearer and more focused."),
                (Title: "Recipes from Grandma's Kitchen", Author: "Rosa Martinez", Category: "Cookbooks", Price: 6.49m,
                    Description: "Sixty comforting family recipes, from weeknight soups to celebration cakes, with tips passed down through generations.")
            };

            foreach (var e in ebooks)
            {
                var pdf = SamplePdf.Create(e.Title, e.Author, new[]
                {
                    e.Description,
                    "This is a sample ebook bundled with the store so you can try buying, downloading and reading a title right away.",
                    "Authors can publish their own PDF or EPUB books from the Publish dashboard."
                });
                using var stream = new MemoryStream(pdf);
                var key = await storage.SaveAsync(stream, "ebooks", ".pdf");

                db.Products.Add(new Product
                {
                    Type = ProductType.Ebook,
                    Name = e.Title,
                    AuthorName = e.Author,
                    Category = e.Category,
                    Price = e.Price,
                    Description = e.Description,
                    Language = "English",
                    PageCount = 1,
                    FilePath = key,
                    FileName = e.Title + ".pdf",
                    FileContentType = FileSignatures.Pdf.ContentType,
                    FileSize = pdf.Length,
                    Status = ListingStatus.Published,
                    PublishedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();
        }
    }
}
