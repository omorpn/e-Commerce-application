using System.Text;

namespace e_Commerce_application.Services
{
    public record FileKind(string Extension, string ContentType, string Label);

    // Identifies uploads by their leading bytes rather than trusting the file name or
    // the browser-supplied content type.
    public static class FileSignatures
    {
        public static readonly FileKind Pdf = new(".pdf", "application/pdf", "PDF");
        public static readonly FileKind Epub = new(".epub", "application/epub+zip", "EPUB");
        public static readonly FileKind Zip = new(".zip", "application/zip", "ZIP");
        public static readonly FileKind Mp3 = new(".mp3", "audio/mpeg", "MP3");
        public static readonly FileKind Wav = new(".wav", "audio/wav", "WAV");
        public static readonly FileKind Mp4 = new(".mp4", "video/mp4", "MP4");
        public static readonly FileKind Jpeg = new(".jpg", "image/jpeg", "JPG");
        public static readonly FileKind Png = new(".png", "image/png", "PNG");
        public static readonly FileKind Webp = new(".webp", "image/webp", "WEBP");

        public static readonly FileKind[] All = { Pdf, Epub, Zip, Mp3, Wav, Mp4, Jpeg, Png, Webp };

        public const long MaxManuscriptBytes = 50 * 1024 * 1024;
        public const long MaxDigitalBytes = 100 * 1024 * 1024;
        public const long MaxImageBytes = 5 * 1024 * 1024;
        public const long MaxUploadRequestBytes = MaxDigitalBytes + MaxImageBytes + 1024 * 1024;

        public static FileKind? FromContentType(string? contentType) => All.FirstOrDefault(k => k.ContentType == contentType);

        public static async Task<FileKind?> DetectManuscriptAsync(IFormFile file)
        {
            var kind = await DetectAsync(file);
            return kind == Pdf || kind == Epub ? kind : null;
        }

        public static async Task<FileKind?> DetectImageAsync(IFormFile file)
        {
            var kind = await DetectAsync(file);
            return kind == Jpeg || kind == Png || kind == Webp ? kind : null;
        }

        // Any supported downloadable: documents, archives, audio, video and images.
        public static Task<FileKind?> DetectDigitalAsync(IFormFile file) => DetectAsync(file);

        private static async Task<FileKind?> DetectAsync(IFormFile file) => Detect(await ReadHeadAsync(file));

        public static FileKind? Detect(byte[] head)
        {
            if (StartsWith(head, "%PDF-"u8))
            {
                return Pdf;
            }
            if (StartsWith(head, "PK\x03\x04"u8))
            {
                // EPUB is a ZIP whose first entry is an uncompressed "mimetype" file.
                return Encoding.ASCII.GetString(head).Contains("application/epub+zip") ? Epub : Zip;
            }
            if (StartsWith(head, [0xFF, 0xD8, 0xFF]))
            {
                return Jpeg;
            }
            if (StartsWith(head, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            {
                return Png;
            }
            if (head.Length >= 12 && StartsWith(head, "RIFF"u8))
            {
                var format = head.AsSpan(8, 4);
                if (format.SequenceEqual("WEBP"u8)) return Webp;
                if (format.SequenceEqual("WAVE"u8)) return Wav;
            }
            if (StartsWith(head, "ID3"u8) || (head.Length >= 2 && head[0] == 0xFF && (head[1] & 0xE0) == 0xE0))
            {
                return Mp3;
            }
            if (head.Length >= 12 && head.AsSpan(4, 4).SequenceEqual("ftyp"u8))
            {
                return Mp4;
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
