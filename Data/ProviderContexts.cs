using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace e_Commerce_application.Data
{
    public class SqliteAppDbContext : AppDbContext
    {
        public SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : base(options) { }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // SQLite has no decimal type; store money as REAL so it can be sorted and compared in SQL.
            configurationBuilder.Properties<decimal>().HaveConversion<double>();
        }
    }

    public class PostgresAppDbContext : AppDbContext
    {
        static PostgresAppDbContext()
        {
            // The app stores UTC and unspecified DateTimes alike; keep them as plain timestamps.
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        public PostgresAppDbContext(DbContextOptions<PostgresAppDbContext> options) : base(options) { }
    }

    // Used by "dotnet ef migrations add"; no database connection is made.
    public class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>
    {
        public SqliteAppDbContext CreateDbContext(string[] args) =>
            new(new DbContextOptionsBuilder<SqliteAppDbContext>().UseSqlite("Data Source=design.db").Options);
    }

    public class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresAppDbContext>
    {
        public PostgresAppDbContext CreateDbContext(string[] args) =>
            new(new DbContextOptionsBuilder<PostgresAppDbContext>().UseNpgsql("Host=localhost;Database=design").Options);
    }
}
