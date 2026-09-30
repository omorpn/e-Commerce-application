using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace e_Commerce_application.Data
{
    public static class DatabaseSetup
    {
        // A PostgreSQL connection string (key=value or postgres:// URL) selects PostgreSQL;
        // anything else is treated as a SQLite connection string.
        public static bool IsPostgres(string connectionString)
        {
            var value = connectionString.TrimStart();
            return value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Host=", StringComparison.OrdinalIgnoreCase);
        }

        public static void AddAppDatabase(this IServiceCollection services, string connectionString)
        {
            if (IsPostgres(connectionString))
            {
                var npgsql = ToNpgsqlConnectionString(connectionString);
                services.AddDbContext<AppDbContext, PostgresAppDbContext>(options => options.UseNpgsql(npgsql));
            }
            else
            {
                services.AddDbContext<AppDbContext, SqliteAppDbContext>(options => options.UseSqlite(connectionString));
            }
        }

        // Hosted databases (Neon, Supabase, Render) hand out URLs; Npgsql needs key=value pairs.
        public static string ToNpgsqlConnectionString(string connectionString)
        {
            var value = connectionString.Trim();
            if (!value.Contains("://"))
            {
                return value;
            }

            var uri = new Uri(value);
            var userInfo = uri.UserInfo.Split(':', 2);
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
                Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
                Username = Uri.UnescapeDataString(userInfo[0]),
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
                // Use TLS when the server offers it (hosted databases); private networks may not.
                SslMode = SslMode.Prefer
            };

            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(parts[0]);
                var setting = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
                if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase) && Enum.TryParse<SslMode>(setting, true, out var mode))
                {
                    builder.SslMode = mode;
                }
                else if (key.Equals("channel_binding", StringComparison.OrdinalIgnoreCase) && Enum.TryParse<ChannelBinding>(setting, true, out var binding))
                {
                    builder.ChannelBinding = binding;
                }
            }

            // Pooled endpoints (PgBouncer, e.g. Neon's "-pooler" hosts) don't keep session state.
            if (builder.Host?.Contains("-pooler", StringComparison.OrdinalIgnoreCase) == true)
            {
                builder.NoResetOnClose = true;
                builder.MaxAutoPrepare = 0;
            }

            return builder.ConnectionString;
        }
    }
}
