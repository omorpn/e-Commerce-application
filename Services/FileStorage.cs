namespace e_Commerce_application.Services
{
    public interface IFileStorage
    {
        Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default);
        Stream? OpenRead(string? key);
        void Delete(string? key);
    }

    // Stores uploads on local disk outside wwwroot, so manuscripts are only reachable
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
        }

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
}
