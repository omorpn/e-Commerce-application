using e_Commerce_application.Data;
using e_Commerce_application.Models;
using e_Commerce_application.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Hosting platforms such as Render and Railway say which port to listen on via PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// SQLite by default; a PostgreSQL connection string (e.g. a free Neon database) switches provider.
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = "Data Source=ecommerce.db";
}
var usesPostgres = DatabaseSetup.IsPostgres(connectionString);
builder.Services.AddAppDatabase(connectionString);

// Sign-in keys live in the database so logins survive restarts and redeploys.
builder.Services.AddDataProtection().PersistKeysToDbContext<AppDbContext>();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddControllersWithViews(options =>
    {
        // Every form post must carry an antiforgery token; the JSON API opts out explicitly.
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddRazorPages();
// Output ₦ and other non-Latin characters as-is instead of as HTML character references.
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(options =>
    options.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));
builder.Services.AddHealthChecks();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = FileSignatures.MaxUploadRequestBytes);
builder.Services.Configure<PublishingOptions>(builder.Configuration.GetSection("Publishing"));
builder.Services.Configure<ShopSettings>(builder.Configuration.GetSection("Store"));
builder.Services.Configure<ShippingOptions>(builder.Configuration.GetSection("Shipping"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<PaystackOptions>(builder.Configuration.GetSection("Payments:Paystack"));

// The app usually runs behind a TLS-terminating proxy in production.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddFileStorage(builder.Configuration, usesPostgres);
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ListingService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, SmtpEmailSender>();
builder.Services.AddHttpClient<IPaymentGateway, PaystackGateway>(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHostedService<UnpaidOrderCleanup>();

var app = builder.Build();

ViewHelpers.CurrencySymbol = app.Configuration["Store:CurrencySymbol"] ?? ViewHelpers.CurrencySymbol;

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");
app.UseStaticFiles();

var culture = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = new[] { culture },
    SupportedUICultures = new[] { culture }
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapHealthChecks("/healthz");
app.MapControllerRoute("areas", "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

await DbInitializer.InitializeAsync(app.Services);

app.Run();

// Exposed for integration tests (WebApplicationFactory<Program>).
public partial class Program { }
