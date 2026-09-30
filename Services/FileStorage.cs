using e_Commerce_application.Data;
using e_Commerce_application.Models;
using Microsoft.EntityFrameworkCore;

namespace e_Commerce_application.Services
{
    public interface IFileStorage
    {
        // Largest single upload this store accepts.
        long MaxFileBytes { get; }

        Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default);
        Stream? OpenRead(string? key);
        void Delete(string? key);
    }

    public static class FileStorageSetup
    {
        // "FileSystem" or "Database". Defaults to Database with PostgreSQL (hosts without a
        // persistent disk) and FileSystem with SQLite.
        public static void AddFileStorage(this IServiceCollection services, IConfiguration configuration, bool usesPostgres)
        {
            var provider = configuration["Storage:Provider"];
            var useDatabase = string.IsNullOrWhiteSpace(provider)
                ? usesPostgres
                : provider.Equals("Database", StringComparison.OrdinalIgnoreCase);

            if (useDatabase)
            {
                services.AddSingleton<IFileStorage, DatabaseFileStorage>();
            }
            else
            {
                services.AddSingleton<IFileStorage, LocalFileStorage>();
            }
        }

        // Storage:MaxFileMB overrides the default (0 or unset keeps it); never above 100 MB.
        public static long MaxFileBytes(IConfiguration configuration, int defaultMegabytes)
        {
            var configured = configuration.GetValue("Storage:MaxFileMB", 0);
            return Math.Min(configured > 0 ? configured : defaultMegabytes, 100) * 1024L * 1024L;
        }
    }

    // Stores uploads on local disk outside wwwroot, so downloads are only reachable
    // through controllers that check ownership.
    public class LocalFileStorage : IFileStorage
    {
        private readonly string _root;

        public LocalFileStorage(IConfiguration configuration, IWebHostEnvironment env)
        {
            var configured = configuration["Storage:Root"];
            _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(env.ContentRootPath, "App_Data", "storage")
                : Path.Combine(env.ContentRootPath, configured));
            Directory.CreateDirectory(_root);
            MaxFileBytes = FileStorageSetup.MaxFileBytes(configuration, 100);
        }

        public long MaxFileBytes { get; }

        public async Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default)
        {
            var key = $"{folder}/{Guid.NewGuid():N}{extension}";
            var path = Resolve(key)!;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var file = File.Create(path);
            await content.CopyToAsync(file, ct);
            return key;
        }

        public Stream? OpenRead(string? key)
        {
            var path = Resolve(key);
            return path != null && File.Exists(path) ? File.OpenRead(path) : null;
        }

        public void Delete(string? key)
        {
            var path = Resolve(key);
            if (path != null && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private string? Resolve(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var path = Path.GetFullPath(Path.Combine(_root, key));
            return path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? path : null;
        }
    }

    // Stores uploads as rows in the database. Each call uses its own DbContext so saving
    // a file never flushes the caller's pending changes.
    public class DatabaseFileStorage : IFileStorage
    {
        private readonly IServiceScopeFactory _scopes;

        public DatabaseFileStorage(IServiceScopeFactory scopes, IConfiguration configuration)
        {
            _scopes = scopes;
            MaxFileBytes = FileStorageSetup.MaxFileBytes(configuration, 25);
        }

        public long MaxFileBytes { get; }

        public async Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);

            var key = $"{folder}/{Guid.NewGuid():N}{extension}";
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.FileBlobs.Add(new FileBlob { Key = key, Data = buffer.ToArray() });
            await db.SaveChangesAsync(ct);
            return key;
        }

        public Stream? OpenRead(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var data = db.FileBlobs.AsNoTracking().Where(f => f.Key == key).Select(f => f.Data).FirstOrDefault();
            return data == null ? null : new MemoryStream(data, writable: false);
        }

        public void Delete(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            using var scope = _scopes.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().FileBlobs.Where(f => f.Key == key).ExecuteDelete();
        }
    }
}
