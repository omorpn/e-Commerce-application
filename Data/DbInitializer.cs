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
            var env = provider.GetRequiredService<IHostEnvironment>();
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbInitializer));

            await ResetLegacyDevelopmentDatabaseAsync(db, env, logger);
            await db.Database.MigrateAsync();

            var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { Roles.Admin, Roles.Seller })
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

        // Earlier builds created the schema without migrations. A development database from
        // those builds can't be migrated, so it is recreated (it only holds sample data).
        private static async Task ResetLegacyDevelopmentDatabaseAsync(AppDbContext db, IHostEnvironment env, ILogger logger)
        {
            if (!db.Database.IsSqlite() || !await db.Database.CanConnectAsync() || (await db.Database.GetAppliedMigrationsAsync()).Any())
            {
                return;
            }

            var legacy = await db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM sqlite_master WHERE type = 'table' AND name = 'Products'")
                .SingleAsync() > 0;
            if (!legacy)
            {
                return;
            }

            if (!env.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "The database was created by an older build without migrations. Back it up and delete it so it can be recreated.");
            }

            logger.LogWarning("Recreating development database created by an older build of the app.");
            await db.Database.EnsureDeletedAsync();
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
            var now = DateTime.UtcNow;

            Product Physical(string name, string category, decimal price, int stock, string description, decimal? listPrice = null) => new()
            {
                Type = ProductType.Physical,
                Name = name,
                Category = category,
                Price = price,
                ListPrice = listPrice,
                Stock = stock,
                Description = description,
                PublishedAt = now
            };

            Product Service(string name, string category, decimal price, int minutes, ServiceLocation location, string area, string description, decimal? listPrice = null) => new()
            {
                Type = ProductType.Service,
                Name = name,
                Category = category,
                Price = price,
                ListPrice = listPrice,
                DurationMinutes = minutes,
                ServiceLocation = location,
                ServiceArea = area,
                Description = description,
                PublishedAt = now
            };

            async Task<Product> Download(ProductType type, string name, string category, decimal price, string description,
                byte[] file, FileKind kind, string fileName, string? author = null, decimal? listPrice = null)
            {
                using var stream = new MemoryStream(file);
                var key = await storage.SaveAsync(stream, "downloads", kind.Extension);
                return new Product
                {
                    Type = type,
                    Name = name,
                    Category = category,
                    Price = price,
                    ListPrice = listPrice,
                    Description = description,
                    AuthorName = author,
                    Language = type == ProductType.Ebook ? "English" : null,
                    PageCount = type == ProductType.Ebook ? 1 : null,
                    FilePath = key,
                    FileName = fileName,
                    FileContentType = kind.ContentType,
                    FileSize = file.Length,
                    PublishedAt = now
                };
            }

            byte[] Pdf(string title, string author, string description) => SamplePdf.Create(title, author, new[]
            {
                description,
                "This is a sample file bundled with the store so you can try buying, downloading and opening an item right away.",
                "Sellers can list their own ebooks, digital downloads, products and services from Seller Central."
            });

            db.Products.AddRange(
                Physical("Wireless Noise-Cancelling Headphones", "Electronics", 129.99m, 25,
                    "Over-ear Bluetooth headphones with active noise cancelling, 30-hour battery life and fast USB-C charging.", 169.99m),
                Physical("Smartwatch Series 5", "Electronics", 199.00m, 15,
                    "Fitness and health tracking, heart-rate monitor, GPS and a bright always-on display. Water resistant to 50 m."),
                Physical("Portable Bluetooth Speaker", "Electronics", 49.99m, 40,
                    "Compact waterproof speaker with deep bass and 12 hours of playtime.", 64.99m),
                Physical("USB-C 7-in-1 Hub", "Computers", 34.99m, 0,
                    "HDMI 4K, 3x USB-A, SD/microSD and 100 W power delivery pass-through."),
                Physical("14\" Ultrabook Laptop, 16 GB RAM", "Computers", 849.00m, 6,
                    "Lightweight aluminium laptop with a 14-inch 2.8K display, 512 GB SSD and all-day battery.", 999.00m),
                Physical("Android Smartphone 128 GB", "Phones & Tablets", 279.00m, 18,
                    "6.5-inch AMOLED display, triple camera and 5000 mAh battery. Dual SIM, unlocked."),
                Physical("Stainless Steel Water Bottle", "Home & Kitchen", 19.99m, 100,
                    "Double-wall insulated 750 ml bottle. Keeps drinks cold for 24 hours or hot for 12."),
                Physical("Ceramic Pour-Over Coffee Set", "Home & Kitchen", 39.00m, 30,
                    "Hand-glazed dripper, server and two cups for a slow, flavourful brew."),
                Physical("Non-stick Frying Pan 28 cm", "Home & Kitchen", 28.75m, 50,
                    "PFOA-free non-stick coating, induction compatible, oven safe to 220 C.", 35.00m),
                Physical("Ergonomic Office Chair", "Furniture", 159.00m, 12,
                    "Adjustable lumbar support, breathable mesh back and 4D armrests."),
                Physical("Classic Denim Jacket", "Fashion", 59.99m, 20,
                    "Timeless mid-wash denim jacket with button front and chest pockets."),
                Physical("Women's Running Sneakers", "Fashion", 74.99m, 35,
                    "Lightweight breathable mesh upper with responsive cushioning.", 89.99m),
                Physical("Sterling Silver Pendant Necklace", "Jewelry", 45.00m, 22,
                    "925 sterling silver pendant on an 18-inch chain, gift boxed."),
                Physical("Vitamin C Brightening Serum", "Beauty", 18.50m, 70,
                    "Lightweight daily serum with 15% vitamin C and hyaluronic acid."),
                Physical("Organic Arabica Coffee Beans 1 kg", "Grocery", 24.00m, 45,
                    "Medium roast whole beans with notes of chocolate and citrus."),
                Physical("Building Blocks Set, 500 Pieces", "Toys & Games", 29.99m, 33,
                    "Creative construction set compatible with major brick brands. Ages 6+.", 39.99m),
                Physical("Non-slip Yoga Mat", "Sports & Outdoors", 22.00m, 45,
                    "6 mm thick eco-friendly mat with carrying strap."),
                Physical("2-Person Camping Tent", "Sports & Outdoors", 119.00m, 4,
                    "Waterproof, quick-pitch dome tent that packs down to 2.3 kg."),
                Physical("Dog Chew Toy Bundle", "Pet Supplies", 16.99m, 60,
                    "Five durable rubber and rope toys for medium and large dogs."),
                Physical("Car Phone Mount", "Automotive", 14.99m, 80,
                    "Magnetic dashboard and vent mount with one-hand release."),

                Service("Home Deep Cleaning (3 hours)", "Home Services", 89.00m, 180, ServiceLocation.CustomerAddress, "Lagos, Abuja",
                    "Two professional cleaners, all supplies included: kitchen, bathrooms, floors, dusting and windows.", 110.00m),
                Service("AC & Appliance Repair Visit", "Repairs", 45.00m, 60, ServiceLocation.CustomerAddress, "Lagos",
                    "A certified technician diagnoses and fixes air conditioners, fridges and washing machines. Parts billed separately."),
                Service("1-on-1 Maths Tutoring (60 min)", "Tutoring & Lessons", 30.00m, 60, ServiceLocation.Online, "Worldwide",
                    "Personalised lesson for secondary school or university maths over video call."),
                Service("Professional Logo Design", "Design & Creative", 149.00m, 4320, ServiceLocation.Online, "Worldwide",
                    "Three original logo concepts, two rounds of revisions and final files in PNG, SVG and PDF."),
                Service("Website Setup Consultation", "Programming & Tech", 60.00m, 60, ServiceLocation.Online, "Worldwide",
                    "A one-hour call to plan your website or online store, choose tools and map out next steps."),
                Service("Personal Fitness Coaching Session", "Fitness & Coaching", 40.00m, 60, ServiceLocation.ProviderLocation, "Victoria Island, Lagos",
                    "One-to-one training session with a certified coach, including a fitness assessment."),
                Service("CV & Cover Letter Writing", "Writing & Translation", 55.00m, 2880, ServiceLocation.Online, "Worldwide",
                    "A professionally rewritten CV and a tailored cover letter, delivered within two days."));

            db.Products.AddRange(new[]
            {
                await Download(ProductType.Ebook, "The Quiet Algorithm", "Science Fiction", 4.99m,
                    "When a city's traffic AI starts making choices no one programmed, a junior engineer has seventy-two hours to find out who - or what - is really in control.",
                    Pdf("The Quiet Algorithm", "Mara Ellison", "A science fiction thriller."), FileSignatures.Pdf, "The Quiet Algorithm.pdf", "Mara Ellison"),
                await Download(ProductType.Ebook, "Small Business, Big Plans", "Business & Money", 9.99m,
                    "A practical, step-by-step playbook for turning a side hustle into a profitable small business: pricing, cash flow, marketing and hiring your first employee.",
                    Pdf("Small Business, Big Plans", "Daniel Okafor", "A small business playbook."), FileSignatures.Pdf, "Small Business, Big Plans.pdf", "Daniel Okafor", 14.99m),
                await Download(ProductType.Ebook, "Learn C# in 30 Days", "Computers & Technology", 14.99m,
                    "Thirty focused lessons that take you from your first Console.WriteLine to building and testing a real ASP.NET Core web application.",
                    Pdf("Learn C# in 30 Days", "Priya Raman", "A beginner's programming course."), FileSignatures.Pdf, "Learn C# in 30 Days.pdf", "Priya Raman"),
                await Download(ProductType.Ebook, "Letters from Lagos", "Fiction", 0m,
                    "A free collection of short stories about love, ambition and family across three generations in one of Africa's busiest cities.",
                    Pdf("Letters from Lagos", "Adaeze Obi", "Short stories."), FileSignatures.Pdf, "Letters from Lagos.pdf", "Adaeze Obi"),
                await Download(ProductType.Ebook, "The Mindful Morning", "Self-Help", 3.99m,
                    "Simple ten-minute routines to start every day calmer, clearer and more focused.",
                    Pdf("The Mindful Morning", "Hannah Brooks", "Morning routines."), FileSignatures.Pdf, "The Mindful Morning.pdf", "Hannah Brooks"),
                await Download(ProductType.Ebook, "Recipes from Grandma's Kitchen", "Cookbooks", 6.49m,
                    "Sixty comforting family recipes, from weeknight soups to celebration cakes, with tips passed down through generations.",
                    Pdf("Recipes from Grandma's Kitchen", "Rosa Martinez", "Family recipes."), FileSignatures.Pdf, "Recipes from Grandma's Kitchen.pdf", "Rosa Martinez"),

                await Download(ProductType.Digital, "Small Business Budget Templates", "Templates", 12.00m,
                    "Monthly budget, cash-flow forecast and invoice templates for CSV and spreadsheet apps. Instant download as a ZIP.",
                    SampleFiles.Zip(("README.txt", "Small Business Budget Templates\n\nOpen the CSV files in any spreadsheet app."),
                        ("monthly-budget.csv", "Category,Planned,Actual\nRent,0,0\nPayroll,0,0\nMarketing,0,0\nSupplies,0,0\n"),
                        ("cash-flow.csv", "Month,Money in,Money out,Balance\nJanuary,0,0,0\nFebruary,0,0,0\n"),
                        ("invoice.csv", "Item,Quantity,Unit price,Total\n,,,\n")),
                    FileSignatures.Zip, "budget-templates.zip", listPrice: 18.00m),
                await Download(ProductType.Digital, "Meditation Bell Sound", "Music", 1.99m,
                    "A calm, high-quality singing bell tone for meditation apps, videos and mindfulness practice. WAV format.",
                    SampleFiles.Tone(528, 4), FileSignatures.Wav, "meditation-bell.wav"),
                await Download(ProductType.Digital, "Freelancer Starter Kit", "Online Courses", 29.00m,
                    "A short course workbook covering finding clients, pricing your work, writing proposals and getting paid on time.",
                    Pdf("Freelancer Starter Kit", "ShopNest Academy", "A course workbook for new freelancers."), FileSignatures.Pdf, "freelancer-starter-kit.pdf"),
                await Download(ProductType.Digital, "Social Media Post Templates Pack", "Graphics & Art", 9.00m,
                    "Thirty ready-to-edit post layouts and a colour guide for Instagram, Facebook and LinkedIn.",
                    SampleFiles.Zip(("README.txt", "Social Media Post Templates Pack\n\nImport the layouts into your favourite design tool."),
                        ("colour-guide.txt", "Primary: #232F3E\nAccent: #FEBD69\nHighlight: #FFA41C\n")),
                    FileSignatures.Zip, "social-templates.zip"),
                await Download(ProductType.Digital, "Weekly Planner Printable", "Templates", 0m,
                    "A free printable weekly planner with priorities, habits and notes sections.",
                    Pdf("Weekly Planner", "ShopNest Studio", "Print this page each week to plan your priorities, habits and notes."), FileSignatures.Pdf, "weekly-planner.pdf")
            });

            await db.SaveChangesAsync();
        }
    }
}
