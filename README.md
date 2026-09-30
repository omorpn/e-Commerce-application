# ShopNest: e-Commerce store with ebook self-publishing

An ASP.NET Core 8 MVC web application: an online store for physical products plus an ebook store where anyone can self-publish and sell their own books.

## Features

**Shopping**
- Storefront with departments, search (title, author, description), category and price filters, sorting and pagination
- Product pages with stock status, quantity selection, *Add to Cart* and *Buy Now*
- Ratings and reviews (one per customer, marked *Verified Purchase* when bought)
- Session shopping cart, checkout with shipping address, demo payment (no real charge)
- *Your Orders*: order history, order tracking and cancellation before shipping (stock is returned automatically)

**eBooks and self-publishing**
- Any registered user can become an author from **Publish your book**
- Upload a PDF or EPUB manuscript and an optional cover (JPG/PNG/WEBP); a typographic cover is generated when none is uploaded
- Set price (or free), genre, language and page count; save as draft or publish immediately; edit, unpublish or delete
- Author dashboard with units sold, sales and royalties (70% by default)
- Buyers get ebooks in **Your Library**: read PDFs in the browser or download. Files are stored outside `wwwroot` and are only served to owners, the author and admins.

**Admin** (`/Admin`)
- Dashboard: revenue, orders awaiting fulfilment, low stock, users and authors
- Products: create, edit, delete, upload images, manage stock and visibility
- Orders: search, filter, view and update status (cancelling returns stock and revokes ebook access)
- eBooks: moderate self-published titles (take down with a reason, reinstate)
- Users: view customers/authors and grant or remove admin access

**Accounts**: registration, sign in, password change, two-factor authentication and personal data download/delete, provided by ASP.NET Core Identity.

## Running locally

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project "e-Commerce application.csproj"
```

Open http://localhost:5195. On first start the app creates a SQLite database (`ecommerce.db`) and seeds a sample catalog, including six ebooks with downloadable PDFs.

In Development, an admin account is created from `appsettings.Development.json`:

| Email | Password |
|---|---|
| `admin@eshop.local` | `Admin@12345` |

Create a normal account with **Create account** to shop or publish.

## Configuration

| Setting | Default | Purpose |
|---|---|---|
| `ConnectionStrings:Default` | `Data Source=ecommerce.db` | SQLite database file |
| `Storage:Root` | `App_Data/storage` | Where manuscripts, covers and product images are stored |
| `Publishing:RoyaltyRate` | `0.70` | Author share of each ebook sale |
| `SeedSampleData` | `true` | Seed the sample catalog when the database is empty |
| `Admin:Email` / `Admin:Password` | empty (set in Development) | Admin account created at startup |

For production, set `Admin__Email` and `Admin__Password` as environment variables (or user secrets) rather than committing them.

The database schema is created with `EnsureCreated`. If you change the entity model, delete `ecommerce.db` to recreate it (or switch to EF Core migrations).

## JSON API

The original order endpoint is kept:

```http
POST /order/orders
Content-Type: application/json

{
  "orderDate": "2026-09-30T12:00:00Z",
  "invoicePrice": 39.98,
  "products": [ { "productCode": 5, "price": 19.99, "quantity": 2 } ]
}
```

Validation: the order date must be within the last five minutes, `invoicePrice` must equal the sum of `price × quantity`, each product must exist with the current catalog price and enough stock. Ebooks can only be bought in the store while signed in. Errors are returned as `400` with one message per line; success returns the saved order.

Read-only catalog: `GET /api/products?q=&type=Physical|Ebook&category=` and `GET /api/products/{code}`.

## Tests

```bash
dotnet test
```

`tests/ECommerce.Tests` runs the real app against a temporary database and covers the order API, checkout, library downloads and access control, publishing, royalties, moderation, admin order management and upload validation.

## Not included

- Real payment processing: checkout records the order with a demo payment method. Plug in a payment provider in `CheckoutController`.
- Email delivery: no email sender is configured, so password-reset and confirmation emails are not sent. Register an `IEmailSender` implementation to enable them.
