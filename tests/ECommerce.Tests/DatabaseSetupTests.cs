using e_Commerce_application.Data;
using Npgsql;

namespace ECommerce.Tests
{
    public class DatabaseSetupTests
    {
        [Theory]
        [InlineData("Data Source=ecommerce.db", false)]
        [InlineData("Host=db;Database=shop;Username=u;Password=p", true)]
        [InlineData("postgres://u:p@db/shop", true)]
        [InlineData("postgresql://u:p@db.neon.tech/shop?sslmode=require", true)]
        public void DetectsProvider(string connectionString, bool postgres) =>
            Assert.Equal(postgres, DatabaseSetup.IsPostgres(connectionString));

        [Fact]
        public void ConvertsHostedDatabaseUrls()
        {
            var converted = DatabaseSetup.ToNpgsqlConnectionString(
                "postgresql://shop_owner:p%40ss%3Aword@ep-cool-1234.eu-central-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require");
            var builder = new NpgsqlConnectionStringBuilder(converted);

            Assert.Equal("ep-cool-1234.eu-central-1.aws.neon.tech", builder.Host);
            Assert.Equal(5432, builder.Port);
            Assert.Equal("neondb", builder.Database);
            Assert.Equal("shop_owner", builder.Username);
            Assert.Equal("p@ss:word", builder.Password);
            Assert.Equal(SslMode.Require, builder.SslMode);
            Assert.Equal(ChannelBinding.Require, builder.ChannelBinding);
        }

        [Fact]
        public void UrlWithoutSslMode_UsesTlsWhenAvailable()
        {
            // Render's internal database URLs carry no sslmode and a default port.
            var builder = new NpgsqlConnectionStringBuilder(
                DatabaseSetup.ToNpgsqlConnectionString("postgresql://shopnest:secret@dpg-abc123-a/shopnest"));

            Assert.Equal("dpg-abc123-a", builder.Host);
            Assert.Equal(5432, builder.Port);
            Assert.Equal("shopnest", builder.Database);
            Assert.Equal(SslMode.Prefer, builder.SslMode);
        }

        [Fact]
        public void KeepsKeyValueConnectionStrings()
        {
            const string value = "Host=localhost;Database=shop;Username=u;Password=p";
            Assert.Equal(value, DatabaseSetup.ToNpgsqlConnectionString(value));
        }
    }
}
