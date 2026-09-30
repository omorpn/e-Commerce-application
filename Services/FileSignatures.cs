using System.Text;

namespace e_Commerce_application.Services
{
    public record FileKind(string Extension, string ContentType);

    // Identifies uploads by their leading bytes rather than trusting the file name or
    // the browser-supplied content type.
    public static class FileSignatures
    {
        public static readonly FileKind Pdf = new(".pdf", "application/pdf");
        public static readonly FileKind Epub = new(".epub", "application/epub+zip");
        public static readonly FileKind Jpeg = new(".jpg", "image/jpeg");
        public static readonly FileKind Png = new(".png", "image/png");
        public static readonly FileKind Webp = new(".webp", "image/webp");

        public const long MaxManuscriptBytes = 50 * 1024 * 1024;
        public const long MaxImageBytes = 5 * 1024 * 1024;

        public static async Task<FileKind?> DetectManuscriptAsync(IFormFile file)
        {
            var head = await ReadHeadAsync(file);
            if (StartsWith(head, "%PDF-"u8))
            {
                return Pdf;
            }

            // EPUB is a ZIP whose first entry is an uncompressed "mimetype" file.
            if (StartsWith(head, "PK\x03\x04"u8) && Encoding.ASCII.GetString(head).Contains("application/epub+zip"))
            {
                return Epub;
            }

            return null;
        }

        public static async Task<FileKind?> DetectImageAsync(IFormFile file)
        {
            var head = await ReadHeadAsync(file);
            if (StartsWith(head, [0xFF, 0xD8, 0xFF]))
            {
                return Jpeg;
            }

            if (StartsWith(head, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            {
                return Png;
            }

            if (head.Length >= 12 && StartsWith(head, "RIFF"u8) && head.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            {
                return Webp;
            }

            return null;
        }

        private static async Task<byte[]> ReadHeadAsync(IFormFile file)
        {
            var buffer = new byte[64];
            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false);
            return buffer[..read];
        }

        private static bool StartsWith(byte[] data, ReadOnlySpan<byte> prefix) =>
            data.AsSpan().StartsWith(prefix);
    }
}
