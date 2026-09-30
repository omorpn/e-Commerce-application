# ShopNest: marketplace for products, services and digital goods

An ASP.NET Core 8 MVC marketplace. Customers buy physical products, book services, download digital goods and read ebooks. Anyone can open a shop and sell all four.

[![Deploy to Render](https://render.com/images/deploy-to-render-button.svg)](https://render.com/deploy?repo=https://github.com/omorpn/e-Commerce-application)

## What it sells

| Type | Examples | How it's delivered |
|---|---|---|
| **Physical products** | Electronics, fashion, home, grocery, toys, automotive... | Stock is tracked; seller or store ships and marks it shipped |
| **Services** | Cleaning, repairs, tutoring, design, coaching... | Customer picks a date and leaves notes at checkout; provider marks it completed |
| **Digital downloads** | Courses, music, templates, software, graphics, video | Instant download (PDF, EPUB, ZIP, MP3, WAV, MP4, images) |
| **eBooks** | Self-published PDF or EPUB books | Instant download or read in the browser |

## Features

**Shopping**
- Departments, search, category and price filters, sorting (price, rating, newest, biggest discount) and paging
- **Today's Deals**: listings with a "was" price show the discount everywhere
- Product pages with stock, service duration/location, file format and size, ratings and verified-purchase reviews
- Wish list, browsing history, seller storefront pages
- Cart and checkout with shipping/service address, service scheduling and demo payment (no real charge)
- **Your Orders**: tracking per item (preparing/shipped, booked/completed) and cancellation before fulfilment (stock is returned automatically)
- **Your Library**: every ebook and digital download you own, open in the browser or download

**Selling (Seller Central, `/Sell`)**
- Any registered user can become a seller
- Create physical, service, digital or ebook listings with images, files, price and optional list price; save as draft or publish
- **Orders to fulfil**: see the customer's address, booking date and notes; mark lines shipped or completed
- Dashboard with units sold, sales and earnings (85% of sales, 70% royalties on ebooks, configurable)
- Public shop page listing everything you sell

**Admin (`/Admin`)**
- Dashboard: revenue, orders awaiting fulfilment, listings by type, low stock, users and sellers
- Store listings: create and manage the store's own products, services and downloads
- Seller listings: moderate everything sellers publish (take down with a reason, reinstate)
- Orders: search, filter, see each line's seller and fulfilment, update status
- Users: see customers and sellers, grant or remove admin access

**Accounts**: registration, sign in, password change, two-factor authentication and personal data download/delete, from ASP.NET Core Identity.

## Get a public link (free, no credit card)

The app runs on [Render](https://render.com)'s free plan with a free PostgreSQL database from [Neon](https://neon.tech). Neither asks for a card. All data, including uploaded files and sign-in keys, lives in the database, so nothing is lost when the free service sleeps or restarts.

1. **Create the database.** Sign up at [neon.tech](https://neon.tech) (GitHub or Google sign-in works) and create a project. On the dashboard click **Connect** and copy the connection string. It looks like
   `postgresql://neondb_owner:xxxx@ep-xxxx.eu-central-1.aws.neon.tech/neondb?sslmode=require`
2. **Deploy the app.** Click **Deploy to Render** above and sign in to Render with GitHub. When asked, fill in:
   - `ConnectionStrings__Default`: the Neon connection string from step 1
   - `Admin__Email`: the email you'll sign in with as admin
   - `Admin__Password`: 8+ characters with upper and lower case letters, a digit and a symbol, e.g. `MyShop#2026`
3. Click **Apply**. After the first build (a few minutes) your store is live at `https://shopnest-XXXX.onrender.com`. Sign in with the admin details to open `/Admin`.

Free plan limits: the service sleeps after 15 minutes without visitors and takes about a minute to wake on the next visit; Neon's free database holds 0.5 GB. Uploads are limited to 25 MB per file when stored in the database (`Storage__MaxFileMB` changes this, up to 100). Upgrading the Render plan removes the sleep; nothing else changes.

The same Docker image runs on any container host (Railway, Fly.io, Azure App Service, a VPS). Point `ConnectionStrings__Default` at PostgreSQL, or leave the default to use SQLite under `/data` with a persistent volume mounted there. The app listens on `$PORT` when set, otherwise 8080.

## Running locally

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project "e-Commerce application.csproj"
```

Open http://localhost:5195. On first start the app creates a SQLite database (`ecommerce.db`) and seeds a sample catalog of products, services, digital downloads and ebooks (with real downloadable files).

In Development an admin account is created from `appsettings.Development.json`:

| Email | Password |
|---|---|
| `admin@eshop.local` | `Admin@12345` |

Create a normal account with **Create account** to shop or sell.

## Configuration

| Setting | Default | Purpose |
|---|---|---|
| `ConnectionStrings:Default` | `Data Source=ecommerce.db` | SQLite file, or a PostgreSQL connection string / `postgres://` URL |
| `Storage:Provider` | `Database` with PostgreSQL, `FileSystem` with SQLite | Where uploads (downloads, covers, images) are kept |
| `Storage:Root` | `App_Data/storage` | Upload folder when using `FileSystem` |
| `Storage:MaxFileMB` | 25 (database) / 100 (file system) | Largest upload allowed |
| `Publishing:SellerRate` | `0.85` | Seller share of each sale |
| `Publishing:RoyaltyRate` | `0.70` | Author share of each ebook sale |
| `SeedSampleData` | `true` | Seed the sample catalog when the database is empty |
| `Admin:Email` / `Admin:Password` | empty (set in Development) | Admin account created at startup |

Environment variables use double underscores, e.g. `Admin__Email`.

## Database

SQLite and PostgreSQL are both supported. The schema is managed with EF Core migrations, one set per database (`Data/Migrations/Sqlite`, `Data/Migrations/Postgres`), applied automatically at startup. Sign-in keys are stored in the database so logins survive restarts. After changing the entity model, add a migration for both:

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <Name> --project "e-Commerce application.csproj" --context SqliteAppDbContext -o Data/Migrations/Sqlite
dotnet dotnet-ef migrations add <Name> --project "e-Commerce application.csproj" --context PostgresAppDbContext -o Data/Migrations/Postgres
```

## JSON API

The original order endpoint accepts physical products:

```http
POST /order/orders
Content-Type: application/json

{
  "orderDate": "2026-09-30T12:00:00Z",
  "invoicePrice": 39.98,
  "products": [ { "productCode": 7, "price": 19.99, "quantity": 2 } ]
}
```

The order date must be within the last five minutes, `invoicePrice` must equal the sum of `price × quantity`, and each product must exist at its current price with enough stock. Services, downloads and ebooks need a signed-in customer and are bought in the store. Errors return `400` with one message per line; success returns the saved order.

Read-only catalog: `GET /api/products?q=&type=Physical|Service|Digital|Ebook&category=` and `GET /api/products/{code}`. Health check: `GET /healthz`.

## Tests

```bash
dotnet test
# against PostgreSQL instead of SQLite:
TEST_POSTGRES="Host=localhost;Username=me;Password=secret" dotnet test
```

`tests/ECommerce.Tests` runs the real app against a temporary database: the order API, checkout, service booking and fulfilment, digital delivery, library access control, publishing, earnings, moderation, wish list, deals, admin order management and upload validation.

## Not included

- **Real payments**: checkout records the order with a demo payment method. Connect a provider (Stripe, Paystack, Flutterwave) in `CheckoutController` using your merchant keys.
- **Email**: no email sender is configured, so password-reset, confirmation and order emails aren't sent. Register an `IEmailSender` implementation to enable them.
- **Seller payouts**: earnings are calculated and shown, but money isn't transferred to sellers.
