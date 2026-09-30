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

            if (config.GetValue("SeedSampleData", true))
            {
                await SeedOrUpgradeCatalogAsync(db, provider.GetRequiredService<IFileStorage>(), logger);
            }
        }

        private const string CatalogVersionKey = "SampleCatalogVersion";
        private const string CatalogVersion = "2";

        // Names from the first sample catalog (US dollar prices), used to recognise an untouched copy.
        private static readonly HashSet<string> Version1Names = new()
        {
            "Wireless Noise-Cancelling Headphones", "Smartwatch Series 5", "Portable Bluetooth Speaker", "USB-C 7-in-1 Hub",
            "14\" Ultrabook Laptop, 16 GB RAM", "Android Smartphone 128 GB", "Stainless Steel Water Bottle", "Ceramic Pour-Over Coffee Set",
            "Non-stick Frying Pan 28 cm", "Ergonomic Office Chair", "Classic Denim Jacket", "Women's Running Sneakers",
            "Sterling Silver Pendant Necklace", "Vitamin C Brightening Serum", "Organic Arabica Coffee Beans 1 kg",
            "Building Blocks Set, 500 Pieces", "Non-slip Yoga Mat", "2-Person Camping Tent", "Dog Chew Toy Bundle", "Car Phone Mount",
            "Home Deep Cleaning (3 hours)", "AC & Appliance Repair Visit", "1-on-1 Maths Tutoring (60 min)", "Professional Logo Design",
            "Website Setup Consultation", "Personal Fitness Coaching Session", "CV & Cover Letter Writing", "The Quiet Algorithm",
            "Small Business, Big Plans", "Learn C# in 30 Days", "Letters from Lagos", "The Mindful Morning",
            "Recipes from Grandma's Kitchen", "Small Business Budget Templates", "Meditation Bell Sound", "Freelancer Starter Kit",
            "Social Media Post Templates Pack", "Weekly Planner Printable"
        };

        private static async Task SeedOrUpgradeCatalogAsync(AppDbContext db, IFileStorage storage, ILogger logger)
        {
            var version = await db.SiteSettings.FindAsync(CatalogVersionKey);
            if (version?.Value == CatalogVersion)
            {
                return;
            }

            if (!await db.Products.AnyAsync())
            {
                await SeedCatalogAsync(db, storage);
                logger.LogInformation("Seeded sample catalog");
            }
            else
            {
                // Replace an untouched older sample catalog (no orders, no seller listings, only sample items).
                var storeProducts = await db.Products.Where(p => p.SellerId == null).ToListAsync();
                var untouched = !await db.Orders.AnyAsync()
                    && !await db.Products.AnyAsync(p => p.SellerId != null)
                    && storeProducts.All(p => Version1Names.Contains(p.Name));
                if (untouched)
                {
                    var codes = storeProducts.Select(p => p.ProductCode).ToList();
                    await db.LibraryEntries.Where(l => codes.Contains(l.ProductCode)).ExecuteDeleteAsync();
                    db.Products.RemoveRange(storeProducts);
                    await db.SaveChangesAsync();
                    foreach (var product in storeProducts)
                    {
                        storage.Delete(product.FilePath);
                        storage.Delete(product.ImagePath);
                    }
                    await SeedCatalogAsync(db, storage);
                    logger.LogInformation("Replaced the previous sample catalog with the Naira catalog");
                }
            }

            if (version == null)
            {
                db.SiteSettings.Add(new SiteSetting { Key = CatalogVersionKey, Value = CatalogVersion });
            }
            else
            {
                version.Value = CatalogVersion;
            }
            await db.SaveChangesAsync();
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

            Product Physical(string name, string category, decimal price, int stock, string description, decimal? listPrice = null, DateTime? dealEndsAt = null) => new()
            {
                Type = ProductType.Physical,
                Name = name,
                Category = category,
                Price = price,
                ListPrice = listPrice,
                DealEndsAt = dealEndsAt,
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

            var flashSaleEnds = DateTime.UtcNow.Date.AddDays(3).AddHours(22);

            db.Products.AddRange(
                Physical("Wireless Noise-Cancelling Headphones", "Electronics", 185000, 25,
                    "Over-ear Bluetooth headphones with active noise cancelling, 30-hour battery life and fast USB-C charging.", 230000, flashSaleEnds),
                Physical("Android Smartphone 128 GB, Dual SIM", "Phones & Tablets", 320000, 18,
                    "6.5-inch AMOLED display, 50 MP triple camera and 5000 mAh battery. Dual SIM, works on all Nigerian networks.", 365000),
                Physical("20,000 mAh Fast-Charging Power Bank", "Phone Accessories", 25000, 60,
                    "Charge your phone up to five times. 22.5 W fast charging, USB-C and two USB-A ports, LED battery display.", 32000, flashSaleEnds),
                Physical("Wireless Earbuds with Charging Case", "Phone Accessories", 28500, 45,
                    "Clear calls, deep bass and 24 hours of playtime with the case. Sweat resistant."),
                Physical("Smartwatch Series 5", "Watches", 250000, 12,
                    "Fitness and health tracking, heart-rate monitor, GPS and a bright always-on display. Water resistant to 50 m."),
                Physical("Men's Chronograph Leather Watch", "Watches", 65000, 20,
                    "Stainless steel case, genuine leather strap and a working chronograph. Gift boxed.", 85000),
                Physical("Oud Wood Eau de Parfum 100 ml", "Fragrances", 85000, 30,
                    "A warm, long-lasting oud and sandalwood scent. 100% genuine, sealed box.", 110000, flashSaleEnds),
                Physical("Floral Bloom Eau de Parfum for Women 50 ml", "Fragrances", 48000, 25,
                    "Fresh jasmine, rose and vanilla notes. 100% genuine, sealed box."),
                Physical("Portable Bluetooth Speaker", "Electronics", 45000, 40,
                    "Compact waterproof speaker with deep bass and 12 hours of playtime.", 60000),
                Physical("14\" Ultrabook Laptop, 16 GB RAM", "Computers", 1150000, 6,
                    "Lightweight aluminium laptop with a 14-inch 2.8K display, 512 GB SSD and all-day battery.", 1350000),
                Physical("USB-C 7-in-1 Hub", "Computers", 28000, 0,
                    "HDMI 4K, 3x USB-A, SD/microSD and 100 W power delivery pass-through."),
                Physical("Men's Senator Kaftan Set", "Fashion", 38000, 30,
                    "Tailored two-piece kaftan in breathable cotton blend. Available in navy, wine and cream."),
                Physical("Ankara Print Maxi Dress", "Fashion", 32000, 22,
                    "Vibrant, lined Ankara maxi dress with pockets. Hand finished.", 40000),
                Physical("Women's Running Sneakers", "Fashion", 55000, 35,
                    "Lightweight breathable mesh upper with responsive cushioning.", 70000),
                Physical("Vitamin C Brightening Serum", "Beauty", 12500, 70,
                    "Lightweight daily serum with 15% vitamin C and hyaluronic acid. 100% genuine."),
                Physical("Stainless Steel Water Bottle", "Home & Kitchen", 15000, 100,
                    "Double-wall insulated 750 ml bottle. Keeps drinks cold for 24 hours or hot for 12."),
                Physical("Non-stick Frying Pan 28 cm", "Home & Kitchen", 22000, 50,
                    "PFOA-free non-stick coating, induction compatible, oven safe to 220 C.", 28000),
                Physical("Ergonomic Office Chair", "Furniture", 145000, 8,
                    "Adjustable lumbar support, breathable mesh back and 4D armrests."),
                Physical("Branded Souvenir Mug Set (6 pieces)", "Souvenirs", 12000, 80,
                    "Ceramic mugs, perfect for weddings, birthdays and company events. Custom printing available on request."),
                Physical("Building Blocks Set, 500 Pieces", "Toys & Games", 25000, 33,
                    "Creative construction set compatible with major brick brands. Ages 6+.", 32000),
                Physical("Car Phone Mount", "Automotive", 7500, 4,
                    "Magnetic dashboard and vent mount with one-hand release."),

                Service("Home Deep Cleaning (3 hours)", "Home Services", 45000, 180, ServiceLocation.CustomerAddress, "Lagos, Abuja, Benin City",
                    "Two professional cleaners, all supplies included: kitchen, bathrooms, floors, dusting and windows.", 55000),
                Service("AC & Appliance Repair Visit", "Repairs", 15000, 60, ServiceLocation.CustomerAddress, "Lagos, Benin City",
                    "A certified technician diagnoses and fixes air conditioners, fridges and washing machines. Parts billed separately."),
                Service("Phone Screen Replacement", "Repairs", 20000, 90, ServiceLocation.ProviderLocation, "Ikeja, Lagos",
                    "Same-day screen replacement for popular Android and iPhone models with a 3-month warranty. Price covers labour."),
                Service("1-on-1 Maths Tutoring (60 min)", "Tutoring & Lessons", 10000, 60, ServiceLocation.Online, "Nationwide",
                    "WAEC, NECO, JAMB and university maths over video call, tailored to the student."),
                Service("Professional Logo Design", "Design & Creative", 75000, 4320, ServiceLocation.Online, "Worldwide",
                    "Three original logo concepts, two rounds of revisions and final files in PNG, SVG and PDF."),
                Service("Business Website Setup", "Programming & Tech", 250000, 10080, ServiceLocation.Online, "Nationwide",
                    "A professional 5-page website for your business, with WhatsApp chat, contact form and one year of support.", 300000),
                Service("Event Photography (4 hours)", "Events & Photography", 120000, 240, ServiceLocation.CustomerAddress, "Lagos, Abuja",
                    "A professional photographer for weddings, birthdays and corporate events, with 100+ edited photos."),
                Service("CV & Cover Letter Writing", "Writing & Translation", 20000, 2880, ServiceLocation.Online, "Nationwide",
                    "A professionally rewritten CV and a tailored cover letter, delivered within two days."));

            db.Coupons.Add(new Coupon
            {
                Code = "WELCOME10",
                Description = "10% off your first order over ₦20,000",
                PercentOff = 10,
                MinSubtotal = 20000
            });

            db.Products.AddRange(new[]
            {
                await Download(ProductType.Ebook, "Small Business, Big Plans", "Business & Money", 5000,
                    "A practical, step-by-step playbook for turning a side hustle into a profitable small business in Nigeria: pricing, cash flow, marketing and hiring your first staff.",
                    Pdf("Small Business, Big Plans", "Daniel Okafor", "A small business playbook."), FileSignatures.Pdf, "Small Business, Big Plans.pdf", "Daniel Okafor", 7500),
                await Download(ProductType.Ebook, "Letters from Lagos", "Fiction", 0,
                    "A free collection of short stories about love, ambition and family across three generations in one of Africa's busiest cities.",
                    Pdf("Letters from Lagos", "Adaeze Obi", "Short stories."), FileSignatures.Pdf, "Letters from Lagos.pdf", "Adaeze Obi"),
                await Download(ProductType.Ebook, "Learn C# in 30 Days", "Computers & Technology", 7500,
                    "Thirty focused lessons that take you from your first Console.WriteLine to building and testing a real ASP.NET Core web application.",
                    Pdf("Learn C# in 30 Days", "Priya Raman", "A beginner's programming course."), FileSignatures.Pdf, "Learn C# in 30 Days.pdf", "Priya Raman"),
                await Download(ProductType.Ebook, "The Quiet Algorithm", "Science Fiction", 2500,
                    "When a city's traffic AI starts making choices no one programmed, a junior engineer has seventy-two hours to find out who - or what - is really in control.",
                    Pdf("The Quiet Algorithm", "Mara Ellison", "A science fiction thriller."), FileSignatures.Pdf, "The Quiet Algorithm.pdf", "Mara Ellison"),
                await Download(ProductType.Ebook, "Naija Kitchen Classics", "Cookbooks", 3500,
                    "Jollof, egusi, ofada stew, puff-puff and fifty more favourites, with shopping lists and tips for busy weeknights.",
                    Pdf("Naija Kitchen Classics", "Funke Adebayo", "Nigerian recipes."), FileSignatures.Pdf, "Naija Kitchen Classics.pdf", "Funke Adebayo"),
                await Download(ProductType.Ebook, "The Mindful Morning", "Self-Help", 2000,
                    "Simple ten-minute routines to start every day calmer, clearer and more focused.",
                    Pdf("The Mindful Morning", "Hannah Brooks", "Morning routines."), FileSignatures.Pdf, "The Mindful Morning.pdf", "Hannah Brooks"),

                await Download(ProductType.Digital, "Small Business Budget Templates", "Templates", 5000,
                    "Monthly budget, cash-flow forecast and invoice templates for Excel, Google Sheets and any spreadsheet app. Instant download as a ZIP.",
                    SampleFiles.Zip(("README.txt", "Small Business Budget Templates\n\nOpen the CSV files in any spreadsheet app."),
                        ("monthly-budget.csv", "Category,Planned,Actual\nRent,0,0\nSalaries,0,0\nMarketing,0,0\nSupplies,0,0\n"),
                        ("cash-flow.csv", "Month,Money in,Money out,Balance\nJanuary,0,0,0\nFebruary,0,0,0\n"),
                        ("invoice.csv", "Item,Quantity,Unit price,Total\n,,,\n")),
                    FileSignatures.Zip, "budget-templates.zip", listPrice: 8000),
                await Download(ProductType.Digital, "Freelancer Starter Kit", "Online Courses", 15000,
                    "A course workbook covering finding clients, pricing your work in Naira and dollars, writing proposals and getting paid on time.",
                    Pdf("Freelancer Starter Kit", "ShopNest Academy", "A course workbook for new freelancers."), FileSignatures.Pdf, "freelancer-starter-kit.pdf"),
                await Download(ProductType.Digital, "Social Media Post Templates Pack", "Graphics & Art", 4500,
                    "Thirty ready-to-edit post layouts and a colour guide for Instagram, Facebook, WhatsApp Status and LinkedIn.",
                    SampleFiles.Zip(("README.txt", "Social Media Post Templates Pack\n\nImport the layouts into your favourite design tool."),
                        ("colour-guide.txt", "Primary: #232F3E\nAccent: #FEBD69\nHighlight: #FFA41C\n")),
                    FileSignatures.Zip, "social-templates.zip"),
                await Download(ProductType.Digital, "Meditation Bell Sound", "Music", 1000,
                    "A calm singing bell tone for meditation apps, videos and mindfulness practice. WAV format.",
                    SampleFiles.Tone(528, 4), FileSignatures.Wav, "meditation-bell.wav"),
                await Download(ProductType.Digital, "Weekly Planner Printable", "Templates", 0,
                    "A free printable weekly planner with priorities, habits and notes sections.",
                    Pdf("Weekly Planner", "ShopNest Studio", "Print this page each week to plan your priorities, habits and notes."), FileSignatures.Pdf, "weekly-planner.pdf")
            });

            await db.SaveChangesAsync();
        }
    }
}
