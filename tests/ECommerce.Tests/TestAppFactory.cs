using e_Commerce_application.Data;
using e_Commerce_application.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.RegularExpressions;

namespace ECommerce.Tests
{
    // Runs the real app against a throwaway SQLite database and storage folder.
    public class TestAppFactory : WebApplicationFactory<Program>
    {
        public const string AdminEmail = "admin@test.local";
        public const string AdminPassword = "Admin@12345";
        public const string UserPassword = "Passw0rd!";

        private readonly string _root = Path.Combine(Path.GetTempPath(), "ecommerce-tests-" + Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(_root);
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={Path.Combine(_root, "test.db")};Pooling=False");
            builder.UseSetting("Storage:Root", Path.Combine(_root, "storage"));
            builder.UseSetting("Admin:Email", AdminEmail);
            builder.UseSetting("Admin:Password", AdminPassword);
            builder.UseSetting("SeedSampleData", "true");
        }

        public HttpClient CreateBrowser() =>
            CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
        {
            using var scope = Services.CreateScope();
            return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public async Task<Product> FindProductAsync(string name) =>
            await WithDbAsync(db => db.Products.AsNoTracking().FirstAsync(p => p.Name == name));

        public async Task<string> CreateUserAsync(string? email = null)
        {
            email ??= $"user-{Guid.NewGuid():N}@test.local";
            using var scope = Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var result = await users.CreateAsync(new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true }, UserPassword);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
            return email;
        }

        // Signs in through the real login page and returns a client carrying the auth cookie.
        public async Task<HttpClient> SignInAsync(string email, string password = UserPassword)
        {
            var client = CreateBrowser();
            var response = await client.PostFormAsync("/Identity/Account/Login", "/Identity/Account/Login", new()
            {
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["Input.RememberMe"] = "false"
            });
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return client;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        }
    }

    public static class HttpClientExtensions
    {
        private static readonly Regex TokenPattern = new("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"", RegexOptions.Compiled);

        public static async Task<string> GetAntiforgeryTokenAsync(this HttpClient client, string pageUrl)
        {
            var html = await client.GetStringAsync(pageUrl);
            var match = TokenPattern.Match(html);
            Assert.True(match.Success, $"No antiforgery token on {pageUrl}");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        // GETs a page for its antiforgery token, then posts the form to the given action.
        public static async Task<HttpResponseMessage> PostFormAsync(this HttpClient client, string pageUrl, string action, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = await client.GetAntiforgeryTokenAsync(pageUrl);
            return await client.PostAsync(action, new FormUrlEncodedContent(fields));
        }
    }
}
