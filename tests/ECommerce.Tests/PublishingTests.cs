using e_Commerce_application.Models;
using e_Commerce_application.Services;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ECommerce.Tests
{
    public class PublishingTests : IClassFixture<TestAppFactory>
    {
        private readonly TestAppFactory _app;

        public PublishingTests(TestAppFactory app) => _app = app;

        private static readonly byte[] Pdf = SamplePdf.Create("Test Book", "Test Author", new[] { "Hello, reader." });

        private static async Task<HttpClient> BecomeAuthorAsync(TestAppFactory app)
        {
            var client = await app.SignInAsync(await app.CreateUserAsync());
            var response = await client.PostFormAsync("/Publish/Profile", "/Publish/Profile", new()
            {
                ["PenName"] = "Test Author",
                ["AcceptTerms"] = "true"
            });
            Assert.Equal("/Publish/Create", response.Headers.Location!.ToString());
            return client;
        }

        private static async Task<HttpResponseMessage> CreateEbookAsync(HttpClient client, string title, byte[] manuscript, string fileName, bool publish)
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(await client.GetAntiforgeryTokenAsync("/Publish/Create")), "__RequestVerificationToken" },
                { new StringContent(title), "Title" },
                { new StringContent("Test Author"), "AuthorName" },
                { new StringContent("A book written for automated tests."), "Description" },
                { new StringContent("Fiction"), "Category" },
                { new StringContent("English"), "Language" },
                { new StringContent("2.99"), "Price" }
            };
            var file = new ByteArrayContent(manuscript);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "Manuscript", fileName);
            if (publish)
            {
                form.Add(new StringContent("true"), "publishNow");
            }
            return await client.PostAsync("/Publish/Create", form);
        }

        [Fact]
        public async Task Author_CanPublish_AndEarnsRoyaltiesOnSales()
        {
            var author = await BecomeAuthorAsync(_app);
            var title = "Automated Adventures " + Guid.NewGuid().ToString("N")[..6];

            var created = await CreateEbookAsync(author, title, Pdf, "book.pdf", publish: true);
            Assert.Equal("/Publish", created.Headers.Location!.ToString());

            var book = await _app.FindProductAsync(title);
            Assert.Equal(ListingStatus.Published, book.Status);
            Assert.Equal(ProductType.Ebook, book.Type);
            Assert.Contains(title, await _app.CreateClient().GetStringAsync("/Products?dept=ebooks"));

            // A reader buys it.
            var reader = await _app.SignInAsync(await _app.CreateUserAsync());
            await reader.PostFormAsync($"/Products/Details/{book.ProductCode}", "/Cart/Add", new() { ["productCode"] = book.ProductCode.ToString() });
            var checkout = await reader.PostFormAsync("/Checkout", "/Checkout", new()
            {
                ["FullName"] = "Reader",
                ["Email"] = "reader@test.local",
                ["PaymentMethod"] = "Card (demo)"
            });
            Assert.Contains("/Orders/Details/", checkout.Headers.Location!.ToString());

            var dashboard = await author.GetStringAsync("/Publish");
            Assert.Contains("$2.09", dashboard); // 70% of $2.99
        }

        [Fact]
        public async Task NonPdfManuscript_IsRejected()
        {
            var author = await BecomeAuthorAsync(_app);
            var title = "Not A Book " + Guid.NewGuid().ToString("N")[..6];

            var response = await CreateEbookAsync(author, title, Encoding.ASCII.GetBytes("<html>nope</html>"), "book.pdf", publish: true);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Manuscripts must be PDF or EPUB files.", await response.Content.ReadAsStringAsync());
            Assert.False(await _app.WithDbAsync(db => db.Products.AnyAsync(p => p.Name == title)));
        }

        [Fact]
        public async Task BlockedEbook_DisappearsFromStore()
        {
            var author = await BecomeAuthorAsync(_app);
            var title = "Questionable Content " + Guid.NewGuid().ToString("N")[..6];
            await CreateEbookAsync(author, title, Pdf, "book.pdf", publish: true);
            var book = await _app.FindProductAsync(title);

            var admin = await _app.SignInAsync(TestAppFactory.AdminEmail, TestAppFactory.AdminPassword);
            await admin.PostFormAsync("/Admin/Ebooks", $"/Admin/Ebooks/Block/{book.ProductCode}", new() { ["reason"] = "Copyright claim" });

            Assert.Equal(ListingStatus.Blocked, (await _app.FindProductAsync(title)).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await _app.CreateClient().GetAsync($"/Products/Details/{book.ProductCode}")).StatusCode);

            // The author can't republish a blocked title.
            await author.PostFormAsync("/Publish", $"/Publish/SetStatus/{book.ProductCode}?publish=true", new());
            Assert.Equal(ListingStatus.Blocked, (await _app.FindProductAsync(title)).Status);
        }

        [Fact]
        public async Task CustomersWithoutAuthorRole_CannotCreateTitles()
        {
            var client = await _app.SignInAsync(await _app.CreateUserAsync());
            var response = await client.GetAsync("/Publish/Create");
            Assert.Contains("AccessDenied", response.Headers.Location!.ToString());
        }
    }
}
